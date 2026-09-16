# encoding: utf-8
"""python -m mxdpf [--host H] [--port P]  — 설정은 환경변수 (mxdpf/config.py 표 참고)."""
import argparse
import logging
import os

from .config import Settings


def main():
    ap = argparse.ArgumentParser(description="MX DPF sidecar server (REST + MCP)")
    ap.add_argument("--host", default=None, help="bind address (default MXDPF_HOST or 127.0.0.1)")
    ap.add_argument("--port", type=int, default=None, help="port (default MXDPF_PORT or 8770)")
    args = ap.parse_args()

    s = Settings.from_env()
    if args.host:
        s.host = args.host
    if args.port:
        s.port = args.port

    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
    log = logging.getLogger("mxdpf")
    log.info("MX DPF server on http://%s:%d  (REST /docs, MCP /mcp)", s.host, s.port)
    log.info("python=%s", s.python)
    log.info("batch_script=%s (exists=%s)", s.batch_script, os.path.isfile(s.batch_script))
    log.info("work_dir=%s concurrency=%d timeout=%ds ttl=%dh", s.work_dir, s.max_concurrency,
             s.job_timeout_sec, s.job_ttl_hours)
    log.info("auth=%s path_roots=%s", "bearer" if s.token else "NONE", s.allowed_roots or "(disabled)")
    if s.host not in ("127.0.0.1", "localhost", "::1") and not s.token:
        log.warning("binding to %s WITHOUT MXDPF_TOKEN — anyone who can reach this port can submit jobs", s.host)

    import uvicorn
    from .app import create_app
    # 잡 상태가 프로세스 메모리에 있으므로 워커는 반드시 1개
    uvicorn.run(create_app(s), host=s.host, port=s.port, workers=1, log_level="info")


if __name__ == "__main__":
    main()
