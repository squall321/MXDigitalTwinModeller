# encoding: utf-8
"""
Parameter-sweep analysis (POSTPROCESS_IDEAS.md §6 Phase 3, the API-key-FREE slice).

Given a set of result cases — each a metadata.json the ACT popup already writes, tagged with
the input parameters that produced it — this computes, with pure numpy (scipy optional):
  * sensitivity  : ∂output/∂input via least-squares linear fit + a normalized elasticity
  * Pareto front : non-dominated cases for a chosen minimize/maximize objective set
  * response surface : a quadratic (or linear) least-squares model output ≈ f(inputs)

NO Anthropic SDK, NO paid API, NO live solve — it operates on EXISTING sampled cases. The heavy
"drive Workbench parameters" DOE loop (which needs Mechanical) is intentionally out of scope; this
slice turns a set the user already has into sensitivity/Pareto/RSM.

A "case" here is a dict:
    {"params": {"rib_mm": 1.5, "wall_mm": 0.6, ...},   # input design variables
     "metrics": {"max_vm": 130.0, "max_def": 0.42, ...}}  # scalar outputs
`load_cases()` builds these from metadata.json files: metrics come from the top-ranked body's
max_def/max_vm (+ strain_energy / |reaction| when present); params must be supplied alongside
(a sibling params.json, or an explicit mapping) since the popup metadata doesn't record inputs.
"""
import os
import json

import numpy as np


# ---------------------------------------------------------------------------
# Loading cases
# ---------------------------------------------------------------------------
def metrics_from_metadata(meta):
    """Reduce one metadata.json to a flat scalar metric dict (worst-body aggregates)."""
    bodies = meta.get("bodies", []) or []
    out = {}
    if bodies:
        out["max_def"] = max((float(b.get("max_def", 0) or 0) for b in bodies), default=0.0)
        out["max_vm"] = max((float(b.get("max_vm", 0) or 0) for b in bodies), default=0.0)
        se = [float(b["strain_energy"]) for b in bodies if b.get("strain_energy") is not None]
        if se:
            out["total_strain_energy"] = float(sum(se))
    reacts = meta.get("reactions", []) or []
    if reacts:
        out["max_reaction"] = max((float(r.get("mag", 0) or 0) for r in reacts), default=0.0)
    return out


def scan_sweep_dir(root):
    """Discover sweep cases under a directory. Each case is a subfolder that holds a
    metadata.json and (optionally) a params.json mapping input design variables to values:
        root/
          case_rib1.0/ metadata.json  params.json {"rib_mm":1.0,"wall_mm":0.6}
          case_rib1.5/ metadata.json  params.json {...}
    A metadata.json directly in root also counts (params.json beside it if present). Returns the
    `entries` list load_cases() consumes. Cases without a params.json are still included with an
    empty params dict (they contribute metrics but no sensitivity)."""
    entries = []

    def _add(meta_path, name):
        params = {}
        pj = os.path.join(os.path.dirname(meta_path), "params.json")
        if os.path.isfile(pj):
            try:
                with open(pj, "r", encoding="utf-8") as f:
                    params = json.load(f)
            except Exception:
                params = {}
        entries.append({"metadata": meta_path, "params": params, "name": name})

    root_meta = os.path.join(root, "metadata.json")
    if os.path.isfile(root_meta):
        _add(root_meta, os.path.basename(root.rstrip("/\\")) or "root")
    try:
        for sub in sorted(os.listdir(root)):
            d = os.path.join(root, sub)
            if os.path.isdir(d):
                mp = os.path.join(d, "metadata.json")
                if os.path.isfile(mp):
                    _add(mp, sub)
    except Exception:
        pass
    return entries


def load_cases(entries):
    """Build case dicts from (metadata_path, params) pairs.
    `entries`: list of {"metadata": path, "params": {name: value}} (or {"metrics": {...}} inline).
    Returns a list of {"name", "params", "metrics"}."""
    cases = []
    for i, e in enumerate(entries):
        params = dict(e.get("params", {}))
        if "metrics" in e:
            metrics = dict(e["metrics"])
            name = e.get("name", "case%d" % i)
        else:
            mp = e["metadata"]
            with open(mp, "r", encoding="utf-8") as f:
                meta = json.load(f)
            metrics = metrics_from_metadata(meta)
            name = e.get("name", meta.get("analysis", os.path.basename(os.path.dirname(mp))))
        cases.append({"name": name, "params": params, "metrics": metrics})
    return cases


def _matrix(cases, keys, kind):
    """Stack a (ncases, nkeys) matrix from case[kind]; keys ordered as given."""
    return np.array([[float(c[kind].get(k, np.nan)) for k in keys] for c in cases], dtype=float)


def _finite_rows(*arrays):
    """Boolean mask of rows finite across all given arrays (same n rows)."""
    mask = np.ones(arrays[0].shape[0], dtype=bool)
    for a in arrays:
        mask &= np.all(np.isfinite(a), axis=1) if a.ndim == 2 else np.isfinite(a)
    return mask


# ---------------------------------------------------------------------------
# Sensitivity
# ---------------------------------------------------------------------------
def sensitivity(cases, param_keys=None, metric_keys=None):
    """Per (metric, param) local sensitivity from a linear least-squares fit across the sweep.
    Returns {metric: {param: {slope, elasticity}}}.
      slope      = ∂metric/∂param (raw units)
      elasticity = (∂metric/∂param)·(mean_param/mean_metric)  — dimensionless % / %
    Needs >= nparams+1 cases with variation; degenerate columns report slope 0."""
    if not cases:
        return {}
    param_keys = param_keys or sorted({k for c in cases for k in c["params"]})
    metric_keys = metric_keys or sorted({k for c in cases for k in c["metrics"]})
    X = _matrix(cases, param_keys, "params")      # (n, p)
    Y = _matrix(cases, metric_keys, "metrics")    # (n, m)
    out = {}
    for mj, mk in enumerate(metric_keys):
        y = Y[:, mj]
        mask = _finite_rows(X, y)
        out[mk] = {}
        if mask.sum() < len(param_keys) + 1:
            for pk in param_keys:
                out[mk][pk] = {"slope": 0.0, "elasticity": 0.0, "note": "insufficient cases"}
            continue
        Xf = X[mask]; yf = y[mask]
        # Drop parameters with (near-)zero variation: they're collinear with the intercept, so
        # least-squares assigns them an arbitrary slope. A constant input HAS no measurable
        # sensitivity — report slope 0 explicitly rather than a fit artefact.
        stds = np.std(Xf, axis=0)
        varying = stds > 1e-12
        ymean = float(np.mean(yf)) or 1.0
        slopes = np.zeros(len(param_keys))
        if varying.any():
            Xv = Xf[:, varying]
            A = np.column_stack([Xv, np.ones(len(Xv))])  # varying design + intercept
            coef, *_ = np.linalg.lstsq(A, yf, rcond=None)
            slopes[varying] = coef[:-1]
        for pj, pk in enumerate(param_keys):
            if not varying[pj]:
                out[mk][pk] = {"slope": 0.0, "elasticity": 0.0, "note": "constant input"}
                continue
            xmean = float(np.mean(Xf[:, pj]))
            slope = float(slopes[pj])
            elas = slope * (xmean / ymean) if ymean else 0.0
            out[mk][pk] = {"slope": slope, "elasticity": float(elas)}
    return out


# ---------------------------------------------------------------------------
# Pareto front
# ---------------------------------------------------------------------------
def pareto_front(cases, objectives):
    """Non-dominated case indices for multi-objective optimisation.
    `objectives`: {metric: 'min'|'max'}. A case dominates another if it is no worse on all
    objectives and strictly better on at least one. Returns (front_indices, dominated_indices)."""
    keys = list(objectives.keys())
    M = _matrix(cases, keys, "metrics")  # (n, k)
    # convert every objective to "minimise": negate the maximise ones
    sign = np.array([1.0 if objectives[k] == "min" else -1.0 for k in keys])
    Z = M * sign
    n = len(cases)
    dominated = np.zeros(n, dtype=bool)
    for i in range(n):
        if not np.all(np.isfinite(Z[i])):
            dominated[i] = True
            continue
        for j in range(n):
            if i == j or not np.all(np.isfinite(Z[j])):
                continue
            if np.all(Z[j] <= Z[i]) and np.any(Z[j] < Z[i]):
                dominated[i] = True
                break
    front = [i for i in range(n) if not dominated[i]]
    dom = [i for i in range(n) if dominated[i]]
    return front, dom


# ---------------------------------------------------------------------------
# Response surface (quadratic least-squares)
# ---------------------------------------------------------------------------
def _poly_design(X, degree=2):
    """Design matrix for a full quadratic (intercept + linear + squares + pairwise) or linear."""
    n, p = X.shape
    cols = [np.ones(n)]
    names = ["1"]
    for i in range(p):
        cols.append(X[:, i]); names.append("x%d" % i)
    if degree >= 2:
        for i in range(p):
            cols.append(X[:, i] ** 2); names.append("x%d^2" % i)
        for i in range(p):
            for j in range(i + 1, p):
                cols.append(X[:, i] * X[:, j]); names.append("x%d*x%d" % (i, j))
    return np.column_stack(cols), names


def response_surface(cases, metric, param_keys=None, degree=2):
    """Fit metric ≈ poly(params) by least squares. Returns {coef, names, r2, degree, params}.
    Falls back to degree=1 when there aren't enough cases for a quadratic."""
    param_keys = param_keys or sorted({k for c in cases for k in c["params"]})
    X = _matrix(cases, param_keys, "params")
    y = _matrix(cases, [metric], "metrics")[:, 0]
    mask = _finite_rows(X, y)
    X = X[mask]; y = y[mask]
    if len(y) == 0:
        return {"error": "no finite cases for metric %s" % metric}
    # need at least as many cases as terms; drop to linear if quadratic is underdetermined
    A, names = _poly_design(X, degree)
    if A.shape[0] < A.shape[1] and degree >= 2:
        A, names = _poly_design(X, 1)
        degree = 1
    coef, *_ = np.linalg.lstsq(A, y, rcond=None)
    yhat = A @ coef
    ss_res = float(np.sum((y - yhat) ** 2))
    ss_tot = float(np.sum((y - np.mean(y)) ** 2)) or 1.0
    r2 = 1.0 - ss_res / ss_tot
    return {"metric": metric, "params": param_keys, "degree": degree,
            "names": names, "coef": [float(c) for c in coef], "r2": float(r2), "n": int(len(y))}


def analyze_sweep(cases, objectives=None):
    """One-call bundle: sensitivity for every metric, Pareto for the given objectives (if any),
    and a response surface for each metric. Pure dict output (JSON-serialisable)."""
    param_keys = sorted({k for c in cases for k in c["params"]})
    metric_keys = sorted({k for c in cases for k in c["metrics"]})
    result = {"n_cases": len(cases), "params": param_keys, "metrics": metric_keys,
              "sensitivity": sensitivity(cases, param_keys, metric_keys),
              "response_surface": {mk: response_surface(cases, mk, param_keys) for mk in metric_keys}}
    if objectives:
        front, dom = pareto_front(cases, objectives)
        result["pareto"] = {"objectives": objectives,
                            "front": [cases[i]["name"] for i in front],
                            "front_idx": front, "dominated_idx": dom}
    return result
