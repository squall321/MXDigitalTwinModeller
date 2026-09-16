# encoding: utf-8
"""
MX DPF server — MCP (Streamable HTTP, JSON 응답 전용) 핸들러.

`POST /mcp` 로 JSON-RPC 2.0 을 받는다. 스트리밍(SSE)은 쓰지 않는다 — 도구는 잡을 제출하고
바로 돌아오거나 wait_seconds 만큼만 기다리므로 단일 JSON 응답으로 충분하다.
SDK 의존성 없이 구현한 이유: SpaceClaim Add-In 의 McpServer.cs 와 같은 최소 표면을 유지하고,
포털 MCP 게이트웨이가 네임스페이스로 붙일 때 추가 설치물이 없게 하려는 것.

파일 업로드는 MCP 로 받지 않는다. 결과 파일은
  (a) REST `POST /jobs` 로 올린 뒤 job_id 로 조회하거나
  (b) 서버가 읽을 수 있는 공유 스토리지 경로(MXDPF_ALLOWED_ROOTS 안)를 넘긴다.
"""
import json

from .jobs import JobError, FINISHED, SUCCEEDED

SERVER_NAME = "mxdtm-dpf"
SERVER_VERSION = "1.0.0"
SUPPORTED_PROTOCOLS = ("2025-06-18", "2025-03-26", "2024-11-05")

MAX_WAIT_SEC = 600

TOOLS = [
    {
        "name": "dpf_health",
        "description": "DPF 사이드카 서버 상태. deep=true 면 실제로 DPF 서버를 띄우고 번들 예제 결과를 읽어 "
                       "ANSYS 설치·라이선스까지 확인한다 (수십 초 걸릴 수 있음).",
        "inputSchema": {"type": "object", "properties": {
            "deep": {"type": "boolean", "default": False}}},
    },
    {
        "name": "dpf_submit_analysis",
        "description": "해석이 끝난 ANSYS 결과 파일(.rst)을 DPF 로 분석하는 잡을 제출한다. 모달이면 고유진동수·"
                       "유효질량(참여율)·MAC, 공통으로 변형에너지 요약과 von Mises 응력 핫스팟 클러스터를 계산한다. "
                       "rst_path 는 서버가 접근 가능한 공유 경로여야 한다. rst2_path 를 주면 두 결과 간 cross-MAC. "
                       "wait_seconds>0 이면 그만큼 끝나기를 기다렸다가 끝났으면 결과까지 돌려준다.",
        "inputSchema": {"type": "object", "required": ["rst_path"], "properties": {
            "rst_path": {"type": "string", "description": "서버 기준 .rst 절대 경로"},
            "rst2_path": {"type": "string", "description": "cross-MAC 비교용 두 번째 .rst (선택)"},
            "wait_seconds": {"type": "number", "minimum": 0, "maximum": MAX_WAIT_SEC, "default": 0},
        }},
    },
    {
        "name": "dpf_get_job",
        "description": "잡 상태 조회. 끝난 잡이면 include_result(기본 true)로 분석 결과 JSON 을 함께 돌려준다. "
                       "wait_seconds>0 이면 끝날 때까지 그만큼 기다린다.",
        "inputSchema": {"type": "object", "required": ["job_id"], "properties": {
            "job_id": {"type": "string"},
            "include_result": {"type": "boolean", "default": True},
            "wait_seconds": {"type": "number", "minimum": 0, "maximum": MAX_WAIT_SEC, "default": 0},
        }},
    },
    {
        "name": "dpf_list_jobs",
        "description": "최근 잡 목록 (최신순).",
        "inputSchema": {"type": "object", "properties": {
            "limit": {"type": "integer", "minimum": 1, "maximum": 500, "default": 20},
            "status": {"type": "string", "enum": ["queued", "running", "succeeded", "failed",
                                                  "timeout", "cancelled", "interrupted"]},
        }},
    },
    {
        "name": "dpf_cancel_job",
        "description": "대기/실행 중인 잡 취소 (실행 중이면 DPF 프로세스 트리를 종료).",
        "inputSchema": {"type": "object", "required": ["job_id"], "properties": {
            "job_id": {"type": "string"}}},
    },
]
_TOOL_NAMES = {t["name"] for t in TOOLS}


class McpHandler:
    def __init__(self, jobs, health_fn):
        self.jobs = jobs
        self.health_fn = health_fn

    # ------------------------------------------------------------------ JSON-RPC
    def handle(self, payload):
        """payload: 파싱된 JSON (dict 또는 batch list). 반환: 응답 dict / list / None(알림만)."""
        if isinstance(payload, list):
            if not payload:
                return _error(None, -32600, "empty batch")
            out = [r for r in (self._one(m) for m in payload) if r is not None]
            return out or None
        return self._one(payload)

    def _one(self, msg):
        if not isinstance(msg, dict) or msg.get("jsonrpc") != "2.0" or not isinstance(msg.get("method"), str):
            return _error(msg.get("id") if isinstance(msg, dict) else None, -32600, "invalid request")
        method = msg["method"]
        is_notification = "id" not in msg
        rid = msg.get("id")
        params = msg.get("params") or {}

        if is_notification:
            return None   # notifications/initialized 등 — 응답 없음

        try:
            if method == "initialize":
                requested = params.get("protocolVersion")
                version = requested if requested in SUPPORTED_PROTOCOLS else SUPPORTED_PROTOCOLS[0]
                return _result(rid, {
                    "protocolVersion": version,
                    "capabilities": {"tools": {"listChanged": False}},
                    "serverInfo": {"name": SERVER_NAME, "version": SERVER_VERSION},
                    "instructions": "ANSYS .rst 결과 파일 DPF 후처리 서버. dpf_submit_analysis 로 잡을 내고 "
                                    "dpf_get_job 으로 결과를 받는다.",
                })
            if method == "ping":
                return _result(rid, {})
            if method == "tools/list":
                return _result(rid, {"tools": TOOLS})
            if method == "tools/call":
                return _result(rid, self._call(params.get("name"), params.get("arguments") or {}))
            return _error(rid, -32601, "method not found: %s" % method)
        except _InvalidParams as e:
            return _error(rid, -32602, str(e))
        except Exception as e:
            return _error(rid, -32603, "internal error: %s: %s" % (type(e).__name__, e))

    # ------------------------------------------------------------------ tools
    def _call(self, name, args):
        if name not in _TOOL_NAMES:
            raise _InvalidParams("unknown tool: %s" % name)
        if not isinstance(args, dict):
            raise _InvalidParams("arguments must be an object")
        try:
            data = getattr(self, "_t_" + name)(args)
            return _tool_content(data, False)
        except JobError as e:
            # 도구 실행 오류는 JSON-RPC 오류가 아니라 isError 결과로 — LLM 이 읽고 고칠 수 있게
            return _tool_content({"error": str(e), "status_code": e.status_code}, True)

    def _t_dpf_health(self, a):
        return self.health_fn(bool(a.get("deep", False)))

    def _t_dpf_submit_analysis(self, a):
        rst = a.get("rst_path")
        if not isinstance(rst, str) or not rst:
            raise _InvalidParams("rst_path (string) is required")
        job = self.jobs.submit_path(rst, a.get("rst2_path") or None)
        return self._maybe_wait(job["job_id"], a.get("wait_seconds", 0), True)

    def _t_dpf_get_job(self, a):
        job_id = a.get("job_id")
        if not isinstance(job_id, str):
            raise _InvalidParams("job_id (string) is required")
        return self._maybe_wait(job_id, a.get("wait_seconds", 0), a.get("include_result", True))

    def _t_dpf_list_jobs(self, a):
        return {"jobs": self.jobs.list(limit=_num(a.get("limit", 20), 1, 500), status=a.get("status"))}

    def _t_dpf_cancel_job(self, a):
        job_id = a.get("job_id")
        if not isinstance(job_id, str):
            raise _InvalidParams("job_id (string) is required")
        return self.jobs.cancel(job_id)

    def _maybe_wait(self, job_id, wait_seconds, include_result):
        wait = _num(wait_seconds, 0, MAX_WAIT_SEC)
        job = self.jobs.wait(job_id, wait) if wait > 0 else self.jobs.get(job_id)
        out = {"job": self.jobs.public(job)}
        if include_result and job["status"] in FINISHED:
            try:
                out["result"] = self.jobs.result(job_id)
            except JobError as e:
                out["result_error"] = str(e)
        if job["status"] not in FINISHED:
            out["hint"] = "아직 %s — dpf_get_job 으로 다시 조회" % job["status"]
        elif job["status"] != SUCCEEDED:
            out["hint"] = "실패 원인은 job.error 와 REST GET /jobs/{id}/log 참고"
        return out


class _InvalidParams(Exception):
    pass


def _num(v, lo, hi):
    try:
        v = float(v)
    except (TypeError, ValueError):
        raise _InvalidParams("expected a number, got %r" % (v,))
    return max(lo, min(hi, v))


def _tool_content(data, is_error):
    return {
        "content": [{"type": "text", "text": json.dumps(data, ensure_ascii=False)}],
        "structuredContent": data,
        "isError": is_error,
    }


def _result(rid, result):
    return {"jsonrpc": "2.0", "id": rid, "result": result}


def _error(rid, code, message):
    return {"jsonrpc": "2.0", "id": rid, "error": {"code": code, "message": message}}
