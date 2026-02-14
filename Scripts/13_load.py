# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 13. Load Definition (하중 정의)
# SpaceClaim Script Editor에서 실행
# Expression, Tabular, PWM 3가지 입력 모드 지원
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 하중 이름
LOAD_NAME = "Load_1"

# 그룹 이름
GROUP_NAME = "Default"

# 입력 모드: "Expression", "Tabular", "Pwm"
INPUT_MODE = "Expression"

# ── 시간 설정 ──
END_TIME = 1.0            # 종료 시간 (초)
DELTA_TIME = 0.001        # 시간 간격 (초)

# ┌───────────────────────────────────────┐
# │  Expression 모드                     │
# └───────────────────────────────────────┘

# 수학 표현식 (변수: t)
# 지원 함수: sin, cos, tan, sqrt, abs, exp, log, pow
# 지원 파형: square(t,freq), saw(t,freq), tri(t,freq), pulse(t,freq,duty), step(t,t0)
# 상수: pi, e
EXPRESSION = "1000 * sin(2 * pi * 50 * t)"

# ┌───────────────────────────────────────┐
# │  Tabular 모드                        │
# └───────────────────────────────────────┘

# 시간-진폭 테이블 (시간, 진폭) 리스트
TABULAR_DATA = [
    (0.0, 0.0),
    (0.1, 500.0),
    (0.2, 1000.0),
    (0.3, 500.0),
    (0.4, 0.0),
    (0.5, -500.0),
    (0.6, -1000.0),
    (0.7, -500.0),
    (0.8, 0.0),
    (0.9, 500.0),
    (1.0, 1000.0),
]

# ┌───────────────────────────────────────┐
# │  PWM 모드                            │
# └───────────────────────────────────────┘

PWM_CARRIER_FREQ = 10000  # 캐리어 주파수 (Hz)
PWM_OUTPUT_AMP = 1000     # 출력 진폭
PWM_BIPOLAR = True        # True=양극성(+/-), False=단극성(0/+)
PWM_TARGET_FREQ = 50      # 타겟 주파수 (Hz)

# 하모닉 성분 리스트: (주파수Hz, 진폭, 위상degree)
PWM_HARMONICS = [
    (50.0, 1.0, 0.0),
    # (150.0, 0.3, 90.0),
]

# 자동 최적화 (True면 HARMONICS 무시하고 자동 계산)
PWM_AUTO_OPTIMIZE = False
PWM_NUM_HARMONICS = 5     # 자동 최적화 시 하모닉 수

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from System.Collections.Generic import List
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Load import (
    LoadDefinition, LoadInputMode, PwmHarmonic)
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Load import LoadService

MODE_MAP = {
    "Expression": LoadInputMode.Expression,
    "Tabular": LoadInputMode.Tabular,
    "Pwm": LoadInputMode.Pwm,
}

def main():
    mode = MODE_MAP.get(INPUT_MODE)
    if mode is None:
        print("[Load] 알 수 없는 모드: %s (Expression, Tabular, Pwm)" % INPUT_MODE)
        return

    ld = LoadDefinition()
    ld.Name = LOAD_NAME
    ld.GroupName = GROUP_NAME
    ld.EndTime = END_TIME
    ld.DeltaTime = DELTA_TIME
    ld.InputMode = mode

    if INPUT_MODE == "Expression":
        ld.Expression = EXPRESSION
        print("[Load] Expression: %s" % EXPRESSION)

    elif INPUT_MODE == "Tabular":
        from System import Array
        times = Array[float]([t for t, a in TABULAR_DATA])
        amps = Array[float]([a for t, a in TABULAR_DATA])
        ld.TimeValues = times
        ld.AmplitudeValues = amps
        print("[Load] Tabular: %d개 포인트" % len(TABULAR_DATA))

    elif INPUT_MODE == "Pwm":
        ld.PwmCarrierFrequency = PWM_CARRIER_FREQ
        ld.PwmOutputAmplitude = PWM_OUTPUT_AMP
        ld.PwmBipolar = PWM_BIPOLAR
        ld.PwmTargetFrequency = PWM_TARGET_FREQ

        harmonics = List[PwmHarmonic]()
        for freq, amp, phase in PWM_HARMONICS:
            harmonics.Add(PwmHarmonic(freq, amp, phase))
        ld.PwmHarmonics = harmonics

        print("[Load] PWM: 캐리어=%.0f Hz, 출력=%.1f, %s" % (
            PWM_CARRIER_FREQ, PWM_OUTPUT_AMP,
            "양극성" if PWM_BIPOLAR else "단극성"))

    # 시계열 생성
    print("[Load] 시계열 생성 중... (T=%.3f s, dt=%.6f s)" % (END_TIME, DELTA_TIME))

    if INPUT_MODE == "Pwm" and PWM_AUTO_OPTIMIZE:
        print("[Load] PWM 자동 최적화 (%d 하모닉)..." % PWM_NUM_HARMONICS)
        LoadService.OptimizePwmHarmonics(ld, PWM_NUM_HARMONICS)

    LoadService.GenerateTimeSeries(ld)

    if ld.ComputedTime is not None and len(ld.ComputedTime) > 0:
        n = len(ld.ComputedTime)
        print("[Load] 생성 완료: %d개 포인트" % n)

        # 최대/최소 진폭
        amp = ld.ComputedAmplitude
        max_a = max(amp)
        min_a = min(amp)
        print("[Load] 진폭 범위: %.2f ~ %.2f" % (min_a, max_a))
    else:
        print("[Load] 시계열 생성 실패")
        return

    # 캐시에 추가
    LoadService.Add(ld)
    print("[Load] '%s' → 하중 캐시에 추가 (총 %d개)" % (LOAD_NAME, LoadService.Count))

    # FFT (선택)
    if INPUT_MODE == "Pwm":
        LoadService.ComputeFFT(ld)
        if ld.FftFrequency is not None:
            print("[Load] FFT 계산 완료: %d개 포인트" % len(ld.FftFrequency))

    print("[Load] 완료")

main()
