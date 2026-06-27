using System;
using System.Collections.Generic;

using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer.Generation
{
    /// <summary>
    /// P3 (FROM_SCRATCH_ROADMAP.md): a STABLE feature identity that survives regeneration.
    ///
    /// The FeatureExtractor assigns positional IDs ("H"+holeId, HoleDetector.cs:113) by
    /// face-iteration order, so inserting an earlier-stage feature RENUMBERS all later ones —
    /// "make hole H3 bigger" then hits the wrong hole after a grille array is added. A handle
    /// instead keys a feature by its PHYSICAL anchor (creation stage + kind + position + axis +
    /// nominal size), which a regeneration preserves, and a resolver maps the handle back to the
    /// CURRENT positional FeatureGraph node by proximity+size match.
    ///
    /// Handles are minted at GENERATION time (we know each feature's stage + intended anchor
    /// from PhoneParameters) and persisted in the SessionContext HandleRegistry, so MCP edits
    /// (set_parameter / change_*) address a stable handle, never a drifting positional ID.
    /// </summary>
    public class FeatureHandle
    {
        public string HandleId;          // stable, human-meaningful, e.g. "S06.hole.0"
        public string Kind;              // "hole" | "boss" | "wall" | "fillet"
        public string Stage;             // creation stage, e.g. "S06"
        public double[] AnchorMm;        // intended centre at creation (x,y,z)
        public double[] Axis;            // intended axis (unit)
        public double NominalSizeMm;     // diameter (hole/boss) or thickness (wall)

        public FeatureHandle() { }

        public FeatureHandle(string stage, string kind, int ordinal,
                             double[] anchorMm, double[] axis, double nominalSizeMm)
        {
            Stage = stage; Kind = kind;
            HandleId = stage + "." + kind + "." + ordinal;
            AnchorMm = anchorMm; Axis = axis; NominalSizeMm = nominalSizeMm;
        }

        /// <summary>
        /// Squared PERPENDICULAR-to-axis distance (mm²) from this handle's anchor to a point.
        /// A hole/boss is identified by its AXIS LINE, not a point — the extractor may report
        /// the anchor at the cylinder centre/base (different Z) while the handle anchors at the
        /// top face, so a full 3D distance falsely rejects. Project the displacement onto the
        /// plane perpendicular to Axis and measure only that (Z-along-axis is irrelevant).
        /// </summary>
        public double Dist2(double[] posMm)
        {
            if (AnchorMm == null || posMm == null || posMm.Length < 3) return double.MaxValue;
            double dx = posMm[0] - AnchorMm[0], dy = posMm[1] - AnchorMm[1], dz = posMm[2] - AnchorMm[2];
            double[] a = (Axis != null && Axis.Length >= 3) ? Axis : new double[] { 0, 0, 1 };
            double amag = Math.Sqrt(a[0] * a[0] + a[1] * a[1] + a[2] * a[2]);
            if (amag < 1e-9) return dx * dx + dy * dy + dz * dz;
            double ax = a[0] / amag, ay = a[1] / amag, az = a[2] / amag;
            double along = dx * ax + dy * ay + dz * az;           // component along the axis
            double px = dx - along * ax, py = dy - along * ay, pz = dz - along * az;
            return px * px + py * py + pz * pz;                   // perpendicular distance²
        }
    }

    /// <summary>
    /// The per-session map of stable handles, owned by SessionContext. Built at generation
    /// time; resolved against the live FeatureGraph on demand for MCP edits.
    /// </summary>
    public class HandleRegistry
    {
        private readonly List<FeatureHandle> _handles = new List<FeatureHandle>();

        public void Clear() { _handles.Clear(); }
        public void Add(FeatureHandle h) { if (h != null) _handles.Add(h); }
        public IList<FeatureHandle> All { get { return _handles; } }

        public FeatureHandle ByHandleId(string handleId)
        {
            foreach (var h in _handles)
                if (string.Equals(h.HandleId, handleId, StringComparison.OrdinalIgnoreCase)) return h;
            return null;
        }

        /// <summary>
        /// Resolve a handle to the CURRENT positional FeatureGraph hole ID by nearest anchor +
        /// matching diameter. Returns null if no hole matches within tolerance (the feature was
        /// removed or the graph drifted beyond tol). posTolMm defaults to a generous 2mm; the
        /// diameter must match within 10%.
        /// </summary>
        public string ResolveHoleId(FeatureGraph graph, string handleId, double posTolMm = 2.0)
        {
            var h = ByHandleId(handleId);
            if (h == null || graph == null || graph.Holes == null) return null;
            string bestId = null; double best2 = posTolMm * posTolMm;
            foreach (var hole in graph.Holes)
            {
                if (hole.PositionMm == null) continue;
                if (h.NominalSizeMm > 0 && Math.Abs(hole.DiameterMm - h.NominalSizeMm) > 0.1 * h.NominalSizeMm)
                    continue;
                double d2 = h.Dist2(hole.PositionMm);
                if (d2 <= best2) { best2 = d2; bestId = hole.Id; }
            }
            return bestId;
        }

        /// <summary>Resolve a handle to the current positional boss ID (nearest anchor + diameter).</summary>
        public string ResolveBossId(FeatureGraph graph, string handleId, double posTolMm = 2.0)
        {
            var h = ByHandleId(handleId);
            if (h == null || graph == null || graph.Bosses == null) return null;
            string bestId = null; double best2 = posTolMm * posTolMm;
            foreach (var boss in graph.Bosses)
            {
                if (boss.BasePositionMm == null) continue;
                if (h.NominalSizeMm > 0 && Math.Abs(boss.DiameterMm - h.NominalSizeMm) > 0.1 * h.NominalSizeMm)
                    continue;
                double d2 = h.Dist2(boss.BasePositionMm);
                if (d2 <= best2) { best2 = d2; bestId = boss.Id; }
            }
            return bestId;
        }
    }
}
