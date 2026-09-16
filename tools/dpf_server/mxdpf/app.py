# encoding: utf-8
"""
MX DPF server — FastAPI 앱.

REST
    GET    /                     서비스 정보
    GET    /health[?deep=true]   상태 (deep = 실제 DPF 기동 + 예제 결과 읽기, 인증 필요)
    POST   /jobs                 multipart 업로드 (rst, rst2?) → 202 잡
    POST   /jobs/by-path         JSON {rst_path, rst2_path?} (MXDPF_ALLOWED_ROOTS 안) → 202 잡
    GET    /jobs                 목록 (?limit, ?status)
    GET    /jobs/{id}            상태 (?wait=초 — 끝날 때까지 최대 그만큼 대기)
    GET    /jobs/{id}/result     mx_batch 결과 JSON
    GET    /jobs/{id}/log        실행 로그 (text)
    POST   /jobs/{id}/cancel     취소
    DELETE /jobs/{id}            끝난 잡 폴더 삭제
MCP
    POST   /mcp                  JSON-RPC 2.0 (Streamable HTTP, JSON 응답)
"""
import hmac
import json
import os
import subprocess
import threading
import time
from contextlib import asynccontextmanager

from fastapi import Depends, FastAPI, File, HTTPException, Query, Request, UploadFile
from fastapi.responses import JSONResponse, PlainTextResponse, Response

from .config import Settings
from .jobs import JobError, JobManager
from .mcp import McpHandler, SERVER_VERSION

CHUNK = 1024 * 1024


def create_app(settings=None):
    s = settings or Settings.from_env()
    state = {"jobs": None}
    deep_cache = {"at": 0.0, "data": None}
    deep_lock = threading.Lock()

    @asynccontextmanager
    async def lifespan(app):
        jm = JobManager(s)
        jm.start()
        state["jobs"] = jm
        app.state.jobs = jm
        app.state.mcp = McpHandler(jm, health)
        try:
            yield
        finally:
            jm.shutdown()

    app = FastAPI(title="MX DPF Sidecar Server", version=SERVER_VERSION, lifespan=lifespan)
    app.state.settings = s

    # ------------------------------------------------------------------ security
    def require_auth(request: Request):
        origin = request.headers.get("origin")
        if origin and origin not in s.allowed_origins:
            # 브라우저발 DNS-rebinding 차단 (MCP 스펙 권고). 게이트웨이의 서버-서버 호출엔 Origin 이 없다.
            raise HTTPException(status_code=403, detail="origin not allowed: %s" % origin)
        if s.token:
            got = request.headers.get("authorization", "")
            if not (got.startswith("Bearer ") and hmac.compare_digest(got[7:].strip(), s.token)):
                raise HTTPException(status_code=401, detail="missing or invalid bearer token",
                                    headers={"WWW-Authenticate": "Bearer"})

    @app.middleware("http")
    async def reject_oversized_upload(request: Request, call_next):
        # 멀티파트는 핸들러 호출 전에 임시파일로 전부 받아지므로, 선언된 크기로 먼저 끊는다
        if request.method == "POST" and request.url.path == "/jobs":
            declared = request.headers.get("content-length", "")
            if declared.isdigit() and int(declared) > 2 * s.max_upload_mb * CHUNK + CHUNK:
                return JSONResponse(status_code=413, content={
                    "error": "upload exceeds MXDPF_MAX_UPLOAD_MB=%d (per file, max 2 files)" % s.max_upload_mb})
        return await call_next(request)

    @app.exception_handler(JobError)
    async def _job_error(request, exc):
        return JSONResponse(status_code=exc.status_code, content={"error": str(exc)})

    # ------------------------------------------------------------------ health
    def health(deep=False):
        jm = state["jobs"]
        out = {
            "service": "mxdtm-dpf", "version": SERVER_VERSION, "status": "ok",
            "python": s.python, "python_exists": os.path.isfile(s.python) or _which(s.python),
            "batch_script": s.batch_script, "batch_script_exists": os.path.isfile(s.batch_script),
            "awp_root252": os.environ.get("AWP_ROOT252"),
            "license_env": {k: ("set" if os.environ.get(k) else None)
                            for k in ("ANSYSLMD_LICENSE_FILE", "ANSYSLI_SERVERS")},
            "max_concurrency": s.max_concurrency, "job_timeout_sec": s.job_timeout_sec,
            "path_submission": bool(s.allowed_roots),
            "jobs": jm.stats() if jm else {},
        }
        if not (out["python_exists"] and out["batch_script_exists"]):
            out["status"] = "degraded"
        if deep:
            with deep_lock:   # 동시에 여러 번 라이선스를 잡지 않게 직렬화 + 60초 캐시
                if deep_cache["data"] is None or time.time() - deep_cache["at"] > 60:
                    deep_cache["data"] = _run_probe(s)
                    deep_cache["at"] = time.time()
            out["deep"] = deep_cache["data"]
            if not deep_cache["data"].get("ok"):
                out["status"] = "degraded"
        return out

    @app.get("/")
    def root():
        return {"service": "mxdtm-dpf", "version": SERVER_VERSION, "docs": "/docs", "mcp": "/mcp",
                "description": "ANSYS .rst DPF post-processing (modal/participation/MAC/strain energy/hotspots)"}

    @app.get("/health")
    def get_health(request: Request, deep: bool = False):
        if deep:
            require_auth(request)
        return health(deep)

    # ------------------------------------------------------------------ jobs
    @app.post("/jobs", status_code=202, dependencies=[Depends(require_auth)])
    def submit_upload(rst: UploadFile = File(...), rst2: UploadFile = File(None)):
        jm = state["jobs"]
        limit = s.max_upload_mb * CHUNK
        for up in (rst, rst2):
            if up is not None and not (up.filename or "").lower().endswith(".rst"):
                raise JobError("only .rst files are accepted (got %r)" % up.filename, 415)
        job_id = jm.create_upload_job()
        try:
            _save_upload(rst, os.path.join(jm.job_dir(job_id), "input.rst"), limit)
            if rst2 is not None:
                _save_upload(rst2, os.path.join(jm.job_dir(job_id), "input2.rst"), limit)
        except Exception:
            jm.discard_upload(job_id)
            raise
        return jm.enqueue_upload(job_id, os.path.basename(rst.filename),
                                 os.path.basename(rst2.filename) if rst2 is not None else None)

    @app.post("/jobs/by-path", status_code=202, dependencies=[Depends(require_auth)])
    def submit_by_path(body: dict):
        if not isinstance(body, dict):
            raise JobError("JSON object body required")
        return state["jobs"].submit_path(body.get("rst_path"), body.get("rst2_path") or None)

    @app.get("/jobs", dependencies=[Depends(require_auth)])
    def list_jobs(limit: int = Query(50, ge=1, le=500), status: str = None):
        return {"jobs": state["jobs"].list(limit=limit, status=status)}

    @app.get("/jobs/{job_id}", dependencies=[Depends(require_auth)])
    def get_job(job_id: str, wait: float = Query(0, ge=0, le=600)):
        jm = state["jobs"]
        job = jm.wait(job_id, wait) if wait > 0 else jm.get(job_id)
        out = jm.public(job)
        out["links"] = {"result": "/jobs/%s/result" % job_id, "log": "/jobs/%s/log" % job_id}
        return out

    @app.get("/jobs/{job_id}/result", dependencies=[Depends(require_auth)])
    def get_result(job_id: str):
        return state["jobs"].result(job_id)

    @app.get("/jobs/{job_id}/log", dependencies=[Depends(require_auth)], response_class=PlainTextResponse)
    def get_log(job_id: str, tail_kb: int = Query(64, ge=1, le=10240)):
        return state["jobs"].log(job_id, tail_kb * 1024)

    @app.post("/jobs/{job_id}/cancel", dependencies=[Depends(require_auth)])
    def cancel_job(job_id: str):
        return state["jobs"].cancel(job_id)

    @app.delete("/jobs/{job_id}", dependencies=[Depends(require_auth)])
    def delete_job(job_id: str):
        return state["jobs"].delete(job_id)

    # ------------------------------------------------------------------ MCP
    @app.post("/mcp", dependencies=[Depends(require_auth)])
    async def mcp_post(request: Request):
        raw = await request.body()
        try:
            payload = json.loads(raw.decode("utf-8"))
        except Exception:
            return JSONResponse({"jsonrpc": "2.0", "id": None,
                                 "error": {"code": -32700, "message": "parse error"}})
        # 도구 호출은 wait_seconds 동안 블로킹할 수 있으므로 스레드풀에서 실행
        from starlette.concurrency import run_in_threadpool
        resp = await run_in_threadpool(request.app.state.mcp.handle, payload)
        if resp is None:
            return Response(status_code=202)
        return JSONResponse(resp)

    @app.get("/mcp")
    def mcp_get():
        # 서버발 SSE 스트림은 제공하지 않는다 (스펙상 405 로 알림)
        return Response(status_code=405, headers={"Allow": "POST"})

    return app


def _save_upload(up, dest, limit):
    written = 0
    with open(dest, "wb") as f:
        while True:
            chunk = up.file.read(CHUNK)
            if not chunk:
                break
            written += len(chunk)
            if written > limit:
                raise JobError("upload exceeds MXDPF_MAX_UPLOAD_MB=%d" % (limit // CHUNK), 413)
            f.write(chunk)
    if written == 0:
        raise JobError("empty upload: %s" % up.filename, 400)


def _which(cmd):
    from shutil import which
    return which(cmd) is not None


def _run_probe(s):
    cmd = [s.python, "-m", "mxdpf.probe", s.batch_script]
    env = os.environ.copy()
    env["PYTHONPATH"] = os.path.dirname(os.path.dirname(os.path.abspath(__file__))) + (
        os.pathsep + env["PYTHONPATH"] if env.get("PYTHONPATH") else "")
    env["PYTHONIOENCODING"] = "utf-8"
    try:
        cp = subprocess.run(cmd, capture_output=True, timeout=180, env=env)
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "probe timed out after 180s (DPF server start or license checkout hung)"}
    except OSError as e:
        return {"ok": False, "error": "cannot start python: %s" % e}
    text = cp.stdout.decode("utf-8", errors="replace").strip().splitlines()
    for line in reversed(text):
        try:
            data = json.loads(line)
            data["return_code"] = cp.returncode
            return data
        except ValueError:
            continue
    return {"ok": False, "return_code": cp.returncode,
            "error": "probe printed no JSON",
            "stderr_tail": cp.stderr.decode("utf-8", errors="replace")[-2000:]}
