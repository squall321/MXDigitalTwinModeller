using System;
using System.IO;
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
    /// Scaffold for importing a .stp/.step file and extracting its FeatureGraph.
    ///
    /// Phase 0 of the CAD-RE plan: when the user supplies a real CAD model, this
    /// service opens it via SpaceClaim's native STEP translator (Document.Open),
    /// pulls out the first DesignBody (depth-first through Parts/Components), and
    /// runs FeatureExtractor on it.
    ///
    /// Caveats (see plan + risks):
    ///   - Relies on SpaceClaim's STEP translator being configured for .stp/.step.
    ///   - Imported bodies frequently have reversed face normals or multi-shell
    ///     topology; FeatureExtractor / classifier should already handle IsReversed,
    ///     but watch for it when extending.
    /// </summary>
    // @lat: [[reverse-engineer#StepImport]]
    public static class StepImportService
    {
        public class StepImportResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }

            /// <summary>First DesignBody located in the imported document (depth-first).</summary>
            public DesignBody ImportedBody { get; set; }

            /// <summary>FeatureGraph extracted from ImportedBody (null when import failed).</summary>
            public FeatureGraph ExtractedGraph { get; set; }
        }

        // -------------------------------------------------------------------
        // ImportAndExtract
        // -------------------------------------------------------------------
        public static StepImportResult ImportAndExtract(string stepFilePath)
        {
            var result = new StepImportResult();

            if (string.IsNullOrEmpty(stepFilePath))
            {
                result.ErrorMessage = "stepFilePath is null or empty";
                return result;
            }
            if (!File.Exists(stepFilePath))
            {
                result.ErrorMessage = "STEP file not found: " + stepFilePath;
                return result;
            }
            string ext = Path.GetExtension(stepFilePath);
            if (string.IsNullOrEmpty(ext)
                || !(ext.Equals(".stp", StringComparison.OrdinalIgnoreCase)
                  || ext.Equals(".step", StringComparison.OrdinalIgnoreCase)))
            {
                result.ErrorMessage = "Unsupported extension (expected .stp/.step): " + ext;
                return result;
            }

            // ---- Open via SpaceClaim's native translator ----
            // Pattern mirrors ConformalMeshService.ImportStep:
            //     var doc = Document.Open(filePath, null);
            // STEP is supported natively; SpaceClaim infers the importer from extension.
            Document doc;
            try
            {
                doc = Document.Open(stepFilePath, null);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "Document.Open failed: " + ex.Message;
                return result;
            }
            if (doc == null)
            {
                result.ErrorMessage = "Document.Open returned null";
                return result;
            }

            Part mainPart = doc.MainPart;
            if (mainPart == null)
            {
                result.ErrorMessage = "Imported document has no MainPart";
                return result;
            }

            // ---- Depth-first search for the first DesignBody ----
            // CRITICAL: many STEP files (CAx-IF AS1 series, assemblies in general)
            // place NO bodies on doc.MainPart.Bodies — the bodies live inside
            // child Components, OR in internal Parts not reachable through
            // MainPart.Components. The Document-level overload walks MainPart
            // first, then falls back to scanning every Part in the doc.
            DesignBody firstBody = PartBodyTraversal.FindFirstDesignBody(doc);
            if (firstBody == null)
            {
                result.ErrorMessage = "No DesignBody found in imported STEP (assembly empty or only contains components/surfaces)";
                return result;
            }
            result.ImportedBody = firstBody;

            // ---- Run FeatureExtractor ----
            try
            {
                var extractor = new FeatureExtractor();
                result.ExtractedGraph = extractor.Extract(firstBody);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "FeatureExtractor.Extract failed: " + ex.Message;
                return result;
            }

            result.Success = true;
            return result;
        }

        // Part/Component DFS lives in PartBodyTraversal so STEP import,
        // RealModelPipeline, and the round-trip / self-test runners all share
        // a single implementation (was previously duplicated 3x).
    }
}
