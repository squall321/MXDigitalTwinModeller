# encoding: utf-8
"""테스트용 가짜 mx_batch.py — 라이선스/ANSYS 없이 서버 동작만 검증한다.
입력 .rst 파일 내용의 첫 단어로 동작을 고른다:
  SLEEP <sec>  -> 그만큼 잠든 뒤 정상 결과
  FATAL        -> fatal JSON 쓰고 exit 1 (mx_batch 의 실제 실패 형태)
  CRASH        -> JSON 없이 exit 3
  GARBAGE      -> 깨진 JSON 쓰고 exit 0
  PARTIAL      -> 정상 종료지만 errors 에 단계 실패 2건
  그 외         -> 모달 결과 정상 출력
"""
import argparse
import json
import sys
import time

AWP = "/fake/ansys/v252"


def start_server():
    import ansys.dpf.core as dpf
    return dpf.server.get_or_create_server(None)


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("rst")
    ap.add_argument("out_json")
    ap.add_argument("--rst2", default=None)
    ap.add_argument("--merge", default=None)
    a = ap.parse_args(argv)
    with open(a.rst, "r", encoding="utf-8", errors="replace") as f:
        words = f.read().split()
    cmd = words[0] if words else ""
    print("FAKE_MX_BATCH start", cmd, flush=True)
    if cmd == "SLEEP":
        time.sleep(float(words[1]))
    if cmd == "CRASH":
        return 3
    with open(a.out_json, "w", encoding="utf-8") as f:
        if cmd == "GARBAGE":
            f.write("{not json")
            return 0
        if cmd == "FATAL":
            json.dump({"schema_version": "2.1", "fatal": "LicenseError: no DPF premium license"}, f)
            return 1
        json.dump({"schema_version": "2.1", "source": "fake", "rst": a.rst, "analysis_type": "modal",
                   "errors": (["participation: RuntimeError: x", "hotspots: KeyError: y"] if cmd == "PARTIAL" else []),
                   "modal": {"n_modes": 3, "freqs_hz": [101.0, 230.5, 412.2], "unit": "Hz"},
                   "mac": {"mode": "cross" if a.rst2 else "self", "present": True},
                   "rst2": a.rst2}, f)
    print("MX_BATCH_DONE:", a.out_json, flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
