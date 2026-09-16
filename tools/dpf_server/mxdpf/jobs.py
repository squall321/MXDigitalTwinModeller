# encoding: utf-8
"""
MX DPF server — 잡 관리자.

잡 1건 = 폴더 1개 (`<work_dir>/<job_id>/`):
    job.json      상태 기록 (재시작 후에도 조회 가능)
    input.rst     업로드 제출일 때만 (경로 제출은 원본 경로를 그대로 씀)
    input2.rst    cross-MAC 용 두 번째 결과 (선택)
    result.json   mx_batch.py 출력
    run.log       자식 프로세스 stdout+stderr

실행은 mx_batch.py 를 **서브프로세스**로 돌린다. 이유:
  - DPF 서버(Ans.Dpf.Grpc)·라이선스 오류가 서버 프로세스를 죽이지 않게 격리
  - 타임아웃·취소를 프로세스 kill 로 확실하게
  - 데스크톱 ACT 가 쓰는 CLI 와 완전히 같은 경로 (결과 JSON 스키마 동일)
asyncio 서브프로세스 대신 스레드 + subprocess.Popen 을 쓴다 — Windows 에서 uvicorn 이
Selector 이벤트 루프를 잡으면 asyncio 서브프로세스가 NotImplementedError 를 던지기 때문.
"""
import json
import os
import queue
import shutil
import subprocess
import threading
import time
import uuid
from datetime import datetime, timezone

QUEUED = "queued"
RUNNING = "running"
SUCCEEDED = "succeeded"
FAILED = "failed"
TIMEOUT = "timeout"
CANCELLED = "cancelled"
INTERRUPTED = "interrupted"   # 서버가 실행 중에 내려갔다가 다시 뜬 경우
FINISHED = (SUCCEEDED, FAILED, TIMEOUT, CANCELLED, INTERRUPTED)

_PUBLIC_KEYS = ("job_id", "status", "created_at", "started_at", "finished_at", "duration_sec",
                "source", "rst_name", "rst2_name", "return_code", "error", "analysis_type", "stage_errors")


def _now():
    return datetime.now(timezone.utc).isoformat(timespec="seconds")


def new_job_id():
    return datetime.now(timezone.utc).strftime("%Y%m%d%H%M%S") + "-" + uuid.uuid4().hex[:8]


def valid_job_id(job_id):
    # 폴더명으로 쓰이므로 경로 조작 문자 차단
    return bool(job_id) and len(job_id) <= 64 and all(c.isalnum() or c == "-" for c in job_id)


class JobError(Exception):
    def __init__(self, message, status_code=400):
        super().__init__(message)
        self.status_code = status_code


class JobManager:
    def __init__(self, settings):
        self.s = settings
        os.makedirs(self.s.work_dir, exist_ok=True)
        self._jobs = {}
        self._procs = {}
        self._lock = threading.RLock()
        self._queue = queue.Queue()
        self._stop = threading.Event()
        self._threads = []
        self._seq = 0
        self._recover()

    # ------------------------------------------------------------------ lifecycle
    def start(self):
        for i in range(self.s.max_concurrency):
            t = threading.Thread(target=self._worker, name="dpf-worker-%d" % i, daemon=True)
            t.start()
            self._threads.append(t)
        if self.s.job_ttl_hours > 0:
            t = threading.Thread(target=self._janitor, name="dpf-janitor", daemon=True)
            t.start()
            self._threads.append(t)

    def shutdown(self):
        self._stop.set()
        with self._lock:
            procs = list(self._procs.values())
        for p in procs:
            _kill(p)
        for _ in self._threads:
            self._queue.put(None)

    def _recover(self):
        """디스크의 job.json 을 다시 읽는다. 실행/대기 중이던 잡은 interrupted 로 닫는다
        (자식 프로세스는 이미 없고, 자동 재실행은 라이선스를 몰래 쓰게 되므로 하지 않는다)."""
        for name in sorted(os.listdir(self.s.work_dir)):
            path = os.path.join(self.s.work_dir, name, "job.json")
            if not (valid_job_id(name) and os.path.isfile(path)):
                continue
            try:
                with open(path, "r", encoding="utf-8") as f:
                    job = json.load(f)
            except Exception:
                continue
            if job.get("status") not in FINISHED:
                job["status"] = INTERRUPTED
                job["finished_at"] = _now()
                job["error"] = "server restarted while the job was %s" % job.get("status")
                self._write(job)
            self._jobs[name] = job
            self._seq = max(self._seq, int(job.get("seq") or 0))

    # ------------------------------------------------------------------ submit
    def job_dir(self, job_id):
        return os.path.join(self.s.work_dir, job_id)

    def create_upload_job(self):
        """업로드 제출: 폴더만 먼저 만들고 파일은 호출자가 채운 뒤 enqueue_upload 호출."""
        job_id = new_job_id()
        os.makedirs(self.job_dir(job_id))
        return job_id

    def enqueue_upload(self, job_id, rst_name, rst2_name=None):
        d = self.job_dir(job_id)
        rst = os.path.join(d, "input.rst")
        rst2 = os.path.join(d, "input2.rst") if rst2_name else None
        return self._enqueue(job_id, "upload", rst, rst2, rst_name, rst2_name)

    def discard_upload(self, job_id):
        shutil.rmtree(self.job_dir(job_id), ignore_errors=True)

    def submit_path(self, rst_path, rst2_path=None):
        rst = self.check_path(rst_path)
        rst2 = self.check_path(rst2_path) if rst2_path else None
        job_id = new_job_id()
        os.makedirs(self.job_dir(job_id))
        return self._enqueue(job_id, "path", rst, rst2,
                             os.path.basename(rst), os.path.basename(rst2) if rst2 else None)

    def check_path(self, p):
        if not self.s.allowed_roots:
            raise JobError("path submission is disabled (set MXDPF_ALLOWED_ROOTS)", 403)
        if not p or not isinstance(p, str):
            raise JobError("rst path is required")
        real = os.path.realpath(p)
        if not any(_is_within(real, root) for root in self.s.allowed_roots):
            raise JobError("path is outside MXDPF_ALLOWED_ROOTS: %s" % p, 403)
        if not real.lower().endswith(".rst"):
            raise JobError("only .rst result files are supported: %s" % p)
        if not os.path.isfile(real):
            raise JobError("file not found: %s" % p, 404)
        return real

    def _enqueue(self, job_id, source, rst, rst2, rst_name, rst2_name):
        job = {
            "job_id": job_id, "status": QUEUED, "created_at": _now(),
            "started_at": None, "finished_at": None, "duration_sec": None,
            "source": source, "rst_name": rst_name, "rst2_name": rst2_name,
            "rst_path": rst, "rst2_path": rst2,
            "return_code": None, "error": None, "analysis_type": None, "stage_errors": None,
        }
        with self._lock:
            self._seq += 1
            job["seq"] = self._seq
            self._jobs[job_id] = job
            self._write(job)
        self._queue.put(job_id)
        return self.public(job)

    # ------------------------------------------------------------------ query
    def get(self, job_id):
        if not valid_job_id(job_id):
            raise JobError("invalid job id", 400)
        with self._lock:
            job = self._jobs.get(job_id)
            if job is None:
                raise JobError("job not found: %s" % job_id, 404)
            return dict(job)

    def public(self, job):
        out = {k: job.get(k) for k in _PUBLIC_KEYS}
        if job.get("source") == "path":   # 업로드 잡은 서버 내부 경로를 노출하지 않는다
            out["rst_path"] = job.get("rst_path")
            out["rst2_path"] = job.get("rst2_path")
        if job.get("status") == QUEUED:
            out["queue_position"] = self._queue_position(job["job_id"])
        return out

    def _queue_position(self, job_id):
        with self._lock:
            queued = sorted((j for j in self._jobs.values() if j["status"] == QUEUED),
                            key=lambda j: j.get("seq") or 0)
        for i, j in enumerate(queued):
            if j["job_id"] == job_id:
                return i + 1
        return None

    def list(self, limit=50, status=None):
        with self._lock:
            jobs = sorted(self._jobs.values(), key=lambda j: (j["created_at"], j.get("seq") or 0), reverse=True)
        if status:
            jobs = [j for j in jobs if j["status"] == status]
        return [self.public(j) for j in jobs[:max(1, min(int(limit), 500))]]

    def result(self, job_id):
        job = self.get(job_id)
        path = os.path.join(self.job_dir(job_id), "result.json")
        if job["status"] not in FINISHED:
            raise JobError("job is %s" % job["status"], 409)
        if not os.path.isfile(path):
            raise JobError("no result.json for job (%s)" % job["status"], 404)
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f)

    def log(self, job_id, tail_bytes=65536):
        self.get(job_id)
        path = os.path.join(self.job_dir(job_id), "run.log")
        if not os.path.isfile(path):
            return ""
        with open(path, "rb") as f:
            f.seek(0, os.SEEK_END)
            size = f.tell()
            f.seek(max(0, size - tail_bytes))
            return f.read().decode("utf-8", errors="replace")

    def wait(self, job_id, timeout_sec):
        deadline = time.time() + max(0.0, float(timeout_sec))
        while True:
            job = self.get(job_id)
            if job["status"] in FINISHED or time.time() >= deadline:
                return job
            time.sleep(0.2)

    def stats(self):
        with self._lock:
            counts = {}
            for j in self._jobs.values():
                counts[j["status"]] = counts.get(j["status"], 0) + 1
        return counts

    # ------------------------------------------------------------------ cancel / delete
    def cancel(self, job_id):
        with self._lock:
            job = self._jobs.get(job_id) if valid_job_id(job_id) else None
            if job is None:
                raise JobError("job not found: %s" % job_id, 404)
            if job["status"] in FINISHED:
                return self.public(job)
            job["cancel_requested"] = True
            proc = self._procs.get(job_id)
            if job["status"] == QUEUED:
                self._finish(job, CANCELLED, error="cancelled before start")
        if proc is not None:
            _kill(proc)
        return self.public(self.get(job_id))

    def delete(self, job_id):
        job = self.get(job_id)
        if job["status"] not in FINISHED:
            raise JobError("job is %s — cancel it first" % job["status"], 409)
        with self._lock:
            self._jobs.pop(job_id, None)
        shutil.rmtree(self.job_dir(job_id), ignore_errors=True)
        return {"job_id": job_id, "deleted": True}

    # ------------------------------------------------------------------ execution
    def _worker(self):
        while not self._stop.is_set():
            job_id = self._queue.get()
            if job_id is None:
                return
            with self._lock:
                job = self._jobs.get(job_id)
                if job is None or job["status"] != QUEUED:
                    continue   # 대기 중 취소/삭제됨
                job["status"] = RUNNING
                job["started_at"] = _now()
                self._write(job)
            try:
                self._run(job)
            except Exception as e:   # 워커 스레드는 절대 죽지 않는다
                with self._lock:
                    self._finish(job, FAILED, error="server error: %s: %s" % (type(e).__name__, e))

    def build_command(self, job):
        out = os.path.join(self.job_dir(job["job_id"]), "result.json")
        cmd = [self.s.python, "-u", self.s.batch_script, job["rst_path"], out]
        if job.get("rst2_path"):
            cmd += ["--rst2", job["rst2_path"]]
        return cmd

    def _run(self, job):
        job_id = job["job_id"]
        d = self.job_dir(job_id)
        if not os.path.isfile(self.s.batch_script):
            with self._lock:
                self._finish(job, FAILED, error="batch script not found: %s" % self.s.batch_script)
            return

        env = os.environ.copy()
        env["PYTHONIOENCODING"] = "utf-8"
        t0 = time.time()
        with open(os.path.join(d, "run.log"), "wb") as log:
            kwargs = {"stdout": log, "stderr": subprocess.STDOUT, "cwd": d, "env": env}
            if os.name == "nt":
                kwargs["creationflags"] = getattr(subprocess, "CREATE_NO_WINDOW", 0)
            else:
                kwargs["start_new_session"] = True   # 손자 프로세스까지 한 번에 kill
            try:
                proc = subprocess.Popen(self.build_command(job), **kwargs)
            except OSError as e:
                with self._lock:
                    self._finish(job, FAILED, error="cannot start python (%s): %s" % (self.s.python, e))
                return
            with self._lock:
                self._procs[job_id] = proc
                cancel_early = job.get("cancel_requested")
            if cancel_early:
                _kill(proc)
            timed_out = False
            try:
                rc = proc.wait(timeout=self.s.job_timeout_sec)
            except subprocess.TimeoutExpired:
                timed_out = True
                _kill(proc)
                rc = proc.wait()
            finally:
                with self._lock:
                    self._procs.pop(job_id, None)

        analysis_type, fatal, stage_errors = _peek_result(os.path.join(d, "result.json"))
        with self._lock:
            job["return_code"] = rc
            job["duration_sec"] = round(time.time() - t0, 2)
            job["analysis_type"] = analysis_type
            job["stage_errors"] = stage_errors   # mx_batch 는 단계별 실패를 errors 에 담고 계속 간다
            if job.get("cancel_requested"):
                self._finish(job, CANCELLED, error="cancelled while running")
            elif timed_out:
                self._finish(job, TIMEOUT, error="exceeded MXDPF_JOB_TIMEOUT_SEC=%d" % self.s.job_timeout_sec)
            elif rc == 0 and analysis_type is not None:
                self._finish(job, SUCCEEDED)
            else:
                self._finish(job, FAILED, error=fatal or "mx_batch exited with code %s" % rc)

    def _finish(self, job, status, error=None):
        job["status"] = status
        job["finished_at"] = _now()
        if error:
            job["error"] = error
        self._write(job)

    def _write(self, job):
        d = self.job_dir(job["job_id"])
        if not os.path.isdir(d):
            return
        tmp = os.path.join(d, "job.json.tmp")
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(job, f, indent=2, ensure_ascii=False)
        os.replace(tmp, os.path.join(d, "job.json"))

    # ------------------------------------------------------------------ TTL cleanup
    def cleanup_expired(self, now=None):
        now = now or time.time()
        ttl = self.s.job_ttl_hours * 3600
        removed = []
        with self._lock:
            for job_id, job in list(self._jobs.items()):
                if job["status"] not in FINISHED or not job.get("finished_at"):
                    continue
                try:
                    finished = datetime.fromisoformat(job["finished_at"]).timestamp()
                except ValueError:
                    continue
                if now - finished > ttl:
                    self._jobs.pop(job_id, None)
                    removed.append(job_id)
        for job_id in removed:
            shutil.rmtree(self.job_dir(job_id), ignore_errors=True)
        return removed

    def _janitor(self):
        while not self._stop.wait(600):
            try:
                self.cleanup_expired()
            except Exception:
                pass


def _is_within(path, root):
    try:
        return os.path.commonpath([os.path.normcase(path), os.path.normcase(root)]) == os.path.normcase(root)
    except ValueError:   # Windows: 드라이브가 다르면 commonpath 가 ValueError
        return False


def _peek_result(path):
    """(analysis_type, fatal, stage_errors) — result.json 이 없거나 깨졌으면 (None, 이유, None)."""
    if not os.path.isfile(path):
        return None, "mx_batch wrote no result.json", None
    try:
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
    except Exception as e:
        return None, "result.json is not valid JSON: %s" % e, None
    if not isinstance(data, dict):
        return None, "result.json is not an object", None
    if data.get("fatal"):
        return None, str(data["fatal"]), None
    errors = data.get("errors")
    return data.get("analysis_type"), None, (len(errors) if isinstance(errors, list) else 0)


def _kill(proc):
    if proc.poll() is not None:
        return
    try:
        if os.name == "nt":
            # DPF 는 Ans.Dpf.Grpc.exe 자식을 띄우므로 트리 전체를 죽인다
            subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)],
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=30)
        else:
            import signal
            os.killpg(proc.pid, signal.SIGKILL)
    except Exception:
        try:
            proc.kill()
        except Exception:
            pass
