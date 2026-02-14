# encoding: utf-8
# ================================================================
# MX Digital Twin Modeller - 15. STEP Export (STEP 파일 내보내기)
# SpaceClaim Script Editor에서 실행
# ================================================================

# ┌───────────────────────────────────────────────────────────────┐
# │  설정 (이 부분만 수정하세요)                                  │
# └───────────────────────────────────────────────────────────────┘

# 출력 파일 경로
OUTPUT_PATH = r"C:\Users\Sonic\Desktop\model.stp"

# 바디 필터 키워드 (빈값=전체 내보내기)
KEYWORD = ""

# ┌───────────────────────────────────────────────────────────────┐
# │  실행 (아래는 수정 불필요)                                    │
# └───────────────────────────────────────────────────────────────┘

from System.IO import File, FileInfo

def main():
    part = Window.ActiveWindow.Document.MainPart

    print("[STEP Export] 경로: %s" % OUTPUT_PATH)
    print("[STEP Export] 내보내기 중...")

    try:
        part.Export(PartExportFormat.Step, OUTPUT_PATH, True, None)
    except Exception as ex:
        print("[STEP Export] 오류: %s" % str(ex))
        return

    if File.Exists(OUTPUT_PATH):
        info = FileInfo(OUTPUT_PATH)
        size = info.Length
        if size < 1024 * 1024:
            print("[STEP Export] 파일 크기: %.1f KB" % (size / 1024.0))
        else:
            print("[STEP Export] 파일 크기: %.1f MB" % (size / (1024.0 * 1024.0)))
    else:
        print("[STEP Export] [WARN] 파일이 생성되지 않았습니다.")
        return

    print("[STEP Export] 완료")

main()
