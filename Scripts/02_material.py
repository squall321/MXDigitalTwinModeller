# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 02. Material (재료 물성 적용)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 재료 적용 리스트: (바디키워드, 재료타입, 재료이름)
# 바디키워드: 빈값("")이면 전체 바디에 적용
# 재료타입: "Steel", "Aluminum", "CFRP", "Custom"
MATERIALS = [
    ("", "Steel", "Steel"),
    # ("Specimen", "CFRP", "CFRP_Specimen"),
    # ("Fixture", "Aluminum", "AL6061"),
]

# Custom 재료 사용 시: (바디키워드, "Custom", 재료이름, 밀도, E, nu, G, sigma, CTE, k, Cp)
# 단위계: mm-tonne-s (밀도: tonne/mm³, E/G/sigma: MPa, CTE: 1/K)
CUSTOM_MATERIALS = [
    # ("Body", "Custom", "Titanium", 4.43e-9, 116000, 0.34, 43280, 900, 8.6e-6, 21.9, 5.2e8),
]

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from System.Collections.Generic import List
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Material import MaterialService

def get_filtered_bodies(part, keyword=""):
    bodies = []
    _collect(part, bodies)
    if not keyword:
        return bodies
    kw = keyword.lower()
    return [b for b in bodies if kw in (b.Name or "").lower()]

def _collect(part, result):
    for b in part.Bodies:
        result.append(b)
    for c in part.Components:
        try:
            if c.Content:
                _collect(c.Content, result)
        except:
            pass

def get_preset_values(preset):
    if preset == "Aluminum":
        return MaterialService.GetAluminumDefaults()
    elif preset == "CFRP":
        return MaterialService.GetCFRPDefaults()
    else:
        return MaterialService.GetSteelDefaults()

def main():
    part = Window.ActiveWindow.Document.MainPart

    all_entries = []
    for item in MATERIALS:
        keyword, preset, mat_name = item[0], item[1], item[2]
        vals = get_preset_values(preset)
        all_entries.append((keyword, mat_name, vals[0], vals[1], vals[2], vals[3],
                            vals[4], vals[5], vals[6], vals[7]))

    for item in CUSTOM_MATERIALS:
        keyword = item[0]
        mat_name = item[2]
        all_entries.append((keyword, mat_name, item[3], item[4], item[5], item[6],
                            item[7], item[8], item[9], item[10]))

    if len(all_entries) == 0:
        print("[Material] 적용할 재료가 없습니다. MATERIALS를 설정하세요.")
        return

    for entry in all_entries:
        keyword, mat_name = entry[0], entry[1]
        density, E, nu, G, sigma, cte, k, cp = entry[2], entry[3], entry[4], entry[5], entry[6], entry[7], entry[8], entry[9]

        bodies = get_filtered_bodies(part, keyword)
        if len(bodies) == 0:
            print("[Material] '%s' 키워드 매칭 바디 없음 - 스킵" % keyword)
            continue

        net_bodies = List[DesignBody]()
        for b in bodies:
            net_bodies.Add(b)

        log = List[str]()

        print("[Material] '%s' → %d개 바디에 적용 중..." % (mat_name, len(bodies)))
        MaterialService.ApplyMaterial(part, mat_name,
            density, E, nu, G, sigma, cte, k, cp,
            net_bodies, log)

        for entry_log in log:
            print("  %s" % entry_log)

    print("[Material] 완료")

main()
