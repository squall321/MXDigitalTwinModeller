# encoding: utf-8
# =====================================================================
# probe_gate_experiments.py — Phase 1 gate experiments (SAFE half)
#
# Runs in ONE SC session (no kernel poisoning here):
#   W1-1  ContainsPoint MaterialSign spike — unit body, 6 probe configs
#   PRE   Body.Copy() / Copy(out fm, out em) smoke
#   E-2   Mechanism B: cached Face handle IsDeleted after SUCCESSFUL offset
#   E-3   SetFaceGeometry/ReplaceFaceGeometry smoke (reflection-discover
#         FaceGeometry construction, then try a hole-radius swap)
#   VOL   Body.Volume before/after a known mod (proof-of-concept for the
#         volume 3-tier gate)
#
# Output: D:\...\Test\RealCAD\probe_gate_results.txt  (+ solo_done.txt)
# =====================================================================
import traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
OUT_LOG   = r"D:\MXDigitalTwinModeller\Test\RealCAD\probe_gate_results.txt"
DONE_MARK = r"D:\MXDigitalTwinModeller\Test\RealCAD\solo_done.txt"

LF = open(OUT_LOG, "w")
def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S"), msg)
    try:
        print(line.encode("ascii", "replace") if isinstance(line, unicode) else line)
    except Exception: pass
    try: LF.write(line + "\n"); LF.flush()
    except Exception: pass

PASS=[]; FAIL=[]; INFO=[]
def verdict(name, ok, detail=""):
    (PASS if ok else FAIL).append(name)
    log("%s %s %s" % ("PASS" if ok else "FAIL", name, detail))

try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window, Part, DesignBody, WriteBlock
    from SpaceClaim.Api.V252.Geometry import (
        Point, Direction, Frame, Plane, Matrix, Vector, RectangleProfile, PointUV,
    )
    from SpaceClaim.Api.V252.Modeler import Body, Face as MFace, Edge as MEdge
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry import ProfileBuilder
    import System

    # Host doc
    doc = Document.Create()
    part = doc.MainPart
    log("Host doc created: %s" % doc)

    # =================================================================
    # Build unit test body: 20x20x10mm box, 5mm-dia through hole at center,
    # so we have: planar faces, a concave (hole) cylinder.
    # =================================================================
    mm = 0.001
    box = None
    def build_box():
        f = Frame.Create(Point.Create(0,0,0), Direction.DirX, Direction.DirY)
        pl = Plane.Create(f)
        prof = RectangleProfile(pl, 20*mm, 20*mm, PointUV.Create(0,0), 0.0)
        return Body.ExtrudeProfile(prof, 10*mm)

    holeTool = None
    def build_hole_tool(radius_m, z0, length):
        f = Frame.Create(Point.Create(0,0,z0), Direction.DirX, Direction.DirY)
        pl = Plane.Create(f)
        b = ProfileBuilder(pl)
        b.AddCircle(Point.Create(0,0,z0), radius_m)
        prof = b.Build()
        return Body.ExtrudeProfile(prof, length)

    unitBody = [None]
    def make_unit():
        WriteBlock.ExecuteTask("unit body", _make_unit_inner)
    def _make_unit_inner():
        bx = build_box()
        tool = build_hole_tool(2.5*mm, -1*mm, 12*mm)
        bx.Subtract(System.Array[Body]([tool]))
        db = DesignBody.Create(part, "unit", bx)
        unitBody[0] = db
    make_unit()
    db = unitBody[0]
    log("Unit body: faces=%d" % sum(1 for _ in db.Faces))

    # locate hole cylinder face + a planar face
    from SpaceClaim.Api.V252.Geometry import Cylinder as GCyl
    holeFace = None; topFace = None
    for df in db.Faces:
        g = df.Shape.Geometry
        tn = type(g).__name__
        if tn == "Cylinder" and holeFace is None:
            holeFace = df
        elif tn == "Plane":
            # top face = max Z
            if topFace is None: topFace = df
    log("holeFace=%s topFace=%s" % (holeFace is not None, topFace is not None))

    # =================================================================
    # W1-1: ContainsPoint MaterialSign spike
    # =================================================================
    log("=" * 60)
    log("W1-1 ContainsPoint spike")
    shape = db.Shape

    # cfg1: center of solid (5,5,5)mm — inside (true)
    p_in = Point.Create(5*mm, 5*mm, 5*mm)
    r1 = shape.ContainsPoint(p_in)
    verdict("CP-1 solid interior", r1 == True, "got %s" % r1)

    # cfg2: far outside
    p_out = Point.Create(50*mm, 50*mm, 50*mm)
    r2 = shape.ContainsPoint(p_out)
    verdict("CP-2 far outside", r2 == False, "got %s" % r2)

    # cfg3: inside hole bore (0,0,5)mm — air (false)
    p_bore = Point.Create(0, 0, 5*mm)
    r3 = shape.ContainsPoint(p_bore)
    verdict("CP-3 hole bore (air)", r3 == False, "got %s" % r3)

    # cfg4/5: MaterialSign oracle on hole cylinder face.
    # face point at (R,0,5)mm on the bore wall. Material is AWAY from axis.
    cylg = holeFace.Shape.Geometry
    R = cylg.Radius
    eps = 0.2 * R
    p_toward_axis = Point.Create(R - eps, 0, 5*mm)   # into the bore → air
    p_away_axis   = Point.Create(R + eps, 0, 5*mm)   # into material → solid
    r4 = shape.ContainsPoint(p_toward_axis)
    r5 = shape.ContainsPoint(p_away_axis)
    verdict("CP-4 hole face toward-axis = air", r4 == False, "got %s" % r4)
    verdict("CP-5 hole face away-axis = material", r5 == True, "got %s" % r5)
    # => MaterialSign(holeFace): material on +radial side — concave w.r.t. axis.

    # cfg6 (on-surface point) MOVED TO END OF SCRIPT — first run crashed the
    # whole SC process at this exact call (log truncated mid-script with no
    # UNHANDLED entry). On-surface ContainsPoint = kernel landmine.

    # =================================================================
    # PRE: Body.Copy smoke
    # =================================================================
    log("=" * 60)
    log("PRE Body.Copy smoke")
    try:
        c1 = shape.Copy()
        verdict("COPY-1 Body.Copy()", c1 is not None,
                "faces=%d vs orig=%d" % (sum(1 for _ in c1.Faces), sum(1 for _ in shape.Faces)))
    except Exception as e:
        verdict("COPY-1 Body.Copy()", False, str(e))

    try:
        from System.Collections.Generic import Dictionary as NetDict
        from SpaceClaim.Api.V252.Modeler import Face as MFace, Edge as MEdge
        fm = clr.Reference[System.Collections.Generic.IDictionary[MFace, MFace]]()
        em = clr.Reference[System.Collections.Generic.IDictionary[MEdge, MEdge]]()
        c2 = shape.Copy(fm, em)
        okmap = c2 is not None and fm.Value is not None and fm.Value.Count > 0
        verdict("COPY-2 Copy(out fm,out em)", okmap,
                "fm.Count=%s" % (fm.Value.Count if fm.Value is not None else "null"))
        # locate the copied hole face via map
        mappedHole = None
        if okmap:
            for kv in fm.Value:
                if kv.Key.Equals(holeFace.Shape):
                    mappedHole = kv.Value; break
            verdict("COPY-3 faceMap resolves hole face", mappedHole is not None, "")
        # COPY-4 (offset on detached copy) MOVED TO END — first run crashed
        # the SC process inside this call.
    except Exception as e:
        verdict("COPY-2 Copy(out fm,out em)", False, str(e))

    # =================================================================
    # VOL: Body.Volume proof-of-concept
    # =================================================================
    log("=" * 60)
    log("VOL Body.Volume gate")
    try:
        v0 = shape.Volume
        # enlarge hole 2.5 → 3.0mm on the LIVE body, expect volume DECREASE
        def _off_live():
            shape.OffsetFaces(System.Array[type(holeFace.Shape)]([holeFace.Shape]), 0.0005)
        WriteBlock.ExecuteTask("vol test offset", _off_live)
        v1 = shape.Volume
        # analytic: dV = -pi*(R1^2-R0^2)*h  (hole enlarged: removed material)
        import math
        dv_analytic = -math.pi * ((0.003)**2 - (0.0025)**2) * 0.010
        dv = v1 - v0
        sign_ok = (dv < 0) == (dv_analytic < 0)
        mag_ok = abs(dv) > 1e-12 and abs(dv / dv_analytic) < 3.0 and abs(dv / dv_analytic) > 0.33
        verdict("VOL-1 sign(dV)==sign(analytic)", sign_ok, "dv=%.3e analytic=%.3e" % (dv, dv_analytic))
        verdict("VOL-2 |dV| in 3x sanity band", mag_ok, "ratio=%.2f" % (dv / dv_analytic))
    except Exception as e:
        verdict("VOL-1 volume gate", False, str(e))

    # =================================================================
    # E-2: Mechanism B — cached handle IsDeleted after SUCCESSFUL offset
    # (the VOL test above already offset holeFace.Shape successfully once;
    #  now check the ORIGINAL cached Face handle state)
    # =================================================================
    log("=" * 60)
    log("E-2 IsDeleted mechanism")
    try:
        # holeFace is the cached DesignFace handle captured BEFORE the offset.
        dfDeleted = None
        try: dfDeleted = holeFace.IsDeleted
        except Exception as e2: dfDeleted = "EXC:%s" % e2
        shpDeleted = None
        try: shpDeleted = holeFace.Shape.IsDeleted
        except Exception as e3: shpDeleted = "EXC:%s" % e3
        INFO.append("E2: DesignFace.IsDeleted=%s Face(shape).IsDeleted=%s" % (dfDeleted, shpDeleted))
        log("INFO E-2 DesignFace.IsDeleted=%s Shape.IsDeleted=%s" % (dfDeleted, shpDeleted))
        # second consecutive offset using the CACHED handle — reproduce or refute
        # the "object is deleted" on second op
        try:
            def _off2():
                shape.OffsetFaces(System.Array[type(holeFace.Shape)]([holeFace.Shape]), 0.0002)
            WriteBlock.ExecuteTask("second offset cached handle", _off2)
            verdict("E2-1 second offset w/ CACHED handle", True, "no exception")
        except Exception as e4:
            verdict("E2-1 second offset w/ CACHED handle", False, str(e4))
        # second offset with FRESH handle
        try:
            freshHole = None
            for f in shape.Faces:
                if type(f.Geometry).__name__ == "Cylinder": freshHole = f
            def _off3():
                shape.OffsetFaces(System.Array[type(freshHole)]([freshHole]), 0.0002)
            WriteBlock.ExecuteTask("third offset fresh handle", _off3)
            verdict("E2-2 offset w/ FRESH handle", True, "no exception")
        except Exception as e5:
            verdict("E2-2 offset w/ FRESH handle", False, str(e5))
    except Exception as e:
        verdict("E2 mechanism probe", False, str(e))

    # =================================================================
    # E-3: ReplaceFaceGeometry smoke
    # =================================================================
    log("=" * 60)
    log("E-3 ReplaceFaceGeometry smoke")
    try:
        from SpaceClaim.Api.V252.Unsupported import BodyMethods
        import System.Reflection as Refl
        # discover FaceGeometry type + how to make one
        fgType = None
        for t in clr.GetClrType(BodyMethods).Assembly.GetTypes():
            if t.Name == "FaceGeometry" and "Modeler" in str(t.Namespace):
                fgType = t; break
        if fgType is None:
            verdict("E3-1 FaceGeometry type found", False, "not in assembly")
        else:
            ctors = fgType.GetConstructors()
            statics = [m.Name for m in fgType.GetMethods() if m.IsStatic]
            props = [p.Name for p in fgType.GetProperties()]
            INFO.append("E3 FaceGeometry ctors=%d statics=%s props=%s" % (len(ctors), statics[:8], props[:8]))
            log("INFO E3 FaceGeometry: ctors=%d statics=%s props=%s" % (len(ctors), statics[:8], props[:8]))
            for c in ctors:
                ps = ", ".join("%s %s" % (p.ParameterType.Name, p.Name) for p in c.GetParameters())
                log("INFO E3 ctor(%s)" % ps)
            verdict("E3-1 FaceGeometry type found", True, "")
            # try the swap: current freshest hole face → new Cylinder geometry R+0.5mm
            try:
                freshHole = None
                for f in shape.Faces:
                    if type(f.Geometry).__name__ == "Cylinder": freshHole = f
                curCyl = freshHole.Geometry
                # Build a new Cylinder surface: same frame, bigger radius.
                from SpaceClaim.Api.V252.Geometry import Cylinder
                cylStatics = [m.Name for m in clr.GetClrType(Cylinder).GetMethods() if m.IsStatic]
                log("INFO E3 Geometry.Cylinder statics=%s" % cylStatics[:10])
                newCyl = None
                try:
                    newCyl = Cylinder.Create(curCyl.Frame, curCyl.Radius + 0.0005)
                except Exception as eC:
                    log("INFO E3 Cylinder.Create(frame,r) failed: %s" % eC)
                if newCyl is not None:
                    # FaceGeometry ctor discovered by reflection: (Surface surface, Boolean reversed)
                    fgInstance = None
                    for c in ctors:
                        prms = c.GetParameters()
                        try:
                            if len(prms) == 2:
                                fgInstance = c.Invoke(System.Array[System.Object](
                                    [newCyl, freshHole.IsReversed]))
                                break
                            elif len(prms) == 1:
                                fgInstance = c.Invoke(System.Array[System.Object]([newCyl]))
                                break
                        except Exception as eCtor:
                            log("INFO E3 ctor invoke failed: %s" % str(eCtor)[:100])
                            continue
                    if fgInstance is None:
                        verdict("E3-2 FaceGeometry construct", False, "no ctor accepted (Cylinder[,bool])")
                    else:
                        verdict("E3-2 FaceGeometry construct", True, "")
                        from System.Collections.Generic import Dictionary
                        from SpaceClaim.Api.V252.Modeler import Face as MFace2
                        d = Dictionary[MFace2, type(fgInstance)]()
                        d[freshHole] = fgInstance
                        try:
                            def _repl():
                                BodyMethods.ReplaceFaceGeometry(shape, d, True, 1e-8, False)
                            WriteBlock.ExecuteTask("replace face geom", _repl)
                            # verify R changed
                            postR = None
                            for f in shape.Faces:
                                if type(f.Geometry).__name__ == "Cylinder": postR = f.Geometry.Radius
                            verdict("E3-3 ReplaceFaceGeometry hole swap",
                                    postR is not None and abs(postR - (curCyl.Radius + 0.0005)) < 1e-6,
                                    "postR=%s" % postR)
                        except Exception as eR:
                            verdict("E3-3 ReplaceFaceGeometry hole swap", False, str(eR))
            except Exception as e:
                verdict("E3-2 swap attempt", False, str(e))
    except Exception as e:
        verdict("E3 import", False, str(e))

    # =================================================================
    log("=" * 60)
    log("SUMMARY: PASS=%d FAIL=%d INFO=%d" % (len(PASS), len(FAIL), len(INFO)))
    for n in PASS: log("  PASS " + n)
    for n in FAIL: log("  FAIL " + n)

    # =================================================================
    # CRASH-CANDIDATE SECTION — everything above is logged+flushed.
    # Ordered least → most dangerous so a crash loses the minimum.
    # =================================================================

    # COPY-4b: wrap the copy in DesignBody.Create FIRST, then offset.
    # (BendingFixtureService pattern — document-attached bodies are the
    # supported path; raw detached ops are the crash suspect.)
    log("COPY-4b attempting DesignBody.Create(copy) + offset...")
    try:
        c4 = shape.Copy()
        attached = [None]
        def _att():
            attached[0] = DesignBody.Create(part, "probe_copy", c4)
        WriteBlock.ExecuteTask("attach copy", _att)
        attachedDb = attached[0]
        # find hole face on the attached copy and offset it
        copyHole = None
        for f in attachedDb.Shape.Faces:
            if type(f.Geometry).__name__ == "Cylinder": copyHole = f
        preR4 = copyHole.Geometry.Radius if copyHole is not None else None
        def _off4():
            attachedDb.Shape.OffsetFaces(System.Array[MFace]([copyHole]), 0.0005)
        WriteBlock.ExecuteTask("offset attached copy", _off4)
        postR4 = None
        for f in attachedDb.Shape.Faces:
            if type(f.Geometry).__name__ == "Cylinder": postR4 = f.Geometry.Radius
        verdict("COPY-4b offset on ATTACHED copy",
                postR4 is not None and preR4 is not None and abs(postR4 - (preR4 + 0.0005)) < 1e-6,
                "preR=%s postR=%s" % (preR4, postR4))
        # original body untouched?
        origR = None
        for f in shape.Faces:
            if type(f.Geometry).__name__ == "Cylinder": origR = f.Geometry.Radius
        log("INFO COPY-4b original body R after copy-op = %s (isolation check)" % origR)
    except Exception as e4b:
        verdict("COPY-4b offset on ATTACHED copy", False, str(e4b)[:150])

    # COPY-4a: offset on RAW DETACHED copy (the crash reproducer) — second
    # to last.
    log("COPY-4a attempting offset on raw detached copy (crashed SC last run)...")
    try:
        if mappedHole is not None:
            def _offD():
                c2.OffsetFaces(System.Array[type(mappedHole)]([mappedHole]), 0.0005)
            WriteBlock.ExecuteTask("offset detached", _offD)
            log("INFO COPY-4a detached offset survived (no crash this time)")
        else:
            log("INFO COPY-4a skipped (no mapped hole)")
    except Exception as e4a:
        log("INFO COPY-4a threw (better than crash): %s" % str(e4a)[:150])

    # CP-6 — LAST, known process-killer from run 1.
    log("CP-6 attempting on-surface ContainsPoint (crashed SC in run 1)...")
    try:
        p_on = Point.Create(R, 0, 5*mm)
        r6 = shape.ContainsPoint(p_on)
        log("INFO CP-6 on-surface → %s (no crash this time)" % r6)
    except Exception as e6:
        log("INFO CP-6 threw (better than crash): %s" % e6)

except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
finally:
    LF.close()

try:
    with open(DONE_MARK, "w") as f: f.write("done\n")
except Exception: pass
