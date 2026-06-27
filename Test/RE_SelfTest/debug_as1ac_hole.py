# encoding: utf-8
# =====================================================================
# debug_as1ac_hole.py — deep dive on a single failing case.
#
# Trace as1-ac-214 Hole modification end-to-end:
#   1. Import via RealModelPipeline
#   2. Dump body state BEFORE mod (all cylinder faces: index, R, axis origin,
#      axis direction, IsReversed)
#   3. Call ChangeHoleDiameter directly
#   4. Dump body state AFTER mod
#   5. Diff — which face changed? Where did the tool actually cut?
# Output: D:\...\Test\RealCAD\debug_as1ac_hole.txt
# =====================================================================
import os, sys, traceback
from datetime import datetime

ADDIN_DLL = r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll"
TARGET_STP = r"D:\MXDigitalTwinModeller\Test\RealCAD\caxif\as1-ac-214.stp"
REAL_CAD   = r"D:\MXDigitalTwinModeller\Test\RealCAD"
OUT_LOG    = r"D:\MXDigitalTwinModeller\Test\RealCAD\debug_as1ac_hole.txt"
DONE_MARK  = r"D:\MXDigitalTwinModeller\Test\RealCAD\solo_done.txt"

# Open log
LF = open(OUT_LOG, "w")
def log(msg):
    line = "[%s] %s" % (datetime.now().strftime("%H:%M:%S.%f")[:-3], msg)
    try:
        if isinstance(line, unicode):
            print(line.encode("ascii", "replace"))
        else:
            print(line)
    except Exception:
        pass
    try: LF.write(line + "\n"); LF.flush()
    except Exception: pass


def dump_body_cylinders(body, label):
    log("===== CYLINDER FACES @ %s =====" % label)
    if body is None or body.Shape is None:
        log("  body or body.Shape is None"); return
    i = 0; cyl_count = 0
    for df in body.Faces:
        try:
            if df is None or df.Shape is None: i += 1; continue
            geom = df.Shape.Geometry
            type_name = type(geom).__name__ if geom is not None else "None"
            if type_name == "Cylinder":
                cyl_count += 1
                r_m = geom.Radius
                fo = geom.Frame.Origin
                fz = geom.Frame.DirZ.UnitVector
                rev = df.Shape.IsReversed
                log("  face[%3d] Cylinder R=%.6fm (%.3fmm)  origin=(%.4f,%.4f,%.4f)  axis=(%.3f,%.3f,%.3f)  rev=%s" % (
                    i, r_m, r_m*1000, fo.X, fo.Y, fo.Z, fz.X, fz.Y, fz.Z, rev))
        except Exception as e:
            log("  face[%3d] inspect EXC: %s" % (i, e))
        i += 1
    log("  Total faces=%d, cylinder count=%d" % (i, cyl_count))


def dump_designbody_xform(body, label):
    log("===== TRANSFORM @ %s =====" % label)
    try:
        # designBody.Transform might exist
        xf = body.Transform if hasattr(body, "Transform") else None
        log("  designBody.Transform = %s" % xf)
        if xf is not None:
            # Just dump string
            log("  xf str = %s" % str(xf))
    except Exception as e:
        log("  xform inspect EXC: %s" % e)


try:
    import clr
    clr.AddReferenceToFileAndPath(ADDIN_DLL)
    from SpaceClaim.Api.V252 import Document, Window
    from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import (
        FeatureExtractor, ModificationService, RealModelPipeline,
    )

    try: RealModelPipeline.ApplyColors = False
    except Exception: pass
    try: RealModelPipeline.ExtractAllBodies = False
    except Exception: pass
    try:
        if Window.ActiveWindow is None: Document.Create()
    except Exception: pass

    log("Pipeline.Run %s" % TARGET_STP)
    pr = RealModelPipeline.Run(TARGET_STP, REAL_CAD)
    if pr is None or not pr.Success:
        log("FATAL: pipeline failed: %s" % (pr.ErrorMessage if pr else "None"))
    else:
        body = pr.ImportedBody
        graph = pr.Graph
        log("body=%s  graph=%s" % (body, graph))
        if graph and graph.Holes:
            log("Graph has %d holes" % graph.Holes.Count)
            for h in graph.Holes:
                log("  Hole %s: D=%.4fmm  CylinderFaceIndex=%d" % (h.Id, h.DiameterMm, h.CylinderFaceIndex))

        dump_designbody_xform(body, "BEFORE")
        dump_body_cylinders(body, "BEFORE")

        # Direct mod
        if graph and graph.Holes and graph.Holes.Count > 0:
            target = graph.Holes[0]
            cur = float(target.DiameterMm)
            new = cur * 1.2
            log("\nCalling ChangeHoleDiameter %s: %.4f -> %.4f mm" % (target.Id, cur, new))
            log("  Target CylinderFaceIndex = %d" % target.CylinderFaceIndex)

            # Pre-mod: dump that specific face
            i = 0
            for df in body.Faces:
                if i == target.CylinderFaceIndex:
                    try:
                        g = df.Shape.Geometry
                        log("  TARGET face[%d]: type=%s, R=%.6fm origin=(%.4f,%.4f,%.4f)" % (
                            i, type(g).__name__,
                            g.Radius if hasattr(g, "Radius") else -1.0,
                            g.Frame.Origin.X, g.Frame.Origin.Y, g.Frame.Origin.Z))
                    except Exception as e:
                        log("  TARGET face inspect EXC: %s" % e)
                    break
                i += 1

            r = ModificationService.ChangeHoleDiameter(body, graph, target.Id, new)
            log("\nMod result: Success=%s  Strategy hint=%s  Err=%s" % (
                r.Success, r.HintMessage, r.ErrorMessage))

            dump_body_cylinders(body, "AFTER")

            # Re-extract
            try:
                extractor = FeatureExtractor()
                g2 = extractor.Extract(body)
                log("\nRe-extracted: %d holes" % (g2.Holes.Count if g2.Holes else 0))
                for h in g2.Holes:
                    log("  Hole %s: D=%.4fmm  CylinderFaceIndex=%d" % (h.Id, h.DiameterMm, h.CylinderFaceIndex))
            except Exception as e:
                log("Re-extract EXC: %s" % e)
                log(traceback.format_exc())

except Exception as ex:
    log("UNHANDLED: %s" % ex)
    log(traceback.format_exc())
finally:
    LF.close()

# Drop done marker so PS1 can kill SC
try:
    with open(DONE_MARK, "w") as f: f.write("done\n")
except Exception: pass
