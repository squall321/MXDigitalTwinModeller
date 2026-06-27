using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    /// Round-trip self-test framework (native .scdocx by default).
    ///
    /// Per-iteration lifecycle (cycle 17 — fully isolated from host doc):
    ///   1. tempDoc1 = Document.Create()  — fresh doc, host doc untouched.
    ///   2. body = spec.Builder(tempDoc1.MainPart)  inside WriteBlock.
    ///   3. graphOriginal = extractor.Extract(body).
    ///   4. tempDoc1.SaveAs(scdocxPath)  — retargets ONLY tempDoc1.
    ///   5. Close tempDoc1 windows BEFORE Document.Load so SC's doc registry
    ///      doesn't dedup and hand us back the same instance.
    ///   6. tempDoc2 = Document.Load(scdocxPath)  — distinct instance.
    ///   7. graphReimported = extractor.Extract(first DesignBody in tempDoc2).
    ///   8. ApplyChecks (faces tol, holes/bosses/walls exact, bbox 0.5mm).
    ///   9. Persist both graphs as JSON; close tempDoc2 windows.
    ///
    /// The passed-in `part` is NEVER modified — SelfTestRunner / RoundTripTestRunner
    /// can continue to use it afterwards. If Document.Create() is not available in
    /// the current SC mode the entire scdocx RT block is skipped with a clear log
    /// message and zero PASS results.
    ///
    /// Goal (native mode): validate that "save → reopen → re-extract" preserves the
    /// FeatureGraph. This is necessary scaffolding before pure STEP RT is feasible.
    /// Goal (STEP mode, future): detect when SpaceClaim's STEP translator silently
    /// mutates topology — invalidates downstream RE on customer STEP imports.
    /// </summary>
    // @lat: [[reverse-engineer#StepRoundTripRunner]]
    public class StepRoundTripRunner
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Round-trip file format. "scdocx" (default, native, headless-safe) or "stp"
        /// (true STEP translator — currently hangs in /Headless behind a translator
        /// dialog; left here as a switch for future investigation).
        /// </summary>
        public string Format { get; set; } = "scdocx";

        // -------------------------------------------------------------------
        // Public DTO
        // -------------------------------------------------------------------
        public class StepRoundTripResult
        {
            public string SpecName { get; set; }
            public FeatureGraph GraphOriginal { get; set; }
            public FeatureGraph GraphReimported { get; set; }
            public Dictionary<string, string> Checks { get; set; }
            public bool OverallPass { get; set; }
            public string ErrorMessage { get; set; }

            public StepRoundTripResult()
            {
                Checks = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Default subset — 5 representative specs to keep test time short
        /// (box-only, fillet, thru-hole, boss, multi-hole pattern). Caller may
        /// override before RunAll() to extend coverage.
        /// </summary>
        // Cycle 12 결과: Document.Open(.stp) 가 SC 의 STEP translator dialog 로 인해
        //   /Headless 에서 hang. v252 API 에는 AP203/AP214/AP242 protocol setter 없음
        //   (StepExportOptions 에는 ExportIdentifiers 만 존재). → 우회: Format="scdocx"
        //   기본값. native 포맷이라 dialog 없이 SaveAs/Load 동작 검증됨 (SelfTestRunner 와 동일).
        //   true STEP RT 는 Format="stp" 로 explicit opt-in (현재 headless 에서는 hang).
        public List<string> SpecFilter { get; set; } = new List<string>
        {
            "01_box_only",
            "02_box_fillet_R2",
            "04_box_thru_hole",
            "06_box_boss",
            "22_box_circular_6holes",
        };

        // -------------------------------------------------------------------
        // RunAll
        // -------------------------------------------------------------------
        public List<StepRoundTripResult> RunAll(Part part, string outputDir)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (string.IsNullOrEmpty(outputDir)) throw new ArgumentException("outputDir empty");
            Directory.CreateDirectory(outputDir);

            // Format-driven extension + reopen strategy.
            //   "scdocx" (default): native SpaceClaim doc → Document.Load (no translator).
            //   "stp"             : STEP translator → Document.Open (currently hangs headless).
            string fmt = string.IsNullOrEmpty(Format) ? "scdocx" : Format.ToLowerInvariant();
            string fileExt;
            if (fmt == "stp" || fmt == "step")
            {
                fmt = "stp";
                fileExt = ".stp";
            }
            else
            {
                fmt = "scdocx";
                fileExt = ".scdocx";
            }

            // intermediate files (re-readable post-mortem) live under outputDir/files/.
            string fileSubDir = Path.Combine(outputDir, "files");
            Directory.CreateDirectory(fileSubDir);

            var generator = new TestModelGenerator();
            var allSpecs = generator.CreateAllSpecs();
            var extractor = new FeatureExtractor();
            var results = new List<StepRoundTripResult>();

            // Cycle 17 ROOT CAUSE FIX (2026-06-03):
            //   Earlier approach used `part.Document.SaveAs(savePath)` which RETARGETS
            //   the HOST document's path to savePath. SC's doc registry then sees
            //   Document.Load(samePath) as the SAME instance as the host doc, causing
            //   cross-iteration state corruption ("object is deleted" on specs 2-5).
            //
            //   New approach: ISOLATE each iteration in a SEPARATE temp Document.
            //     - tempDoc1 = Document.Create() — fresh doc, host doc untouched.
            //     - spec.Builder(tempDoc1.MainPart) → body.
            //     - tempDoc1.SaveAs(scdocxPath) — retargets ONLY tempDoc1.
            //     - close tempDoc1 windows BEFORE Document.Load so SC doesn't dedup.
            //     - tempDoc2 = Document.Load(scdocxPath) — new instance.
            //     - extract reimported from tempDoc2's first DesignBody.
            //     - close tempDoc2 windows.
            //   The passed-in `part` is NEVER mutated; SelfTest / RoundTrip can reuse it.
            //
            //   FALLBACK: if Document.Create fails (some SC modes restrict it), skip the
            //   scdocx RT block silently with a clear log message and return 0 results.
            //   We probe Create() once up front rather than per-iteration.
            bool canCreateTempDocs = true;
            try
            {
                // NOTE (2026-06-05): Document.Create() throws NullReferenceException when
                //   wrapped in WriteBlock.ExecuteTask in this SC mode. headless_selftest.py
                //   calls Document.Create() at top level (outside WriteBlock) without issue.
                //   Match that pattern — call directly, no WriteBlock wrapper.
                Document probe = Document.Create();
                if (probe == null)
                {
                    canCreateTempDocs = false;
                }
                else
                {
                    // Close the probe doc's windows; we only needed to confirm Create works.
                    try
                    {
                        var wins = Window.GetWindows(probe);
                        if (wins != null)
                        {
                            foreach (var w in wins)
                            {
                                try { w.Close(); } catch { /* best-effort */ }
                            }
                        }
                    }
                    catch { /* best-effort */ }
                }
            }
            catch (Exception probeEx)
            {
                canCreateTempDocs = false;
                // Stash the probe error on the first synthetic result below.
                var skip = new StepRoundTripResult
                {
                    SpecName = "(scdocx RT skipped)",
                    OverallPass = false,
                    ErrorMessage = "scdocx RT requires Document.Create which is unavailable in this SC mode: "
                                   + probeEx.Message
                };
                results.Add(skip);
            }

            if (!canCreateTempDocs)
            {
                if (results.Count == 0)
                {
                    results.Add(new StepRoundTripResult
                    {
                        SpecName = "(scdocx RT skipped)",
                        OverallPass = false,
                        ErrorMessage = "scdocx RT requires Document.Create which is unavailable in this SC mode"
                    });
                }
                // Still emit the markdown report so callers see why nothing ran.
                try
                {
                    string reportPathSkip = Path.Combine(outputDir, "00_step_roundtrip_summary.md");
                    File.WriteAllText(reportPathSkip, BuildMarkdownReport(results, fmt), Encoding.UTF8);
                }
                catch { /* swallow */ }
                return results;
            }

            foreach (string specName in SpecFilter)
            {
                var result = new StepRoundTripResult { SpecName = specName };

                TestModelGenerator.TestSpec spec = allSpecs.FirstOrDefault(s => s.Name == specName);
                if (spec == null)
                {
                    result.ErrorMessage = "TestModelGenerator spec not found: " + specName;
                    result.OverallPass = false;
                    results.Add(result);
                    continue;
                }

                Document tempDoc1 = null;
                Document tempDoc2 = null;
                DesignBody body = null;
                try
                {
                    // 1) Create an isolated temp doc. Direct call (no WriteBlock — see probe note).
                    tempDoc1 = Document.Create();
                    if (tempDoc1 == null || tempDoc1.MainPart == null)
                    {
                        result.ErrorMessage = "Document.Create() returned null or no MainPart";
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }

                    // 2) Build body inside tempDoc1.MainPart (NOT the host part).
                    WriteBlock.ExecuteTask("STEP-RT build " + specName, () =>
                    {
                        body = spec.Builder(tempDoc1.MainPart);
                    });
                    if (body == null)
                    {
                        result.ErrorMessage = "Builder returned null body";
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }

                    // 3) Extract original graph.
                    var graphOriginal = extractor.Extract(body);
                    result.GraphOriginal = graphOriginal;

                    // 4) SaveAs tempDoc1 → retargets ONLY tempDoc1's path. Host doc safe.
                    string savePath = Path.Combine(fileSubDir, specName + fileExt);
                    try
                    {
                        tempDoc1.SaveAs(savePath);
                    }
                    catch (Exception saveEx)
                    {
                        result.ErrorMessage = "SaveAs(" + fileExt + ") failed: " + saveEx.Message;
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }

                    if (!File.Exists(savePath))
                    {
                        result.ErrorMessage = "Output file was not created: " + savePath;
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }

                    // 5) Close tempDoc1 BEFORE Document.Load so SC doesn't return the same
                    //    instance from its doc registry. body is owned by tempDoc1 and
                    //    becomes invalid after this point — do not touch it again.
                    try
                    {
                        var wins1 = Window.GetWindows(tempDoc1);
                        if (wins1 != null)
                        {
                            foreach (var w in wins1)
                            {
                                try { w.Close(); } catch { /* best-effort */ }
                            }
                        }
                    }
                    catch { /* best-effort */ }
                    tempDoc1 = null;
                    body = null;

                    // 6) Re-open into a fresh tempDoc2 instance.
                    try
                    {
                        if (fmt == "stp")
                        {
                            tempDoc2 = Document.Open(savePath, null);
                        }
                        else
                        {
                            tempDoc2 = Document.Load(savePath);
                        }
                    }
                    catch (Exception openEx)
                    {
                        result.ErrorMessage = (fmt == "stp" ? "Document.Open" : "Document.Load")
                            + "(" + fileExt + ") failed: " + openEx.Message;
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }
                    if (tempDoc2 == null || tempDoc2.MainPart == null)
                    {
                        result.ErrorMessage = "Reopened document is null or has no MainPart";
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }

                    // 7) DFS-walk to first DesignBody — shared with StepImportService.
                    DesignBody importedBody = PartBodyTraversal.FindFirstDesignBody(tempDoc2.MainPart);
                    if (importedBody == null)
                    {
                        result.ErrorMessage = "No DesignBody found in reopened " + fileExt + " document";
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }

                    // 8) Extract reimported graph.
                    FeatureGraph graphReimported;
                    try
                    {
                        graphReimported = extractor.Extract(importedBody);
                    }
                    catch (Exception extEx)
                    {
                        result.ErrorMessage = "FeatureExtractor.Extract (reimported) failed: " + extEx.Message;
                        result.OverallPass = false;
                        results.Add(result);
                        continue;
                    }
                    result.GraphReimported = graphReimported;

                    // 9) Apply topology checks.
                    ApplyChecks(graphOriginal, graphReimported, result);

                    // 10) Persist both graphs as JSON for post-mortem.
                    try
                    {
                        string origJson = Path.Combine(outputDir, specName + "_original.json");
                        FeatureGraphJsonWriter.WriteToFile(graphOriginal, origJson);
                    }
                    catch (Exception jex)
                    {
                        result.Checks["original_json"] = "WARN: JSON write failed - " + jex.Message;
                    }
                    try
                    {
                        string reimpJson = Path.Combine(outputDir, specName + "_reimported.json");
                        FeatureGraphJsonWriter.WriteToFile(graphReimported, reimpJson);
                    }
                    catch (Exception jex)
                    {
                        result.Checks["reimported_json"] = "WARN: JSON write failed - " + jex.Message;
                    }
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = "FRAMEWORK: " + ex.Message;
                    result.OverallPass = false;
                }
                finally
                {
                    // Cleanup: close tempDoc1 windows (if still open after an early failure),
                    // then tempDoc2 windows. NEVER touch the host doc (the passed-in `part`).
                    // Note: each tempDoc was created via Document.Create() so it is by
                    // construction a different instance from the host doc.
                    if (tempDoc1 != null)
                    {
                        try
                        {
                            var wins = Window.GetWindows(tempDoc1);
                            if (wins != null)
                            {
                                foreach (var w in wins)
                                {
                                    try { w.Close(); } catch { /* best-effort */ }
                                }
                            }
                        }
                        catch { /* best-effort */ }
                    }
                    if (tempDoc2 != null)
                    {
                        try
                        {
                            var wins = Window.GetWindows(tempDoc2);
                            if (wins != null)
                            {
                                foreach (var w in wins)
                                {
                                    try { w.Close(); } catch { /* best-effort */ }
                                }
                            }
                        }
                        catch { /* best-effort */ }
                    }
                }

                results.Add(result);
            }

            // Aggregate markdown report.
            try
            {
                string reportPath = Path.Combine(outputDir, "00_step_roundtrip_summary.md");
                File.WriteAllText(reportPath, BuildMarkdownReport(results, fmt), Encoding.UTF8);
            }
            catch { /* report failure shouldn't tank the run */ }

            return results;
        }

        // -------------------------------------------------------------------
        // Checks
        // -------------------------------------------------------------------
        private void ApplyChecks(FeatureGraph orig, FeatureGraph reimp, StepRoundTripResult result)
        {
            bool allOk = true;

            // face_count: tolerate small delta — STEP translator can split/merge faces.
            int fcO = orig.Faces != null ? orig.Faces.Count : 0;
            int fcR = reimp.Faces != null ? reimp.Faces.Count : 0;
            bool faceOk = Math.Abs(fcR - fcO) <= 2;
            result.Checks["face_count"] = faceOk
                ? string.Format(Inv, "PASS (orig={0}, reimp={1}, delta={2})", fcO, fcR, fcR - fcO)
                : string.Format(Inv, "FAIL (orig={0}, reimp={1}, delta={2} > 2)", fcO, fcR, fcR - fcO);
            if (!faceOk) allOk = false;

            // hole_count: exact match.
            int hcO = orig.Holes != null ? orig.Holes.Count : 0;
            int hcR = reimp.Holes != null ? reimp.Holes.Count : 0;
            bool holeOk = hcO == hcR;
            result.Checks["hole_count"] = holeOk
                ? string.Format(Inv, "PASS ({0} holes)", hcO)
                : string.Format(Inv, "FAIL (orig={0}, reimp={1})", hcO, hcR);
            if (!holeOk) allOk = false;

            // boss_count: exact match.
            int bcO = orig.Bosses != null ? orig.Bosses.Count : 0;
            int bcR = reimp.Bosses != null ? reimp.Bosses.Count : 0;
            bool bossOk = bcO == bcR;
            result.Checks["boss_count"] = bossOk
                ? string.Format(Inv, "PASS ({0} bosses)", bcO)
                : string.Format(Inv, "FAIL (orig={0}, reimp={1})", bcO, bcR);
            if (!bossOk) allOk = false;

            // wall_count: exact match.
            int wcO = orig.Walls != null ? orig.Walls.Count : 0;
            int wcR = reimp.Walls != null ? reimp.Walls.Count : 0;
            bool wallOk = wcO == wcR;
            result.Checks["wall_count"] = wallOk
                ? string.Format(Inv, "PASS ({0} walls)", wcO)
                : string.Format(Inv, "FAIL (orig={0}, reimp={1})", wcO, wcR);
            if (!wallOk) allOk = false;

            // bbox: 0.5mm tolerance per axis (after sort, to be orientation-invariant
            // in case STEP changes the principal axes).
            bool bboxOk = true;
            string bboxDetail;
            if (orig.BboxSizeMm == null || reimp.BboxSizeMm == null
                || orig.BboxSizeMm.Length != 3 || reimp.BboxSizeMm.Length != 3)
            {
                bboxOk = false;
                bboxDetail = "bbox arrays missing or wrong length";
            }
            else
            {
                var o = new[] { orig.BboxSizeMm[0], orig.BboxSizeMm[1], orig.BboxSizeMm[2] };
                var r = new[] { reimp.BboxSizeMm[0], reimp.BboxSizeMm[1], reimp.BboxSizeMm[2] };
                Array.Sort(o);
                Array.Sort(r);
                double tol = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    if (Math.Abs(o[i] - r[i]) > tol) { bboxOk = false; break; }
                }
                bboxDetail = string.Format(Inv,
                    "orig {0:F2}/{1:F2}/{2:F2} vs reimp {3:F2}/{4:F2}/{5:F2}",
                    o[0], o[1], o[2], r[0], r[1], r[2]);
            }
            result.Checks["bbox"] = bboxOk
                ? "PASS (" + bboxDetail + ")"
                : "FAIL (" + bboxDetail + ")";
            if (!bboxOk) allOk = false;

            result.OverallPass = allOk;
        }

        // Part/Component DFS lives in PartBodyTraversal — shared with StepImportService.

        // -------------------------------------------------------------------
        // Markdown report
        // -------------------------------------------------------------------
        private string BuildMarkdownReport(List<StepRoundTripResult> results, string fmt)
        {
            var sb = new StringBuilder();
            string title = fmt == "stp"
                ? "# STEP Round-Trip Self-Test Summary"
                : "# Native (.scdocx) Round-Trip Self-Test Summary";
            sb.AppendLine(title);
            sb.AppendLine();
            sb.AppendLine("Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Format: " + fmt);
            sb.AppendLine();
            sb.AppendLine("| # | Spec | Status | Faces | Holes | Bosses | Walls | Bbox |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|");

            int idx = 1;
            foreach (var r in results)
            {
                string status = r.OverallPass ? "PASS" : "FAIL";
                sb.AppendFormat(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} |\n",
                    idx, r.SpecName, status,
                    Glyph(r, "face_count"),
                    Glyph(r, "hole_count"),
                    Glyph(r, "boss_count"),
                    Glyph(r, "wall_count"),
                    Glyph(r, "bbox"));
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
                if (!string.IsNullOrEmpty(r.ErrorMessage))
                {
                    sb.AppendLine();
                    sb.AppendLine("Error: " + r.ErrorMessage);
                }
                if (r.GraphOriginal != null && r.GraphReimported != null)
                {
                    sb.AppendLine();
                    sb.AppendFormat(Inv, "- Original:   faces={0}, holes={1}, bosses={2}, walls={3}, bbox={4:F2}x{5:F2}x{6:F2}\n",
                        r.GraphOriginal.Faces.Count,
                        r.GraphOriginal.Holes.Count,
                        r.GraphOriginal.Bosses.Count,
                        r.GraphOriginal.Walls.Count,
                        r.GraphOriginal.BboxSizeMm[0], r.GraphOriginal.BboxSizeMm[1], r.GraphOriginal.BboxSizeMm[2]);
                    sb.AppendFormat(Inv, "- Reimported: faces={0}, holes={1}, bosses={2}, walls={3}, bbox={4:F2}x{5:F2}x{6:F2}\n",
                        r.GraphReimported.Faces.Count,
                        r.GraphReimported.Holes.Count,
                        r.GraphReimported.Bosses.Count,
                        r.GraphReimported.Walls.Count,
                        r.GraphReimported.BboxSizeMm[0], r.GraphReimported.BboxSizeMm[1], r.GraphReimported.BboxSizeMm[2]);
                }
                sb.AppendLine();
                sb.AppendLine("Checks:");
                foreach (var kv in r.Checks)
                    sb.AppendFormat("  - **{0}**: {1}\n", kv.Key, kv.Value);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string Glyph(StepRoundTripResult r, string key)
        {
            if (r.Checks == null || !r.Checks.TryGetValue(key, out var v)) return "-";
            if (v.StartsWith("PASS")) return "PASS";
            if (v.StartsWith("FAIL")) return "FAIL";
            return "?";
        }
    }
}
