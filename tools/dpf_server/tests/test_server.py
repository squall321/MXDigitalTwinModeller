# encoding: utf-8
"""
MX DPF server 테스트 — ANSYS/라이선스 없이 도는 부분 전부.
가짜 mx_batch.py(tests/fake) 로 잡 실행 경로를, 가짜 ansys.dpf.core 로 딥 헬스를 검증한다.

    cd tools/dpf_server && python -m pytest -q tests
"""
import io
import json
import os
import sys
import time

import pytest
from fastapi.testclient import TestClient

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
FAKE = os.path.join(HERE, "fake")
sys.path.insert(0, ROOT)

from mxdpf.app import create_app          # noqa: E402
from mxdpf.config import Settings          # noqa: E402
from mxdpf.jobs import JobManager          # noqa: E402


def make_settings(tmp_path, **kw):
    base = dict(work_dir=str(tmp_path / "jobs"), python=sys.executable,
                batch_script=os.path.join(FAKE, "mx_batch.py"), max_concurrency=1,
                job_timeout_sec=30, max_upload_mb=5, job_ttl_hours=0, token=None,
                allowed_roots=[], allowed_origins=[])
    base.update(kw)
    return Settings(**base)


@pytest.fixture
def client_factory(tmp_path):
    clients = []

    def make(**kw):
        c = TestClient(create_app(make_settings(tmp_path, **kw)))
        c.__enter__()
        clients.append(c)
        return c

    yield make
    for c in clients:
        c.__exit__(None, None, None)


def upload(client, content=b"OK", name="model.rst", rst2=None, headers=None):
    files = {"rst": (name, io.BytesIO(content), "application/octet-stream")}
    if rst2 is not None:
        files["rst2"] = ("other.rst", io.BytesIO(rst2), "application/octet-stream")
    return client.post("/jobs", files=files, headers=headers or {})


def wait_done(client, job_id, timeout=30, headers=None):
    r = client.get("/jobs/%s?wait=%d" % (job_id, timeout), headers=headers or {})
    assert r.status_code == 200, r.text
    return r.json()


# ---------------------------------------------------------------------------- basics
def test_root_and_health(client_factory):
    c = client_factory()
    assert c.get("/").json()["mcp"] == "/mcp"
    h = c.get("/health").json()
    assert h["status"] == "ok"
    assert h["batch_script_exists"] is True
    assert h["path_submission"] is False


def test_health_degraded_when_batch_missing(client_factory, tmp_path):
    c = client_factory(batch_script=str(tmp_path / "nope.py"))
    assert c.get("/health").json()["status"] == "degraded"


# ---------------------------------------------------------------------------- upload jobs
def test_upload_success_result_and_log(client_factory):
    c = client_factory()
    r = upload(c)
    assert r.status_code == 202, r.text
    job = r.json()
    assert job["status"] in ("queued", "running")
    done = wait_done(c, job["job_id"])
    assert done["status"] == "succeeded", done
    assert done["analysis_type"] == "modal"
    assert done["rst_name"] == "model.rst"
    res = c.get("/jobs/%s/result" % job["job_id"]).json()
    assert res["modal"]["freqs_hz"] == [101.0, 230.5, 412.2]
    assert res["mac"]["mode"] == "self"
    log = c.get("/jobs/%s/log" % job["job_id"]).text
    assert "FAKE_MX_BATCH start" in log and "MX_BATCH_DONE" in log


def test_upload_cross_mac_passes_rst2(client_factory):
    c = client_factory()
    job = upload(c, rst2=b"OK").json()
    done = wait_done(c, job["job_id"])
    assert done["status"] == "succeeded" and done["rst2_name"] == "other.rst"
    res = c.get("/jobs/%s/result" % job["job_id"]).json()
    assert res["mac"]["mode"] == "cross" and res["rst2"].endswith("input2.rst")


def test_upload_rejects_wrong_extension_empty_and_oversize(client_factory):
    c = client_factory(max_upload_mb=1)
    assert upload(c, name="model.txt").status_code == 415
    r = upload(c, content=b"")
    assert r.status_code == 400 and "empty" in r.json()["error"]
    r = upload(c, content=b"x" * (int(1.5 * 1024 * 1024)))     # 파일 한도 초과 (스트리밍 저장 중 차단)
    assert r.status_code == 413
    r = upload(c, content=b"x" * (4 * 1024 * 1024))             # 선언 크기로 미들웨어가 먼저 차단
    assert r.status_code == 413 and "per file" in r.json()["error"]
    # 거부된 업로드는 잡 폴더를 남기지 않는다
    assert c.get("/jobs").json()["jobs"] == []


def test_result_before_finish_is_409(client_factory):
    c = client_factory()
    job = upload(c, content=b"SLEEP 3").json()
    r = c.get("/jobs/%s/result" % job["job_id"])
    assert r.status_code == 409
    assert wait_done(c, job["job_id"])["status"] == "succeeded"


@pytest.mark.parametrize("content,needle", [
    (b"FATAL", "LicenseError"),
    (b"CRASH", "no result.json"),
    (b"GARBAGE", "not valid JSON"),
])
def test_failures_are_reported(client_factory, content, needle):
    c = client_factory()
    job = upload(c, content=content).json()
    done = wait_done(c, job["job_id"])
    assert done["status"] == "failed", done
    assert needle in done["error"]


def test_partial_stage_errors_still_succeed(client_factory, tmp_path):
    c = client_factory()
    done = wait_done(c, upload(c, content=b"PARTIAL").json()["job_id"])
    assert done["status"] == "succeeded" and done["stage_errors"] == 2
    assert "rst_path" not in done                       # 업로드 잡은 서버 경로 비노출


def test_missing_batch_script_fails_job(client_factory, tmp_path):
    c = client_factory(batch_script=str(tmp_path / "nope.py"))
    done = wait_done(c, upload(c).json()["job_id"])
    assert done["status"] == "failed" and "batch script not found" in done["error"]


def test_bad_python_fails_job(client_factory, tmp_path):
    c = client_factory(python=str(tmp_path / "no-python"))
    done = wait_done(c, upload(c).json()["job_id"])
    assert done["status"] == "failed" and "cannot start python" in done["error"]


def test_timeout_kills_job(client_factory):
    c = client_factory(job_timeout_sec=1)
    t0 = time.time()
    done = wait_done(c, upload(c, content=b"SLEEP 20").json()["job_id"])
    assert done["status"] == "timeout", done
    assert time.time() - t0 < 10


# ---------------------------------------------------------------------------- cancel / delete / queue
def test_cancel_running_and_queued(client_factory):
    c = client_factory(max_concurrency=1)
    running = upload(c, content=b"SLEEP 20").json()
    queued = upload(c, content=b"OK").json()
    # 첫 잡이 실제로 실행되기를 기다린다
    for _ in range(50):
        if c.get("/jobs/%s" % running["job_id"]).json()["status"] == "running":
            break
        time.sleep(0.1)
    q = c.get("/jobs/%s" % queued["job_id"]).json()
    assert q["status"] == "queued" and q["queue_position"] == 1

    assert c.delete("/jobs/%s" % running["job_id"]).status_code == 409   # 실행 중 삭제 금지

    r = c.post("/jobs/%s/cancel" % queued["job_id"]).json()
    assert r["status"] == "cancelled"
    t0 = time.time()
    c.post("/jobs/%s/cancel" % running["job_id"])
    done = wait_done(c, running["job_id"])
    assert done["status"] == "cancelled" and time.time() - t0 < 10

    # 취소된 잡은 워커가 건너뛰고, 다음 잡은 정상 처리된다
    nxt = wait_done(c, upload(c).json()["job_id"])
    assert nxt["status"] == "succeeded"
    assert c.get("/jobs/%s" % queued["job_id"]).json()["status"] == "cancelled"

    assert c.delete("/jobs/%s" % running["job_id"]).json()["deleted"] is True
    assert c.get("/jobs/%s" % running["job_id"]).status_code == 404


def test_invalid_job_id(client_factory):
    c = client_factory()
    assert c.get("/jobs/..%2F..%2Fetc").status_code in (400, 404)
    assert c.get("/jobs/bad_id!").status_code == 400
    assert c.get("/jobs/20260101000000-deadbeef").status_code == 404


def test_list_filters(client_factory):
    c = client_factory()
    a = upload(c).json()
    b = upload(c, content=b"FATAL").json()
    wait_done(c, a["job_id"]); wait_done(c, b["job_id"])
    jobs = c.get("/jobs").json()["jobs"]
    assert [j["job_id"] for j in jobs][:2] == [b["job_id"], a["job_id"]]
    failed = c.get("/jobs?status=failed").json()["jobs"]
    assert [j["job_id"] for j in failed] == [b["job_id"]]


# ---------------------------------------------------------------------------- path submission
def test_path_submission_disabled_by_default(client_factory, tmp_path):
    c = client_factory()
    r = c.post("/jobs/by-path", json={"rst_path": str(tmp_path / "a.rst")})
    assert r.status_code == 403


def test_path_submission_rules(client_factory, tmp_path):
    share = tmp_path / "share"
    share.mkdir()
    (share / "a.rst").write_bytes(b"OK")
    (share / "notes.txt").write_bytes(b"x")
    outside = tmp_path / "outside.rst"
    outside.write_bytes(b"OK")
    c = client_factory(allowed_roots=[str(share)])

    ok = c.post("/jobs/by-path", json={"rst_path": str(share / "a.rst")})
    assert ok.status_code == 202
    done = wait_done(c, ok.json()["job_id"])
    assert done["status"] == "succeeded" and done["stage_errors"] == 0
    assert done["rst_path"] == os.path.realpath(str(share / "a.rst"))

    assert c.post("/jobs/by-path", json={"rst_path": str(outside)}).status_code == 403
    assert c.post("/jobs/by-path", json={"rst_path": str(share / ".." / "outside.rst")}).status_code == 403
    assert c.post("/jobs/by-path", json={"rst_path": str(share / "notes.txt")}).status_code == 400
    assert c.post("/jobs/by-path", json={"rst_path": str(share / "missing.rst")}).status_code == 404
    assert c.post("/jobs/by-path", json={}).status_code == 400

    # 허용 루트 이름으로 시작하는 형제 폴더(share-evil)는 밖이다
    evil = tmp_path / "share-evil"
    evil.mkdir()
    (evil / "b.rst").write_bytes(b"OK")
    assert c.post("/jobs/by-path", json={"rst_path": str(evil / "b.rst")}).status_code == 403


def test_path_symlink_escape_blocked(client_factory, tmp_path):
    share = tmp_path / "share"
    share.mkdir()
    secret = tmp_path / "secret.rst"
    secret.write_bytes(b"OK")
    link = share / "link.rst"
    try:
        link.symlink_to(secret)
    except (OSError, NotImplementedError):
        pytest.skip("symlinks not available")
    c = client_factory(allowed_roots=[str(share)])
    assert c.post("/jobs/by-path", json={"rst_path": str(link)}).status_code == 403


# ---------------------------------------------------------------------------- security
def test_bearer_token(client_factory):
    c = client_factory(token="s3cret")
    assert c.get("/health").status_code == 200                      # 기본 헬스는 공개
    assert c.get("/health?deep=true").status_code == 401            # 딥 헬스는 라이선스를 쓰므로 인증
    assert c.get("/jobs").status_code == 401
    assert c.get("/jobs", headers={"Authorization": "Bearer wrong"}).status_code == 401
    assert c.post("/mcp", json={"jsonrpc": "2.0", "id": 1, "method": "ping"}).status_code == 401
    h = {"Authorization": "Bearer s3cret"}
    job = upload(c, headers=h).json()
    assert wait_done(c, job["job_id"], headers=h)["status"] == "succeeded"


def test_origin_blocked_unless_allowed(client_factory):
    c = client_factory(allowed_origins=["https://portal.example"])
    assert c.get("/jobs", headers={"Origin": "https://evil.example"}).status_code == 403
    assert c.get("/jobs", headers={"Origin": "https://portal.example"}).status_code == 200
    assert c.get("/jobs").status_code == 200


# ---------------------------------------------------------------------------- deep health (fake DPF)
def test_deep_health_ok_and_license_failure(client_factory, monkeypatch):
    monkeypatch.setenv("PYTHONPATH", FAKE)
    c = client_factory()
    d = c.get("/health?deep=true").json()
    assert d["deep"]["ok"] is True, d
    assert d["deep"]["stages"]["server"] == "ok"
    assert d["deep"]["server_version"] == "10.0-fake"
    assert d["status"] == "ok"

    monkeypatch.setenv("FAKE_DPF_NO_LICENSE", "1")
    c2 = client_factory()                                            # 새 앱 = 캐시 없음
    d2 = c2.get("/health?deep=true").json()
    assert d2["deep"]["ok"] is False and "license" in d2["deep"]["stages"]["server"]
    assert d2["status"] == "degraded"


def test_deep_health_without_dpf_installed(client_factory, monkeypatch):
    monkeypatch.delenv("PYTHONPATH", raising=False)
    c = client_factory()
    d = c.get("/health?deep=true").json()
    assert d["deep"]["ok"] is False
    assert d["deep"]["stages"]["import"].startswith("fail")


# ---------------------------------------------------------------------------- MCP
def rpc(c, method, params=None, rid=1, headers=None):
    msg = {"jsonrpc": "2.0", "id": rid, "method": method}
    if params is not None:
        msg["params"] = params
    r = c.post("/mcp", json=msg, headers=headers or {})
    assert r.status_code == 200, r.text
    return r.json()


def test_mcp_handshake_and_tools_list(client_factory):
    c = client_factory()
    init = rpc(c, "initialize", {"protocolVersion": "2025-03-26", "capabilities": {},
                                 "clientInfo": {"name": "t", "version": "0"}})
    assert init["result"]["protocolVersion"] == "2025-03-26"
    assert init["result"]["serverInfo"]["name"] == "mxdtm-dpf"
    unknown = rpc(c, "initialize", {"protocolVersion": "1999-01-01"})
    assert unknown["result"]["protocolVersion"] == "2025-06-18"

    r = c.post("/mcp", json={"jsonrpc": "2.0", "method": "notifications/initialized"})
    assert r.status_code == 202 and r.content == b""

    tools = rpc(c, "tools/list")["result"]["tools"]
    names = {t["name"] for t in tools}
    assert names == {"dpf_health", "dpf_submit_analysis", "dpf_get_job", "dpf_list_jobs", "dpf_cancel_job"}
    for t in tools:
        assert t["inputSchema"]["type"] == "object" and t["description"]
    assert rpc(c, "ping")["result"] == {}


def test_mcp_submit_wait_and_get(client_factory, tmp_path):
    share = tmp_path / "share"
    share.mkdir()
    (share / "a.rst").write_bytes(b"OK")
    c = client_factory(allowed_roots=[str(share)])

    res = rpc(c, "tools/call", {"name": "dpf_submit_analysis",
                                "arguments": {"rst_path": str(share / "a.rst"), "wait_seconds": 30}})["result"]
    assert res["isError"] is False
    body = res["structuredContent"]
    assert json.loads(res["content"][0]["text"]) == body
    assert body["job"]["status"] == "succeeded"
    assert body["result"]["modal"]["n_modes"] == 3
    job_id = body["job"]["job_id"]

    got = rpc(c, "tools/call", {"name": "dpf_get_job",
                                "arguments": {"job_id": job_id, "include_result": False}})["result"]
    assert got["structuredContent"]["job"]["job_id"] == job_id and "result" not in got["structuredContent"]

    listed = rpc(c, "tools/call", {"name": "dpf_list_jobs", "arguments": {"limit": 5}})["result"]
    assert listed["structuredContent"]["jobs"][0]["job_id"] == job_id

    health = rpc(c, "tools/call", {"name": "dpf_health", "arguments": {}})["result"]
    assert health["structuredContent"]["service"] == "mxdtm-dpf"


def test_mcp_submit_no_wait_then_poll_and_cancel(client_factory, tmp_path):
    share = tmp_path / "share"
    share.mkdir()
    (share / "slow.rst").write_bytes(b"SLEEP 20")
    c = client_factory(allowed_roots=[str(share)])
    body = rpc(c, "tools/call", {"name": "dpf_submit_analysis",
                                 "arguments": {"rst_path": str(share / "slow.rst")}})["result"]["structuredContent"]
    assert body["job"]["status"] in ("queued", "running") and "hint" in body
    cancel = rpc(c, "tools/call", {"name": "dpf_cancel_job", "arguments": {"job_id": body["job"]["job_id"]}})
    assert cancel["result"]["isError"] is False
    final = rpc(c, "tools/call", {"name": "dpf_get_job",
                                  "arguments": {"job_id": body["job"]["job_id"], "wait_seconds": 15}})
    assert final["result"]["structuredContent"]["job"]["status"] == "cancelled"


def test_mcp_errors(client_factory):
    c = client_factory()   # 경로 제출 비활성
    e = rpc(c, "tools/call", {"name": "dpf_submit_analysis", "arguments": {"rst_path": "/x/a.rst"}})["result"]
    assert e["isError"] is True and e["structuredContent"]["status_code"] == 403

    assert rpc(c, "tools/call", {"name": "nope", "arguments": {}})["error"]["code"] == -32602
    assert rpc(c, "tools/call", {"name": "dpf_submit_analysis", "arguments": {}})["error"]["code"] == -32602
    assert rpc(c, "tools/call", {"name": "dpf_get_job", "arguments": {"job_id": "x", "wait_seconds": "abc"}}
               )["error"]["code"] == -32602
    nf = rpc(c, "tools/call", {"name": "dpf_get_job", "arguments": {"job_id": "20260101000000-deadbeef"}})
    assert nf["result"]["isError"] is True and nf["result"]["structuredContent"]["status_code"] == 404
    assert rpc(c, "resources/list")["error"]["code"] == -32601

    r = c.post("/mcp", content=b"{broken", headers={"content-type": "application/json"})
    assert r.json()["error"]["code"] == -32700
    assert c.post("/mcp", json={"id": 1, "method": "ping"}).json()["error"]["code"] == -32600
    assert c.get("/mcp").status_code == 405


def test_mcp_batch(client_factory):
    c = client_factory()
    r = c.post("/mcp", json=[
        {"jsonrpc": "2.0", "id": 1, "method": "ping"},
        {"jsonrpc": "2.0", "method": "notifications/initialized"},
        {"jsonrpc": "2.0", "id": 2, "method": "tools/list"},
    ]).json()
    assert [m["id"] for m in r] == [1, 2]


# ---------------------------------------------------------------------------- persistence / TTL
def test_restart_marks_inflight_interrupted_and_keeps_finished(tmp_path):
    s = make_settings(tmp_path)
    jm = JobManager(s)
    jm.start()
    done = jm.enqueue_upload(_write_input(jm, b"OK"), "a.rst")
    assert jm.wait(done["job_id"], 30)["status"] == "succeeded"
    jm.shutdown()

    # 실행 중이던 잡을 흉내: job.json 을 running 으로 남겨둔다
    jm2 = JobManager(s)
    jid = _write_input(jm2, b"OK")
    jm2._enqueue(jid, "upload", os.path.join(jm2.job_dir(jid), "input.rst"), None, "b.rst", None)
    job = jm2.get(jid)
    job["status"] = "running"
    jm2._write(job)

    jm3 = JobManager(s)   # "재시작"
    assert jm3.get(jid)["status"] == "interrupted"
    assert "restarted" in jm3.get(jid)["error"]
    assert jm3.get(done["job_id"])["status"] == "succeeded"
    assert jm3.result(done["job_id"])["modal"]["n_modes"] == 3
    new = jm3.enqueue_upload(_write_input(jm3, b"OK"), "c.rst")
    assert jm3.get(new["job_id"])["created_at"] and jm3._seq >= 3


def test_ttl_cleanup(tmp_path):
    s = make_settings(tmp_path, job_ttl_hours=1)
    jm = JobManager(s)
    jm.start()
    j = jm.enqueue_upload(_write_input(jm, b"OK"), "a.rst")
    assert jm.wait(j["job_id"], 30)["status"] == "succeeded"
    assert jm.cleanup_expired() == []
    assert jm.cleanup_expired(now=time.time() + 2 * 3600) == [j["job_id"]]
    assert not os.path.exists(jm.job_dir(j["job_id"]))
    jm.shutdown()


def _write_input(jm, content):
    jid = jm.create_upload_job()
    with open(os.path.join(jm.job_dir(jid), "input.rst"), "wb") as f:
        f.write(content)
    return jid
