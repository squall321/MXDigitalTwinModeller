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
    /// Headless verification of the full real-model pipeline using SYNTHETIC
    /// .scdocx files generated from <see cref="TestModelGenerator"/>.
    ///
    /// Goal: prove that AnalyzeRealModelCommand's underlying pipeline
    /// (file → import → extract → visualize → JSON+report) works end-to-end
    /// BEFORE the user supplies a real STEP. When the customer model arrives,
    /// the only unknown is the geometry itself — the plumbing is already
    /// validated.
    ///
    /// Per spec lifecycle (mirrors StepRoundTripRunner cycle 17 isolation):
    ///   1. tempDoc = Document.Create()             — fresh doc, host untouched.
    ///   2. body    = spec.Builder(tempDoc.MainPart) inside WriteBlock.
    ///   3. baseline = FeatureExtractor.Extract(body) — used as ground truth.
    ///   4. tempDoc.SaveAs(outputDir/files/{spec}.scdocx).
    ///   5. Close tempDoc windows so RealModelPipeline.Run gets a NEW instance
    ///      via Document.Load (avoids SC doc-registry dedup / path-retarget).
    ///   6. RealModelPipeline.Run(scdocxPath, outputDir/reports/).
    ///   7. Checks:
    ///        - PipelineResult.Success == true
    ///        - PipelineResult.Graph != null && Graph.Faces.Count > 0
    ///        - JSON + Markdown report files exist on disk
    ///        - reimported face count within 2 of baseline (matches RT tol)
    ///   8. Close any docs opened by RealModelPipeline.Run.
    ///
    /// The passed-in `hostPart` is NEVER mutated. If Document.Create() is not
    /// available in the current SC mode the entire RealModel SelfTest block is
    /// skipped with a clear message and zero results — same fallback as STEP RT.
    /// </summary>
    // @lat: [[reverse-engineer#RealModelPipelineSelfTest]]
    public class RealModelPipelineSelfTest
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Default subset — keep test time short while exercising every major
        /// feature category (box-only, fillet, thru-hole, boss, phone-realistic).
        /// Caller may override before <see cref="RunAll"/>.
        /// </summary>
        public List<string> SpecFilter { get; set; } = new List<string>
        {
            "01_box_only",
            "02_box_fillet_R2",
            "04_box_thru_hole",
            "06_box_boss",
            "56_phone_realistic_v2",
        };

        public class Result
        {
            public string SpecName;
            public string GeneratedFilePath;
            public RealModelPipelineResult PipelineResult;
            public Dictionary<string, string> Checks;
            public bool OverallPass;
            public string ErrorMessage;

            public Result()
            {
                Checks = new Dictionary<string, string>();
            }
        }

        public List<Result> RunAll(Part hostPart, string outputDir)
        {
            if (hostPart == null) throw new ArgumentNullException(nameof(hostPart));
            if (string.IsNullOrEmpty(outputDir)) throw new ArgumentException("outputDir empty");

            Directory.CreateDirectory(outputDir);
            string filesDir = Path.Combine(outputDir, "files");
            string reportsDir = Path.Combine(outputDir, "reports");
            Directory.CreateDirectory(filesDir);
            Directory.CreateDirectory(reportsDir);

            var results = new List<Result>();

            // --- Probe Document.Create up front (same pattern as StepRoundTripRunner) ---
            // NOTE (2026-06-05): Document.Create() throws NullReferenceException inside
            //   WriteBlock.ExecuteTask. Call directly at top level (matches headless_selftest.py).
            bool canCreateTempDocs = true;
            try
            {
                Document probe = Document.Create();
                if (probe == null)
                {
                    canCreateTempDocs = false;
                }
                else
                {
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
                results.Add(new Result
                {
                    SpecName = "(RealModel SelfTest skipped)",
                    OverallPass = false,
                    ErrorMessage = "RealModel SelfTest requires Document.Create which is unavailable in this SC mode: "
                                   + probeEx.Message
                });
            }

            if (!canCreateTempDocs)
            {
                if (results.Count == 0)
                {
                    results.Add(new Result
                    {
                        SpecName = "(RealModel SelfTest skipped)",
                        OverallPass = false,
                        ErrorMessage = "RealModel SelfTest requires Document.Create which is unavailable in this SC mode"
                    });
                }
                TryWriteSummary(results, outputDir);
                return results;
            }

            var generator = new TestModelGenerator();
            var allSpecs = generator.CreateAllSpecs();
            var extractor = new FeatureExtractor();

            foreach (string specName in SpecFilter)
            {
                var r = new Result { SpecName = specName };

                TestModelGenerator.TestSpec spec = allSpecs.FirstOrDefault(s => s.Name == specName);
                if (spec == null)
                {
                    r.ErrorMessage = "TestModelGenerator spec not found: " + specName;
                    r.OverallPass = false;
                    results.Add(r);
                    continue;
                }

                Document tempDoc = null;
                DesignBody body = null;
                int baselineFaceCount = 0;
                string scdocxPath = Path.Combine(filesDir, specName + ".scdocx");

                try
                {
                    // 1) Build the synthetic scdocx inside its own isolated tempDoc.
                    //    Document.Create() called directly (no WriteBlock — see probe note).
                    tempDoc = Document.Create();
                    if (tempDoc == null || tempDoc.MainPart == null)
                    {
                        r.ErrorMessage = "Document.Create() returned null or no MainPart";
                        r.OverallPass = false;
                        results.Add(r);
                        continue;
                    }

                    WriteBlock.ExecuteTask("RealModel-SelfTest build " + specName, () =>
                    {
                        body = spec.Builder(tempDoc.MainPart);
                    });
                    if (body == null)
                    {
                        r.ErrorMessage = "Builder returned null body";
                        r.OverallPass = false;
                        results.Add(r);
                        continue;
                    }

                    // 2) Compute baseline (face count for delta check) before SaveAs.
                    try
                    {
                        var baseline = extractor.Extract(body);
                        baselineFaceCount = (baseline != null && baseline.Faces != null)
                            ? baseline.Faces.Count
                            : 0;
                    }
                    catch (Exception bex)
                    {
                        r.ErrorMessage = "Baseline FeatureExtractor.Extract failed: " + bex.Message;
                        r.OverallPass = false;
                        results.Add(r);
                        continue;
                    }

                    // 3) SaveAs tempDoc → only tempDoc gets its path retargeted.
                    try
                    {
                        tempDoc.SaveAs(scdocxPath);
                    }
                    catch (Exception saveEx)
                    {
                        r.ErrorMessage = "SaveAs(.scdocx) failed: " + saveEx.Message;
                        r.OverallPass = false;
                        results.Add(r);
                        continue;
                    }

                    if (!File.Exists(scdocxPath))
                    {
                        r.ErrorMessage = "Output .scdocx was not created: " + scdocxPath;
                        r.OverallPass = false;
                        results.Add(r);
                        continue;
                    }
                    r.GeneratedFilePath = scdocxPath;

                    // 4) Close tempDoc BEFORE RealModelPipeline.Run so SC doesn't
                    //    return the same instance from its registry when the pipeline
                    //    calls Document.Load. body is owned by tempDoc and becomes
                    //    invalid after this point.
                    try
                    {
                        var wins = Window.GetWindows(tempDoc);
                        if (wins != null)
                        {
                            foreach (var w in wins)
                            {
                                try { w.Close(); } catch { /* best-effort */ }
                            }
                        }
                    }
                    catch { /* best-effort */ }
                    tempDoc = null;
                    body = null;

                    // 5) Run the real-model pipeline against the synthetic file.
                    //    Disable visualization to avoid mutating the reopened body
                    //    (we only care that the JSON + report land on disk).
                    bool prevApplyColors = RealModelPipeline.ApplyColors;
                    RealModelPipelineResult pr = null;
                    try
                    {
                        RealModelPipeline.ApplyColors = false;
                        pr = RealModelPipeline.Run(scdocxPath, reportsDir);
                    }
                    catch (Exception runEx)
                    {
                        r.ErrorMessage = "RealModelPipeline.Run threw: " + runEx.Message;
                        r.OverallPass = false;
                        results.Add(r);
                        continue;
                    }
                    finally
                    {
                        RealModelPipeline.ApplyColors = prevApplyColors;
                    }

                    r.PipelineResult = pr;

                    // 6) Checks.
                    ApplyChecks(r, pr, baselineFaceCount);

                    // 7) Close the doc opened by RealModelPipeline.Run (if any) so
                    //    subsequent specs don't trip the doc-registry dedup path.
                    if (pr != null && pr.ImportedBody != null)
                    {
                        try
                        {
                            var ownerDoc = pr.ImportedBody.Document;
                            if (ownerDoc != null)
                            {
                                var wins = Window.GetWindows(ownerDoc);
                                if (wins != null)
                                {
                                    foreach (var w in wins)
                                    {
                                        try { w.Close(); } catch { /* best-effort */ }
                                    }
                                }
                            }
                        }
                        catch { /* best-effort */ }
                    }
                }
                catch (Exception ex)
                {
                    r.ErrorMessage = "FRAMEWORK: " + ex.Message;
                    r.OverallPass = false;
                }
                finally
                {
                    // Cleanup: close tempDoc windows if still open after an early failure.
                    if (tempDoc != null)
                    {
                        try
                        {
                            var wins = Window.GetWindows(tempDoc);
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

                results.Add(r);
            }

            TryWriteSummary(results, outputDir);
            return results;
        }

        // -------------------------------------------------------------------
        // Checks
        // -------------------------------------------------------------------
        private void ApplyChecks(Result r, RealModelPipelineResult pr, int baselineFaceCount)
        {
            bool allOk = true;

            // pipeline_success
            if (pr == null)
            {
                r.Checks["pipeline_success"] = "FAIL (result is null)";
                r.OverallPass = false;
                return;
            }
            bool successOk = pr.Success;
            r.Checks["pipeline_success"] = successOk
                ? "PASS"
                : "FAIL (" + (pr.ErrorMessage ?? "(no message)") + ")";
            if (!successOk) allOk = false;

            // graph_present
            bool graphOk = pr.Graph != null;
            int faceCount = (graphOk && pr.Graph.Faces != null) ? pr.Graph.Faces.Count : 0;
            r.Checks["graph_present"] = graphOk
                ? string.Format(Inv, "PASS (faces={0})", faceCount)
                : "FAIL (Graph is null)";
            if (!graphOk) allOk = false;

            // faces_positive
            bool facesOk = faceCount > 0;
            r.Checks["faces_positive"] = facesOk
                ? "PASS"
                : "FAIL (Graph.Faces.Count == 0)";
            if (!facesOk) allOk = false;

            // json_exists
            string jsonPath = (pr.JsonPath ?? "");
            bool jsonOk = !string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath);
            r.Checks["json_exists"] = jsonOk
                ? "PASS (" + jsonPath + ")"
                : "FAIL (path=" + jsonPath + ", exists=" + (File.Exists(jsonPath ?? "") ? "y" : "n") + ")";
            if (!jsonOk) allOk = false;

            // report_exists
            string mdPath = (pr.ReportPath ?? "");
            bool mdOk = !string.IsNullOrEmpty(mdPath) && File.Exists(mdPath);
            r.Checks["report_exists"] = mdOk
                ? "PASS (" + mdPath + ")"
                : "FAIL (path=" + mdPath + ", exists=" + (File.Exists(mdPath ?? "") ? "y" : "n") + ")";
            if (!mdOk) allOk = false;

            // face_count_delta vs baseline (built BEFORE SaveAs)
            int delta = faceCount - baselineFaceCount;
            bool deltaOk = Math.Abs(delta) <= 2;
            r.Checks["face_count_delta"] = deltaOk
                ? string.Format(Inv, "PASS (baseline={0}, reimp={1}, delta={2})",
                    baselineFaceCount, faceCount, delta)
                : string.Format(Inv, "FAIL (baseline={0}, reimp={1}, delta={2} > 2)",
                    baselineFaceCount, faceCount, delta);
            if (!deltaOk) allOk = false;

            r.OverallPass = allOk;
        }

        // -------------------------------------------------------------------
        // Markdown summary
        // -------------------------------------------------------------------
        private void TryWriteSummary(List<Result> results, string outputDir)
        {
            try
            {
                string path = Path.Combine(outputDir, "00_real_model_selftest_summary.md");
                File.WriteAllText(path, BuildSummary(results), Encoding.UTF8);
            }
            catch { /* report failure shouldn't tank the run */ }
        }

        private string BuildSummary(List<Result> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Real Model Pipeline Self-Test Summary");
            sb.AppendLine();
            sb.AppendLine("Generated at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("| # | Spec | Status | Success | Graph | Faces | JSON | Report | Delta |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|");

            int idx = 1;
            foreach (var r in results)
            {
                string status = r.OverallPass ? "PASS" : "FAIL";
                sb.AppendFormat(Inv, "| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |\n",
                    idx, r.SpecName, status,
                    Glyph(r, "pipeline_success"),
                    Glyph(r, "graph_present"),
                    Glyph(r, "faces_positive"),
                    Glyph(r, "json_exists"),
                    Glyph(r, "report_exists"),
                    Glyph(r, "face_count_delta"));
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
                if (!string.IsNullOrEmpty(r.GeneratedFilePath))
                    sb.AppendLine("- generated: `" + r.GeneratedFilePath + "`");
                if (r.PipelineResult != null)
                {
                    if (!string.IsNullOrEmpty(r.PipelineResult.JsonPath))
                        sb.AppendLine("- json:      `" + r.PipelineResult.JsonPath + "`");
                    if (!string.IsNullOrEmpty(r.PipelineResult.ReportPath))
                        sb.AppendLine("- report:    `" + r.PipelineResult.ReportPath + "`");
                }
                if (!string.IsNullOrEmpty(r.ErrorMessage))
                    sb.AppendLine("- error: " + r.ErrorMessage);
                sb.AppendLine();
                sb.AppendLine("Checks:");
                foreach (var kv in r.Checks)
                    sb.AppendFormat("  - **{0}**: {1}\n", kv.Key, kv.Value);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string Glyph(Result r, string key)
        {
            if (r.Checks == null || !r.Checks.TryGetValue(key, out var v)) return "-";
            if (v.StartsWith("PASS")) return "PASS";
            if (v.StartsWith("FAIL")) return "FAIL";
            return "?";
        }
    }
}
