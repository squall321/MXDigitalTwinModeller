using System;
using System.Globalization;
using System.IO;

using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Modeler;
using SpaceClaim.Api.V251.Geometry;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Modeler;
using SpaceClaim.Api.V252.Geometry;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation
{
    /// <summary>
    /// FEA-freeze hop (FROM_SCRATCH_ROADMAP.md). The from-scratch design keeps EVERYTHING on the
    /// single live Parasolid kernel — generation, mod tools, MCP all mutate the SAME body — and
    /// crosses to STEP EXACTLY ONCE, at the FEA boundary, to hand the geometry to the mesher/solver.
    /// This is that single, deliberate crossing.
    ///
    /// The #1 RE risk is that SpaceClaim's STEP translator silently mutates topology (face split/merge,
    /// reversed normals, gap insertion) — which would corrupt downstream FEA. So a freeze is NOT just
    /// "export and hope": it captures the live body's KERNEL-TRUTH fingerprint (volume, bbox, closed-
    /// solid) BEFORE the write, exports STEP, then VERIFIES the artifact:
    ///   1. file exists, is non-trivial, carries the ISO-10303 (STEP) header.
    ///   2. (headless-safe path) the body's pre-freeze fingerprint is recorded alongside so the
    ///      mesher can assert volume/bbox parity after it re-imports.
    ///   3. (opt-in, non-headless) reopenAndVerify=true re-imports the STEP via Document.Open and
    ///      compares volume + bbox within tol — proving the translator preserved the solid.
    ///      NOTE: Document.Open(.stp) HANGS behind a translator dialog in /Headless mode
    ///      (StepRoundTripRunner cycle-12). So reopenAndVerify defaults FALSE and is only for
    ///      interactive sessions; headless gates verify the file + carry the fingerprint forward.
    ///
    /// Caller must already be on the SC API thread (like GenerationService / a WriteBlock task).
    /// </summary>
    public static class FeaFreezeService
    {
        public class FreezeResult
        {
            public bool Success;
            public string Error;
            public string StepPath;          // the path actually written (extension reflects Format)
            public string Format;            // "step" or "scdocx" — the format actually used
            public bool DowngradedFromStep;  // true if STEP was requested but fell back to scdocx (license)
            public long FileBytes;

            // kernel-truth fingerprint captured from the LIVE body before the STEP write.
            public double VolumeMm3;
            public double[] BboxSizeMm;      // sorted ascending (orientation-invariant)
            public bool ClosedSolid;

            // populated only when reopenAndVerify succeeds (non-headless).
            public bool Reopened;
            public double ReopenedVolumeMm3;
            public double[] ReopenedBboxSizeMm;
            public bool FingerprintMatch;
        }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// Freeze <paramref name="body"/> to an FEA-handoff file. Captures the live kernel-truth
        /// fingerprint, writes the artifact, and validates it.
        ///
        /// format: "scdocx" (DEFAULT) writes a native SpaceClaim document — license-free, headless-
        /// safe, reopenable via Document.Load, and kernel-truth-faithful (same Parasolid, no
        /// translator). "step" writes ISO-10303 STEP via part.Export — the universal FEA interchange,
        /// but the ANSYS STUDENT license REFUSES it ("not licensed for the specified operation").
        /// When "step" is requested and the export throws a LICENSE error, we AUTO-FALL-BACK to
        /// scdocx and set DowngradedFromStep so the caller knows the artifact is native, not STEP.
        ///
        /// outPath's extension is normalized to match the format actually written.
        /// reopenAndVerify re-imports and asserts volume/bbox parity — STEP reopen (Document.Open)
        /// HANGS in /Headless (StepRoundTripRunner cycle-12), so it defaults false and is interactive-only.
        /// </summary>
        public static FreezeResult Freeze(
            DesignBody body, string outPath, bool reopenAndVerify = false, string format = "scdocx")
        {
            var r = new FreezeResult();
            if (body == null) { r.Error = "body is null"; return r; }
            if (string.IsNullOrEmpty(outPath)) { r.Error = "outPath is null/empty"; return r; }

            string fmt = string.IsNullOrEmpty(format) ? "scdocx" : format.ToLowerInvariant();
            bool wantStep = (fmt == "step" || fmt == "stp");

            // 1) capture the LIVE kernel-truth fingerprint BEFORE the write.
            try
            {
                r.VolumeMm3 = body.Shape.Volume * 1e9;
                r.ClosedSolid = r.VolumeMm3 > 0 && SafeIsClosed(body);
                r.BboxSizeMm = BboxSortedMm(body);
            }
            catch (Exception ex) { r.Error = "fingerprint capture failed: " + ex.Message; return r; }

            if (!(r.VolumeMm3 > 0))
            { r.Error = "refusing to freeze a zero/negative-volume body (vol=" + r.VolumeMm3.ToString("0.###", Inv) + ")"; return r; }

            Part part = body.Parent as Part;
            if (part == null) { r.Error = "body has no parent Part to export"; return r; }
            string baseNoExt = StripKnownExt(outPath);
            try
            {
                var dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception ex) { r.Error = "could not create output dir: " + ex.Message; return r; }

            // 2) write the artifact. STEP first if requested; auto-fall-back to scdocx on a license error.
            if (wantStep)
            {
                string stepPath = baseNoExt + ".stp";
                try
                {
                    part.Export(PartExportFormat.Step, stepPath, true, null);
                    if (File.Exists(stepPath) && new FileInfo(stepPath).Length >= 256 && HasStepHeader(stepPath))
                    {
                        r.Format = "step"; r.StepPath = stepPath;
                    }
                    else
                    {
                        r.Error = "STEP export produced no valid file"; // fall through to scdocx below
                    }
                }
                catch (Exception ex)
                {
                    bool licenseBlocked = ex.Message.IndexOf("licens", StringComparison.OrdinalIgnoreCase) >= 0;
                    r.Error = "STEP export threw: " + ex.Message + (licenseBlocked ? " (license — falling back to scdocx)" : "");
                    if (!licenseBlocked) return r;   // a non-license failure is a real error
                }

                if (r.Format == "step")
                {
                    r.FileBytes = new FileInfo(r.StepPath).Length;
                    r.Success = true;
                }
                else
                {
                    // license-blocked → downgrade to native scdocx.
                    r.DowngradedFromStep = true;
                    if (!WriteScdocx(part, baseNoExt, r)) return r;
                }
            }
            else
            {
                if (!WriteScdocx(part, baseNoExt, r)) return r;
            }

            // 4) optional reopen + fingerprint parity. scdocx reopens via Document.Load (headless-
            //    safe); STEP via Document.Open which HANGS in /Headless — interactive only.
            if (reopenAndVerify)
            {
                try
                {
                    DesignBody reBody = null;
                    if (r.Format == "scdocx")
                    {
                        Document doc2 = Document.Load(r.StepPath);
                        if (doc2 != null && doc2.MainPart != null)
                            reBody = PartBodyTraversal.FindFirstDesignBody(doc2.MainPart);
                    }
                    else
                    {
                        var imp = StepImportService.ImportAndExtract(r.StepPath);
                        if (imp != null && imp.Success) reBody = imp.ImportedBody;
                        else r.Error = "reopen verify: " + (imp != null ? imp.ErrorMessage : "import returned null");
                    }

                    if (reBody != null)
                    {
                        r.Reopened = true;
                        r.ReopenedVolumeMm3 = reBody.Shape.Volume * 1e9;
                        r.ReopenedBboxSizeMm = BboxSortedMm(reBody);
                        r.FingerprintMatch = FingerprintsMatch(r, 0.5, 0.01);
                    }
                }
                catch (Exception ex) { r.Error = "reopen verify threw: " + ex.Message; }
            }

            return r;
        }

        /// <summary>Write the body's owning document as a native .scdocx (license-free, headless-safe)
        /// and validate the file. Populates r.Format/StepPath/FileBytes/Success on success.</summary>
        private static bool WriteScdocx(Part part, string baseNoExt, FreezeResult r)
        {
            string scPath = baseNoExt + ".scdocx";
            try
            {
                // The generated phone lives in the active document; persisting it IS the freeze.
                // SaveAs retargets that document's path (same as StepRoundTripRunner's tempDoc1).
                part.Document.SaveAs(scPath);
            }
            catch (Exception ex) { r.Error = "scdocx SaveAs threw: " + ex.Message; return false; }

            if (!File.Exists(scPath)) { r.Error = "scdocx file was not created: " + scPath; return false; }
            long bytes;
            try { bytes = new FileInfo(scPath).Length; } catch { bytes = 0; }
            if (bytes < 256) { r.Error = "scdocx file too small (" + bytes + " bytes)"; return false; }

            r.Format = "scdocx"; r.StepPath = scPath; r.FileBytes = bytes; r.Success = true;
            return true;
        }

        /// <summary>Strip a trailing .stp/.step/.scdocx (case-insensitive) so the caller can supply
        /// any of them and we normalize to the format actually written.</summary>
        private static string StripKnownExt(string path)
        {
            string ext = Path.GetExtension(path);
            if (!string.IsNullOrEmpty(ext))
            {
                string e = ext.ToLowerInvariant();
                if (e == ".stp" || e == ".step" || e == ".scdocx")
                    return path.Substring(0, path.Length - ext.Length);
            }
            return path;
        }

        // -------------------------------------------------------------------
        private static bool FingerprintsMatch(FreezeResult r, double bboxTolMm, double volRelTol)
        {
            if (r.ReopenedBboxSizeMm == null || r.BboxSizeMm == null) return false;
            for (int i = 0; i < 3; i++)
                if (Math.Abs(r.BboxSizeMm[i] - r.ReopenedBboxSizeMm[i]) > bboxTolMm) return false;
            double denom = Math.Abs(r.VolumeMm3) > 1e-9 ? Math.Abs(r.VolumeMm3) : 1.0;
            return Math.Abs(r.VolumeMm3 - r.ReopenedVolumeMm3) / denom <= volRelTol;
        }

        private static double[] BboxSortedMm(DesignBody body)
        {
            var bb = body.Shape.GetBoundingBox(Matrix.Identity);
            double[] s =
            {
                GeometryUtils.MetersToMm(bb.MaxCorner.X - bb.MinCorner.X),
                GeometryUtils.MetersToMm(bb.MaxCorner.Y - bb.MinCorner.Y),
                GeometryUtils.MetersToMm(bb.MaxCorner.Z - bb.MinCorner.Z),
            };
            Array.Sort(s);
            return s;
        }

        private static bool SafeIsClosed(DesignBody body)
        {
            try { return body.Shape.IsClosed; } catch { return false; }
        }

        private static bool HasStepHeader(string path)
        {
            try
            {
                // STEP (ISO 10303-21) files begin with "ISO-10303-21;" within the first chunk.
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var sr = new StreamReader(fs))
                {
                    char[] buf = new char[512];
                    int n = sr.Read(buf, 0, buf.Length);
                    string head = new string(buf, 0, Math.Max(0, n));
                    return head.IndexOf("ISO-10303", StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch { return false; }
        }
    }
}
