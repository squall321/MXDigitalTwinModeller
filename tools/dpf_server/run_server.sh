#!/usr/bin/env bash
# MX DPF server - Linux 실행 (라이선스 서버가 잡힌 머신에서)
# 처음 한 번:  python3 -m venv .venv && .venv/bin/pip install -r requirements.txt
set -euo pipefail
cd "$(dirname "$0")"

if [ ! -x .venv/bin/python ]; then
  echo "[mxdpf] .venv 가 없습니다. 먼저: python3 -m venv .venv && .venv/bin/pip install -r requirements.txt" >&2
  exit 1
fi

export MXDPF_HOST="${MXDPF_HOST:-127.0.0.1}"
export MXDPF_PORT="${MXDPF_PORT:-8770}"
export MXDPF_WORK_DIR="${MXDPF_WORK_DIR:-$HOME/.mxdtm/dpf_jobs}"
export MXDPF_MAX_CONCURRENCY="${MXDPF_MAX_CONCURRENCY:-1}"
# export MXDPF_TOKEN=...                      (0.0.0.0 으로 열 때는 반드시)
# export MXDPF_ALLOWED_ROOTS=/mnt/cae:/data/results
# export AWP_ROOT252=/ansys_inc/v252
# export ANSYSLMD_LICENSE_FILE=1055@license-server

exec .venv/bin/python -m mxdpf "$@"
