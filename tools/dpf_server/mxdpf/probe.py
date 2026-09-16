# encoding: utf-8
"""
MX DPF server — 딥 헬스 프로브 (GET /health?deep=true 가 서브프로세스로 실행).

라이선스가 잡힌 서버에서 "DPF 서버 기동 → ansys-dpf-core 번들 예제 static.rst 읽기" 까지
끝까지 가 보고 결과 한 줄 JSON 을 stdout 에 쓴다. 네트워크를 쓰지 않는다 (번들 예제만).

    python -m mxdpf.probe <mx_batch.py 경로>
"""
import json
import os
import sys
import time


def main():
    out = {"ok": False, "stages": {}}
    t0 = time.time()
    batch = sys.argv[1] if len(sys.argv) > 1 else None
    try:
        import ansys.dpf.core as dpf
        out["dpf_core_version"] = getattr(dpf, "__version__", None)
        out["stages"]["import"] = "ok"
    except Exception as e:
        out["stages"]["import"] = "fail: %s: %s" % (type(e).__name__, e)
        print(json.dumps(out))
        return 1

    try:
        if batch and os.path.isfile(batch):
            sys.path.insert(0, os.path.dirname(os.path.abspath(batch)))
            import mx_batch
            server = mx_batch.start_server()     # 실제 잡과 똑같은 기동 경로
            out["awp_root"] = mx_batch.AWP
        else:
            server = dpf.server.get_or_create_server(None)
        out["server_version"] = str(getattr(server, "version", "") or "")
        out["ansys_path"] = str(getattr(server, "ansys_path", "") or "")
        out["stages"]["server"] = "ok"
    except Exception as e:
        out["stages"]["server"] = "fail: %s: %s" % (type(e).__name__, str(e)[:300])
        print(json.dumps(out))
        return 1

    try:
        from ansys.dpf.core import examples
        model = dpf.Model(examples.find_static_rst())
        disp = model.results.displacement().eval()
        out["stages"]["read_result"] = "ok (%d nodes)" % len(disp[0].data)
        out["ok"] = True
    except Exception as e:
        # 라이선스 문제는 보통 여기서 드러난다 (premium 컨텍스트 operator 호출)
        out["stages"]["read_result"] = "fail: %s: %s" % (type(e).__name__, str(e)[:300])

    out["elapsed_sec"] = round(time.time() - t0, 2)
    print(json.dumps(out))
    return 0 if out["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
