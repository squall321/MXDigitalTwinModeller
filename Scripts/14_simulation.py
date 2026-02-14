# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 14. Simulation Setup (시뮬레이션 설정)
# SpaceClaim Script Editor에서 실행
# LS-DYNA 모달 해석 키워드 파일 생성
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# ── 일반 설정 ──
TITLE = "Modal_Analysis"

# ── 고유값 설정 (Eigenvalue) ──
NUM_MODES = 10             # 모드 수
MIN_FREQUENCY = 0          # 최소 주파수 (Hz, 0=제한 없음)
MAX_FREQUENCY = 0          # 최대 주파수 (Hz, 0=제한 없음)

# 고유값 방법: 2=Lanczos, 3=InversePower, 101=BCSLIB
EIGENVALUE_METHOD = 2

# ── 솔버 설정 ──
# 솔버 타입: 2=Multi-frontal Sparse, 4=PARDISO, 6=MUMPS
SOLVER_TYPE = 2

# 자동 SPC (단점 자동 구속)
AUTO_SPC = True

# 음수 고유값 처리: 0=중단, 1=경고, 2=허용
NEGATIVE_EIGENVALUE = 2

# ── Implicit 설정 ──
GEOMETRIC_STIFFNESS = False   # 기하 강성 (IGS)
IMPLICIT_FORMULATION = 2      # 2=순수 고유값, 12=정적+고유값

# ── 출력 설정 ──
OUTPUT_EIGOUT = True       # d3eigv 파일 출력
OUTPUT_D3PLOT = True       # d3plot 파일 출력
OUTPUT_NODOUT = False      # nodout 파일 출력
OUTPUT_ELOUT = False       # elout 파일 출력

# ── 추가 제어 ──
CONTROL_ENERGY = True      # 에너지 제어
CONTROL_HOURGLASS = True   # Hourglass 제어
CONTROL_ACCURACY = True    # 정확도 제어
HOURGLASS_TYPE = 6         # IHQ (Hourglass 타입)
HOURGLASS_COEFF = 0.1      # QH (Hourglass 계수)

# ── 출력 파일 경로 (선택사항) ──
# 빈값이면 키워드만 콘솔에 출력, 경로 있으면 파일로 저장
OUTPUT_PATH = ""
# OUTPUT_PATH = r"C:\Users\Sonic\Desktop\modal.k"

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from System.IO import File
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simulation import (
    SimulationParameters, SimulationType)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simulation import SimulationKeywordService

def main():
    p = SimulationParameters()
    p.Title = TITLE
    p.Type = SimulationType.ModalAnalysis
    p.NumModes = NUM_MODES
    p.MinFrequency = MIN_FREQUENCY
    p.MaxFrequency = MAX_FREQUENCY
    p.EigenvalueMethod = EIGENVALUE_METHOD
    p.SolverType = SOLVER_TYPE
    p.AutoSPC = AUTO_SPC
    p.NegativeEigenvalue = NEGATIVE_EIGENVALUE
    p.GeometricStiffness = GEOMETRIC_STIFFNESS
    p.ImplicitFormulation = IMPLICIT_FORMULATION
    p.OutputEigout = OUTPUT_EIGOUT
    p.OutputD3plot = OUTPUT_D3PLOT
    p.OutputNodeout = OUTPUT_NODOUT
    p.OutputElout = OUTPUT_ELOUT
    p.ControlEnergy = CONTROL_ENERGY
    p.ControlHourglass = CONTROL_HOURGLASS
    p.ControlAccuracy = CONTROL_ACCURACY
    p.HourglassType = HOURGLASS_TYPE
    p.HourglassCoeff = HOURGLASS_COEFF

    print("[Simulation] 모달 해석 키워드 생성")
    print("[Simulation] 모드: %d, 솔버: %d, 방법: %d" % (
        NUM_MODES, SOLVER_TYPE, EIGENVALUE_METHOD))

    # 키워드 생성
    keywords = SimulationKeywordService.GenerateKeywords(p)

    if not keywords:
        print("[Simulation] 키워드 생성 실패")
        return

    lines = keywords.split('\n')
    print("[Simulation] %d줄 키워드 생성" % len(lines))

    # 파일 저장 또는 콘솔 출력
    if OUTPUT_PATH:
        File.WriteAllText(OUTPUT_PATH, keywords)
        print("[Simulation] 저장: %s" % OUTPUT_PATH)
    else:
        print("[Simulation] 키워드 미리보기 (처음 30줄):")
        for line in lines[:30]:
            print("  %s" % line)
        if len(lines) > 30:
            print("  ... (%d줄 더)" % (len(lines) - 30))

    # 현재 시뮬레이션 설정으로 저장
    SimulationKeywordService.Current = p
    print("[Simulation] 완료")

main()
