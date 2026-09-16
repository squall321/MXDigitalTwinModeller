# encoding: utf-8
"""
MX DPF server — 설정 (전부 환경변수, 기본값은 개발 PC 기준).

| 변수                      | 기본값                                              | 의미 |
|---------------------------|-----------------------------------------------------|------|
| MXDPF_HOST                | 127.0.0.1                                           | 바인드 주소 (게이트웨이 뒤라면 0.0.0.0) |
| MXDPF_PORT                | 8770                                                | 포트 |
| MXDPF_TOKEN               | (없음)                                              | 설정하면 /health 외 모든 요청에 Bearer 토큰 요구 |
| MXDPF_WORK_DIR            | ~/.mxdtm/dpf_jobs                                   | 잡 폴더 루트 (입력 .rst, 결과 JSON, 로그) |
| MXDPF_PYTHON              | 현재 인터프리터                                     | mx_batch.py 를 돌릴 python (ansys-dpf-core 설치된 venv) |
| MXDPF_BATCH_SCRIPT        | <repo>/Mechanical/MXSimulator/batch/mx_batch.py    | 사이드카 스크립트 경로 |
| MXDPF_MAX_CONCURRENCY     | 1                                                   | 동시 실행 잡 수 (DPF/라이선스 동시성 검증 전까지 1) |
| MXDPF_JOB_TIMEOUT_SEC     | 1800                                                | 잡 1건 최대 실행 시간 |
| MXDPF_MAX_UPLOAD_MB       | 4096                                                | 업로드 1파일 최대 크기 |
| MXDPF_JOB_TTL_HOURS       | 72                                                  | 끝난 잡 폴더 자동 삭제 (0 = 삭제 안 함) |
| MXDPF_ALLOWED_ROOTS       | (없음)                                              | 경로 지정 제출을 허용할 루트들 (os.pathsep 구분). 비면 경로 제출 금지 |
| MXDPF_ALLOWED_ORIGINS     | (없음)                                              | 브라우저 Origin 허용 목록 (쉼표 구분). Origin 헤더가 있는데 목록에 없으면 403 |
| AWP_ROOT252               | (mx_batch 기본값)                                   | ANSYS v252 설치 경로 — 자식 프로세스로 그대로 전달 |
"""
import os
import sys
from dataclasses import dataclass, field
from typing import List, Optional

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
DEFAULT_BATCH = os.path.join(REPO_ROOT, "Mechanical", "MXSimulator", "batch", "mx_batch.py")


def _int(name, default):
    try:
        return int(os.environ.get(name, default))
    except (TypeError, ValueError):
        return default


@dataclass
class Settings:
    host: str = "127.0.0.1"
    port: int = 8770
    token: Optional[str] = None
    work_dir: str = os.path.join(os.path.expanduser("~"), ".mxdtm", "dpf_jobs")
    python: str = sys.executable
    batch_script: str = DEFAULT_BATCH
    max_concurrency: int = 1
    job_timeout_sec: int = 1800
    max_upload_mb: int = 4096
    job_ttl_hours: int = 72
    allowed_roots: List[str] = field(default_factory=list)
    allowed_origins: List[str] = field(default_factory=list)

    @classmethod
    def from_env(cls):
        roots = [r for r in os.environ.get("MXDPF_ALLOWED_ROOTS", "").split(os.pathsep) if r.strip()]
        return cls(
            host=os.environ.get("MXDPF_HOST", "127.0.0.1"),
            port=_int("MXDPF_PORT", 8770),
            token=os.environ.get("MXDPF_TOKEN") or None,
            work_dir=os.environ.get("MXDPF_WORK_DIR") or cls.work_dir,
            python=os.environ.get("MXDPF_PYTHON") or sys.executable,
            batch_script=os.environ.get("MXDPF_BATCH_SCRIPT") or DEFAULT_BATCH,
            max_concurrency=max(1, _int("MXDPF_MAX_CONCURRENCY", 1)),
            job_timeout_sec=max(10, _int("MXDPF_JOB_TIMEOUT_SEC", 1800)),
            max_upload_mb=max(1, _int("MXDPF_MAX_UPLOAD_MB", 4096)),
            job_ttl_hours=max(0, _int("MXDPF_JOB_TTL_HOURS", 72)),
            allowed_roots=[os.path.realpath(r) for r in roots],
            allowed_origins=[o.strip() for o in os.environ.get("MXDPF_ALLOWED_ORIGINS", "").split(",") if o.strip()],
        )
