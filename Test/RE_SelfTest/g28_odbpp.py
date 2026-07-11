# encoding: utf-8
# g28 (ODB++ import, g26-level bar): synthetic ODB++ trees written to spec exercise
# every parser/builder path with independent analytics:
#   P1 parse : L-shape island (shoelace 2100) + hole->cutout, 2 PKGs (explicit outline
#              + bbox fallback + '&' continuation), 3 CMPs (rot 90 CW, mirror M,
#              COMP_HEIGHT prop), pin totals, component layer from matrix
#   P2 import: board V = (2100-48)*t EXACT + PieceCount 1; U1 height 1.4 honored
#              (z-span over pads); R1 rotated CW (pad at hand-transformed coords);
#              U2 mirrored to the BOTTOM face; 10 pads with exact z spans
#   P3 units : INCH-default fixture (no UNITS line) -> 1x1 inch = 645.16 mm2; PRP
#              COMP_HEIGHT in the inch file scales 25.4x; pin-less comp sits FLUSH
#   P4 errors: .tgz-like file path / unknown step / min_footprint filter counts
#   P5 parse2: hardened fixture - glued ';attr' suffix on PKG, '@n .comp_height'
#              indexed attribute, pin contour NOT stolen as package outline, RC/CR
#              outline records, package hole contours, full-circle OC arc, multi-island
#              profile warning, step-and-repeat panel detection + step auto-select
#   P6 import2: mirror+rot placement (rotate CW THEN mirror - spec-derived pads),
#              rot 180/270, RC rect / CR circle volumes, frame = ring volume,
#              flush shield, degenerate package -> loud skip, foreign cutout skipped,
#              pad dia = 0.55*pitch asserted, duplicate RefDes deduped, kernel dims
#   P7 guards: max_components loud abort with ZERO bodies, 1e10 clamp, re-import
#              into the same document rejected loudly
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File, Directory, Path
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g28_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g28_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g28_done.txt"
FIX = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_fixture"
FIX2 = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_fixture_inch"
FIX3 = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_fixture_hard"

def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8Encoding(False))
    except Exception: pass
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(unprintable)")

H = {}
UTF8 = UTF8Encoding(False)

def wf(path, text):
    Directory.CreateDirectory(Path.GetDirectoryName(path))
    File.WriteAllText(path, text, UTF8)

def build_fixture():
    wf(FIX + r"\matrix\matrix",
       "STEP {\n   COL=1\n   NAME=pcb\n}\n"
       "LAYER {\n   NAME=comp_+_top\n   TYPE=COMPONENT\n}\n"
       "LAYER {\n   NAME=top\n   TYPE=SIGNAL\n}\n")
    wf(FIX + r"\steps\pcb\profile",
       "UNITS=MM\nS P 0\n"
       "OB 0 0 I\nOS 60 0\nOS 60 25\nOS 30 25\nOS 30 45\nOS 0 45\nOE\n"
       "OB 10 10 H\nOS 18 10\nOS 18 16\nOS 10 16\nOE\n")
    wf(FIX + r"\steps\pcb\eda\data",
       "UNITS=MM\n# packages\n"
       "PKG qfn12 1.0 -6\n& -6 6 6\n"           # '&' continuation joins the PKG record
       "OB -6 -6\nOS 6 -6\nOS 6 6\nOS -6 6\nOE\n"
       "PIN 1 T -4.5 -4.5 0 E\nPIN 2 T 4.5 -4.5 0 E\n"
       "PIN 3 T 4.5 4.5 0 E\nPIN 4 T -4.5 4.5 0 E\n"
       "PKG res0402 0.5 -1 -0.5 1 0.5\n"        # no outline records -> bbox fallback
       "PIN 1 T -0.75 0 0 E\nPIN 2 T 0.75 0 0 E\n")
    wf(FIX + r"\steps\pcb\layers\comp_+_top\components",
       "UNITS=MM\n"
       "CMP 0 40 12 0 N U1 AP-CHIP ;attr=1\n"
       "PRP COMP_HEIGHT '1.4'\n"
       "CMP 1 15 35 90 N R1 RES\n"
       "CMP 0 10 30 0 M U2 BOTTOM-CHIP\n")

def build_fixture_inch():
    # NO UNITS lines anywhere -> ODB++ default INCH must apply (positions, bbox,
    # and COMP_HEIGHT alike); the box package has no pins -> flush seating
    wf(FIX2 + r"\matrix\matrix", "STEP {\n   NAME=pcb\n}\n")
    wf(FIX2 + r"\steps\pcb\profile",
       "OB 0 0 I\nOS 1 0\nOS 1 1\nOS 0 1\nOE\n")
    wf(FIX2 + r"\steps\pcb\eda\data",
       "PKG box 0 -0.1 -0.1 0.1 0.1\n")
    wf(FIX2 + r"\steps\pcb\layers\comp_+_top\components",
       "CMP 0 0.5 0.5 0 N U1 X\n"
       "PRP COMP_HEIGHT '0.055'\n")

def build_fixture_hard():
    # two steps: 'array' is a step-and-repeat panel (alphabetically FIRST - the
    # auto-select must skip it), 'pcb' is the real board
    wf(FIX3 + r"\matrix\matrix",
       "STEP {\n   COL=1\n   NAME=array\n}\n"
       "STEP {\n   COL=2\n   NAME=pcb\n}\n"
       "LAYER {\n   NAME=comp_+_top\n   TYPE=COMPONENT\n}\n"
       "LAYER {\n   NAME=comp_+_bot\n   TYPE=COMPONENT\n}\n")
    wf(FIX3 + r"\steps\array\profile",
       "UNITS=MM\nOB 0 0 I\nOS 10 0\nOS 10 10\nOS 0 10\nOE\n")
    wf(FIX3 + r"\steps\array\stephdr",
       "SR {\n   NAME=pcb\n   X=5 Y=5 NX=2 NY=3 DX=63.5 DY=25.4\n}\n")
    # profile: 100x50 island + interior 8x6 hole + FULL-CIRCLE OC hole (rounded
    # endpoint 1e-5 off) + second island (warned) + its hole (foreign -> skipped);
    # units via Board Station 'U MM' record (real Mentor exports use this, not UNITS=)
    wf(FIX3 + r"\steps\pcb\profile",
       "U MM\nS P 0\n"
       "OB 0 0 I\nOS 100 0\nOS 100 50\nOS 0 50\nOE\n"
       "OB 20 20 H\nOS 28 20\nOS 28 26\nOS 20 26\nOE\n"
       "OB 65 25 H\nOC 65.00001 25 60 25 Y\nOE\n"
       "OB 190 190 I\nOS 220 190\nOS 220 220\nOS 190 220\nOE\n"
       "OB 200 200 H\nOS 210 200\nOS 210 210\nOS 200 210\nOE\n")
    # packages: 0 qfn12 (glued ';' suffix + CT outline), 1 so8 (NO pkg outline, pin
    # contour must NOT be stolen -> bbox 4x6), 2 rcbody (RC rect DIFFERENT from its
    # 18x18 declared bbox), 3 cap_d4 (CR circle r=2), 4 frame (ring: island+hole),
    # 5 shield (pinless bbox), 6 bad (degenerate - no bbox at all)
    wf(FIX3 + r"\steps\pcb\eda\data",
       "UNITS=MM\n"
       "PKG qfn12 1.0 -6 -6 6 6;0=1,1=0.5\n"
       "CT\nOB -6 -6 I\nOS 6 -6\nOS 6 6\nOS -6 6\nOE\nCE\n"
       "PIN 1 T -4.5 -4.5 0 E\nPIN 2 T 4.5 4.5 0 E\n"
       "PKG so8 1.27 -2 -3 2 3\n"
       "PIN 1 S -1.5 -2 0 E\n"
       "CT\nOB -1.8 -2.3 I\nOS -1.2 -2.3\nOS -1.2 -1.7\nOS -1.8 -1.7\nOE\nCE\n"
       "PKG rcbody 0 -9 -9 9 9\n"
       "RC -2 -3 4 6\n"
       "PKG cap_d4 0 -2 -2 2 2\n"
       "CR 0 0 2\n"
       "PKG frame 0 -5 -5 5 5\n"
       "CT\nOB -5 -5 I\nOS 5 -5\nOS 5 5\nOS -5 5\nOE\n"
       "OB -4 -4 H\nOS 4 -4\nOS 4 4\nOS -4 4\nOE\nCE\n"
       "PKG shield 0 -3 -3 3 3\n"
       "PKG bad 0.5\n"
       "NET dummy\n")
    wf(FIX3 + r"\steps\pcb\layers\comp_+_top\components",
       "UNITS=MM\n"
       "@0 .comp_mount_type\n"
       "@1 .comp_height\n"
       "&0 chip\n"   # attribute STRING TABLE - must stay standalone, not a continuation
       "&1 smd\n"
       "CMP 0 30 25 90 M U3 CHIP ;0=1,1=2.5\n"
       "CMP 1 70 25 0 N U4 SO8\n"
       "CMP 1 15 10 180 N U6 SO8\n"
       "CMP 1 70 40 270 N U5 SO8\n"
       "CMP 1 40 10 0 N U4 SO8DUP\n"
       "CMP 2 85 10 0 N RC1 RCBODY\n"
       "CMP 3 20 40 0 N C4 CAP\n"
       "CMP 4 80 40 0 N FR1 FRAME\n"
       "CMP 5 50 40 0 N SH1 SHIELD\n"
       "CMP 6 50 10 0 N BAD1 X\n"
       "TOP 0 26.67 41.91 270 N 1 0 r170_50\n")
    # bottom layer: mirror field says 'N' but the LAYER decides (real Mentor exports)
    wf(FIX3 + r"\steps\pcb\layers\comp_+_bot\components",
       "UNITS=MM\n"
       "CMP 1 60 10 0 N B1 SO8BOT\n")

def env_num(env, key):
    import re
    m = re.search('"%s": ([-0-9.eE]+)' % key, env)
    return float(m.group(1)) if m else float("nan")

def body_by_name(part, name):
    for b in part.Bodies:
        if b.Name == name: return b
    return None

def bb_mm(db):
    bb = db.Shape.GetBoundingBox(Matrix.Identity)
    return (bb.MinCorner.X * 1000, bb.MinCorner.Y * 1000, bb.MinCorner.Z * 1000,
            bb.MaxCorner.X * 1000, bb.MaxCorner.Y * 1000, bb.MaxCorner.Z * 1000)

def vol_mm3(db):
    return db.Shape.Volume * 1e9

def pad_center_ok(part, name, cx, cy):
    p = bb_mm(body_by_name(part, name))
    return abs((p[0] + p[3]) / 2 - cx) < 1e-6 and abs((p[1] + p[4]) / 2 - cy) < 1e-6

def _do():
    _mk("do-start")
    build_fixture()
    build_fixture_inch()
    build_fixture_hard()
    fixj = FIX.replace("\\", "\\\\")
    fix2j = FIX2.replace("\\", "\\\\")
    fix3j = FIX3.replace("\\", "\\\\")

    # ---- P1: parse summary ------------------------------------------------------
    env = LlmToolDispatcher.Dispatch(None, None, "parse_odbpp", '{"path": "%s"}' % fixj)
    H["P1"] = ('"success": true' in env
               and '"steps": ["pcb"]' in env and '"outline_points": 6' in env
               and abs(env_num(env, "outline_area_mm2") - 2100) < 0.01
               and '"cutouts": 1' in env and '"packages": 2' in env
               and '"components": 3' in env and '"components_bottom": 1' in env
               and '"total_pins": 10' in env and 'comp_+_top' in env)
    _mk("P1 parse -> %s | %s" % (H["P1"], env[:260]))

    # ---- P2: import (kernel-true placement) --------------------------------------
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.2}' % fixj)
    okP2 = '"success": true' in env
    if okP2:
        part = Window.ActiveWindow.Document.MainPart
        board = body_by_name(part, "Pcb_Board")
        vb = board.Shape.Volume * 1e9
        exp = (2100.0 - 48.0) * 1.2
        okP2 = abs(vb - exp) < exp * 0.001 and board.Shape.PieceCount == 1
        # kernel-truth dims must match the real solid
        okP2 = okP2 and abs(env_num(env, "board_v_mm3") - exp) < exp * 0.001
        # 1 board + 3 comps + 10 pads = 14 bodies
        okP2 = okP2 and len(list(part.Bodies)) == 14
        # U1: COMP_HEIGHT 1.4, seated on pads (t + padT = 1.25)
        u1 = bb_mm(body_by_name(part, "Comp_U1"))
        okP2 = okP2 and abs(u1[2] - 1.25) < 1e-6 and abs(u1[5] - 2.65) < 1e-6 \
               and abs(u1[0] - 34) < 1e-6 and abs(u1[3] - 46) < 1e-6
        # R1: 90 deg CW rotation -> bbox 1 wide x 2 tall; pad1 at (15, 35.75)
        r1 = bb_mm(body_by_name(part, "Comp_R1"))
        okP2 = okP2 and abs((r1[3] - r1[0]) - 1.0) < 1e-6 and abs((r1[4] - r1[1]) - 2.0) < 1e-6
        p1 = bb_mm(body_by_name(part, "Comp_R1_Pad_001"))
        okP2 = okP2 and abs((p1[0] + p1[3]) / 2 - 15.0) < 1e-6 \
               and abs((p1[1] + p1[4]) / 2 - 35.75) < 1e-6 \
               and abs(p1[2] - 1.2) < 1e-6 and abs(p1[5] - 1.25) < 1e-6
        # U2: mirrored -> BOTTOM face, z span [-1.05, -0.05], pads [-0.05, 0]
        u2 = bb_mm(body_by_name(part, "Comp_U2"))
        up = bb_mm(body_by_name(part, "Comp_U2_Pad_001"))
        okP2 = okP2 and abs(u2[5] + 0.05) < 1e-6 and abs(u2[2] + 1.05) < 1e-6 \
               and abs(up[2] + 0.05) < 1e-6 and abs(up[5]) < 1e-6
        _mk("P2 import: V=%.3f(exp %.3f) bodies=%d U1z[%.2f..%.2f] R1w=%.3f pad1=(%.2f,%.2f) U2z[%.2f..%.2f] -> %s" % (
            vb, exp, len(list(part.Bodies)), u1[2], u1[5], r1[3] - r1[0],
            (p1[0] + p1[3]) / 2, (p1[1] + p1[4]) / 2, u2[2], u2[5], okP2))
    else:
        _mk("P2 env=" + env[:260])
    H["P2"] = okP2

    # ---- P3: INCH default units (profile + COMP_HEIGHT + flush seating) ----------
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.0}' % fix2j)
    okP3 = '"success": true' in env
    if okP3:
        vb = env_num(env, "board_volume_mm3")
        exp = 25.4 * 25.4 * 1.0
        okP3 = abs(vb - exp) < exp * 0.001
        # U1: PRP COMP_HEIGHT '0.055' in the INCH file -> 1.397mm tall; box package
        # has no pins -> seated FLUSH on the board top (z = t), no phantom pad gap
        part = Window.ActiveWindow.Document.MainPart
        u1 = bb_mm(body_by_name(part, "Comp_U1"))
        okP3 = okP3 and abs(u1[2] - 1.0) < 1e-6 and abs(u1[5] - (1.0 + 0.055 * 25.4)) < 1e-6
        okP3 = okP3 and env_num(env, "pads_built") == 0
        _mk("P3 inch: V=%.3f(exp %.3f) U1z[%.4f..%.4f] -> %s" % (vb, exp, u1[2], u1[5], okP3))
    else:
        _mk("P3 env=" + env[:200])
    H["P3"] = okP3

    # ---- P4: errors + footprint filter ---------------------------------------------
    tgz = FIX + r"\fake.tgz"
    File.WriteAllText(tgz, "not really an archive", UTF8)
    e1 = LlmToolDispatcher.Dispatch(None, None, "parse_odbpp",
        '{"path": "%s"}' % tgz.replace("\\", "\\\\"))
    e2 = LlmToolDispatcher.Dispatch(None, None, "parse_odbpp",
        '{"path": "%s", "step": "nope"}' % fixj)
    Document.Create()
    e3 = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "min_footprint_mm": 5}' % fixj)
    H["P4"] = ('EXTRACTED first' in e1 and "not found - available: pcb" in e2
               and '"success": true' in e3
               and '"components_built": 2' in e3 and '"components_skipped": 1' in e3
               and '"pads_built": 8' in e3)
    _mk("P4 errors/filter -> %s | %s | %s | %s" % (H["P4"], e1[:80], e2[:80], e3[:150]))

    # ---- P5: hardened parse (glued attrs, contour ownership, panels) ---------------
    env = LlmToolDispatcher.Dispatch(None, None, "parse_odbpp", '{"path": "%s"}' % fix3j)
    okP5 = ('"success": true' in env
            and '"step": "pcb"' in env                      # auto-select skipped 'array'
            and '"packages": 7' in env and '"components": 11' in env
            and '"components_bottom": 2' in env             # U3 (M flag) + B1 (bot LAYER)
            and '"cutouts": 3' in env and '"total_pins": 7' in env
            and abs(env_num(env, "outline_area_mm2") - 5000) < 0.01
            and 'multiple islands' in env                   # second profile island
            and 'auto-selected' in env
            and 'degenerate bbox' in env                    # PKG bad warned at parse
            and 'unknown record' in env)                    # NET / TOP deduped warning
    envA = LlmToolDispatcher.Dispatch(None, None, "parse_odbpp",
        '{"path": "%s", "step": "array"}' % fix3j)
    okP5 = okP5 and 'step-and-repeat panel' in envA and '"step": "array"' in envA
    H["P5"] = okP5
    _mk("P5 parse2 -> %s | %s | array: %s" % (okP5, env[:300], envA[:160]))

    # ---- P6: hardened import (placement composition + shapes + loud skips) ---------
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.2}' % fix3j)
    okP6 = '"success": true' in env
    if okP6:
        part = Window.ActiveWindow.Document.MainPart
        board = body_by_name(part, "Pcb_Board")
        # board: 100x50 - 8x6 - 24-gon(r=5); foreign cutout skipped
        a24 = 0.5 * 24 * 25 * math.sin(math.pi / 12)
        exp = (5000.0 - 48.0 - a24) * 1.2
        vb = board.Shape.Volume * 1e9
        okP6 = abs(vb - exp) < exp * 1e-6 and board.Shape.PieceCount == 1
        okP6 = okP6 and abs(env_num(env, "board_v_mm3") - exp) < exp * 1e-6
        okP6 = okP6 and 'outside the board island' in env   # foreign cutout, loud
        # 1 board + 10 comps + 7 pads
        okP6 = okP6 and len(list(part.Bodies)) == 18
        okP6 = okP6 and '"components_built": 10' in env and '"components_skipped": 1' in env
        okP6 = okP6 and '"pads_built": 7' in env and 'BAD1 SKIPPED' in env
        # B1: bottom LAYER (mirror field 'N') -> under the board, X-mirrored pad
        b1 = bb_mm(body_by_name(part, "Comp_B1"))
        okP6 = okP6 and abs(b1[5] + 0.05) < 1e-6 and abs(b1[2] + 1.05) < 1e-6
        okP6 = okP6 and pad_center_ok(part, "Comp_B1_Pad_001", 61.5, 8.0)
        # U3 mirrored+rot90: rotate CW FIRST then mirror (spec order):
        # pin1(-4.5,-4.5) -> (-4.5,4.5) -> (4.5,4.5) -> (34.5,29.5)
        # pin2(4.5,4.5)   -> (4.5,-4.5) -> (-4.5,-4.5) -> (25.5,20.5)
        okP6 = okP6 and pad_center_ok(part, "Comp_U3_Pad_001", 34.5, 29.5)
        okP6 = okP6 and pad_center_ok(part, "Comp_U3_Pad_002", 25.5, 20.5)
        # U3: indexed '@1 .comp_height' attr 2.5 -> z [-2.55, -0.05]; pads [-0.05, 0]
        u3 = bb_mm(body_by_name(part, "Comp_U3"))
        u3p = bb_mm(body_by_name(part, "Comp_U3_Pad_001"))
        okP6 = okP6 and abs(u3[2] + 2.55) < 1e-6 and abs(u3[5] + 0.05) < 1e-6 \
               and abs(u3p[2] + 0.05) < 1e-6 and abs(u3p[5]) < 1e-6
        # pad dia = 0.55 * pitch(1.0) - the heuristic is ASSERTED, not cancelled out
        okP6 = okP6 and abs((u3p[3] - u3p[0]) - 0.55) < 1e-6
        # U4: pin contour NOT stolen -> bbox-fallback 4x6x1 = 24; pad (68.5, 23)
        okP6 = okP6 and abs(vol_mm3(body_by_name(part, "Comp_U4")) - 24.0) < 24 * 1e-6
        okP6 = okP6 and pad_center_ok(part, "Comp_U4_Pad_001", 68.5, 23.0)
        # U6 rot180: pin(-1.5,-2) -> (1.5,2) -> (16.5,12); U5 rot270: -> (2,-1.5) -> (72,38.5)
        okP6 = okP6 and pad_center_ok(part, "Comp_U6_Pad_001", 16.5, 12.0)
        okP6 = okP6 and pad_center_ok(part, "Comp_U5_Pad_001", 72.0, 38.5)
        # duplicate RefDes U4 -> deduped body + pad names, loud log
        okP6 = okP6 and body_by_name(part, "Comp_U4_2") is not None
        okP6 = okP6 and pad_center_ok(part, "Comp_U4_2_Pad_001", 38.5, 8.0)
        okP6 = okP6 and 'duplicate RefDes' in env
        # RC1: RC rect (4x6), NOT the 18x18 declared bbox -> V=24, width 4
        rc1 = body_by_name(part, "Comp_RC1")
        rb = bb_mm(rc1)
        okP6 = okP6 and abs(vol_mm3(rc1) - 24.0) < 24 * 1e-6 and abs((rb[3] - rb[0]) - 4.0) < 1e-6
        # C4: CR circle r=2 -> 24-gon V = 0.5*24*4*sin(15deg)*1, pinless -> flush
        c4 = body_by_name(part, "Comp_C4")
        vexp = 0.5 * 24 * 4 * math.sin(math.pi / 12) * 1.0
        cb = bb_mm(c4)
        okP6 = okP6 and abs(vol_mm3(c4) - vexp) < vexp * 1e-6 and abs(cb[2] - 1.2) < 1e-6
        # FR1: frame ring 10x10 - 8x8 = 36, flush z [1.2, 2.2]
        fr1 = body_by_name(part, "Comp_FR1")
        fb = bb_mm(fr1)
        okP6 = okP6 and abs(vol_mm3(fr1) - 36.0) < 36 * 1e-6 \
               and abs(fb[2] - 1.2) < 1e-6 and abs(fb[5] - 2.2) < 1e-6
        # SH1: pinless shield flush on the board (no phantom 0.05 gap)
        sh1 = bb_mm(body_by_name(part, "Comp_SH1"))
        okP6 = okP6 and abs(sh1[2] - 1.2) < 1e-6
        _mk("P6 import2: V=%.4f(exp %.4f) bodies=%d U3pad=(%.2f,%.2f) U4v=%.3f C4v=%.4f FR1v=%.3f -> %s" % (
            vb, exp, len(list(part.Bodies)),
            (u3p[0] + u3p[3]) / 2, (u3p[1] + u3p[4]) / 2,
            vol_mm3(body_by_name(part, "Comp_U4")), vol_mm3(c4), vol_mm3(fr1), okP6))
    else:
        _mk("P6 env=" + env[:300])
    H["P6"] = okP6

    # ---- P7: limit guards + re-import guard -----------------------------------------
    Document.Create()
    g1 = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "max_components": 2}' % fix3j)
    part = Window.ActiveWindow.Document.MainPart
    okP7 = ('"success": false' in g1 and 'max_components' in g1
            and len(list(part.Bodies)) == 0)                # loud abort, NOTHING built
    g2 = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "max_components": 10000000000}' % fix3j)
    okP7 = okP7 and '"success": true' in g2                 # 1e10 clamps, no overflow
    Document.Create()
    g3 = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.0}' % fixj)
    g4 = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.2}' % fixj)
    okP7 = okP7 and '"success": true' in g3 \
           and '"success": false' in g4 and 'already exists' in g4
    H["P7"] = okP7
    _mk("P7 guards -> %s | g1: %s | g2: %s | g4: %s" % (okP7, g1[:120], g2[:80], g4[:140]))

try:
    WriteBlock.ExecuteTask("g28", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["P1", "P2", "P3", "P4", "P5", "P6", "P7"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G28_PASS ALL=%s (%d/7)" % (allp, sum(1 for k in KEYS if H.get(k))))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8Encoding(False))
File.WriteAllText(DONE, "done\n", UTF8Encoding(False))
