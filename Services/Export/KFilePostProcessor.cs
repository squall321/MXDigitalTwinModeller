using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Load;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Load;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Material;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simulation;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Export
{
    /// <summary>
    /// LS-DYNA .k 파일 후처리: MeshMethods.SaveDYNA()가 생성한 더미 물성을
    /// MaterialService에서 설정한 실제 물성으로 교체.
    /// mm-tonne-s 단위계 사용.
    ///
    /// 전략: 기존 더미 *MAT_ELASTIC 블록을 삭제하고,
    /// 캐시된 실제 물성으로 *MAT_ELASTIC 카드를 새로 생성.
    /// *PART의 mid 필드도 올바른 MID로 갱신.
    /// </summary>
    public static class KFilePostProcessor
    {
        // *PART 카드 파싱 결과
        private class PartEntry
        {
            public string Title;        // PART 타이틀 (= 바디 이름)
            public int DataLineIndex;   // 데이터행 인덱스 (pid, secid, mid, ...)
            public int CurrentMid;      // 현재 mid 값
        }

        /// <summary>
        /// .k 파일의 더미 *MAT_ 카드를 삭제하고 실제 물성으로 새로 작성
        /// </summary>
        public static int PatchMaterials(string kFilePath, Part part, List<string> log)
        {
            if (!File.Exists(kFilePath))
            {
                if (log != null) log.Add("[KFile] 파일을 찾을 수 없습니다: " + kFilePath);
                return 0;
            }

            // ── 1) SpaceClaim에서 바디이름→재료명, 재료명→물성값 수집 ──
            var bodyToMatName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matValues = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);

            foreach (var body in MaterialService.GetAllDesignBodies(part))
            {
                double densityMTS;
                string matName = MaterialService.ReadBodyMaterialName(body, out densityMTS);
                if (matName == null) continue;

                string bodyName = body.Name ?? "";
                if (!string.IsNullOrEmpty(bodyName) && !bodyToMatName.ContainsKey(bodyName))
                    bodyToMatName[bodyName] = matName;

                if (!matValues.ContainsKey(matName))
                {
                    double[] cached = MaterialService.GetCachedValues(matName);
                    if (cached != null)
                        matValues[matName] = cached;
                }
            }

            if (matValues.Count == 0)
            {
                if (log != null) log.Add("[KFile] 캐시된 재료 정보가 없습니다. 물성 후처리 스킵.");
                return 0;
            }

            // ── 2) .k 파일 읽기 ──
            string content = File.ReadAllText(kFilePath, Encoding.UTF8);
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // ── 3) *PART 카드 파싱 ──
            var partEntries = ParsePartCards(lines);

            // ── 4) PART 타이틀 → 재료명 퍼지 매칭, 고유 재료마다 MID 부여 ──
            int nextMid = 1;
            var matToMid = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var lineToNewMid = new Dictionary<int, int>(); // dataLineIndex → newMid

            foreach (var entry in partEntries)
            {
                string matchedMat = MatchPartToMaterial(entry.Title, bodyToMatName, matValues);
                if (matchedMat == null) continue;

                if (!matToMid.ContainsKey(matchedMat))
                {
                    matToMid[matchedMat] = nextMid++;
                    if (log != null)
                        log.Add(string.Format("[KFile] 재료 '{0}' → MID {1}", matchedMat, matToMid[matchedMat]));
                }

                lineToNewMid[entry.DataLineIndex] = matToMid[matchedMat];
            }

            if (matToMid.Count == 0)
            {
                if (log != null) log.Add("[KFile] PART 타이틀과 매칭되는 재료가 없습니다.");
                return 0;
            }

            // ── 5) 파일 재작성 ──
            var newLines = new List<string>();
            bool insertedNewMats = false;
            int idx = 0;

            while (idx < lines.Length)
            {
                // *MAT_ELASTIC 블록 감지 → 제거하고 새 카드 삽입
                if (lines[idx].TrimStart().StartsWith("*MAT_ELASTIC", StringComparison.OrdinalIgnoreCase) ||
                    lines[idx].TrimStart().StartsWith("*MAT_RIGID", StringComparison.OrdinalIgnoreCase))
                {
                    // 첫 번째 *MAT 위치에 새 카드 삽입
                    if (!insertedNewMats)
                    {
                        foreach (var kvp in matToMid)
                        {
                            double[] vals = matValues[kvp.Key];
                            newLines.Add("*MAT_ELASTIC");
                            newLines.Add("$      MID        RO         E        PR        DA        DB         K");
                            newLines.Add(FormatMatElastic(kvp.Value, vals[0], vals[1], vals[2]));
                        }
                        insertedNewMats = true;
                    }

                    // 기존 블록 스킵: 헤더
                    idx++;
                    // $ 주석행 스킵
                    while (idx < lines.Length && lines[idx].TrimStart().StartsWith("$"))
                        idx++;
                    // 데이터행 스킵 (1개 이상)
                    while (idx < lines.Length &&
                           !lines[idx].TrimStart().StartsWith("*") &&
                           !lines[idx].TrimStart().StartsWith("$"))
                        idx++;
                    continue;
                }

                // *PART 데이터행의 mid 필드 갱신
                if (lineToNewMid.ContainsKey(idx))
                {
                    newLines.Add(ReplaceField(lines[idx], 2, lineToNewMid[idx]));
                    idx++;
                    continue;
                }

                newLines.Add(lines[idx]);
                idx++;
            }

            // ── 6) 파일 쓰기 ──
            File.WriteAllText(kFilePath, string.Join("\n", newLines.ToArray()), new UTF8Encoding(false));
            if (log != null)
                log.Add(string.Format("[KFile] {0}개 재료 카드 생성, {1}개 PART mid 갱신",
                    matToMid.Count, lineToNewMid.Count));

            return matToMid.Count;
        }

        // ==========================================
        //  *PART 카드 파싱
        // ==========================================

        private static List<PartEntry> ParsePartCards(string[] lines)
        {
            var entries = new List<PartEntry>();

            for (int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].TrimStart().StartsWith("*PART", StringComparison.OrdinalIgnoreCase))
                    continue;
                // *PART_CONTACT 등 다른 키워드 제외
                string kw = lines[i].Trim();
                if (!kw.Equals("*PART", StringComparison.OrdinalIgnoreCase) &&
                    !kw.StartsWith("*PART ", StringComparison.OrdinalIgnoreCase))
                    continue;

                // $ 주석 스킵
                int j = i + 1;
                while (j < lines.Length && lines[j].TrimStart().StartsWith("$"))
                    j++;

                // 타이틀행
                string title = "";
                if (j < lines.Length && !lines[j].TrimStart().StartsWith("*"))
                {
                    title = lines[j].Trim();
                    j++;
                }

                // $ 주석 스킵
                while (j < lines.Length && lines[j].TrimStart().StartsWith("$"))
                    j++;

                // 데이터행: pid(0), secid(1), mid(2), ...
                if (j < lines.Length &&
                    !lines[j].TrimStart().StartsWith("*") &&
                    !lines[j].TrimStart().StartsWith("$"))
                {
                    int mid = ParseFieldInt(lines[j], 2);
                    entries.Add(new PartEntry
                    {
                        Title = title,
                        DataLineIndex = j,
                        CurrentMid = mid
                    });
                }
            }

            return entries;
        }

        // ==========================================
        //  PART 타이틀 → 재료 퍼지 매칭
        // ==========================================

        private static string MatchPartToMaterial(
            string partTitle,
            Dictionary<string, string> bodyToMat,
            Dictionary<string, double[]> matValues)
        {
            string normTitle = NormalizeName(partTitle);

            // 바디이름으로 매칭 시도 (양방향 contains)
            string bestMat = null;
            int bestLen = 0;

            foreach (var kvp in bodyToMat)
            {
                string normBody = NormalizeName(kvp.Key);
                if (string.IsNullOrEmpty(normBody)) continue;

                bool match = normTitle == normBody
                          || normTitle.Contains(normBody)
                          || normBody.Contains(normTitle);

                if (match && matValues.ContainsKey(kvp.Value))
                {
                    if (normBody.Length > bestLen)
                    {
                        bestLen = normBody.Length;
                        bestMat = kvp.Value;
                    }
                }
            }

            if (bestMat != null) return bestMat;

            // 폴백: 재료가 1개면 모든 PART에 적용
            if (matValues.Count == 1)
            {
                foreach (var kvp in matValues)
                    return kvp.Key;
            }

            return null;
        }

        /// <summary>
        /// 이름 정규화: 언더스코어/특수문자 → 공백, 대문자, 축약
        /// SaveDYNA가 바디이름의 공백→언더스코어, 괄호→언더스코어 변환하므로
        /// 양쪽 다 정규화해서 비교
        /// </summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string s = name.Replace('_', ' ');
            s = Regex.Replace(s, @"[^a-zA-Z0-9\s]", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s.ToUpperInvariant();
        }

        // ==========================================
        //  i10 고정폭 필드 교체
        // ==========================================

        /// <summary>
        /// i10 포맷 데이터행에서 fieldIndex번째 필드를 newValue로 교체
        /// </summary>
        private static string ReplaceField(string line, int fieldIndex, int newValue)
        {
            int start = fieldIndex * 10;
            string newField = newValue.ToString().PadLeft(10);

            if (start + 10 <= line.Length)
            {
                return line.Substring(0, start) + newField + line.Substring(start + 10);
            }

            // 라인이 짧으면 패딩
            return line.PadRight(start) + newField;
        }

        // ==========================================
        //  *MAT_ELASTIC 데이터행 포맷
        // ==========================================

        private static string FormatMatElastic(int mid, double density, double e, double nu)
        {
            // i10 포맷: 10자리 × 7필드
            // mid, ro, e, pr, da, db, not_used
            return string.Format("{0,10}{1}{2}{3}{4}{5}{6}",
                mid,
                FormatField(density),
                FormatField(e),
                FormatField(nu),
                FormatField(0),
                FormatField(0),
                "         0");
        }

        private static string FormatField(double v)
        {
            if (v == 0) return " 0.000E+00";

            // 과학적 표기법으로 10자리 맞춤
            string s = v.ToString("E3");
            if (s.Length > 10)
                s = v.ToString("E2");
            return s.PadLeft(10);
        }

        // ==========================================
        //  i10/i8/콤마 구분 필드 파싱
        // ==========================================

        private static int ParseFieldInt(string line, int fieldIndex)
        {
            try
            {
                // 콤마구분
                if (line.Contains(","))
                {
                    string[] parts = line.Split(',');
                    if (fieldIndex < parts.Length)
                    {
                        int val;
                        if (int.TryParse(parts[fieldIndex].Trim(), out val))
                            return val;
                    }
                }

                // i10 고정폭
                int start = fieldIndex * 10;
                if (start + 10 <= line.Length)
                {
                    string field = line.Substring(start, 10).Trim();
                    int val;
                    if (int.TryParse(field, out val))
                        return val;
                    double dval;
                    if (double.TryParse(field, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out dval))
                        return (int)dval;
                }

                // i8 고정폭
                int start8 = fieldIndex * 8;
                if (start8 + 8 <= line.Length)
                {
                    string field = line.Substring(start8, 8).Trim();
                    int val;
                    if (int.TryParse(field, out val))
                        return val;
                }
            }
            catch { }
            return 0;
        }

        // ==========================================
        //  시뮬레이션 제어 카드 + 하중 커브 삽입
        // ==========================================

        /// <summary>
        /// .k 파일에 시뮬레이션 제어 키워드(*CONTROL_*, *DATABASE_*)와
        /// 하중 정의(*DEFINE_CURVE)를 *END 앞에 삽입.
        /// </summary>
        public static int AppendControlCards(string kFilePath, List<string> log)
        {
            if (!File.Exists(kFilePath))
                return 0;

            string content = File.ReadAllText(kFilePath, Encoding.UTF8);
            string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            var newLines = new List<string>();
            int inserted = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                // *END 직전에 제어 카드 삽입
                if (lines[i].TrimStart().Equals("*END", StringComparison.OrdinalIgnoreCase))
                {
                    // ── 시뮬레이션 제어 키워드 삽입 ──
                    var simParams = SimulationKeywordService.Current;
                    string controlCards = SimulationKeywordService.GenerateControlCardsOnly(simParams);
                    if (!string.IsNullOrEmpty(controlCards))
                    {
                        newLines.Add("$");
                        string[] ccLines = controlCards.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        foreach (string cl in ccLines)
                            newLines.Add(cl);
                        inserted++;
                        if (log != null)
                            log.Add("[KFile] 시뮬레이션 제어 키워드 삽입 완료");
                    }

                    // ── 하중 커브 삽입 (*DEFINE_CURVE) ──
                    var loads = LoadService.GetAll();
                    if (loads.Count > 0)
                    {
                        newLines.Add("$");
                        newLines.Add("$==========================================================================");
                        newLines.Add("$  LOAD CURVES (하중 시간이력 커브)");
                        newLines.Add("$==========================================================================");

                        int lcid = 1;
                        foreach (var ld in loads)
                        {
                            // 데이터 없으면 생성
                            if (ld.ComputedTime == null || ld.ComputedAmplitude == null)
                            {
                                LoadService.GenerateTimeSeries(ld);
                                LoadService.ComputeFFT(ld);
                            }

                            if (ld.ComputedTime == null || ld.ComputedAmplitude == null)
                                continue;

                            int n = Math.Min(ld.ComputedTime.Length, ld.ComputedAmplitude.Length);
                            if (n < 2) continue;

                            newLines.Add("$");
                            newLines.Add(string.Format("$ Load: {0}  Group: {1}  Mode: {2}",
                                ld.Name ?? "", ld.GroupName ?? "", ld.InputMode));
                            newLines.Add("$");
                            newLines.Add("*DEFINE_CURVE");
                            newLines.Add("$#    lcid      sidr       sfa       sfo      offa      offo    dattyp     lcint");
                            newLines.Add(
                                lcid.ToString().PadLeft(10) +
                                "         0" +
                                "       1.0" +
                                "       1.0" +
                                "       0.0" +
                                "       0.0" +
                                "         0" +
                                "         0");
                            newLines.Add("$#                a1                  o1");

                            for (int p = 0; p < n; p++)
                            {
                                // 20-char wide fields for curve data
                                string a1 = ld.ComputedTime[p].ToString("G10", CultureInfo.InvariantCulture).PadLeft(20);
                                string o1 = ld.ComputedAmplitude[p].ToString("G10", CultureInfo.InvariantCulture).PadLeft(20);
                                newLines.Add(a1 + o1);
                            }

                            if (log != null)
                                log.Add(string.Format("[KFile] 하중 커브 '{0}' → LCID {1} ({2} points)",
                                    ld.Name ?? "?", lcid, n));
                            lcid++;
                            inserted++;
                        }
                    }

                    // *END 추가
                    newLines.Add("*END");
                    continue;
                }

                newLines.Add(lines[i]);
            }

            if (inserted > 0)
            {
                File.WriteAllText(kFilePath, string.Join("\n", newLines.ToArray()), new UTF8Encoding(false));
            }

            return inserted;
        }
    }
}
