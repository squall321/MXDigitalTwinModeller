# encoding: utf-8
# =====================================================================
# kernel_truth.py — extractor-INDEPENDENT ground-truth geometry, read
# straight from the live kernel B-rep. Validated by gate_kernel_truth.py
# (mutation test on nist: ChangeHoleDiameter 25->30 detected as exactly
# one cylinder radius 12.5->15mm move, stable + sensitive + specific).
#
# Use as the L1 oracle foundation AND as the W4-1 live-cylinder relocator:
# never trust FeatureExtractor coordinates on curved parts — read the
# Cylinder surface's own Axis/Radius from the kernel.
#
# All distances in METERS internally; helpers expose *_mm where noted.
# =====================================================================
import math

# quantization (meters / m^3): pos 0.05mm, dir 1e-3, radius 0.002mm, len 0.05mm, vol 1e-10
QP, QD, QR, QL, QV = 5e-5, 1e-3, 2e-6, 5e-5, 1e-10


def _q(v, step):
    return int(round(v / step))


def _canon_dir(d):
    """Sign-canonicalize a unit direction: first significant component positive.
    An axis/plane-normal has no inherent orientation for fingerprint matching."""
    for c in d:
        if abs(c) > 1e-6:
            return (-d[0], -d[1], -d[2]) if c < 0 else (d[0], d[1], d[2])
    return d


def _surface(face):
    try:
        g = face.Shape.Geometry
        if g is not None:
            return g
    except Exception:
        pass
    try:
        return face.Geometry
    except Exception:
        return None


def _axis_extent(face, Du):
    """Axial extent (length, meters) of a face's bbox projected onto direction Du,
    plus the signed [tmin,tmax] band along Du."""
    from SpaceClaim.Api.V252.Geometry import Matrix
    bb = face.Shape.GetBoundingBox(Matrix.Identity)
    ts = []
    for ci in range(8):
        cx = bb.MinCorner.X if (ci & 1) == 0 else bb.MaxCorner.X
        cy = bb.MinCorner.Y if (ci & 2) == 0 else bb.MaxCorner.Y
        cz = bb.MinCorner.Z if (ci & 4) == 0 else bb.MaxCorner.Z
        ts.append(cx * Du[0] + cy * Du[1] + cz * Du[2])
    return (max(ts) - min(ts)), min(ts), max(ts)


def cylinder_record(face):
    """Rich kernel-truth record of a cylindrical face, or None.
    Keys (meters): radius, axis_dir (canonical unit), axis_foot (foot of perp
    from world origin), t_lo/t_hi (signed axial band of THIS face along axis_dir
    measured from world origin), extent. This is the W4-1 relocation input."""
    g = _surface(face)
    if g is None or type(g).__name__ != "Cylinder":
        return None
    try:
        rad = float(g.Radius)
    except Exception:
        return None
    ax = g.Axis  # Line
    A = ax.Origin
    D = ax.Direction
    Du = (D.X, D.Y, D.Z)
    dc = _canon_dir(Du)
    # foot of perpendicular from world origin onto the axis line
    t = -(A.X * Du[0] + A.Y * Du[1] + A.Z * Du[2])
    foot = (A.X + Du[0] * t, A.Y + Du[1] * t, A.Z + Du[2] * t)
    ext, tlo, thi = _axis_extent(face, dc)
    return {"radius": rad, "axis_dir": dc, "axis_foot": foot,
            "t_lo": tlo, "t_hi": thi, "extent": ext, "face": face}


def cylinders(body):
    """All cylindrical-face records in the live body (kernel truth)."""
    out = []
    for f in body.Faces:
        r = cylinder_record(f)
        if r is not None:
            out.append(r)
    return out


def find_cylinder_near(body, point_m, axis_u=None, radius_m=None,
                       pos_tol_m=2e-3, dir_tol=0.02, rad_tol_m=5e-4):
    """W4-1 relocation: find the live cylinder whose axis passes nearest `point_m`,
    optionally constrained to ~axis_u direction and ~radius_m. Returns the best
    cylinder_record or None. Distance = perpendicular from point to the axis line."""
    best, best_d = None, 1e30
    for c in cylinders(body):
        if radius_m is not None and abs(c["radius"] - radius_m) > rad_tol_m:
            continue
        if axis_u is not None:
            dot = abs(c["axis_dir"][0]*axis_u[0] + c["axis_dir"][1]*axis_u[1] + c["axis_dir"][2]*axis_u[2])
            if dot < 1.0 - dir_tol:
                continue
        # perpendicular distance from point to axis line (through axis_foot, dir)
        f = c["axis_foot"]; d = c["axis_dir"]
        wx, wy, wz = point_m[0]-f[0], point_m[1]-f[1], point_m[2]-f[2]
        proj = wx*d[0] + wy*d[1] + wz*d[2]
        px, py, pz = wx-proj*d[0], wy-proj*d[1], wz-proj*d[2]
        dist = math.sqrt(px*px + py*py + pz*pz)
        if dist < best_d:
            best, best_d = c, dist
    if best is not None and best_d <= pos_tol_m:
        best = dict(best); best["match_dist"] = best_d
        return best
    return None


def face_sig(face):
    """Canonical kernel signature of one face (hashable tuple)."""
    g = _surface(face)
    if g is None:
        return ("UNK",)
    tn = type(g).__name__
    try:
        area = _q(float(face.Area), 5e-7)
    except Exception:
        area = 0
    if tn == "Plane":
        try:
            fr = g.Frame
            n = _canon_dir((fr.DirZ.X, fr.DirZ.Y, fr.DirZ.Z))
            o = fr.Origin
            d = o.X*n[0] + o.Y*n[1] + o.Z*n[2]
            return ("PLN", _q(n[0], QD), _q(n[1], QD), _q(n[2], QD), _q(d, QP), area)
        except Exception:
            return ("PLN_ERR",)
    if tn == "Cylinder":
        c = cylinder_record(face)
        if c is None:
            return ("CYL_ERR",)
        fo = c["axis_foot"]; dc = c["axis_dir"]
        return ("CYL", _q(dc[0], QD), _q(dc[1], QD), _q(dc[2], QD),
                _q(fo[0], QP), _q(fo[1], QP), _q(fo[2], QP),
                _q(c["radius"], QR), _q(c["extent"], QL))
    if tn in ("Cone", "Sphere", "Torus"):
        return (tn.upper()[:3], area)
    return (tn[:4].upper(), area)


def fingerprint(body):
    """Whole-body kernel fingerprint: {faces: {sig:count}, volume, bbox}."""
    from SpaceClaim.Api.V252.Geometry import Matrix
    sigs = {}
    for f in body.Faces:
        s = face_sig(f)
        sigs[s] = sigs.get(s, 0) + 1
    try:
        vol = _q(float(body.Shape.Volume), QV)
    except Exception:
        vol = None
    try:
        bb = body.Shape.GetBoundingBox(Matrix.Identity)
        bbq = [_q(bb.MinCorner.X, QP), _q(bb.MinCorner.Y, QP), _q(bb.MinCorner.Z, QP),
               _q(bb.MaxCorner.X, QP), _q(bb.MaxCorner.Y, QP), _q(bb.MaxCorner.Z, QP)]
    except Exception:
        bbq = None
    return {"faces": sigs, "volume": vol, "bbox": bbq}


def diff(fp_before, fp_after):
    """Fingerprint delta: face sigs removed/added (multiset), volume & bbox change.
    The extractor-free L1 oracle primitive — assert the EXPECTED delta, nothing more."""
    fb, fa = fp_before["faces"], fp_after["faces"]
    removed = {s: fb[s]-fa.get(s, 0) for s in fb if fb[s]-fa.get(s, 0) > 0}
    added   = {s: fa[s]-fb.get(s, 0) for s in fa if fa[s]-fb.get(s, 0) > 0}
    dvol = None
    if fp_before["volume"] is not None and fp_after["volume"] is not None:
        dvol = (fp_after["volume"] - fp_before["volume"]) * QV
    return {"removed": removed, "added": added, "dvol_m3": dvol}


def cyl_radii(fp):
    """Multiset {quantized_radius: count} of cylinder faces in a fingerprint."""
    out = {}
    for s, c in fp["faces"].items():
        if s[0] == "CYL" and len(s) >= 8 and s[7] is not None:
            out[s[7]] = out.get(s[7], 0) + c
    return out
