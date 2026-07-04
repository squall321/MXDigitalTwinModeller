# PyAnsys / DPF Student-license probe — result & decision

POSTPROCESS_IDEAS.md §10.1 (probe) + §10.5 (decision gate). Run on **2026-07-05**,
ANSYS **Student v252**, host **Python 3.13.7**.

## TL;DR — Phase 2 gate: **GREEN (conditional)**

DPF reads solved result files (.rst) on this Student seat — that is exactly what Phase 2
needs. The only failure (`launch_mechanical`) is for a *live interactive Mechanical session*,
which Phase 2 does **not** require (the sidecar reads .rst files the user already solved).

| Step | Result | Detail |
|------|--------|--------|
| `python -m venv` | ✅ | Python 3.13.7 venv (`.venv-pyansys`, git-ignored) |
| `pip install ansys-mechanical-core ansys-dpf-core ansys-dpf-post` | ✅ | **cp313 wheels exist** — no Python-3.13 gap. Installed: ansys-dpf-core 0.16.1, ansys-dpf-post 0.12.0, ansys-mechanical-core 0.12.10 |
| `import ansys.dpf.core` | ✅ | clean |
| **DPF local server start** | ✅ | `Ans.Dpf.Grpc.exe` (v252\aisol\bin\winx64) launches; **server_version 10.0**, path = v252 Student |
| **DPF read displacement field** | ✅ | bundled `static.rst` → 81 nodes, \|u\|max = 1.482e-08 m |
| **DPF read stress field** | ✅ | 64 entities |
| `launch_mechanical(version=252)` | ❌ | "Mechanical version 252 does not support secure transport modes. Update to Service Pack SP03+ for secure gRPC support." |

## What this means for the phased plan

- **Phase 2 (DPF sidecar) is BUILDABLE on this seat.** Modal participation, strain-energy
  field, MAC, hot-spot clustering — all are DPF operators over an `.rst`, which the probe
  proved works. The sidecar consumes result files the user solved in Mechanical (interactive
  or the existing ACT flow); it does **not** need to drive Mechanical.
- **`launch_mechanical` (live PyMechanical session) is BLOCKED** by the Student v252's lack of
  SP03 secure-gRPC. This only matters if we ever want PyAnsys to *edit/solve* a model headless
  (not a Phase-2 goal). Workarounds if ever needed: install v252 SP03+, or use the insecure
  transport path, or keep solving via the ACT extension and only post-process via DPF.

## Decision (§10.5)

**Green-light Phase 2 as DPF-over-.rst**, sidecar launched from the ACT extension via the same
`System.Diagnostics.Process` pattern that starts MXPostViewer.exe. Keep it OPT-IN with graceful
fallback to the popup-only path when the `.venv-pyansys` isn't present (end-user machines won't
have it unless we bundle it — that's a Phase-2 packaging decision, likely a separate PyInstaller
sidecar like MXPostViewer).

## Reproduce

```powershell
# from Mechanical\MXSimulator\postprocess\
.\pyansys_probe.ps1                      # step 1-2 (venv + pip) + the §10.1 commands
.\.venv-pyansys\Scripts\python.exe pyansys_probe2.py   # the decisive DPF field-read test
```

`.venv-pyansys/` is git-ignored (large, machine-specific). The two probe scripts
(`pyansys_probe.ps1`, `pyansys_probe2.py`) are tracked so anyone can re-run the gate.
