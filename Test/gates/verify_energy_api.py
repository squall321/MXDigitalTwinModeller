# encoding: utf-8
# ============================================================================
# verify_energy_api.py
#
# GATE - "파트별 진동에너지 -> 상위 파트 자동 뷰" 기능의 전제를 실측한다.
# 구현 전에 이것부터 돌려서 어느 경로가 살아있는지 확정할 것.
#
# 검증 대상 (POSTPROCESS_IDEAS.md M10 / T4 관련):
#   G1  Solution.AddEnergyContribution()  - 해석 타입별 가용성
#   G2  EnergyContribution 프로퍼티 쓰기  - EnergyType / TopBodiesToDisplay /
#                                          ModeSelection / ShowTextOnMosaic
#   G3  Result.Total                      - body-scoped 총 에너지 (현행 버그의 수정 근거)
#   G4  EnergyContribution 숫자 회수      - Total / PlotData / TabularData / Export
#   G5  Result.AddFigure()                - 상위 파트 뷰 자동 추가
#   G6  모드/스텝별 평가                  - SetNumber / CreateResultsAtAllSets
#
# 사용법:
#   1. 이미 solve 된 모델을 Mechanical 에서 연다 (Modal / Harmonic / Transient 아무거나)
#   2. File -> Scripting -> Open Script -> 이 파일 -> Run
#   3. 콘솔 맨 아래 "ENERGY_GATE: ..." 한 줄이 판정.
#      상세는 solve 폴더의 energy_gate_result.json
#
# 안전: 생성한 result 객체는 전부 되돌린다(역순 Delete). 기존 트리는 건드리지 않는다.
#       단, 도중에 예외나 콘솔 Stop 으로 끊기면 [7] 정리가 돌지 않는다 -> 이름이 'GATE_' 로
#       시작하는 결과 객체를 수동으로 지울 것.
# ============================================================================

import json
import os

print("=" * 74)
print("MX GATE - Vibration Energy API (EnergyContribution / Result.Total / AddFigure)")
print("=" * 74)

PASS, FAIL, NA = [], [], []
CREATED = []          # 되돌릴 객체 (역순 Delete)
R = {}                # JSON 리포트


def check(name, cond, detail=""):
    if cond is True:
        PASS.append((name, detail))
        print("  [PASS] {0}{1}".format(name, " - " + detail if detail else ""))
    elif cond is False:
        FAIL.append((name, detail))
        print("  [FAIL] {0}{1}".format(name, " - " + detail if detail else ""))
    else:
        NA.append((name, detail))
        print("  [N/A ] {0}{1}".format(name, " - " + detail if detail else ""))


def safe(fn, *a, **kw):
    try:
        return True, fn(*a, **kw)
    except Exception as ex:
        return False, str(ex)[:160]


def num(obj, attr):
    """Quantity-like 속성 -> float. 실패하면 None (0.0 으로 뭉개지 않는다)."""
    try:
        v = getattr(obj, attr)
    except Exception:
        return None
    if v is None:
        return None
    try:
        return float(v)
    except Exception:
        pass
    try:
        return float(str(v).split()[0])
    except Exception:
        return None


def track(obj):
    if obj is not None:
        CREATED.append(obj)
    return obj


# ----------------------------------------------------------------------------
# [0] 컨텍스트 - 모델 / 해석 / solve 상태 / 바디
# ----------------------------------------------------------------------------
print("\n[0] Context")

try:
    _ = ExtAPI
except NameError:
    print("  [FAIL] ExtAPI 없음 - Mechanical 스크립팅 컨텍스트가 아니다.")
    raise SystemExit(1)

model = ExtAPI.DataModel.Project.Model
ok, ver = safe(lambda: str(ExtAPI.ExtensionManager.ProductVersion))
check("ProductVersion", ok, ver)
R['product_version'] = ver if ok else None

analyses = list(model.GetChildren(DataModelObjectCategory.Analysis, True))
check("model.Analyses", len(analyses) > 0, "{0} 개".format(len(analyses)))
if not analyses:
    print("\n  해석이 하나도 없다. solve 된 모델을 열고 다시 돌릴 것.")
    print("\nENERGY_GATE: BLOCKED - no analysis")
    raise SystemExit(0)

by_type = []
for a in analyses:
    try:
        atype = str(a.AnalysisType)
    except Exception:
        atype = "?"
    solved = False
    try:
        solved = (str(a.Solution.Status) == "Done")
    except Exception:
        pass
    by_type.append((a, atype, solved))
    print("      - {0:28s} type={1:14s} solved={2}".format(a.Name[:28], atype, solved))

R['analyses'] = [{'name': a.Name, 'type': t, 'solved': s} for a, t, s in by_type]

solved_list = [(a, t) for a, t, s in by_type if s]
check("solve 된 해석 존재", len(solved_list) > 0,
      "{0} 개".format(len(solved_list)) if solved_list else "없음 - 값 회수 항목은 전부 N/A")

bodies = list(model.Geometry.GetChildren(DataModelObjectCategory.Body, True))
target_body = None
for b in bodies:
    try:
        if b.Suppressed:
            continue
        target_body = b
        break
    except Exception:
        continue
check("scoping 대상 body", target_body is not None,
      target_body.Name if target_body is not None else "body 없음")
R['target_body'] = target_body.Name if target_body is not None else None

sel = None
if target_body is not None:
    ok, sel = safe(lambda: ExtAPI.SelectionManager.CreateSelectionInfo(
        SelectionTypeEnum.GeometryEntities))
    if ok:
        try:
            sel.Entities = [target_body.GetGeoBody()]
        except Exception as ex:
            check("sel.Entities = [GeoBody]", False, str(ex)[:120])
            sel = None
    else:
        sel = None


# ----------------------------------------------------------------------------
# [1] Enum 가용성
# ----------------------------------------------------------------------------
print("\n[1] Enums")

E = {}
for ename in ('EnergyContributionEnergyType', 'ModeSelectionMethod', 'ShowTextOnMosaicMode'):
    try:
        mod = __import__('Ansys.Mechanical.DataModel.Enums', globals(), locals(), [ename])
        E[ename] = getattr(mod, ename)
        members = [m for m in dir(E[ename]) if not m.startswith('_')]
        check("enum " + ename, True, ", ".join(sorted(members)[:6]))
    except Exception as ex:
        E[ename] = None
        check("enum " + ename, False, str(ex)[:120])
R['enums'] = dict((k, v is not None) for k, v in E.items())


# ----------------------------------------------------------------------------
# [2] G3 - Result.Total (현행 버그의 수정 근거)
#     main.py:2143 은 ElementalStrainEnergy 에 MaximumOfMaximumOverTime 을 쓴다.
#     그건 "그 바디에서 가장 뜨거운 요소 1개" 값이지 바디 총합이 아니다.
#     .Total 이 살아있으면 한 단어 교체로 해결된다. 두 값을 나란히 찍어 증명한다.
# ----------------------------------------------------------------------------
print("\n[2] G3 - Result.Total vs MaximumOfMaximumOverTime (bug proof)")

R['G3'] = {}
if not solved_list or sel is None:
    check("G3", None, "solve 된 해석 또는 body scoping 불가 - skip")
else:
    a0, t0 = solved_list[0]
    sol0 = a0.Solution
    ok, se = safe(lambda: sol0.AddElementalStrainEnergy())
    check("Solution.AddElementalStrainEnergy()", ok, "" if ok else se)
    if ok:
        se = track(se)
        se.Name = "GATE_SE_probe"
        ok2, err = safe(lambda: setattr(se, 'Location', sel))
        check("se.Location = <body>", ok2, "" if ok2 else err)

        ok3, err = safe(lambda: se.EvaluateAllResults())
        check("se.EvaluateAllResults()", ok3, "" if ok3 else err)

        v_total = num(se, 'Total')
        v_max = num(se, 'Maximum')
        v_mmot = num(se, 'MaximumOfMaximumOverTime')
        v_avg = num(se, 'Average')

        check("se.Total 읽힘", v_total is not None,
              repr(v_total) if v_total is not None else "None - .Total 경로 불가")
        check("se.MaximumOfMaximumOverTime 읽힘", v_mmot is not None, repr(v_mmot))
        if v_total is not None and not ok2:
            # Location 이 안 먹었으면 전체모델 값이다 - "body-scoped Total" 의 증거로 쓰지 않는다
            check("G3 scoping 실효", False, "se.Location 실패 -> 이 Total 은 body 값이 아님 (증거 제외)")

        R['G3'] = {'analysis': a0.Name, 'type': t0, 'body': R['target_body'],
                   'scoped': bool(ok2),
                   'Total': v_total, 'Maximum': v_max,
                   'MaximumOfMaximumOverTime': v_mmot, 'Average': v_avg}

        if v_total is not None and v_mmot not in (None, 0.0):
            ratio = v_total / v_mmot
            R['G3']['total_over_max_ratio'] = ratio
            # 요소가 여럿이면 총합은 최대 요소값보다 유의하게 커야 정상.
            check("Total >> MaxOfMaxOverTime (버그 실증)", ratio > 1.5,
                  "Total/Max = {0:.1f}x -> 현행 strain_energy 는 총합이 아니다".format(ratio)
                  if ratio > 1.5 else
                  "ratio={0:.3f} - 단일요소 스코핑이거나 .Total 의미가 다르다. 확인 필요".format(ratio))


# ----------------------------------------------------------------------------
# [3] G5 - Result.AddFigure()  (상위 파트 뷰 자동 추가)
# ----------------------------------------------------------------------------
print("\n[3] G5 - Result.AddFigure()")

R['G5'] = {}
if not solved_list or sel is None:
    check("G5", None, "skip")
else:
    a0, _t = solved_list[0]
    ok, td = safe(lambda: a0.Solution.AddTotalDeformation())
    if ok:
        td = track(td)
        td.Name = "GATE_TD_probe"
        safe(lambda: setattr(td, 'Location', sel))
        safe(lambda: td.EvaluateAllResults())
        ok2, fig = safe(lambda: td.AddFigure())
        check("TotalDeformation.AddFigure()", ok2, "" if ok2 else fig)
        R['G5']['AddFigure'] = bool(ok2)
        if ok2:
            ok3, figs = safe(lambda: list(td.Figures))
            check("result.Figures 열거", ok3, "{0} 개".format(len(figs)) if ok3 else figs)
            R['G5']['Figures_count'] = len(figs) if ok3 else None
    else:
        check("Solution.AddTotalDeformation()", False, td)


# ----------------------------------------------------------------------------
# [4] G1 / G2 - EnergyContribution: 해석 타입별 가용성 + 프로퍼티 쓰기
# ----------------------------------------------------------------------------
print("\n[4] G1/G2 - EnergyContribution")

R['G1'] = {}
R['G2'] = {}
ec_ok_any = False
ec_obj = None
ec_analysis = None

for a, atype, solved in by_type:
    ok, ec = safe(lambda: a.Solution.AddEnergyContribution())
    check("AddEnergyContribution() on [{0}] {1}".format(atype, a.Name[:22]), ok,
          "" if ok else ec)
    R['G1'][atype] = bool(ok)
    if ok:
        track(ec)
        ec_ok_any = True
        if ec_obj is None or (solved and ec_analysis is not None
                              and str(ec_analysis.Solution.Status) != "Done"):
            ec_obj, ec_analysis = ec, a

if ec_obj is not None:
    safe(lambda: setattr(ec_obj, 'Name', "GATE_EC_probe"))

    et = E.get('EnergyContributionEnergyType')
    if et is not None:
        for mname in ('StrainEnergy', 'KineticEnergy', 'TotalEnergy'):
            try:
                val = getattr(et, mname)
            except Exception:
                check("  EnergyType = " + mname, False, "enum 멤버 없음")
                R['G2']['EnergyType_' + mname] = False
                continue
            ok, err = safe(lambda: setattr(ec_obj, 'EnergyType', val))
            check("  EnergyType = " + mname, ok, "" if ok else err)
            R['G2']['EnergyType_' + mname] = bool(ok)
    else:
        check("  EnergyType", None, "enum 미가용")

    ok, err = safe(lambda: setattr(ec_obj, 'TopBodiesToDisplay', 5))
    check("  TopBodiesToDisplay = 5", ok, "" if ok else err)
    R['G2']['TopBodiesToDisplay'] = bool(ok)
    if ok:
        R['G2']['TopBodiesToDisplay_readback'] = num(ec_obj, 'TopBodiesToDisplay')

    msm = E.get('ModeSelectionMethod')
    if msm is not None:
        for mname in ('ModalEffectiveMass', 'None'):
            try:
                val = getattr(msm, mname)
            except Exception:
                continue
            ok, err = safe(lambda: setattr(ec_obj, 'ModeSelection', val))
            check("  ModeSelection = " + mname, ok, "" if ok else err)
            R['G2']['ModeSelection_' + mname] = bool(ok)
    else:
        check("  ModeSelection", None, "enum 미가용")

    stm = E.get('ShowTextOnMosaicMode')
    if stm is not None:
        ok, err = safe(lambda: setattr(ec_obj, 'ShowTextOnMosaic', stm.YesRefined))
        check("  ShowTextOnMosaic = YesRefined", ok, "" if ok else err)
        R['G2']['ShowTextOnMosaic'] = bool(ok)

    ok, err = safe(lambda: setattr(ec_obj, 'MosaicChartLegendMax', 100.0))
    check("  MosaicChartLegendMax = 100", ok, "" if ok else err)
    R['G2']['MosaicChartLegendMax'] = bool(ok)
else:
    check("EnergyContribution 생성", False, "어떤 해석 타입에서도 실패")


# ----------------------------------------------------------------------------
# [5] G4 - EnergyContribution 숫자 회수
#     mosaic 차트 결과라 Result 베이스의 회수 경로가 살아있는지가 핵심.
#     하나라도 되면 파트별 수치 랭킹을 ACT 단독으로 뽑을 수 있다.
# ----------------------------------------------------------------------------
print("\n[5] G4 - EnergyContribution numeric readback")

R['G4'] = {}
_ec_solved = False
if ec_analysis is not None:
    try:
        _ec_solved = (str(ec_analysis.Solution.Status) == "Done")
    except Exception:
        _ec_solved = False

if ec_obj is None:
    check("G4", None, "EnergyContribution 없음 - skip")
elif not _ec_solved:
    check("G4", None, "해당 해석이 solve 안 됨 - skip")
else:
    ok, err = safe(lambda: ec_obj.EvaluateAllResults())
    check("ec.EvaluateAllResults()", ok, "" if ok else err)

    for attr in ('Total', 'Maximum', 'Minimum', 'Average'):
        v = num(ec_obj, attr)
        check("  ec.{0}".format(attr), v is not None, repr(v))
        R['G4'][attr] = v

    ok, pd = safe(lambda: ec_obj.PlotData)
    check("  ec.PlotData", ok, str(type(pd).__name__) if ok else pd)
    R['G4']['PlotData'] = bool(ok)
    if ok and pd is not None:
        ok2, ids = safe(lambda: list(pd.Identifiers))
        check("    PlotData.Identifiers", ok2,
              ", ".join([str(i) for i in ids][:8]) if ok2 else ids)
        R['G4']['PlotData_identifiers'] = [str(i) for i in ids] if ok2 else None

    ok, tb = safe(lambda: ec_obj.TabularData)
    check("  ec.TabularData", ok, str(type(tb).__name__) if ok else tb)
    R['G4']['TabularData'] = bool(ok)

    try:
        out_dir = ec_analysis.WorkingDir
    except Exception:
        out_dir = os.environ.get('TEMP', '.')
    ec_txt = os.path.join(out_dir, "gate_energy_contribution.txt")
    ok, err = safe(lambda: ec_obj.ExportToTextFile(ec_txt))
    check("  ec.ExportToTextFile(...)", ok, ec_txt if ok else err)
    R['G4']['ExportToTextFile'] = bool(ok)
    if ok and os.path.exists(ec_txt):
        try:
            f = open(ec_txt, 'r')
            try:
                head = [f.readline().rstrip('\n') for _ in range(6)]
            finally:
                f.close()
            print("      파일 앞부분:")
            for ln in head:
                if ln:
                    print("        | " + ln[:100])
            R['G4']['export_head'] = [h for h in head if h]
        except Exception as ex:
            print("      (파일 읽기 실패: {0})".format(str(ex)[:80]))


# ----------------------------------------------------------------------------
# [6] G6 - 모드/스텝별 평가 (모달은 모드마다 따로 봐야 의미가 있다)
# ----------------------------------------------------------------------------
print("\n[6] G6 - per-mode / per-set evaluation")

R['G6'] = {}
modal = [(a, t) for a, t, s in by_type if 'Modal' in t and s]
if not modal:
    check("G6", None, "solve 된 Modal 해석 없음 - skip")
elif sel is None:
    check("G6", None, "body scoping 불가 - skip")
else:
    am, _tm = modal[0]
    ok, se2 = safe(lambda: am.Solution.AddElementalStrainEnergy())
    if ok:
        se2 = track(se2)
        se2.Name = "GATE_SE_modal"
        safe(lambda: setattr(se2, 'Location', sel))

        ok2, err = safe(lambda: setattr(se2, 'SetNumber', 1))
        check("se.SetNumber = 1 (모드 1)", ok2, "" if ok2 else err)
        R['G6']['SetNumber'] = bool(ok2)

        if ok2:
            per_mode = []
            for mode in (1, 2, 3):
                try:
                    se2.SetNumber = mode
                    se2.EvaluateAllResults()
                    per_mode.append((mode, num(se2, 'Total')))
                except Exception:
                    per_mode.append((mode, None))
            got = [v for _m, v in per_mode if v is not None]
            check("모드별 Total 회수", len(got) >= 2,
                  ", ".join(["m{0}={1}".format(m, ("%.4g" % v) if v is not None else "-")
                             for m, v in per_mode]))
            R['G6']['per_mode_total'] = dict((str(m), v) for m, v in per_mode)
            if len(got) >= 2:
                allsame = all(abs(g - got[0]) < 1e-12 for g in got)
                check("모드별 값이 서로 다름 (SetNumber 실효)", not allsame,
                      "동일값 - SetNumber 가 평가에 반영되지 않는다" if allsame else "OK")
                R['G6']['setnumber_effective'] = not allsame

        # CreateResultsAtAllSets 는 결과를 대량 생성한다. 존재 여부만 확인하고
        # 생성분은 정리 목록에 넣는다.
        # 주의: Children 열거마다 ACT 가 새 래퍼 객체를 줄 수 있어 id() 로 diff 하면 기존
        # 사용자 결과까지 "새로 생긴 것"으로 오인해 지워 버린다. 트리 객체의 안정 키
        # (ObjectId, 없으면 Name) 로 diff 하고, 그래도 불확실하면 아무것도 지우지 않는다.
        def _tree_key(o):
            try:
                return ('oid', int(o.ObjectId))
            except Exception:
                pass
            try:
                return ('name', str(o.Name))
            except Exception:
                return None
        before = set()
        before_ok = False
        try:
            before = set([_tree_key(c) for c in am.Solution.Children])
            before_ok = (None not in before)
        except Exception:
            before_ok = False
        ok3, err = safe(lambda: se2.CreateResultsAtAllSets())
        check("se.CreateResultsAtAllSets()", ok3, "" if ok3 else err)
        R['G6']['CreateResultsAtAllSets'] = bool(ok3)
        if ok3:
            if not before_ok:
                check("CreateResultsAtAllSets 생성분 추적", None,
                      "안정 키를 못 얻어 자동 정리 생략 - 'GATE_SE_modal*' 결과를 수동으로 지울 것")
            else:
                try:
                    se2_key = _tree_key(se2)
                    for ch in list(am.Solution.Children):
                        k = _tree_key(ch)
                        if k is not None and k not in before and k != se2_key:
                            CREATED.append(ch)
                except Exception:
                    pass


# ----------------------------------------------------------------------------
# 정리 + 판정
# ----------------------------------------------------------------------------
print("\n[7] Cleanup")
removed, kept = 0, 0
for obj in reversed(CREATED):
    try:
        obj.Delete()
        removed += 1
    except Exception:
        kept += 1
check("생성 객체 정리", kept == 0, "{0} 삭제, {1} 잔존".format(removed, kept))

print("\n" + "=" * 74)
print("SUMMARY   PASS={0}  FAIL={1}  N/A={2}".format(len(PASS), len(FAIL), len(NA)))
print("=" * 74)
if FAIL:
    print("FAILED:")
    for n, d in FAIL:
        print("  x {0}{1}".format(n, " - " + d if d else ""))
if NA:
    print("N/A:")
    for n, d in NA:
        print("  - {0}{1}".format(n, " - " + d if d else ""))

# --- 판정 입력 -----------------------------------------------------------
# OK 는 main.py EnergyDialog 가 실제로 의존하는 전제가 전부 성립할 때만 준다:
#   Result.Total 회수 / AddEnergyContribution 생성 + 프로퍼티 쓰기 / AddFigure /
#   (solve 된 Modal 이 있으면) SetNumber 가 평가에 실제로 반영됨.
# EnergyContribution 숫자 회수(G4)는 KE 를 숫자로 줄 수 있느냐의 문제라 OK 조건이 아니다
# (막히면 KE 는 차트 전용으로 degrade - 설계된 동작).
_g3 = R.get('G3', {})
have_total = (_g3.get('Total') is not None) and bool(_g3.get('scoped'))
have_ec = ec_ok_any
_g2 = R.get('G2', {})
have_ec_props = bool(_g2.get('EnergyType_StrainEnergy')) and bool(_g2.get('TopBodiesToDisplay'))
have_figure = R.get('G5', {}).get('AddFigure') is True
have_ec_num = False
for k in ('Total', 'PlotData', 'TabularData', 'ExportToTextFile'):
    if R.get('G4', {}).get(k):
        have_ec_num = True
_g6 = R.get('G6', {})
modal_checked = ('setnumber_effective' in _g6)
modal_ok = (_g6.get('setnumber_effective') is True)

R['verdict_inputs'] = {'Result_Total': have_total, 'EnergyContribution': have_ec,
                       'EC_props_settable': have_ec_props, 'AddFigure': have_figure,
                       'EC_numeric_readback': have_ec_num,
                       'Modal_SetNumber_effective': (modal_ok if modal_checked else None)}

problems = []
if not have_ec:
    problems.append("AddEnergyContribution 생성 불가 -> 모자이크 없이 .Total 랭킹만")
elif not have_ec_props:
    problems.append("EnergyContribution 프로퍼티 쓰기 불가(EnergyType/TopBodiesToDisplay)")
if not have_figure:
    problems.append("AddFigure 불가 -> 뷰는 수동/DPF")
if modal_checked and not modal_ok:
    problems.append("Modal SetNumber 가 평가에 반영 안 됨(G6) -> 모드별 에너지 불가")

if not have_total:
    verdict = "BLOCKED"
    msg = "Result.Total 회수 불가 -> DPF 또는 APDL(ETABLE,SENE + SSUM) 경로로 전환 필요"
elif not problems:
    verdict = "OK"
    msg = "전 경로 확보 - 계획 2~5 그대로 구현 가능"
else:
    verdict = "PARTIAL"
    msg = "미확보: " + " / ".join(problems)
if not have_ec_num:
    msg += "  [KE 숫자 회수 불가 -> 운동에너지는 차트 전용]"

R['verdict'] = verdict
R['verdict_msg'] = msg
R['pass'] = len(PASS)
R['fail'] = len(FAIL)
R['na'] = len(NA)

try:
    _od = analyses[0].WorkingDir
except Exception:
    _od = os.environ.get('TEMP', '.')
_jp = os.path.join(_od, "energy_gate_result.json")
try:
    _f = open(_jp, 'w')
    try:
        json.dump(R, _f, indent=2)
    finally:
        _f.close()
    print("\n리포트: {0}".format(_jp))
except Exception as ex:
    print("\n(리포트 쓰기 실패: {0})".format(str(ex)[:100]))

print("\nENERGY_GATE: {0} - {1}".format(verdict, msg))
