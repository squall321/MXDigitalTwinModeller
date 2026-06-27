using System;
using System.Collections.Generic;

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
    /// Shared depth-first traversal of the SpaceClaim Part/Component tree, used by
    /// every Reverse-Engineer entrypoint that needs to locate <see cref="DesignBody"/>s
    /// inside an arbitrary document (native .scdocx, imported STEP, etc.).
    ///
    /// WHY this is shared:
    ///   Many real STEP files exported from CAD assemblies (CAx-IF AS1 series,
    ///   ANSYS samplemodel2, OCCT samples, ...) put NO bodies in
    ///   <c>doc.MainPart.Bodies</c>. The top Part is a pure container; the
    ///   actual <see cref="DesignBody"/> instances live one or more levels deep
    ///   inside <see cref="IPart.Components"/> → <see cref="IComponent.Content"/>.
    ///   A traversal that stops at <c>MainPart.Bodies</c> reports 0 faces for
    ///   every such file even though the geometry loaded fine.
    ///
    /// Two helpers are exposed:
    ///   - <see cref="FindFirstDesignBody"/>: depth-first, returns the first
    ///     DesignBody encountered. Used everywhere we currently extract only
    ///     a single shell (StepImportService / RealModelPipeline / SelfTests).
    ///   - <see cref="FindAllDesignBodies"/>: collects EVERY DesignBody, used
    ///     by the future "multi-shell" pipeline.
    ///
    /// Both helpers swallow per-part Bodies/Components exceptions — SpaceClaim
    /// can throw on Parts whose geometry is unloaded or whose owning document
    /// has been partially closed — and continue traversal best-effort.
    /// </summary>
    // @lat: [[reverse-engineer#PartBodyTraversal]]
    public static class PartBodyTraversal
    {
        /// <summary>
        /// Document-level entrypoint: walks the MainPart subtree first (declared
        /// order), then — if nothing was found — every other <see cref="Part"/>
        /// in <see cref="Document.Parts"/>.
        ///
        /// WHY two-phase walk: SpaceClaim's STEP translator for assemblies
        /// (CAx-IF AS1 series, ANSYS samplemodel2, etc.) sometimes leaves the
        /// imported bodies inside internal Parts that are NOT reachable from
        /// <c>MainPart.Components</c>. <c>Document.Parts</c> is documented to
        /// return "all the parts in the document, including the MainPart",
        /// which is the only reliable way to reach those orphaned parts.
        /// </summary>
        public static DesignBody FindFirstDesignBody(Document doc)
        {
            if (doc == null) return null;
            var fromMain = FindFirstDesignBody(doc.MainPart);
            if (fromMain != null) return fromMain;

            // Fallback: scan every Part in the doc directly. doc.Parts contains
            // MainPart too — we already walked that subtree above, but scanning
            // it again is cheap (it won't have bodies if MainPart didn't) and
            // keeps this branch a one-liner.
            try
            {
                foreach (var p in doc.Parts)
                {
                    if (p == null) continue;
                    var db = FindFirstDesignBody((IPart)p);
                    if (db != null) return db;
                }
            }
            catch
            {
                // best-effort: Document.Parts can throw if the doc was
                // half-closed concurrently. Treat as "nothing found".
            }
            return null;
        }

        /// <summary>
        /// Document-level entrypoint that returns EVERY <see cref="DesignBody"/>
        /// in the document, deduplicated. Walks MainPart first, then iterates
        /// <see cref="Document.Parts"/> to pick up any internal parts that
        /// aren't reachable through MainPart.Components.
        /// </summary>
        public static List<DesignBody> FindAllDesignBodies(Document doc)
        {
            var result = new List<DesignBody>();
            if (doc == null) return result;
            var seen = new HashSet<DesignBody>();

            // Phase 1: MainPart subtree.
            foreach (var b in FindAllDesignBodies(doc.MainPart))
            {
                if (b != null && seen.Add(b)) result.Add(b);
            }

            // Phase 2: every Part in the doc — picks up internal parts that
            // aren't reachable from MainPart.Components.
            try
            {
                foreach (var p in doc.Parts)
                {
                    if (p == null) continue;
                    foreach (var b in FindAllDesignBodies((IPart)p))
                    {
                        if (b != null && seen.Add(b)) result.Add(b);
                    }
                }
            }
            catch
            {
                // best-effort
            }
            return result;
        }

        /// <summary>
        /// Depth-first walk that returns the first <see cref="DesignBody"/>
        /// discovered in <paramref name="root"/>, in declared order. Returns
        /// null only if the entire subtree contains zero design bodies.
        ///
        /// Traversal order matches the SC GUI tree:
        ///   1. root.Bodies (cast each to DesignBody)
        ///   2. for each child component (declared order): recurse into
        ///      component.Content
        /// Children are pushed onto the DFS stack in REVERSE so the pop
        /// order is declared order — required by callers that expect the
        /// first body in the part list, not the first body in some arbitrary
        /// component.
        /// </summary>
        public static DesignBody FindFirstDesignBody(IPart root)
        {
            if (root == null) return null;
            var stack = new Stack<IPart>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var p = stack.Pop();

                // 1) Direct bodies on this Part.
                try
                {
                    foreach (var ib in p.Bodies)
                    {
                        var db = ib as DesignBody;
                        if (db != null) return db;
                    }
                }
                catch
                {
                    // Some Parts throw on Bodies if geometry is unloaded.
                    // Ignore and continue into Components below.
                }

                // 2) Recurse into child components (push in reverse for DFS-in-order).
                try
                {
                    var children = new List<IPart>();
                    foreach (var comp in p.Components)
                    {
                        if (comp != null && comp.Content != null)
                            children.Add(comp.Content);
                    }
                    for (int i = children.Count - 1; i >= 0; i--)
                        stack.Push(children[i]);
                }
                catch
                {
                    // best-effort: skip unreadable subtree
                }
            }
            return null;
        }

        /// <summary>
        /// Depth-first walk that returns EVERY <see cref="DesignBody"/> in the
        /// subtree rooted at <paramref name="root"/>, in declared order. Never
        /// returns null — empty subtree yields an empty list. Used by future
        /// multi-shell / multi-part pipelines that want to process all
        /// design bodies in an imported assembly rather than just the first.
        /// </summary>
        public static List<DesignBody> FindAllDesignBodies(IPart root)
        {
            var result = new List<DesignBody>();
            if (root == null) return result;

            // Iterative pre-order traversal so we can swallow per-node errors
            // without losing siblings (recursive impl would propagate the
            // exception past the catch in the parent frame).
            var stack = new Stack<IPart>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var p = stack.Pop();

                // 1) Direct bodies on this Part.
                try
                {
                    foreach (var ib in p.Bodies)
                    {
                        var db = ib as DesignBody;
                        if (db != null) result.Add(db);
                    }
                }
                catch
                {
                    // Some Parts throw on Bodies if geometry is unloaded.
                }

                // 2) Recurse into child components (push reversed → pop in declared order).
                try
                {
                    var children = new List<IPart>();
                    foreach (var comp in p.Components)
                    {
                        if (comp != null && comp.Content != null)
                            children.Add(comp.Content);
                    }
                    for (int i = children.Count - 1; i >= 0; i--)
                        stack.Push(children[i]);
                }
                catch
                {
                    // best-effort: skip unreadable subtree
                }
            }
            return result;
        }
    }
}
