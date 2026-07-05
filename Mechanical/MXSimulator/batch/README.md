# DPF post-process sidecar (Phase 2)

`mx_batch.py` reads a solved ANSYS result file (`.rst`) with **ansys-dpf-core** and writes a
`dpf_sidecar.json` summary of the deep-analysis quantities the interactive popup can't compute:
modal frequencies, participation / effective mass, strain-energy field, MAC, and stress hot-spot
clusters. Pure DPF + numpy — no Anthropic SDK, no paid API, no network.

## Why there's no bundled runtime

DPF runs against **your local ANSYS install** — it launches `Ans.Dpf.Grpc.exe` from
`<ANSYS>\v252\aisol\bin\winx64`. So the sidecar only works on a machine that already has ANSYS
v252, and there is nothing to bundle for the server. The only thing you provide is a small Python
environment with `ansys-dpf-core`. Freezing it into a standalone .exe wouldn't remove the ANSYS
dependency, so we ship the source + a requirements file instead of a 240 MB venv.

## One-time setup (power users)

From this folder:

```powershell
python -m venv .venv-pyansys
.\.venv-pyansys\Scripts\Activate.ps1
pip install -r requirements.txt
```

The ACT post-process dialog's **"Run DPF deep analysis"** checkbox looks for
`..\postprocess\.venv-pyansys\Scripts\python.exe`; if that venv exists it launches the sidecar
automatically after export (async, non-fatal). Absent → the checkbox is a no-op and the normal
export/viewer flow is unchanged.

> The license probe that verified DPF works on ANSYS Student v252 is documented in
> `..\postprocess\README_pyansys_probe.md`. `launch_mechanical` (a live PyMechanical session) is
> blocked on Student by the SP03 secure-gRPC gap, but the sidecar does NOT need it — it reads
> `.rst` files you already solved.

## Run it manually

```powershell
.venv-pyansys\Scripts\python.exe mx_batch.py <solved.rst> dpf_sidecar.json
# optional: --rst2 <other.rst> for a cross-MAC ; --merge <metadata.json> to fold results in
```

## Gate

```powershell
.venv-pyansys\Scripts\python.exe selftest_mx_batch.py   # -> GATE_OK
```

runs on the DPF-bundled modal + static example result files (no network) and asserts non-empty
frequencies / clusters / strain-energy / participation.
