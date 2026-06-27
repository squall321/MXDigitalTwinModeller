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
    /// Result bag returned by <see cref="RealModelPipeline.Run"/>.
    /// All fields are populated best-effort; on failure the partial state is
    /// preserved so callers can show what made it through.
    /// </summary>
    // @lat: [[reverse-engineer#RealModelPipeline]]
    public class RealModelPipelineResult
    {
        /// <summary>Input file path (echoed back for convenience).</summary>
        public string FilePath;

        /// <summary>First DesignBody located in the imported document.</summary>
        public DesignBody ImportedBody;

        /// <summary>FeatureGraph extracted from <see cref="ImportedBody"/>.</summary>
        public FeatureGraph Graph;

        /// <summary>W4-4 multi-body: every DesignBody in the imported assembly, in the
        /// SAME order as <see cref="FeatureGraph.Shells"/> (so AllBodies[i] is the body
        /// that Shells[i] was extracted from). Populated only when ExtractAllBodies=true;
        /// null otherwise. Lets callers do body-TARGETED modification on an assembly's
        /// non-first bodies (gate-proven: body-targeted AddHole on the plate works).</summary>
        public System.Collections.Generic.List<DesignBody> AllBodies;

        /// <summary>Human-readable Markdown report (also written to disk).</summary>
        public string ReportMarkdown;

        /// <summary>Path of the saved Markdown report (null on failure).</summary>
        public string ReportPath;

        /// <summary>Path of the saved FeatureGraph JSON (null on failure).</summary>
        public string JsonPath;

        /// <summary>True iff import + extract + persistence all succeeded.</summary>
        public bool Success;

        /// <summary>Populated when <see cref="Success"/> is false.</summary>
        public string ErrorMessage;
    }

    /// <summary>
    /// End-to-end real-model pipeline:
    ///   1) Validate file (.stp/.step/.scdocx)
    ///   2) Open document (STEP via StepImportService, scdocx via Document.Open)
    ///   3) Run FeatureExtractor
    ///   4) Optionally tint faces (FeatureVisualizer)
    ///   5) Write FeatureGraph JSON + Markdown report
    ///
    /// Designed as a thin orchestration layer over the existing services so
    /// callers (ribbon command, headless script) get a single Run() entrypoint.
    /// </summary>
    // @lat: [[reverse-engineer#RealModelPipeline]]
    public static class RealModelPipeline
    {
        /// <summary>When true, FeatureVisualizer.ApplyColors is invoked after extraction.</summary>
        public static bool ApplyColors = true;

        /// <summary>
        /// Opt-in multi-shell mode. When true, every DesignBody discovered by
        /// <see cref="PartBodyTraversal.FindAllDesignBodies"/> is extracted and
        /// the per-shell graphs are attached to <see cref="FeatureGraph.Shells"/>;
        /// aggregate counts + union bbox are also populated. Defaults to FALSE
        /// so existing 101/101 + 20/20 PASS behavior is preserved bit-for-bit
        /// (top-level graph remains the first-body extraction).
        /// </summary>
        // @lat: [[reverse-engineer#multi-shell]]
        public static bool ExtractAllBodies = false;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // -------------------------------------------------------------------
        // Run
        // -------------------------------------------------------------------
        public static RealModelPipelineResult Run(string filePath, string outputDir)
        {
            var r = new RealModelPipelineResult { FilePath = filePath };

            // ---- 1. Validate input ----
            if (string.IsNullOrEmpty(filePath))
            {
                r.ErrorMessage = "filePath is null or empty";
                return r;
            }
            if (!File.Exists(filePath))
            {
                r.ErrorMessage = "File not found: " + filePath;
                return r;
            }
            string ext = (Path.GetExtension(filePath) ?? "").ToLowerInvariant();
            bool isStep = (ext == ".stp" || ext == ".step");
            // .scdocx = native; .scdoc = legacy native (ANSYS sample library). Both
            // load through Document.Open — SC handles the version discrimination.
            bool isScdocx = (ext == ".scdocx" || ext == ".scdoc");
            if (!isStep && !isScdocx)
            {
                r.ErrorMessage = "Unsupported extension (expected .stp/.step/.scdoc/.scdocx): " + ext;
                return r;
            }

            // ---- 2. Resolve output directory ----
            if (string.IsNullOrEmpty(outputDir))
                outputDir = Path.GetDirectoryName(filePath);
            try
            {
                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                r.ErrorMessage = "Failed to create output dir: " + ex.Message;
                return r;
            }

            // ---- 3. Import + extract ----
            // We track the imported Document so the multi-shell branch can walk
            // every DesignBody (StepImportService only exposes the first one).
            DesignBody body = null;
            FeatureGraph graph = null;
            Document importedDoc = null;

            if (isStep)
            {
                StepImportService.StepImportResult sr;
                try
                {
                    sr = StepImportService.ImportAndExtract(filePath);
                }
                catch (Exception ex)
                {
                    r.ErrorMessage = "StepImportService threw: " + ex.Message;
                    return r;
                }
                if (sr == null || !sr.Success)
                {
                    r.ErrorMessage = (sr != null && sr.ErrorMessage != null)
                        ? sr.ErrorMessage
                        : "StepImportService failed";
                    return r;
                }
                body = sr.ImportedBody;
                graph = sr.ExtractedGraph;
                // Recover the Document from the first body so multi-shell can
                // re-walk it. body.Document is available on DesignBody (IDocObject).
                try { if (body != null) importedDoc = body.Document; }
                catch { /* best-effort */ }
            }
            else // .scdocx
            {
                // Cycle 30 fix (real CAD dim-change demo): use Document.Open(path, null)
                // instead of Document.Load(path) for .scdocx files. In SC's /RunScript
                // host (IronPython), Document.Load opens the doc WITHOUT creating a
                // Window. Any subsequent SaveAs on that doc then throws "Sequence
                // contains no elements" because SC SaveAs assumes there's at least one
                // Window attached. Document.Open creates a Window and produces an
                // equally-usable Document for extraction, so we prefer it here.
                Document doc;
                try
                {
                    doc = Document.Open(filePath, null);
                }
                catch (Exception ex)
                {
                    r.ErrorMessage = "Document.Open failed: " + ex.Message;
                    return r;
                }
                if (doc == null || doc.MainPart == null)
                {
                    r.ErrorMessage = "Document.Open returned no MainPart";
                    return r;
                }
                // Shared traversal: walks doc.MainPart.Bodies AND every nested
                // component AND every other Part in doc.Parts (assembly .scdocx
                // such as samplemodel2.scdoc behaves the same way as assembly STEPs).
                body = PartBodyTraversal.FindFirstDesignBody(doc);
                if (body == null)
                {
                    r.ErrorMessage = "No DesignBody found in scdocx";
                    return r;
                }
                try
                {
                    graph = new FeatureExtractor().Extract(body);
                }
                catch (Exception ex)
                {
                    r.ErrorMessage = "FeatureExtractor.Extract failed: " + ex.Message;
                    return r;
                }
                importedDoc = doc;
            }

            r.ImportedBody = body;
            r.Graph = graph;

            // ---- 3b. Optional multi-shell extraction ----
            // Default (ExtractAllBodies=false): SKIP this block entirely — top-level
            // graph stays exactly as the single-shell pipeline produced it, so the
            // headless 101/101 + 20/20 PASS behavior is preserved.
            //
            // When enabled, walk every DesignBody in the imported document and
            // append per-shell graphs + aggregate counts onto the top-level graph.
            if (ExtractAllBodies && importedDoc != null && graph != null)
            {
                try
                {
                    var allBodies = PartBodyTraversal.FindAllDesignBodies(importedDoc);
                    AttachMultiShell(graph, body, allBodies);
                    r.AllBodies = allBodies; // W4-4: expose parallel body list for targeting
                }
                catch (Exception ex)
                {
                    // Multi-shell is "extra info" — failures must not poison the
                    // base report. Log and continue with first-body-only graph.
                    System.Diagnostics.Debug.WriteLine(
                        "RealModelPipeline: multi-shell extraction failed: " + ex.Message);
                }
            }

            // ---- 4. Visualization (best effort) ----
            if (ApplyColors && body != null && graph != null)
            {
                try
                {
                    FeatureVisualizer.ApplyColors(body, graph);
                }
                catch (Exception ex)
                {
                    // Visualization failures are non-fatal — we still want JSON/report.
                    System.Diagnostics.Debug.WriteLine(
                        "RealModelPipeline: ApplyColors failed: " + ex.Message);
                }
            }

            // ---- 5. Persist JSON ----
            string baseName = SafeBaseName(filePath);
            string jsonPath = Path.Combine(outputDir, baseName + "_features.json");
            try
            {
                FeatureGraphJsonWriter.WriteToFile(graph, jsonPath);
                r.JsonPath = jsonPath;
            }
            catch (Exception ex)
            {
                r.ErrorMessage = "JSON write failed: " + ex.Message;
                return r;
            }

            // ---- 6. Build + persist Markdown report ----
            string md = BuildMarkdownReport(filePath, body, graph);
            string mdPath = Path.Combine(outputDir, baseName + "_report.md");
            try
            {
                File.WriteAllText(mdPath, md, Encoding.UTF8);
                r.ReportMarkdown = md;
                r.ReportPath = mdPath;
            }
            catch (Exception ex)
            {
                r.ErrorMessage = "Report write failed: " + ex.Message;
                return r;
            }

            r.Success = true;
            return r;
        }

        // -------------------------------------------------------------------
        // Multi-shell extraction
        // -------------------------------------------------------------------
        /// <summary>
        /// Populate <paramref name="topGraph"/>.Shells + Aggregate* fields by
        /// running FeatureExtractor on every body in <paramref name="allBodies"/>.
        /// The body that was already extracted (<paramref name="firstBody"/>) is
        /// reused with the existing top-level graph so we don't pay the extraction
        /// cost twice for the first shell.
        ///
        /// Bodies that throw during Extract() are recorded as an empty FeatureGraph
        /// with BodyName == "(extract-failed)" so the caller can still see the
        /// shell count is N even if one of them was malformed.
        /// </summary>
        private static void AttachMultiShell(
            FeatureGraph topGraph,
            DesignBody firstBody,
            List<DesignBody> allBodies)
        {
            if (topGraph == null || allBodies == null || allBodies.Count == 0)
                return;

            var shells = new List<FeatureGraph>(allBodies.Count);
            var extractor = new FeatureExtractor();

            for (int i = 0; i < allBodies.Count; i++)
            {
                var b = allBodies[i];
                if (b == null) continue;

                // Reuse the already-computed top-level graph for the first body.
                // PartBodyTraversal.FindAllDesignBodies returns bodies in the same
                // declared order as FindFirstDesignBody walks, so the first entry
                // SHOULD match firstBody — but compare by reference to be safe.
                if (object.ReferenceEquals(b, firstBody))
                {
                    shells.Add(topGraph);
                    continue;
                }

                FeatureGraph sg;
                try
                {
                    sg = extractor.Extract(b);
                }
                catch (Exception ex)
                {
                    // Sentinel: keep shell count honest, mark which one failed.
                    sg = new FeatureGraph();
                    sg.BodyName = "(extract-failed: " + ex.Message + ")";
                }
                shells.Add(sg);
            }

            topGraph.Shells = shells;
            topGraph.ShellCount = shells.Count;

            // Aggregate counts
            int faceTot = 0, wallTot = 0, filTot = 0, holeTot = 0, bossTot = 0, slitTot = 0;
            double? xmin = null, ymin = null, zmin = null;
            double? xmax = null, ymax = null, zmax = null;

            foreach (var sg in shells)
            {
                if (sg == null) continue;
                faceTot += (sg.Faces != null) ? sg.Faces.Count : 0;
                wallTot += (sg.Walls != null) ? sg.Walls.Count : 0;
                filTot  += (sg.Fillets != null) ? sg.Fillets.Count : 0;
                holeTot += (sg.Holes != null) ? sg.Holes.Count : 0;
                bossTot += (sg.Bosses != null) ? sg.Bosses.Count : 0;
                slitTot += (sg.Slits != null) ? sg.Slits.Count : 0;

                if (sg.BboxMinMm != null && sg.BboxMinMm.Length >= 3
                    && sg.BboxMaxMm != null && sg.BboxMaxMm.Length >= 3)
                {
                    if (!xmin.HasValue || sg.BboxMinMm[0] < xmin.Value) xmin = sg.BboxMinMm[0];
                    if (!ymin.HasValue || sg.BboxMinMm[1] < ymin.Value) ymin = sg.BboxMinMm[1];
                    if (!zmin.HasValue || sg.BboxMinMm[2] < zmin.Value) zmin = sg.BboxMinMm[2];
                    if (!xmax.HasValue || sg.BboxMaxMm[0] > xmax.Value) xmax = sg.BboxMaxMm[0];
                    if (!ymax.HasValue || sg.BboxMaxMm[1] > ymax.Value) ymax = sg.BboxMaxMm[1];
                    if (!zmax.HasValue || sg.BboxMaxMm[2] > zmax.Value) zmax = sg.BboxMaxMm[2];
                }
            }

            topGraph.AggregateFaceCount   = faceTot;
            topGraph.AggregateWallCount   = wallTot;
            topGraph.AggregateFilletCount = filTot;
            topGraph.AggregateHoleCount   = holeTot;
            topGraph.AggregateBossCount   = bossTot;
            topGraph.AggregateSlitCount   = slitTot;

            if (xmin.HasValue && xmax.HasValue)
            {
                topGraph.AggregateBboxMinMm  = new[] { xmin.Value, ymin.Value, zmin.Value };
                topGraph.AggregateBboxMaxMm  = new[] { xmax.Value, ymax.Value, zmax.Value };
                topGraph.AggregateBboxSizeMm = new[]
                {
                    xmax.Value - xmin.Value,
                    ymax.Value - ymin.Value,
                    zmax.Value - zmin.Value,
                };
            }
        }

        // -------------------------------------------------------------------
        // Markdown report builder
        // -------------------------------------------------------------------
        private static string BuildMarkdownReport(string filePath, DesignBody body, FeatureGraph g)
        {
            var sb = new StringBuilder();
            string bodyName = (body != null && body.Name != null) ? body.Name : "(unnamed)";

            sb.AppendLine("# Real Model Analysis Report");
            sb.AppendLine();
            sb.AppendFormat("- **File**: `{0}`", filePath).AppendLine();
            sb.AppendFormat("- **Body**: {0}", bodyName).AppendLine();
            sb.AppendFormat("- **Extracted**: {0}", g.ExtractedAt ?? "").AppendLine();
            sb.AppendFormat("- **Extractor**: {0}", g.ExtractorVersion ?? "").AppendLine();
            sb.AppendLine();

            // Multi-shell section — only emitted when ExtractAllBodies populated Shells.
            // Placed early so it shows up before per-feature tables (which reflect the
            // FIRST shell only). Single-shell runs skip this entirely.
            if (g.Shells != null && g.Shells.Count > 1)
            {
                sb.AppendLine("## Multi-Shell Breakdown");
                sb.AppendLine();
                sb.AppendFormat("- Shells detected: **{0}**", g.ShellCount).AppendLine();
                sb.AppendFormat("- Aggregate faces : **{0}**", g.AggregateFaceCount).AppendLine();
                sb.AppendFormat("- Aggregate walls : **{0}**", g.AggregateWallCount).AppendLine();
                sb.AppendFormat("- Aggregate fillets: **{0}**", g.AggregateFilletCount).AppendLine();
                sb.AppendFormat("- Aggregate holes : **{0}**", g.AggregateHoleCount).AppendLine();
                sb.AppendFormat("- Aggregate bosses: **{0}**", g.AggregateBossCount).AppendLine();
                sb.AppendFormat("- Aggregate slits : **{0}**", g.AggregateSlitCount).AppendLine();
                if (g.AggregateBboxSizeMm != null && g.AggregateBboxSizeMm.Length >= 3)
                {
                    sb.AppendFormat(Inv, "- Union bbox (mm): {0:F2} x {1:F2} x {2:F2}",
                        g.AggregateBboxSizeMm[0],
                        g.AggregateBboxSizeMm[1],
                        g.AggregateBboxSizeMm[2]).AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine("| # | Body | Faces | Walls | Fillets | Holes | Bosses | Slits | BBox (mm) |");
                sb.AppendLine("|---|------|-------|-------|---------|-------|--------|-------|-----------|");
                for (int si = 0; si < g.Shells.Count; si++)
                {
                    var sg = g.Shells[si];
                    if (sg == null) continue;
                    string sName = (sg.BodyName != null) ? sg.BodyName : "(shell " + si + ")";
                    string bbox = "?";
                    if (sg.BboxSizeMm != null && sg.BboxSizeMm.Length >= 3)
                    {
                        bbox = string.Format(Inv, "{0:F1} x {1:F1} x {2:F1}",
                            sg.BboxSizeMm[0], sg.BboxSizeMm[1], sg.BboxSizeMm[2]);
                    }
                    sb.AppendFormat("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} |",
                        si,
                        sName,
                        (sg.Faces != null) ? sg.Faces.Count : 0,
                        (sg.Walls != null) ? sg.Walls.Count : 0,
                        (sg.Fillets != null) ? sg.Fillets.Count : 0,
                        (sg.Holes != null) ? sg.Holes.Count : 0,
                        (sg.Bosses != null) ? sg.Bosses.Count : 0,
                        (sg.Slits != null) ? sg.Slits.Count : 0,
                        bbox).AppendLine();
                }
                sb.AppendLine();
                sb.AppendLine("> Per-feature tables below describe shell #0 (the first DesignBody) only.");
                sb.AppendLine();
            }

            // Bounding box
            sb.AppendLine("## Bounding Box (mm)");
            sb.AppendLine();
            if (g.BboxSizeMm != null && g.BboxSizeMm.Length >= 3)
            {
                sb.AppendFormat(Inv, "- Size  : {0:F2} x {1:F2} x {2:F2}",
                    g.BboxSizeMm[0], g.BboxSizeMm[1], g.BboxSizeMm[2]).AppendLine();
            }
            if (g.BboxMinMm != null && g.BboxMinMm.Length >= 3)
            {
                sb.AppendFormat(Inv, "- Min   : ({0:F2}, {1:F2}, {2:F2})",
                    g.BboxMinMm[0], g.BboxMinMm[1], g.BboxMinMm[2]).AppendLine();
            }
            if (g.BboxMaxMm != null && g.BboxMaxMm.Length >= 3)
            {
                sb.AppendFormat(Inv, "- Max   : ({0:F2}, {1:F2}, {2:F2})",
                    g.BboxMaxMm[0], g.BboxMaxMm[1], g.BboxMaxMm[2]).AppendLine();
            }
            sb.AppendLine();

            // Face counts
            sb.AppendLine("## Faces");
            sb.AppendLine();
            int faceCount = (g.Faces != null) ? g.Faces.Count : 0;
            sb.AppendFormat("- Total: **{0}**", faceCount).AppendLine();
            if (g.FaceTypeCounts != null && g.FaceTypeCounts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| Type | Count |");
                sb.AppendLine("|------|-------|");
                foreach (var kv in g.FaceTypeCounts.OrderByDescending(k => k.Value))
                    sb.AppendFormat("| {0} | {1} |", kv.Key, kv.Value).AppendLine();
            }
            sb.AppendLine();

            // Walls
            sb.AppendLine("## Walls");
            sb.AppendLine();
            int wallCount = (g.Walls != null) ? g.Walls.Count : 0;
            sb.AppendFormat("- Count: **{0}**", wallCount).AppendLine();
            if (wallCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| Id | Face A | Face B | Thickness (mm) | Area (mm^2) |");
                sb.AppendLine("|----|--------|--------|----------------|-------------|");
                foreach (var w in g.Walls)
                {
                    sb.AppendFormat(Inv, "| {0} | {1} | {2} | {3:F3} | {4:F1} |",
                        w.Id, w.FaceA, w.FaceB, w.ThicknessMm, w.AreaMm2).AppendLine();
                }
            }
            sb.AppendLine();

            // Fillets
            sb.AppendLine("## Fillets");
            sb.AppendLine();
            int filletCount = (g.Fillets != null) ? g.Fillets.Count : 0;
            int chainCount = (g.FilletChains != null) ? g.FilletChains.Count : 0;
            sb.AppendFormat("- Count: **{0}**  (chains: {1})", filletCount, chainCount).AppendLine();
            if (filletCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| Id | Face | R (mm) | Snapped R (mm) | Type | Concavity |");
                sb.AppendLine("|----|------|--------|----------------|------|-----------|");
                foreach (var f in g.Fillets)
                {
                    sb.AppendFormat(Inv, "| {0} | {1} | {2:F3} | {3:F3} | {4} | {5} |",
                        f.Id, f.FaceIndex, f.RadiusMm, f.SnappedRadiusMm,
                        f.BlendType ?? "", f.Concavity ?? "").AppendLine();
                }
            }
            if (chainCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("### Fillet Chains");
                sb.AppendLine();
                sb.AppendLine("| Id | R (mm) | Faces | Edges | Corners |");
                sb.AppendLine("|----|--------|-------|-------|---------|");
                foreach (var c in g.FilletChains)
                {
                    sb.AppendFormat(Inv, "| {0} | {1:F3} | {2} | {3} | {4} |",
                        c.Id, c.RadiusMm,
                        (c.FaceIndices != null) ? c.FaceIndices.Count : 0,
                        c.EdgeFilletCount, c.CornerBlendCount).AppendLine();
                }
            }
            sb.AppendLine();

            // Holes
            sb.AppendLine("## Holes");
            sb.AppendLine();
            int holeCount = (g.Holes != null) ? g.Holes.Count : 0;
            sb.AppendFormat("- Count: **{0}**", holeCount).AppendLine();
            if (holeCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| Id | Face | D (mm) | Depth (mm) | Through | Position (mm) |");
                sb.AppendLine("|----|------|--------|------------|---------|---------------|");
                foreach (var h in g.Holes)
                {
                    string pos = "(?, ?, ?)";
                    if (h.PositionMm != null && h.PositionMm.Length >= 3)
                        pos = string.Format(Inv, "({0:F2}, {1:F2}, {2:F2})",
                            h.PositionMm[0], h.PositionMm[1], h.PositionMm[2]);
                    sb.AppendFormat(Inv, "| {0} | {1} | {2:F3} | {3:F3} | {4} | {5} |",
                        h.Id, h.CylinderFaceIndex, h.DiameterMm, h.DepthMm,
                        h.IsThrough ? "yes" : "no", pos).AppendLine();
                }
            }
            sb.AppendLine();

            // Bosses
            sb.AppendLine("## Bosses");
            sb.AppendLine();
            int bossCount = (g.Bosses != null) ? g.Bosses.Count : 0;
            sb.AppendFormat("- Count: **{0}**", bossCount).AppendLine();
            if (bossCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("| Id | Face | D (mm) | H (mm) | Base position (mm) |");
                sb.AppendLine("|----|------|--------|--------|--------------------|");
                foreach (var b in g.Bosses)
                {
                    string pos = "(?, ?, ?)";
                    if (b.BasePositionMm != null && b.BasePositionMm.Length >= 3)
                        pos = string.Format(Inv, "({0:F2}, {1:F2}, {2:F2})",
                            b.BasePositionMm[0], b.BasePositionMm[1], b.BasePositionMm[2]);
                    sb.AppendFormat(Inv, "| {0} | {1} | {2:F3} | {3:F3} | {4} |",
                        b.Id, b.CylinderFaceIndex, b.DiameterMm, b.HeightMm, pos).AppendLine();
                }
            }
            sb.AppendLine();

            // Slits
            int slitCount = (g.Slits != null) ? g.Slits.Count : 0;
            if (slitCount > 0)
            {
                sb.AppendLine("## Slits");
                sb.AppendLine();
                sb.AppendFormat("- Count: **{0}**", slitCount).AppendLine();
                sb.AppendLine();
                sb.AppendLine("| Id | Face A | Face B | Width (mm) | Length (mm) | Aspect |");
                sb.AppendLine("|----|--------|--------|------------|-------------|--------|");
                foreach (var s in g.Slits)
                {
                    sb.AppendFormat(Inv, "| {0} | {1} | {2} | {3:F3} | {4:F3} | {5:F1} |",
                        s.Id, s.FaceA, s.FaceB, s.WidthMm, s.LengthMm, s.AspectRatio).AppendLine();
                }
                sb.AppendLine();
            }

            // Hole patterns
            int hpCount = (g.HolePatterns != null) ? g.HolePatterns.Count : 0;
            int bpCount = (g.BossPatterns != null) ? g.BossPatterns.Count : 0;
            if (hpCount > 0 || bpCount > 0)
            {
                sb.AppendLine("## Patterns");
                sb.AppendLine();
                if (hpCount > 0)
                {
                    sb.AppendFormat("- Hole patterns: **{0}**", hpCount).AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("| Id | Type | Count | R (mm) | Spacing (mm) | Score % |");
                    sb.AppendLine("|----|------|-------|--------|--------------|---------|");
                    foreach (var p in g.HolePatterns)
                    {
                        sb.AppendFormat(Inv, "| {0} | {1} | {2} | {3:F2} | {4:F2} | {5:F0} |",
                            p.Id, p.PatternType, p.Count,
                            p.Radius_mm, p.Spacing_mm, p.ScorePercent).AppendLine();
                    }
                    sb.AppendLine();
                }
                if (bpCount > 0)
                {
                    sb.AppendFormat("- Boss patterns: **{0}**", bpCount).AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("| Id | Type | Count | R (mm) | Spacing (mm) | Score % |");
                    sb.AppendLine("|----|------|-------|--------|--------------|---------|");
                    foreach (var p in g.BossPatterns)
                    {
                        sb.AppendFormat(Inv, "| {0} | {1} | {2} | {3:F2} | {4:F2} | {5:F0} |",
                            p.Id, p.PatternType, p.Count,
                            p.Radius_mm, p.Spacing_mm, p.ScorePercent).AppendLine();
                    }
                    sb.AppendLine();
                }
            }

            // Symmetries
            int symCount = (g.Symmetries != null) ? g.Symmetries.Count : 0;
            if (symCount > 0)
            {
                sb.AppendLine("## Symmetries");
                sb.AppendLine();
                sb.AppendFormat("- Count: **{0}**", symCount).AppendLine();
                sb.AppendLine();
                sb.AppendLine("| Id | Plane normal | Score % |");
                sb.AppendLine("|----|--------------|---------|");
                foreach (var s in g.Symmetries)
                {
                    string n = "(?, ?, ?)";
                    if (s.MirrorPlaneNormal_mm != null && s.MirrorPlaneNormal_mm.Length >= 3)
                        n = string.Format(Inv, "({0:F2}, {1:F2}, {2:F2})",
                            s.MirrorPlaneNormal_mm[0], s.MirrorPlaneNormal_mm[1],
                            s.MirrorPlaneNormal_mm[2]);
                    sb.AppendFormat(Inv, "| {0} | {1} | {2:F0} |",
                        s.Id, n, s.ScorePercent).AppendLine();
                }
                sb.AppendLine();
            }

            // Composites
            int compCount = (g.Composites != null) ? g.Composites.Count : 0;
            if (compCount > 0)
            {
                sb.AppendLine("## Composites");
                sb.AppendLine();
                sb.AppendFormat("- Count: **{0}**", compCount).AppendLine();
                sb.AppendLine();
                sb.AppendLine("| Id | Type | Members | Symmetry |");
                sb.AppendLine("|----|------|---------|----------|");
                foreach (var c in g.Composites)
                {
                    string members = (c.MemberPatternIds != null)
                        ? string.Join(", ", c.MemberPatternIds.ToArray())
                        : "";
                    sb.AppendFormat("| {0} | {1} | {2} | {3} |",
                        c.Id, c.Type ?? "",
                        members, c.SymmetryId ?? "").AppendLine();
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------
        private static string SafeBaseName(string path)
        {
            try { return Path.GetFileNameWithoutExtension(path); }
            catch { return "model"; }
        }

        // Part/Component DFS lives in PartBodyTraversal — single source of truth
        // shared with StepImportService / StepRoundTripRunner / RealModelPipelineSelfTest.
    }
}
