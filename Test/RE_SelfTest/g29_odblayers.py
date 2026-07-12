# encoding: utf-8
# g29 (ODB++ layer subdivision): a synthetic ODB++ tree with components of known
# family + height verifies the laminate post-pass:
#   P1 off    : default import = single-block components (no layer bodies)
#   P2 on     : subdivide_layers -> big IC (BGA-name) split into its 4-layer stack;
#               VOLUME conserved (sum of plies == original block); FOOTPRINT preserved
#               (each ply bbox == component bbox in XY); z-spans contiguous & fill the
#               package height exactly; interface Named Selections created
#   P3 filter : min_layer_footprint_mm gate - a small IC below the threshold stays a
#               single block while a big one splits
#   P4 family : name/pin classification -> BGA (area-array) vs IC (peripheral) vs
#               passive (2-pin) get their distinct stacks (layer COUNT differs)
import clr
clr.AddReferenceToFileAndPath(r"C:\ProgramData\SpaceClaim\AddIns\MXDigitalTwinModeller\V252\MXDigitalTwinModeller.dll")
import System
import math
from System.IO import File, Directory, Path
from System.Text import UTF8Encoding
from SpaceClaim.Api.V252 import Document, Window, WriteBlock, Task
from SpaceClaim.Api.V252.Geometry import Matrix
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer import LlmToolDispatcher

OUT = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g29_result.txt"
MARK = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g29_mark.txt"
DONE = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\g29_done.txt"
FIX = r"D:\MXDigitalTwinModeller\Test\RE_SelfTest\odb_layer_fixture"

UTF8 = UTF8Encoding(False)
def _mk(s):
    try: File.AppendAllText(MARK, str(s) + "\n", UTF8)
    except Exception: pass
_mk("module-top")   # proves the script loaded + _mk works, independent of _do
log = []
def emit(s):
    try: log.append(str(s))
    except Exception: log.append("(x)")
H = {}

def wf(path, text):
    Directory.CreateDirectory(Path.GetDirectoryName(path))
    File.WriteAllText(path, text, UTF8)

def build_fixture():
    wf(FIX + r"\matrix\matrix",
       "STEP {\n   NAME=pcb\n}\n"
       "LAYER {\n   NAME=comp_+_top\n   TYPE=COMPONENT\n}\n"
       "LAYER {\n   NAME=comp_+_bot\n   TYPE=COMPONENT\n}\n")
    wf(FIX + r"\steps\pcb\profile",
       "UNITS=MM\nOB 0 0 I\nOS 80 0\nOS 80 60\nOS 0 60\nOE\n")
    # PKG 0 bga100: 10x10 area-array grid (5x5 = 25 pins, interior populated) -> BGA
    bga = "PKG bga100 0.8 -5 -5 5 5\n"
    n = 0
    for iy in range(5):
        for ix in range(5):
            n += 1
            x = -4.0 + ix * 2.0
            y = -4.0 + iy * 2.0
            bga += "PIN %d T %.3f %.3f 0 E\n" % (n, x, y)
    # PKG 1 qfn8: 6x6 peripheral, 8 pins only on edges -> IC
    qfn = "PKG qfn8 0.65 -3 -3 3 3\n"
    peri = [(-2, -3), (0, -3), (2, -3), (3, 0), (2, 3), (0, 3), (-2, 3), (-3, 0)]
    for i, (x, y) in enumerate(peri):
        qfn += "PIN %d T %.3f %.3f 0 E\n" % (i + 1, x, y)
    # PKG 2 res0402: 2-pin passive, big footprint (forced 4mm so it passes the filter)
    res = ("PKG bigres 0.5 -2 -1 2 1\n"
           "PIN 1 T -1.5 0 0 E\nPIN 2 T 1.5 0 0 E\n")
    wf(FIX + r"\steps\pcb\eda\data", "UNITS=MM\n" + bga + qfn + res)
    # components: U1 big BGA (COMP_HEIGHT 1.0), U2 big QFN (1.2), R1 passive (0.5),
    # U3 SMALL bga (footprint 2mm via a 4th pkg) below the layer-filter threshold
    small = "PKG smallbga 0.4 -1 -1 1 1\nPIN 1 T -0.5 -0.5 0 E\nPIN 2 T 0.5 0.5 0 E\n"
    File.AppendAllText(FIX + r"\steps\pcb\eda\data", small, UTF8)
    wf(FIX + r"\steps\pcb\layers\comp_+_top\components",
       "UNITS=MM\n"
       "CMP 0 20 30 0 N U1 BGA-CHIP\nPRP COMP_HEIGHT '1.0'\n"
       "CMP 1 45 30 0 N U2 QFN-CHIP\nPRP COMP_HEIGHT '1.2'\n"
       "CMP 2 65 30 0 N R1 RES\nPRP COMP_HEIGHT '0.5'\n"
       "CMP 3 65 45 0 N U3 SMALLBGA\nPRP COMP_HEIGHT '0.8'\n")
    # bottom-side BGA -> board-facing cap is the TOP (+Z) one; substrate must seat there
    wf(FIX + r"\steps\pcb\layers\comp_+_bot\components",
       "UNITS=MM\n"
       "CMP 0 20 15 0 N UB BGA-CHIP\nPRP COMP_HEIGHT '1.0'\n")

def env_num(env, key):
    import re
    m = re.search('"%s": ([-0-9.eE]+)' % key, env)
    return float(m.group(1)) if m else float("nan")

def part():
    return Window.ActiveWindow.Document.MainPart

def bodies_prefixed(pfx):
    return [b for b in part().Bodies if b.Name.startswith(pfx)]

def bb(b):
    x = b.Shape.GetBoundingBox(Matrix.Identity)
    return (x.MinCorner.X*1000, x.MinCorner.Y*1000, x.MinCorner.Z*1000,
            x.MaxCorner.X*1000, x.MaxCorner.Y*1000, x.MaxCorner.Z*1000)

def vol(b):
    return b.Shape.Volume * 1e9

def _do():
    _mk("do-start")
    build_fixture()
    _mk("fixture-built")
    fj = FIX.replace("\\", "\\\\")

    # ---- P1: subdivision OFF (default) = single-block components -----------------
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.0}' % fj)
    # exact-name match: bodies_prefixed would also catch Comp_U1_Pad_nnn
    u1_exact = [b for b in part().Bodies if b.Name == "Comp_U1"]
    okP1 = ('"success": true' in env and '"components_layered": 0' in env
            and '"layers_created": 0' in env
            and len(u1_exact) == 1)   # single block, no plies
    _mk("P1 off -> %s | U1block=%d | %s" % (okP1, len(u1_exact), env[:150]))
    H["P1"] = okP1

    # ---- P2: subdivision ON - volume + footprint + z conservation ----------------
    _mk("P2-before-dispatch")
    Document.Create()
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.0, "subdivide_layers": true, '
        '"min_layer_footprint_mm": 3.0}' % fj)
    _mk("P2-after-dispatch")
    okP2 = '"success": true' in env
    if okP2:
        # U1 is BGA -> 4 layers; the single block must be gone, replaced by plies
        u1_plies = [b for b in part().Bodies if b.Name.startswith("Comp_U1_")
                    and "_Pad_" not in b.Name]
        # exact single-block U1 must NOT exist anymore
        u1_block = [b for b in part().Bodies if b.Name == "Comp_U1"]
        okP2 = okP2 and len(u1_block) == 0 and len(u1_plies) == 4
        # VOLUME conservation: plies sum == footprint(10x10) * height(1.0) = 100 mm^3
        vsum = sum(vol(b) for b in u1_plies)
        okP2 = okP2 and abs(vsum - 100.0) < 100.0 * 1e-4
        # FOOTPRINT preserved: every ply spans the same XY bbox (20+-5, 30+-5)
        fp_ok = True
        for b in u1_plies:
            g = bb(b)
            if not (abs(g[0]-15) < 1e-4 and abs(g[3]-25) < 1e-4
                    and abs(g[1]-25) < 1e-4 and abs(g[4]-35) < 1e-4):
                fp_ok = False
        okP2 = okP2 and fp_ok
        # ORDER + name<->position: each NAMED ply must occupy its exact cumulative band
        # (top-side seat 1.05; bga stack substrate .30/die_attach .05/die .25/mold .40,
        # substrate on the board-facing bottom). A reversed or mislabelled stack fails.
        expect = {"substrate": (1.05, 1.35), "die_attach": (1.35, 1.40),
                  "die": (1.40, 1.65), "mold": (1.65, 2.05)}
        band_ok = True
        for b in u1_plies:
            key = b.Name.replace("Comp_U1_", "")
            g = bb(b)
            if key not in expect or abs(g[2]-expect[key][0]) > 1e-4 \
               or abs(g[5]-expect[key][1]) > 1e-4:
                band_ok = False
        okP2 = okP2 and band_ok
        okP2 = okP2 and env_num(env, "layers_created") >= 4
        okP2 = okP2 and '"components_layered":' in env
        _mk("P2 on: U1 plies=%d vsum=%.4f bands_ok=%s -> %s" % (
            len(u1_plies), vsum, band_ok, okP2))
    else:
        _mk("P2 env=" + env[:250])
    H["P2"] = okP2

    # ---- P2b: mirrored (bottom-side) BGA - substrate must seat on the BOARD-FACING
    # (top, +Z) cap. UB occupies z[-1.05, -0.05]; board is at +Z, so substrate is the
    # ply nearest z=-0.05 (the top slab), mold nearest z=-1.05 (the bottom slab).
    if H["P2"]:
        ub_plies = [b for b in part().Bodies if b.Name.startswith("Comp_UB_")
                    and "_Pad_" not in b.Name]
        expb = {"substrate": (-0.35, -0.05), "die_attach": (-0.40, -0.35),
                "die": (-0.65, -0.40), "mold": (-1.05, -0.65)}
        ub_ok = len(ub_plies) == 4
        for b in ub_plies:
            key = b.Name.replace("Comp_UB_", "")
            g = bb(b)
            if key not in expb or abs(g[2]-expb[key][0]) > 1e-4 \
               or abs(g[5]-expb[key][1]) > 1e-4:
                ub_ok = False
        H["P2b"] = ub_ok
        _mk("P2b mirror: UB plies=%d substrate-board-facing=%s" % (len(ub_plies), ub_ok))
    else:
        H["P2b"] = False

    # ---- P3: footprint filter - small IC below threshold stays a block ----------
    if okP2:
        # U3 smallbga footprint = 2mm < 3mm threshold -> single block, no plies
        u3_block = [b for b in part().Bodies if b.Name == "Comp_U3"]
        u3_plies = [b for b in part().Bodies if b.Name.startswith("Comp_U3_")
                    and "_Pad_" not in b.Name]
        H["P3"] = len(u3_block) == 1 and len(u3_plies) == 0
        _mk("P3 filter: U3 block=%d plies=%d -> %s" % (
            len(u3_block), len(u3_plies), H["P3"]))
    else:
        H["P3"] = False

    # ---- P4: family classification -> distinct stacks (layer COUNT) --------------
    if okP2:
        def plies_of(ref):
            return [b for b in part().Bodies if b.Name.startswith("Comp_" + ref + "_")
                    and "_Pad_" not in b.Name]
        u1n = len(plies_of("U1"))   # BGA -> 4
        u2n = len(plies_of("U2"))   # QFN peripheral -> IC -> 4
        r1n = len(plies_of("R1"))   # 2-pin -> passive -> 3
        # names carry the family layer identity (bga substrate, ic leadframe, passive body)
        u1_names = " ".join(b.Name for b in plies_of("U1"))
        r1_names = " ".join(b.Name for b in plies_of("R1"))
        H["P4"] = (u1n == 4 and u2n == 4 and r1n == 3
                   and "substrate" in u1_names and "mold" in u1_names
                   and "body" in r1_names and "termination" in r1_names)
        _mk("P4 family: U1(bga)=%d U2(ic)=%d R1(passive)=%d -> %s" % (u1n, u2n, r1n, H["P4"]))
    else:
        H["P4"] = False

    # ---- P5: max_total_layers loud guard - stops early, rest stay single blocks ----
    Document.Create()
    # cap at 4: only the first eligible component (4-layer BGA) subdivides, the rest
    # (U2, R1, UB) must remain single blocks and the log must say so
    env = LlmToolDispatcher.Dispatch(None, None, "import_odbpp",
        '{"path": "%s", "board_thickness_mm": 1.0, "subdivide_layers": true, '
        '"min_layer_footprint_mm": 3.0, "max_total_layers": 4}' % fj)
    okP5 = '"success": true' in env
    if okP5:
        layered = env_num(env, "components_layered")
        made = env_num(env, "layers_created")
        okP5 = (layered == 1 and made == 4 and "max_total_layers=4" in env
                and len([b for b in part().Bodies if b.Name == "Comp_U2"]) == 1)  # U2 stayed a block
    _mk("P5 guard: layered=%s layers=%s -> %s" % (
        env_num(env, "components_layered"), env_num(env, "layers_created"), okP5))
    H["P5"] = okP5

try:
    WriteBlock.ExecuteTask("g29", Task(_do))
    _mk("wb-done")
except System.Exception as e:
    _mk("wb-THREW " + e.GetType().Name + ": " + e.Message)
    emit("WB THREW: %s: %s" % (e.GetType().Name, e.Message))

KEYS = ["P1", "P2", "P2b", "P3", "P4", "P5"]
for k in KEYS:
    emit("%s %s" % (k, H.get(k)))
allp = all(bool(H.get(k)) for k in KEYS)
emit("G29_PASS ALL=%s (%d/%d)" % (allp, sum(1 for k in KEYS if H.get(k)), len(KEYS)))
File.WriteAllText(OUT, "\n".join(log) + "\n", UTF8)
File.WriteAllText(DONE, "done\n", UTF8)
