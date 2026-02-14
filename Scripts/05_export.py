# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 05. Export (메쉬 내보내기)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 내보내기 포맷: "LS-DYNA", "ANSYS", "Abaqus", "Fluent", "CGNS"
FORMAT = "LS-DYNA"

# 출력 파일 경로
OUTPUT_PATH = r"C:\Users\Sonic\Desktop\model.k"

# LS-DYNA 전용 후처리 옵션
PATCH_MATERIALS = True      # 더미 물성을 실제 물성으로 교체
APPEND_CONTROL_CARDS = True # 시뮬레이션 제어카드 + 하중 커브 삽입

# STEP 내보내기도 동시에 할지 여부
EXPORT_STEP = False
STEP_PATH = r"C:\Users\Sonic\Desktop\model.stp"

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

import clr
clr.AddReference("MXDigitalTwinModeller")

from System.IO import File, FileInfo
from SpaceClaim.Api.V252.Analysis import MeshMethods
from System.Collections.Generic import List
from SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Export import KFilePostProcessor

def main():
    part = Window.ActiveWindow.Document.MainPart

    # 메쉬 내보내기
    print("[Export] 포맷: %s" % FORMAT)
    print("[Export] 경로: %s" % OUTPUT_PATH)
    print("[Export] 내보내기 중...")

    if FORMAT == "LS-DYNA":
        MeshMethods.SaveDYNA(OUTPUT_PATH)
    elif FORMAT == "ANSYS":
        MeshMethods.SaveANSYS(OUTPUT_PATH)
    elif FORMAT == "Abaqus":
        MeshMethods.SaveAbaqus(OUTPUT_PATH)
    elif FORMAT == "Fluent":
        MeshMethods.SaveFluentMesh(OUTPUT_PATH)
    elif FORMAT == "CGNS":
        MeshMethods.SaveCGNS(OUTPUT_PATH)
    else:
        print("[Export] 알 수 없는 포맷: %s" % FORMAT)
        return

    if File.Exists(OUTPUT_PATH):
        info = FileInfo(OUTPUT_PATH)
        size = info.Length
        if size < 1024 * 1024:
            size_str = "%.1f KB" % (size / 1024.0)
        else:
            size_str = "%.1f MB" % (size / (1024.0 * 1024.0))
        print("[Export] 파일 크기: %s" % size_str)
    else:
        print("[Export] [WARN] 파일이 생성되지 않았습니다. 메쉬가 존재하는지 확인하세요.")
        return

    # LS-DYNA 후처리
    if FORMAT == "LS-DYNA" and File.Exists(OUTPUT_PATH):
        mat_log = List[str]()

        if PATCH_MATERIALS:
            print("[Export] LS-DYNA 물성 후처리 중...")
            try:
                patched = KFilePostProcessor.PatchMaterials(OUTPUT_PATH, part, mat_log)
                print("[Export] 물성 교체: %d개 재료" % patched)
            except Exception as ex:
                print("[Export] 물성 후처리 오류: %s" % str(ex))

        if APPEND_CONTROL_CARDS:
            print("[Export] LS-DYNA 제어카드 삽입 중...")
            try:
                inserted = KFilePostProcessor.AppendControlCards(OUTPUT_PATH, mat_log)
                print("[Export] 제어카드: %d개 블록" % inserted)
            except Exception as ex:
                print("[Export] 제어카드 삽입 오류: %s" % str(ex))

        for entry in mat_log:
            print("  %s" % entry)

    # STEP 내보내기 (선택)
    if EXPORT_STEP:
        print("[Export] STEP 내보내기: %s" % STEP_PATH)
        try:
            part.Export(PartExportFormat.Step, STEP_PATH, True, None)
            if File.Exists(STEP_PATH):
                info = FileInfo(STEP_PATH)
                print("[Export] STEP 파일 크기: %.1f KB" % (info.Length / 1024.0))
            else:
                print("[Export] [WARN] STEP 파일 생성 실패")
        except Exception as ex:
            print("[Export] STEP 내보내기 오류: %s" % str(ex))

    print("[Export] 완료")

main()
