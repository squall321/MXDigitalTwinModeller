# encoding: utf-8
"""
MX Digital Twin — Phase-2 DPF post-process sidecar (POSTPROCESS_IDEAS.md §6 Phase 2).

Reads a SOLVED ANSYS result file (.rst) with ansys-dpf-core and emits a JSON summary of the
deep-analysis quantities the interactive popup can't compute: modal frequencies, participation /
effective mass, strain-energy field summary, MAC (self or cross), and stress hot-spot clusters.

PURE ansys-dpf-core + numpy (+ optional scipy for the fast clustering path). NO Anthropic SDK,
NO paid API, NO network — it starts the LOCAL DPF server (Ans.Dpf.Grpc.exe, probe-verified GREEN
on ANSYS Student v252) and reads on-disk result files.

Run by the .venv-pyansys interpreter:
    .venv-pyansys\\Scripts\\python.exe mx_batch.py <rst> <out.json> [--rst2 R] [--merge META]

Every stage is wrapped so a partial failure still writes a JSON with whatever succeeded plus an
`errors` list — the report degrades honestly instead of crashing.
"""
import os
import sys
import json
import argparse
import traceback

import numpy as np

AWP = os.environ.get("AWP_ROOT252", r"D:\Program Files\ANSYS Inc\ANSYS Student\v252")


# ---------------------------------------------------------------------------
# Server bootstrap (proven in pyansys_probe2.py)
# ---------------------------------------------------------------------------
def start_server():
    import ansys.dpf.core as dpf
    try:
        dpf.set_default_server_context(dpf.AvailableServerContexts.premium)
    except Exception:
        pass
    try:
        dpf.start_local_server(ansys_path=AWP)
    except Exception:
        pass  # get_or_create picks up an ambient server if one is already up
    return dpf.server.get_or_create_server(None)


# ---------------------------------------------------------------------------
# Analysis type + modal frequencies
# ---------------------------------------------------------------------------
def analysis_type(model):
    try:
        at = model.metadata.result_info.analysis_type
        return str(at)
    except Exception:
        pass
    # fallback: multiple sets whose "time" axis is in Hz -> modal
    try:
        tfs = model.metadata.time_freq_support
        unit = (tfs.time_frequencies.unit or "").lower()
        n = len(tfs.time_frequencies.data)
        if n > 1 and ("hz" in unit or "frequency" in unit):
            return "modal"
    except Exception:
        pass
    return "static"


def read_modal(model):
    """Eigenfrequencies from the time_freq_support (not an operator)."""
    tfs = model.metadata.time_freq_support
    freqs = list(np.asarray(tfs.time_frequencies.data, dtype=float).ravel())
    unit = tfs.time_frequencies.unit or "Hz"
    return {"n_modes": len(freqs), "freqs_hz": freqs, "unit": unit}


# ---------------------------------------------------------------------------
# Mode shapes -> matrix, MAC
# ---------------------------------------------------------------------------
def mode_matrix(disp_fields_container):
    """Stack mode-shape vectors into an (ndof, nmodes) matrix, node-id sorted so two
    containers with the same mesh align dof-for-dof."""
    cols = []
    for field in disp_fields_container:
        ids = np.asarray(field.scoping.ids)
        order = np.argsort(ids)
        data = np.asarray(field.data, dtype=float)  # (nnodes, ncomp)
        data = data[order]
        cols.append(data.ravel())
    if not cols:
        return np.zeros((0, 0))
    ncol = min(len(c) for c in cols)
    return np.column_stack([c[:ncol] for c in cols])


def mac_matrix(A, B):
    """Modal Assurance Criterion between two mode-shape matrices (ndof x nmodes)."""
    if A.size == 0 or B.size == 0:
        return np.zeros((0, 0))
    n = min(A.shape[0], B.shape[0])
    A = A[:n]; B = B[:n]
    num = np.abs(A.T @ B) ** 2
    a2 = np.sum(A * A, axis=0)  # (nA,)
    b2 = np.sum(B * B, axis=0)  # (nB,)
    denom = np.outer(a2, b2)
    with np.errstate(divide="ignore", invalid="ignore"):
        mac = np.where(denom > 0, num / denom, 0.0)
    return mac


# ---------------------------------------------------------------------------
# Participation / effective mass
# ---------------------------------------------------------------------------
def participation(model, disp_fc):
    """Effective modal mass per global direction. Tries a lumped nodal mass from the
    ElementalMass operator; on any failure falls back to a UNIT-mass estimate (M=I) and
    labels which path ran (honest — the numbers differ in scale)."""
    import ansys.dpf.core as dpf
    out = {"method": "unit_mass", "m_eff": {"x": [], "y": [], "z": []},
           "cum_frac": {"x": [], "y": [], "z": []}}
    # assemble mode shapes as (nnodes, 3, nmodes)
    shapes = []
    node_ids = None
    for field in disp_fc:
        ids = np.asarray(field.scoping.ids)
        order = np.argsort(ids)
        if node_ids is None:
            node_ids = ids[order]
        data = np.asarray(field.data, dtype=float)[order]  # (nnodes, 3)
        if data.shape[1] < 3:
            pad = np.zeros((data.shape[0], 3))
            pad[:, :data.shape[1]] = data
            data = pad
        shapes.append(data[:, :3])
    if not shapes:
        return out
    phi = np.stack(shapes, axis=2)  # (nnodes, 3, nmodes)
    nnodes = phi.shape[0]

    # try a lumped nodal mass vector
    m = None
    try:
        em = model.results.element_nodal_forces  # placeholder; real op resolved below
    except Exception:
        em = None
    try:
        mass_op = dpf.operators.result.elemental_mass()  # may be unavailable on Student
        mass_op.inputs.data_sources(model.metadata.data_sources)
        mfield = mass_op.outputs.fields_container()[0]
        # crude lump: distribute elemental mass equally to a per-node mass; if scoping is nodal use directly
        mvals = np.asarray(mfield.data, dtype=float).ravel()
        if mvals.size == nnodes:
            m = mvals
    except Exception:
        m = None

    if m is None:
        m = np.ones(nnodes)  # unit mass
        out["method"] = "unit_mass"
    else:
        out["method"] = "lumped_mass"

    # participation factor gamma_dir = sum_nodes m * phi_dir ; effective mass = gamma^2
    dirs = {"x": 0, "y": 1, "z": 2}
    for d, di in dirs.items():
        gamma = np.einsum("n,nk->k", m, phi[:, di, :])  # (nmodes,)
        meff = gamma ** 2
        total = meff.sum() if meff.sum() > 0 else 1.0
        cum = np.cumsum(meff) / total
        out["m_eff"][d] = [float(v) for v in meff]
        out["cum_frac"][d] = [float(v) for v in cum]
    return out


# ---------------------------------------------------------------------------
# Strain energy
# ---------------------------------------------------------------------------
def strain_energy_summary(model, is_modal, top_n=10):
    """Static: elemental strain energy field (Joules). Modal: per-mode relative SED
    fraction (mode units, not physical J)."""
    import ansys.dpf.core as dpf
    if not is_modal:
        try:
            se = model.results.stiffness_matrix_energy().eval()
            f = se[0]
            data = np.asarray(f.data, dtype=float).ravel()
            eids = np.asarray(f.scoping.ids)
            total = float(np.nansum(data))
            order = np.argsort(-np.abs(data))[:top_n]
            top = [{"element": int(eids[i]), "energy": float(data[i])} for i in order if i < len(eids)]
            return {"branch": "static", "unit": f.unit or "J", "total": total, "top": top}
        except Exception as e:
            return {"branch": "static", "error": "{}: {}".format(type(e).__name__, str(e)[:200])}
    # modal: SED per mode from stress . elastic_strain (elemental-nodal), relative fraction
    try:
        stress = model.results.stress().eval()
        strain = model.results.elastic_strain().eval()
        per_mode = []
        nmodes = len(stress)
        for k in range(nmodes):
            s = np.asarray(stress[k].data, dtype=float)
            e = np.asarray(strain[k].data, dtype=float)
            m = min(len(s), len(e))
            sed = 0.5 * float(np.nansum(s[:m] * e[:m]))
            per_mode.append(sed)
        tot = sum(abs(v) for v in per_mode) or 1.0
        frac = [abs(v) / tot for v in per_mode]
        return {"branch": "modal", "mode_units": True,
                "per_mode_sed": [float(v) for v in per_mode],
                "per_mode_frac": [float(v) for v in frac]}
    except Exception as e:
        return {"branch": "modal", "error": "{}: {}".format(type(e).__name__, str(e)[:200])}


# ---------------------------------------------------------------------------
# Hot-spot clustering (numpy-only default; scipy fast path when available)
# ---------------------------------------------------------------------------
def _cluster_numpy(pts, eps):
    """Union-find spatial clustering: points within eps are one cluster. O(N^2) — only
    ever called on the top-quantile node set (tens of nodes), so it's fine."""
    n = len(pts)
    parent = list(range(n))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    eps2 = eps * eps
    for i in range(n):
        for j in range(i + 1, n):
            d = pts[i] - pts[j]
            if float(d @ d) <= eps2:
                union(i, j)
    groups = {}
    for i in range(n):
        groups.setdefault(find(i), []).append(i)
    return list(groups.values())


def hotspot_clusters(model, quantile=0.99, eps_mm=2.0, max_report=20):
    import ansys.dpf.core as dpf
    try:
        stress_fc = model.results.stress.on_location(dpf.locations.nodal).eval()
        vm_fc = dpf.operators.invariant.von_mises_eqv_fc(fields_container=stress_fc).eval()
        vm_field = vm_fc[-1]  # last set (final load step / last mode)
        vm = np.asarray(vm_field.data, dtype=float).ravel()
        nids = np.asarray(vm_field.scoping.ids)
    except Exception as e:
        return {"error": "{}: {}".format(type(e).__name__, str(e)[:200])}
    if vm.size == 0:
        return {"n_clusters": 0, "clusters": []}

    thr = float(np.nanquantile(vm, quantile))
    sel = np.where(vm >= thr)[0]
    if sel.size == 0:
        return {"n_clusters": 0, "clusters": [], "vm_unit": vm_field.unit or "Pa", "threshold": thr}

    # node coordinates for the selected nodes
    try:
        mesh = model.metadata.meshed_region
        coords = np.asarray(mesh.nodes.coordinates_field.data, dtype=float)
        coord_ids = np.asarray(mesh.nodes.scoping.ids)
        id_to_row = {int(i): r for r, i in enumerate(coord_ids)}
        pts = np.array([coords[id_to_row[int(nids[i])]] for i in sel
                        if int(nids[i]) in id_to_row], dtype=float)
    except Exception:
        # no coords -> each hot node is its own cluster
        pts = None

    if pts is None or len(pts) != len(sel):
        clusters = [[i] for i in range(len(sel))]
    else:
        try:
            from scipy.spatial import cKDTree
            from scipy.sparse.csgraph import connected_components
            from scipy.sparse import coo_matrix
            tree = cKDTree(pts)
            pairs = tree.query_pairs(r=eps_mm, output_type="ndarray")
            n = len(pts)
            if len(pairs):
                row = np.concatenate([pairs[:, 0], pairs[:, 1]])
                col = np.concatenate([pairs[:, 1], pairs[:, 0]])
                g = coo_matrix((np.ones(len(row)), (row, col)), shape=(n, n))
                ncomp, labels = connected_components(g, directed=False)
                clusters = [np.where(labels == c)[0].tolist() for c in range(ncomp)]
            else:
                clusters = [[i] for i in range(n)]
        except Exception:
            clusters = _cluster_numpy(pts, eps_mm)

    out = []
    for grp in clusters:
        rows = [sel[i] for i in grp]
        peak = max(rows, key=lambda r: vm[r])
        out.append({"peak_node": int(nids[peak]), "peak_vm": float(vm[peak]), "size": len(rows)})
    out.sort(key=lambda c: -c["peak_vm"])
    return {"vm_unit": vm_field.unit or "Pa", "quantile": quantile, "threshold": thr,
            "n_clusters": len(out), "clusters": out[:max_report]}


# ---------------------------------------------------------------------------
# Orchestration
# ---------------------------------------------------------------------------
def analyze(rst, rst2=None):
    import ansys.dpf.core as dpf
    start_server()
    result = {"schema_version": "2.1", "source": "mx_batch DPF sidecar",
              "rst": os.path.abspath(rst), "errors": []}

    model = dpf.Model(rst)
    at = analysis_type(model)
    result["analysis_type"] = at
    is_modal = ("modal" in at.lower())

    def guard(name, fn):
        try:
            return fn()
        except Exception as e:
            result["errors"].append("{}: {}: {}".format(name, type(e).__name__, str(e)[:200]))
            return None

    if is_modal:
        result["modal"] = guard("modal", lambda: read_modal(model))
        disp_fc = guard("disp", lambda: model.results.displacement().eval())
        if disp_fc is not None:
            result["participation"] = guard("participation", lambda: participation(model, disp_fc))
            A = mode_matrix(disp_fc)
            if rst2:
                model2 = dpf.Model(rst2)
                B = mode_matrix(model2.results.displacement().eval())
                mac = mac_matrix(A, B)
                result["mac"] = {"mode": "cross", "present": True,
                                 "matrix": mac.tolist(),
                                 "paired_diag": [float(mac[i, i]) for i in range(min(mac.shape))]}
            else:
                mac = mac_matrix(A, A)
                result["mac"] = {"mode": "self", "present": True,
                                 "diag_min": float(np.min(np.diag(mac))) if mac.size else 0.0,
                                 "off_diag_max": float(np.max(mac - np.eye(mac.shape[0]))) if mac.size else 0.0}
    result["strain_energy"] = guard("strain_energy", lambda: strain_energy_summary(model, is_modal))
    result["hotspots"] = guard("hotspots", lambda: hotspot_clusters(model))
    return result


def main(argv=None):
    ap = argparse.ArgumentParser(description="MX DPF post-process sidecar")
    ap.add_argument("rst", help="solved .rst path")
    ap.add_argument("out_json", help="output JSON path")
    ap.add_argument("--rst2", default=None, help="second .rst for cross-MAC")
    ap.add_argument("--merge", default=None, help="metadata.json to merge into (adds a 'dpf' key)")
    args = ap.parse_args(argv)

    try:
        result = analyze(args.rst, rst2=args.rst2)
    except Exception as e:
        result = {"schema_version": "2.1", "source": "mx_batch DPF sidecar",
                  "rst": args.rst, "fatal": "{}: {}".format(type(e).__name__, str(e)[:400]),
                  "traceback": traceback.format_exc()[:2000]}

    with open(args.out_json, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=2, ensure_ascii=False)

    if args.merge and os.path.isfile(args.merge):
        try:
            with open(args.merge, "r", encoding="utf-8") as f:
                meta = json.load(f)
            meta["dpf"] = result
            with open(args.merge, "w", encoding="utf-8") as f:
                json.dump(meta, f, indent=2, ensure_ascii=False)
        except Exception:
            pass

    print("MX_BATCH_DONE:", args.out_json)
    return 0 if not result.get("fatal") else 1


if __name__ == "__main__":
    sys.exit(main())
