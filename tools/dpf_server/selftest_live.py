# encoding: utf-8
"""
실서버 GATE — 라이선스가 잡힌 서버에서 **실제 DPF** 로 끝까지 돌려 본다.
표준 라이브러리만 사용 (서버와 다른 PC 에서도 실행 가능).

    python selftest_live.py --url http://<서버>:8770 [--token T] [--rst a.rst ...] [--path /share/b.rst ...]

--rst / --path 를 하나도 안 주면, 이 PC 에 ansys-dpf-core 가 있을 때 번들 예제 static.rst 를 업로드한다.
--modal-example 을 주면 DPF 예제 modal_frame.rst 를 내려받아(네트워크 필요) 모달 경로까지 확인한다.

판정: 마지막 줄 LIVE_GATE_OK / LIVE_GATE_FAIL
"""
import argparse
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
import uuid

FAILS = []


def check(name, cond, detail=""):
    print("  [%s] %s%s" % ("PASS" if cond else "FAIL", name, (" - " + str(detail)) if detail else ""))
    if not cond:
        FAILS.append(name)
    return cond


class Client:
    def __init__(self, url, token, timeout=900):
        self.url = url.rstrip("/")
        self.token = token
        self.timeout = timeout

    def _req(self, method, path, body=None, headers=None):
        h = dict(headers or {})
        if self.token:
            h["Authorization"] = "Bearer " + self.token
        req = urllib.request.Request(self.url + path, data=body, headers=h, method=method)
        try:
            with urllib.request.urlopen(req, timeout=self.timeout) as r:
                raw = r.read()
                return r.status, (json.loads(raw) if raw and "json" in r.headers.get("content-type", "") else raw)
        except urllib.error.HTTPError as e:
            raw = e.read()
            try:
                return e.code, json.loads(raw)
            except ValueError:
                return e.code, raw

    def get(self, path):
        return self._req("GET", path)

    def post_json(self, path, obj):
        return self._req("POST", path, json.dumps(obj).encode("utf-8"), {"Content-Type": "application/json"})

    def upload(self, rst_path):
        boundary = uuid.uuid4().hex
        name = os.path.basename(rst_path)
        with open(rst_path, "rb") as f:
            data = f.read()
        ctype = mimetypes.guess_type(name)[0] or "application/octet-stream"
        body = (("--%s\r\nContent-Disposition: form-data; name=\"rst\"; filename=\"%s\"\r\n"
                 "Content-Type: %s\r\n\r\n" % (boundary, name, ctype)).encode("utf-8")
                + data + ("\r\n--%s--\r\n" % boundary).encode("utf-8"))
        return self._req("POST", "/jobs", body, {"Content-Type": "multipart/form-data; boundary=" + boundary})


def verify_result(res):
    at = (res.get("analysis_type") or "").lower()
    check("no fatal", not res.get("fatal"), res.get("fatal", ""))
    if res.get("errors"):
        print("      (stage errors: %s)" % res["errors"])
    if "modal" in at:
        md = res.get("modal") or {}
        check("modal n_modes >= 1", md.get("n_modes", 0) >= 1, md.get("n_modes"))
        check("modal freqs > 0", all(f > 0 for f in md.get("freqs_hz", [])) and md.get("freqs_hz"),
              md.get("freqs_hz", [])[:5])
        mac = res.get("mac") or {}
        if mac.get("mode") == "self":
            check("self-MAC diag ~ 1", mac.get("diag_min", 0) > 0.99, mac.get("diag_min"))
        check("participation method", (res.get("participation") or {}).get("method") in ("lumped_mass", "unit_mass"))
    else:
        se = res.get("strain_energy") or {}
        check("strain_energy block", se.get("branch") == "static", se.get("error") or se.get("total"))
    check("hotspots block", isinstance(res.get("hotspots"), dict), (res.get("hotspots") or {}).get("n_clusters"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--url", default="http://127.0.0.1:8770")
    ap.add_argument("--token", default=os.environ.get("MXDPF_TOKEN"))
    ap.add_argument("--rst", action="append", default=[], help="이 PC 의 .rst 를 업로드")
    ap.add_argument("--path", action="append", default=[], help="서버 기준 .rst 경로로 제출")
    ap.add_argument("--modal-example", action="store_true")
    ap.add_argument("--wait", type=int, default=900)
    a = ap.parse_args()
    c = Client(a.url, a.token, timeout=a.wait + 60)

    print("[1] health")
    st, h = c.get("/health")
    check("GET /health 200", st == 200, st)
    if st == 200:
        check("batch script on server", h.get("batch_script_exists"), h.get("batch_script"))

    print("[2] deep health (DPF 기동 + 라이선스)")
    st, h = c.get("/health?deep=true")
    deep = (h or {}).get("deep", {}) if isinstance(h, dict) else {}
    check("deep ok", st == 200 and deep.get("ok"), json.dumps(deep, ensure_ascii=False)[:400])

    if not a.rst and not a.path:
        try:
            from ansys.dpf.core import examples
            a.rst.append(examples.find_static_rst())
            if a.modal_example:
                a.rst.append(examples.download_modal_frame())
        except Exception as e:
            check("local DPF examples available (or pass --rst/--path)", False, e)

    print("[3] jobs")
    job_ids = []
    for p in a.rst:
        st, j = c.upload(p)
        if check("upload %s -> 202" % os.path.basename(p), st == 202, j if st != 202 else j["job_id"]):
            job_ids.append(j["job_id"])
    for p in a.path:
        st, j = c.post_json("/jobs/by-path", {"rst_path": p})
        if check("by-path %s -> 202" % p, st == 202, j if st != 202 else j["job_id"]):
            job_ids.append(j["job_id"])

    for jid in job_ids:
        t0 = time.time()
        st, j = c.get("/jobs/%s?wait=%d" % (jid, min(a.wait, 600)))
        while st == 200 and j["status"] in ("queued", "running") and time.time() - t0 < a.wait:
            st, j = c.get("/jobs/%s?wait=60" % jid)
        ok = check("job %s succeeded" % jid, st == 200 and j.get("status") == "succeeded",
                   "%s %s (%.0fs)" % (j.get("status"), j.get("error") or "", time.time() - t0))
        if not ok:
            _, log = c.get("/jobs/%s/log" % jid)
            print("      --- log tail ---\n" + (log.decode("utf-8", "replace") if isinstance(log, bytes) else str(log))[-1500:])
            continue
        _, res = c.get("/jobs/%s/result" % jid)
        print("   result: analysis_type=%s" % res.get("analysis_type"))
        verify_result(res)

    print("[4] MCP")
    st, init = c.post_json("/mcp", {"jsonrpc": "2.0", "id": 1, "method": "initialize",
                                    "params": {"protocolVersion": "2025-06-18", "capabilities": {},
                                               "clientInfo": {"name": "selftest_live", "version": "1"}}})
    check("initialize", st == 200 and "result" in init, init if st != 200 else init["result"]["serverInfo"])
    st, tl = c.post_json("/mcp", {"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
    check("tools/list = 5", st == 200 and len(tl["result"]["tools"]) == 5)
    if job_ids:
        st, g = c.post_json("/mcp", {"jsonrpc": "2.0", "id": 3, "method": "tools/call",
                                     "params": {"name": "dpf_get_job", "arguments": {"job_id": job_ids[0]}}})
        check("tools/call dpf_get_job", st == 200 and not g["result"]["isError"]
              and "result" in g["result"]["structuredContent"])

    print("\nLIVE_GATE_%s%s" % ("OK" if not FAILS else "FAIL", "" if not FAILS else " - " + ", ".join(FAILS)))
    return 0 if not FAILS else 1


if __name__ == "__main__":
    sys.exit(main())
