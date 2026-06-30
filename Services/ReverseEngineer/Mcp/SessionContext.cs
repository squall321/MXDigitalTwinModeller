using System;
using System.Linq;

using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Mcp
{
    /// <summary>
    /// The single, process-lived interactive design session backing the MCP server.
    /// Holds the working DesignBody + its FeatureGraph so a sequence of MCP tool calls
    /// (get_feature_graph -> change_hole_diameter -> get_feature_graph ...) all hit the
    /// SAME in-memory body, unlike the matrix harness which was one-shot per process.
    ///
    /// All access MUST be on the SC API thread (resolve the body, re-extract the graph)
    /// via ApiThreadMarshaller — DesignBody/FeatureExtractor are not thread-safe.
    /// </summary>
    public sealed class SessionContext
    {
        private static readonly SessionContext _instance = new SessionContext();
        public static SessionContext Instance { get { return _instance; } }
        private SessionContext() { }

        private readonly object _gate = new object();
        public DesignBody Body { get; private set; }
        public FeatureGraph Graph { get; private set; }
        public string SourcePath { get; private set; }

        // P7: the from-scratch parametric design held BESIDE the body — the single source of
        // truth. MCP set_parameter edits this, then regenerates, so the body never drifts.
        public PhoneParameters Params { get; private set; }
        public Generation.HandleRegistry Handles { get; private set; }

        /// <summary>
        /// P7: generate a phone from the given parameters into a fresh document, binding the
        /// result + its handles + a freshly-extracted graph into the session. API-thread only,
        /// inside a WriteBlock (GenerationService mutates geometry).
        /// </summary>
        public string GeneratePhone(PhoneParameters p)
        {
            return GeneratePhone(p, null);
        }

        /// <summary>Generate, optionally HALTING after a named stage (S00 base slab, S00a curved
        /// back, S00b hollow, S04 pocket, S05 camera, S06 holes, S07 ports, S08 grille). null =
        /// full build. Lets the LLM ask for "just the base slab" or an incremental/partial part.</summary>
        public string GeneratePhone(PhoneParameters p, string stopAtStage)
        {
            try
            {
                Document.Create();
                Part part = Window.ActiveWindow.Document.MainPart;
                var gs = new Generation.GenerationService();
                var res = gs.Generate(part, p, stopAtStage);
                if (!res.Success || res.Body == null)
                    return "generation failed: " + (res.Error ?? "(no body)");
                lock (_gate)
                {
                    Params = p;
                    Body = res.Body;
                    Handles = res.Handles;
                    Graph = new FeatureExtractor().Extract(res.Body);
                }
                return null; // success
            }
            catch (Exception ex) { return "GeneratePhone exception: " + ex.Message; }
        }

        /// <summary>
        /// Resolve the active DesignBody from the open window and (re)extract its FeatureGraph.
        /// MUST be called on the API thread. Returns false if no active DesignBody exists.
        /// </summary>
        public bool BindActiveBody(out string error)
        {
            error = null;
            try
            {
                var win = Window.ActiveWindow;
                if (win == null || win.Document == null) { error = "no active window/document"; return false; }

                // STEP/assembly imports nest the body in Components→Content, NOT MainPart.Bodies
                // directly (PartBodyTraversal.cs:22-25). Use the project's depth-first finder so
                // assemblies like as1-oc resolve to their first DesignBody.
                DesignBody body = PartBodyTraversal.FindFirstDesignBody(win.Document);
                if (body == null) { error = "no DesignBody in active document"; return false; }

                lock (_gate)
                {
                    Body = body;
                    Graph = new FeatureExtractor().Extract(body);
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>Re-extract the graph after a successful mutation. API-thread only.</summary>
        public void RefreshGraph()
        {
            lock (_gate)
            {
                if (Body != null) Graph = new FeatureExtractor().Extract(Body);
            }
        }

        public void SetSource(string path) { lock (_gate) { SourcePath = path; } }
    }
}
