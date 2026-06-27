using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// 모든 test spec 생성 + extraction 실행 + 결과 비교.
    /// 결과 한 폴더에 JSON + 요약 report.
    /// </summary>
    public class SelfTestRunner
    {
        /// <summary>
        /// Opt-in: if true, SelfTestRunner.RunAll() will apply color-coded
        /// FeatureGraph visualization to each test body before SaveAs (.scdocx).
        /// When the user opens the saved file in SpaceClaim, faces appear
        /// colored by their classified role. Default false (no visual change).
        /// </summary>
        public static bool VisualizeFeatureGraph { get; set; } = false;

        /// <summary>
        /// 한 test 의 결과.
        /// </summary>
        public class TestResult
        {
            public string SpecName { get; set; }
            public string Description { get; set; }
            public Dictionary<string, double> ExpectedParams { get; set; }
            public FeatureGraph Graph { get; set; }
            public Dictionary<string, string> Checks { get; set; }  // check_name → "PASS"/"FAIL: reason"
            public bool OverallPass { get; set; }

            public TestResult()
            {
                ExpectedParams = new Dictionary<string, double>();
                Checks = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// 모든 test 실행.
        /// </summary>
        /// <param name="part">test body 가 만들어질 Part</param>
        /// <param name="outputDir">결과 저장 폴더</param>
        public List<TestResult> RunAll(Part part, string outputDir)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (string.IsNullOrEmpty(outputDir)) throw new ArgumentException("outputDir empty");
            Directory.CreateDirectory(outputDir);

            var generator = new TestModelGenerator();
            var extractor = new FeatureExtractor();
            var results = new List<TestResult>();

            foreach (var spec in generator.CreateAllSpecs())
            {
                var result = new TestResult
                {
                    SpecName = spec.Name,
                    Description = spec.Description,
                    ExpectedParams = new Dictionary<string, double>(spec.Params),
                };

                DesignBody body = null;
                try
                {
                    // body 생성 (WriteBlock 안에서)
                    WriteBlock.ExecuteTask("Build test " + spec.Name, () =>
                    {
                        body = spec.Builder(part);
                    });

                    // extraction
                    var graph = extractor.Extract(body);
                    result.Graph = graph;

                    // 검증
                    ApplyChecks(spec, result);

                    // JSON 저장
                    string jsonPath = Path.Combine(outputDir, spec.Name + ".json");
                    FeatureGraphJsonWriter.WriteToFile(graph, jsonPath);

                    // Opt-in: feature-graph 색상 시각화. body 색상 변경은 SaveAs 에
                    // 함께 저장되므로 사용자가 .scdocx 더블클릭 시 색상이 보인다.
                    if (VisualizeFeatureGraph && body != null && graph != null)
                    {
                        try
                        {
                            FeatureVisualizer.ApplyColors(body, graph);
                        }
                        catch (Exception vizEx)
                        {
                            result.Checks["visualize"] = "WARN: ApplyColors failed - " + vizEx.Message;
                        }
                    }

                    // .scdocx 저장: 사용자가 더블클릭해 SC 에서 시각적으로 확인 가능
                    //   body 삭제 직전, doc 에 이 spec body 만 존재할 때 SaveAs.
                    //   각 spec 마다 별도 파일로 저장.
                    try
                    {
                        string scdocxPath = Path.Combine(outputDir, spec.Name + ".scdocx");
                        part.Document.SaveAs(scdocxPath);
                    }
                    catch (Exception saveEx)
                    {
                        result.Checks["scdocx_save"] = "WARN: SaveAs failed - " + saveEx.Message;
                    }
                }
                catch (Exception ex)
                {
                    result.Checks["EXTRACTION"] = "FAIL: " + ex.Message;
                    result.OverallPass = false;
                }
                finally
                {
                    // 정리: test body 삭제
                    if (body != null)
                    {
                        try
                        {
                            WriteBlock.ExecuteTask("Cleanup test " + spec.Name, () =>
                            {
                                body.Delete();
                            });
                        }
                        catch { /* swallow */ }
                    }
                }

                results.Add(result);
            }

            // 요약 report 저장
            string reportPath = Path.Combine(outputDir, "00_summary.md");
            File.WriteAllText(reportPath, BuildMarkdownReport(results), Encoding.UTF8);

            return results;
        }

        // ---- 각 spec 의 expected params vs extracted graph 비교 ----
        private void ApplyChecks(TestModelGenerator.TestSpec spec, TestResult result)
        {
            var graph = result.Graph;
            var p = spec.Params;

            bool allOk = true;

            // bbox 검증 (있으면)
            //   - L/W/T 형 (대부분의 spec)
            //   - outer_L/outer_W/T + branch_H 형 (#10 concave_fillet: L-shape)
            bool hasStandardBbox = p.ContainsKey("L_mm") && p.ContainsKey("W_mm") && p.ContainsKey("T_mm");
            bool hasLShapeBbox = p.ContainsKey("outer_L_mm") && p.ContainsKey("outer_W_mm") && p.ContainsKey("T_mm") && p.ContainsKey("branch_H_mm");
            if (hasStandardBbox || hasLShapeBbox)
            {
                double expectedL, expectedW, expectedT;
                if (hasStandardBbox)
                {
                    expectedL = p["L_mm"];
                    expectedW = p["W_mm"];
                    expectedT = p["T_mm"];
                }
                else
                {
                    expectedL = p["outer_L_mm"];
                    expectedW = p["outer_W_mm"];
                    // L-shape: branch box sits on top of main, both extruded by T
                    //   bbox Z = T (main) + T (branch) = 2*T
                    //   ("branch_H_mm" in spec is footprint length along Y, NOT height)
                    expectedT = 2.0 * p["T_mm"];
                }

                // boss 가 박스 top 면 위로 추가되면 Z 방향 bbox 가 boss_H_mm 만큼 늘어남.
                // 단, side_face_pos_x 가 set 이면 boss 가 +X 옆면에서 돌출 → W (X) 가 늘어남.
                if (p.ContainsKey("boss_H_mm") && hasStandardBbox)
                {
                    if (p.ContainsKey("side_face_pos_x"))
                        expectedW += p["boss_H_mm"];
                    else
                        expectedT += p["boss_H_mm"];
                }

                // wall 이 박스 top 면 위로 솟아오르면 Z 방향 bbox 가 wall_H_mm 만큼 늘어남
                // (예: 17_T_junction_3way — wall_H_mm=20 이 base top 면에서 위로 추가됨)
                if (p.ContainsKey("wall_H_mm") && hasStandardBbox)
                {
                    expectedT += p["wall_H_mm"];
                }

                // rib 가 박스 top 면 위로 솟아오르면 Z 방향 bbox 가 rib_H_mm 만큼 늘어남
                // (예: 19_box_with_rib — rib_H_mm 은 wall_H_mm 의 별칭처럼 동작)
                if (p.ContainsKey("rib_H_mm") && hasStandardBbox)
                {
                    expectedT += p["rib_H_mm"];
                }

                // stepped boss: base + pin 이 차곡차곡 쌓이는 형태이므로 두 높이를 모두 더함
                // (예: 20_box_stepped_boss — base_H_mm=4 + pin_H_mm=6 둘 다 위로 stack)
                if (p.ContainsKey("base_H_mm") && hasStandardBbox)
                {
                    expectedT += p["base_H_mm"];
                }
                if (p.ContainsKey("pin_H_mm") && hasStandardBbox)
                {
                    expectedT += p["pin_H_mm"];
                }

                // bbox size 의 세 차원을 정렬해 비교 (방향 invariant)
                var actual = new[] { graph.BboxSizeMm[0], graph.BboxSizeMm[1], graph.BboxSizeMm[2] };
                Array.Sort(actual);
                var expected = new[] { expectedT, expectedW, expectedL };
                Array.Sort(expected);

                double tol = 0.5; // 0.5mm 허용 (fillet 으로 인한 약간의 bbox 변화)
                bool bboxOk = true;
                for (int i = 0; i < 3; i++)
                {
                    if (Math.Abs(actual[i] - expected[i]) > tol) { bboxOk = false; break; }
                }
                result.Checks["bbox"] = bboxOk
                    ? string.Format("PASS (expected {0}/{1}/{2}, got {3:F2}/{4:F2}/{5:F2})",
                        expected[0], expected[1], expected[2], actual[0], actual[1], actual[2])
                    : string.Format("FAIL (expected {0}/{1}/{2}, got {3:F2}/{4:F2}/{5:F2})",
                        expected[0], expected[1], expected[2], actual[0], actual[1], actual[2]);
                if (!bboxOk) allOk = false;
            }

            // fillet R 검증 (있으면) — 가장 흔한 R 이 expected 와 매칭되는지
            if (p.ContainsKey("fillet_R_mm"))
            {
                double expectedR = p["fillet_R_mm"];
                if (graph.Fillets.Count == 0)
                {
                    result.Checks["fillet"] = string.Format("FAIL (expected R={0}, no fillets detected)", expectedR);
                    allOk = false;
                }
                else
                {
                    // 가장 흔한 snapped R 사용
                    var rGroups = new Dictionary<double, int>();
                    foreach (var f in graph.Fillets)
                    {
                        double r = f.SnappedRadiusMm > 0 ? f.SnappedRadiusMm : f.RadiusMm;
                        if (!rGroups.ContainsKey(r)) rGroups[r] = 0;
                        rGroups[r]++;
                    }
                    double dominantR = 0;
                    int maxCount = 0;
                    foreach (var kv in rGroups)
                    {
                        if (kv.Value > maxCount) { maxCount = kv.Value; dominantR = kv.Key; }
                    }
                    double diff = Math.Abs(dominantR - expectedR);
                    bool rOk = diff <= 0.3;
                    result.Checks["fillet"] = rOk
                        ? string.Format("PASS (expected R={0}, dominant R={1:F2}, {2} fillets)", expectedR, dominantR, graph.Fillets.Count)
                        : string.Format("FAIL (expected R={0}, dominant R={1:F2}, diff {2:F2})", expectedR, dominantR, diff);
                    if (!rOk) allOk = false;
                }
            }

            // hole D 검증
            if (p.ContainsKey("hole_D_mm"))
            {
                double expectedD = p["hole_D_mm"];
                int expectedCount = p.ContainsKey("hole_count") ? (int)p["hole_count"] : 1;
                if (graph.Holes.Count == 0)
                {
                    result.Checks["hole"] = string.Format("FAIL (expected {0} holes D={1}, none detected)", expectedCount, expectedD);
                    allOk = false;
                }
                else
                {
                    int matchCount = 0;
                    foreach (var h in graph.Holes)
                    {
                        if (Math.Abs(h.DiameterMm - expectedD) <= 0.3) matchCount++;
                    }
                    bool holeOk = matchCount == expectedCount;
                    result.Checks["hole"] = holeOk
                        ? string.Format("PASS ({0} holes D≈{1})", matchCount, expectedD)
                        : string.Format("FAIL (expected {0} holes D={1}, got {2} matching)", expectedCount, expectedD, matchCount);
                    if (!holeOk) allOk = false;
                }
            }

            // boss 검증
            if (p.ContainsKey("boss_D_mm"))
            {
                double expectedD = p["boss_D_mm"];
                if (graph.Bosses.Count == 0)
                {
                    result.Checks["boss"] = string.Format("FAIL (expected boss D={0}, none detected)", expectedD);
                    allOk = false;
                }
                else
                {
                    var b = graph.Bosses[0];
                    bool bossOk = Math.Abs(b.DiameterMm - expectedD) <= 0.5;
                    result.Checks["boss"] = bossOk
                        ? string.Format("PASS (boss D={0:F2})", b.DiameterMm)
                        : string.Format("FAIL (expected D={0}, got D={1:F2})", expectedD, b.DiameterMm);
                    if (!bossOk) allOk = false;
                }
            }

            // boss count 검증 (#13_3bosses_row: 3개 boss 가 일렬로)
            if (p.ContainsKey("boss_count"))
            {
                int expectedCount = (int)p["boss_count"];
                bool countOk = graph.Bosses.Count == expectedCount;
                result.Checks["boss_count"] = countOk
                    ? string.Format("PASS ({0} bosses detected)", graph.Bosses.Count)
                    : string.Format("FAIL (expected {0} bosses, got {1})", expectedCount, graph.Bosses.Count);
                if (!countOk) allOk = false;
            }

            // counterbore 검증 (#11): outer D + inner D cluster 모두 존재해야 함
            //   각 corner 마다 cylinder 2개 (outer + inner) ⇒ 총 holes = 2 * hole_count
            if (p.ContainsKey("cb_outer_D_mm") && p.ContainsKey("cb_inner_D_mm") && p.ContainsKey("hole_count"))
            {
                double outerD = p["cb_outer_D_mm"];
                double innerD = p["cb_inner_D_mm"];
                int holeCount = (int)p["hole_count"];
                int outerMatch = 0, innerMatch = 0;
                foreach (var h in graph.Holes)
                {
                    if (Math.Abs(h.DiameterMm - outerD) <= 0.3) outerMatch++;
                    else if (Math.Abs(h.DiameterMm - innerD) <= 0.3) innerMatch++;
                }
                bool cbOk = outerMatch == holeCount && innerMatch == holeCount;
                result.Checks["counterbore"] = cbOk
                    ? string.Format("PASS ({0} outer D={1}, {2} inner D={3})", outerMatch, outerD, innerMatch, innerD)
                    : string.Format("FAIL (expected {0} outer+{0} inner, got outer={1} D={2}, inner={3} D={4})",
                        holeCount, outerMatch, outerD, innerMatch, innerD);
                if (!cbOk) allOk = false;
            }

            // countersink 검증 (#12): inner D 존재 + outer cylinder approx 존재 (현재는 cylinder fallback)
            //   cs_inner_D_mm 매칭 hole 개수가 의도된 corner 수만큼 있어야 함.
            //   spec 에 hole_count 가 없으면 corner 갯수 = 2 로 가정 (12_box_countersink 만)
            if (p.ContainsKey("cs_inner_D_mm"))
            {
                double innerD = p["cs_inner_D_mm"];
                double outerD = p.ContainsKey("cs_outer_D_mm") ? p["cs_outer_D_mm"] : -1;
                int expectedCount = p.ContainsKey("hole_count") ? (int)p["hole_count"] : 2;
                int innerMatch = 0, outerMatch = 0;
                foreach (var h in graph.Holes)
                {
                    if (Math.Abs(h.DiameterMm - innerD) <= 0.3) innerMatch++;
                    else if (outerD > 0 && Math.Abs(h.DiameterMm - outerD) <= 0.3) outerMatch++;
                }
                bool csOk = innerMatch == expectedCount && (outerD < 0 || outerMatch == expectedCount);
                result.Checks["countersink"] = csOk
                    ? string.Format("PASS ({0} inner D={1}, {2} outer D={3})", innerMatch, innerD, outerMatch, outerD)
                    : string.Format("FAIL (expected {0} inner+{0} outer, got inner={1} outer={2})",
                        expectedCount, innerMatch, outerMatch);
                if (!csOk) allOk = false;
            }

            // 차등 R 검증 (#14_triple_corner_R): corner_R_mm + edge_R_mm 모두 cluster 로 존재
            if (p.ContainsKey("corner_R_mm") && p.ContainsKey("edge_R_mm"))
            {
                double cornerR = p["corner_R_mm"];
                double edgeR = p["edge_R_mm"];
                int cornerMatch = 0, edgeMatch = 0;
                foreach (var f in graph.Fillets)
                {
                    double r = f.SnappedRadiusMm > 0 ? f.SnappedRadiusMm : f.RadiusMm;
                    if (Math.Abs(r - cornerR) <= 0.3) cornerMatch++;
                    else if (Math.Abs(r - edgeR) <= 0.3) edgeMatch++;
                }
                // 최소 4 corner edge + 일부 edge fillet 이 있어야 함
                bool multiOk = cornerMatch >= 4 && edgeMatch >= 4;
                result.Checks["multi_R"] = multiOk
                    ? string.Format("PASS (corner R={0}: {1} fillets, edge R={2}: {3} fillets)", cornerR, cornerMatch, edgeR, edgeMatch)
                    : string.Format("FAIL (expected ≥4+≥4, got corner R={0}: {1}, edge R={2}: {3})", cornerR, cornerMatch, edgeR, edgeMatch);
                if (!multiOk) allOk = false;
            }

            // slit 검증
            if (p.ContainsKey("slit_width_mm"))
            {
                double expectedW = p["slit_width_mm"];
                if (graph.Slits.Count == 0)
                {
                    result.Checks["slit"] = string.Format("FAIL (expected slit W={0}, none detected)", expectedW);
                    allOk = false;
                }
                else
                {
                    var sl = graph.Slits[0];
                    bool slitOk = Math.Abs(sl.WidthMm - expectedW) <= 0.3;
                    result.Checks["slit"] = slitOk
                        ? string.Format("PASS (slit W={0:F3})", sl.WidthMm)
                        : string.Format("FAIL (expected W={0}, got W={1:F3})", expectedW, sl.WidthMm);
                    if (!slitOk) allOk = false;
                }
            }

            // Stage 3: pattern 검증
            //   expected_pattern_type   ∈ {1=Linear, 2=Grid, 3=Circular}
            //   expected_member_count   = 멤버 개수
            //   expected_pattern_target ∈ {0 (default, hole), 1 (boss)}
            if (p.ContainsKey("expected_pattern_type") && p.ContainsKey("expected_member_count"))
            {
                int expectedTypeCode = (int)p["expected_pattern_type"];
                int expectedCount = (int)p["expected_member_count"];
                bool expectBoss = p.ContainsKey("expected_pattern_target") &&
                                   p["expected_pattern_target"] > 0.5;

                string typeName = expectedTypeCode == 1 ? "Linear"
                                : expectedTypeCode == 2 ? "Grid"
                                : expectedTypeCode == 3 ? "Circular" : "Unknown";

                bool patternOk = false;
                int actualCount = -1;
                if (expectBoss)
                {
                    foreach (var bp in graph.BossPatterns)
                    {
                        if ((int)bp.PatternType == expectedTypeCode && bp.Count == expectedCount)
                        { patternOk = true; actualCount = bp.Count; break; }
                    }
                }
                else
                {
                    foreach (var hp in graph.HolePatterns)
                    {
                        if ((int)hp.PatternType == expectedTypeCode && hp.Count == expectedCount)
                        { patternOk = true; actualCount = hp.Count; break; }
                    }
                }
                result.Checks["pattern"] = patternOk
                    ? string.Format("PASS ({0} with {1} members)", typeName, actualCount)
                    : string.Format("FAIL (expected {0} with {1} members; got {2} hole patterns, {3} boss patterns)",
                        typeName, expectedCount, graph.HolePatterns.Count, graph.BossPatterns.Count);
                if (!patternOk) allOk = false;
            }

            // Stage 3+: symmetry plane count check.
            //   expected_symmetry_planes = score >= 80 인 symmetry 개수.
            if (p.ContainsKey("expected_symmetry_planes"))
            {
                int expectedSym = (int)p["expected_symmetry_planes"];
                int actualSym = 0;
                if (graph.Symmetries != null)
                {
                    foreach (var s in graph.Symmetries)
                        if (s.ScorePercent >= 80.0) actualSym++;
                }
                bool symOk = actualSym == expectedSym;
                result.Checks["symmetry"] = symOk
                    ? string.Format("PASS ({0} symmetry planes ≥80%)", actualSym)
                    : string.Format("FAIL (expected {0} planes ≥80%, got {1})", expectedSym, actualSym);
                if (!symOk) allOk = false;
            }

            // Stage 3+: composite count check.
            //   expected_composites = composite 개수.
            if (p.ContainsKey("expected_composites"))
            {
                int expectedC = (int)p["expected_composites"];
                int actualC = graph.Composites != null ? graph.Composites.Count : 0;
                bool cOk = actualC == expectedC;
                result.Checks["composites"] = cOk
                    ? string.Format("PASS ({0} composites)", actualC)
                    : string.Format("FAIL (expected {0} composites, got {1})", expectedC, actualC);
                if (!cOk) allOk = false;
            }

            // Stage 3+: expected_composite_type = "mirrored_grid" 같은 문자열을 spec.ExpectedTypes 에서 가져옴.
            //   숫자가 아니라 문자열이라 spec.Params 외 다른 곳에서 읽음.
            string expectedCompositeType = null;
            if (spec.ExpectedTypes != null && spec.ExpectedTypes.TryGetValue("expected_composite_type", out var v0))
                expectedCompositeType = v0;
            if (!string.IsNullOrEmpty(expectedCompositeType))
            {
                bool typeOk = false;
                if (graph.Composites != null)
                {
                    foreach (var c in graph.Composites)
                    {
                        if (string.Equals(c.Type, expectedCompositeType, StringComparison.OrdinalIgnoreCase))
                        { typeOk = true; break; }
                    }
                }
                result.Checks["composite_type"] = typeOk
                    ? string.Format("PASS (found composite type '{0}')", expectedCompositeType)
                    : string.Format("FAIL (expected composite type '{0}', not found among {1} composites)",
                        expectedCompositeType, graph.Composites != null ? graph.Composites.Count : 0);
                if (!typeOk) allOk = false;
            }

            // ----------------------------------------------------------------
            // Audit strengthening (added 2026-06-05) — these checks plug
            // silent passes where expected_* keys existed in spec.Params but
            // ApplyChecks had no handler. Each check enforces the spec's
            // stated invariant directly against graph contents.
            // ----------------------------------------------------------------

            // [AUDIT] expected_chain_count — used by specs 51, 61, 14, 25, 48, 66.
            //   FilletChainGrouper is supposed to merge tangent-continuous fillet
            //   faces into a single chain. When grouping fails, every face
            //   becomes its own 1-face chain (chain count = fillet count) and
            //   the spec was silently passing. This check makes that visible.
            if (p.ContainsKey("expected_chain_count"))
            {
                int expectedChains = (int)p["expected_chain_count"];
                int actualChains = graph.FilletChains != null ? graph.FilletChains.Count : 0;
                bool chainOk = actualChains == expectedChains;
                result.Checks["chain_count"] = chainOk
                    ? string.Format("PASS ({0} chains)", actualChains)
                    : string.Format("FAIL (expected {0} chains, got {1}; fillet face count={2})",
                        expectedChains, actualChains, graph.Fillets != null ? graph.Fillets.Count : 0);
                if (!chainOk) allOk = false;
            }

            // [AUDIT] expected_inverted_walls — used by spec 64.
            //   GEOMETRIC IsInverted rule: a wall is "inverted" (inner cavity)
            //   iff BOTH of its paired faces are strictly INSIDE the body's
            //   AABB (not on any of the 6 bbox planes within 0.5 mm). Outer-
            //   shell walls always have at least one face on the boundary,
            //   so they are never inverted; inner-cavity walls have both
            //   faces away from the bbox surface, so they are inverted.
            if (p.ContainsKey("expected_inverted_walls"))
            {
                int expectedInv = (int)p["expected_inverted_walls"];
                int invertedCount = 0;
                int totalWalls = 0;
                if (graph.Walls != null)
                {
                    foreach (var w in graph.Walls)
                    {
                        totalWalls++;
                        if (w.IsInverted) invertedCount++;
                    }
                }
                bool invOk = invertedCount == expectedInv;
                result.Checks["inverted_walls"] = invOk
                    ? string.Format("PASS ({0} inverted walls of {1} total)", invertedCount, totalWalls)
                    : string.Format("FAIL (expected {0} inverted walls, got {1} of {2} total)",
                        expectedInv, invertedCount, totalWalls);
                if (!invOk) allOk = false;
            }

            // [AUDIT] expected_no_slit — used by spec 67.
            //   Spec 67 is a 200x2x40 plate-with-hole. After SlitDetector fix,
            //   it should NOT be detected as a slit (semantic plate, not slit).
            //   Enforce Slits.Count == 0 explicitly so any regression resurfaces.
            if (p.ContainsKey("expected_no_slit") && p["expected_no_slit"] > 0.5)
            {
                int slitCount = graph.Slits != null ? graph.Slits.Count : 0;
                bool noSlitOk = slitCount == 0;
                result.Checks["no_slit"] = noSlitOk
                    ? "PASS (0 slits detected, plate not misclassified)"
                    : string.Format("FAIL (expected 0 slits, got {0} — plate misclassified as slit)", slitCount);
                if (!noSlitOk) allOk = false;
            }

            // [AUDIT] expected_cone_face_count — used by spec 63 (cone boss).
            //   Verifies at least one face was classified as Cone (not collapsed
            //   to cylinder fallback). When revolve succeeds → exactly 1 cone face.
            //   Stacked-cylinder fallback path will have 0 cone faces — flag it.
            if (p.ContainsKey("expected_cone_face_count"))
            {
                int expectedCones = (int)p["expected_cone_face_count"];
                int actualCones = 0;
                if (graph.Faces != null)
                {
                    foreach (var f in graph.Faces)
                        if (f.Type == SurfaceType.Cone) actualCones++;
                }
                bool coneOk = actualCones >= expectedCones;
                result.Checks["cone_face"] = coneOk
                    ? string.Format("PASS ({0} cone face(s); expected ≥{1})", actualCones, expectedCones)
                    : string.Format("FAIL (expected ≥{0} cone faces, got {1}; possible fallback path)",
                        expectedCones, actualCones);
                if (!coneOk) allOk = false;
            }

            // [AUDIT] vert_R_mm / horiz_R_mm — used by specs 51, 66.
            //   Spec 66: vert_R_mm=3 + horiz_R_mm=5. Both radii must appear in
            //   the fillet feature list (i.e. extraction did not lose the
            //   smaller radius family). Each radius is checked independently
            //   against snapped/raw R with the same ±0.3mm tolerance used
            //   elsewhere. Silent-pass before because there was no handler.
            if (p.ContainsKey("vert_R_mm"))
            {
                double vR = p["vert_R_mm"];
                int vMatch = 0;
                if (graph.Fillets != null)
                {
                    foreach (var f in graph.Fillets)
                    {
                        double r = f.SnappedRadiusMm > 0 ? f.SnappedRadiusMm : f.RadiusMm;
                        if (Math.Abs(r - vR) <= 0.3) vMatch++;
                    }
                }
                bool vrOk = vMatch >= 1;
                result.Checks["vert_R"] = vrOk
                    ? string.Format("PASS (vert R={0} found in {1} fillets)", vR, vMatch)
                    : string.Format("FAIL (vert R={0} not found among {1} fillets — extraction may have lost the smaller-R family)",
                        vR, graph.Fillets != null ? graph.Fillets.Count : 0);
                if (!vrOk) allOk = false;
            }
            if (p.ContainsKey("horiz_R_mm"))
            {
                double hR = p["horiz_R_mm"];
                int hMatch = 0;
                if (graph.Fillets != null)
                {
                    foreach (var f in graph.Fillets)
                    {
                        double r = f.SnappedRadiusMm > 0 ? f.SnappedRadiusMm : f.RadiusMm;
                        if (Math.Abs(r - hR) <= 0.3) hMatch++;
                    }
                }
                bool hrOk = hMatch >= 1;
                result.Checks["horiz_R"] = hrOk
                    ? string.Format("PASS (horiz R={0} found in {1} fillets)", hR, hMatch)
                    : string.Format("FAIL (horiz R={0} not found among {1} fillets)",
                        hR, graph.Fillets != null ? graph.Fillets.Count : 0);
                if (!hrOk) allOk = false;
            }

            // [AUDIT] Strengthen the existing hole check with a "no extra holes"
            //   guard for specs that declare hole_count. Spec 39 was passing
            //   because 4 D≈10 segments happened to match expected_count=4,
            //   but the extraction ALSO returned 3 spurious D≈10.6 groove
            //   holes. Total hole count was 7, not 4 — pollution went silent.
            //   Add a "hole_total" check: total holes must equal hole_count
            //   exactly when no other hole-diameter param (cb_*, cs_*) is set,
            //   because those specs legitimately have multiple D families.
            if (p.ContainsKey("hole_D_mm") && p.ContainsKey("hole_count"))
            {
                bool hasMultiDiameter =
                    p.ContainsKey("cb_outer_D_mm") || p.ContainsKey("cb_inner_D_mm") ||
                    p.ContainsKey("cs_outer_D_mm") || p.ContainsKey("cs_inner_D_mm") ||
                    p.ContainsKey("center_hole_D_mm");  // spec 25: corner + center 동시 보유
                if (!hasMultiDiameter)
                {
                    int expectedTotal = (int)p["hole_count"];
                    int actualTotal = graph.Holes != null ? graph.Holes.Count : 0;
                    bool totalOk = actualTotal == expectedTotal;
                    result.Checks["hole_total"] = totalOk
                        ? string.Format("PASS (exactly {0} holes, no pollution)", actualTotal)
                        : string.Format("FAIL (expected {0} total holes, got {1} — spurious holes from grooves/slits polluting output)",
                            expectedTotal, actualTotal);
                    if (!totalOk) allOk = false;
                }
                else if (p.ContainsKey("center_hole_D_mm"))
                {
                    // spec 25: corner hole_count + center_hole_count = expected total.
                    int expectedCorner = (int)p["hole_count"];
                    int expectedCenter = p.ContainsKey("center_hole_count") ? (int)p["center_hole_count"] : 1;
                    int expectedTotal = expectedCorner + expectedCenter;
                    int actualTotal = graph.Holes != null ? graph.Holes.Count : 0;
                    bool totalOk = actualTotal == expectedTotal;
                    result.Checks["hole_total"] = totalOk
                        ? string.Format("PASS (exactly {0} holes = {1} corner + {2} center)",
                            actualTotal, expectedCorner, expectedCenter)
                        : string.Format("FAIL (expected {0} = {1} corner + {2} center, got {3})",
                            expectedTotal, expectedCorner, expectedCenter, actualTotal);
                    if (!totalOk) allOk = false;
                }
            }

            result.OverallPass = allOk;
        }

        // ---- Markdown 요약 ----
        private string BuildMarkdownReport(List<TestResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# RE Self-Test Summary");
            sb.AppendLine();
            sb.AppendLine("Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("| # | Spec | Status | Bbox | Fillet | Hole | Boss | Slit | Pattern |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

            int idx = 1;
            foreach (var r in results)
            {
                string status = r.OverallPass ? "✓ PASS" : "✗ FAIL";
                sb.AppendFormat("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |\n",
                    idx, r.SpecName, status,
                    GetCheck(r, "bbox"),
                    GetCheck(r, "fillet"),
                    GetCheck(r, "hole"),
                    GetCheck(r, "boss"),
                    GetCheck(r, "slit"),
                    GetCheck(r, "pattern"));
                idx++;
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Detailed results");
            sb.AppendLine();
            foreach (var r in results)
            {
                sb.AppendLine("### " + r.SpecName);
                sb.AppendLine("Description: " + r.Description);
                sb.AppendLine();
                sb.AppendLine("Expected:");
                foreach (var kv in r.ExpectedParams)
                    sb.AppendFormat("  - {0} = {1}\n", kv.Key, kv.Value);
                sb.AppendLine();
                if (r.Graph != null)
                {
                    sb.AppendLine("Extracted:");
                    sb.AppendFormat("  - bbox = {0:F2} × {1:F2} × {2:F2} mm\n",
                        r.Graph.BboxSizeMm[0], r.Graph.BboxSizeMm[1], r.Graph.BboxSizeMm[2]);
                    sb.AppendFormat("  - faces = {0} (", r.Graph.Faces.Count);
                    var counts = new List<string>();
                    foreach (var kv in r.Graph.FaceTypeCounts)
                        counts.Add(string.Format("{0}={1}", kv.Key, kv.Value));
                    sb.AppendLine(string.Join(", ", counts) + ")");
                    sb.AppendFormat("  - walls = {0}\n", r.Graph.Walls.Count);
                    sb.AppendFormat("  - fillets = {0} (chains: {1})\n", r.Graph.Fillets.Count, r.Graph.FilletChains.Count);
                    sb.AppendFormat("  - holes = {0}\n", r.Graph.Holes.Count);
                    sb.AppendFormat("  - bosses = {0}\n", r.Graph.Bosses.Count);
                    sb.AppendFormat("  - slits = {0}\n", r.Graph.Slits.Count);
                }
                sb.AppendLine();
                sb.AppendLine("Checks:");
                foreach (var kv in r.Checks)
                    sb.AppendFormat("  - **{0}**: {1}\n", kv.Key, kv.Value);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GetCheck(TestResult r, string name)
        {
            if (!r.Checks.TryGetValue(name, out var v)) return "—";
            if (v.StartsWith("PASS")) return "✓";
            if (v.StartsWith("FAIL")) return "✗";
            return "?";
        }
    }
}
