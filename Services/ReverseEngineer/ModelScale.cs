using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Per-body characteristic length scale.
    ///
    /// Why this exists:
    ///   Most absolute-mm thresholds across the reverse-engineering pipeline
    ///   (groove vs hole, slit thickness, wall thickness, pattern tolerance,
    ///    fillet radius snapping...) were calibrated for desktop-CAD bodies
    ///   where the bounding box is on the order of ~100 mm. They silently
    ///   fail when applied to micrometer-scale (MEMS) or meter-scale (large
    ///   assembly) geometry. By computing a model-wide Scale once and
    ///   expressing every threshold as a RATIO of the relevant scale value,
    ///   the detectors become self-calibrating.
    ///
    /// Anchors:
    ///   - MaxBboxDimMm    : longest AABB edge — anchor for "feature size"
    ///                       (hole depth, boundary tolerance, ...).
    ///   - MinBboxDimMm    : shortest AABB edge — anchor for "wall / slit
    ///                       thickness" gates (slit must be thin vs body
    ///                       short dim).
    ///   - MidBboxDimMm    : middle AABB edge.
    ///   - DiagonalMm      : full AABB diagonal — anchor for area- or
    ///                       variance-like quantities (PCA eigenvalues etc).
    ///   - DominantFeatureMm
    ///                     : best-effort estimate of a "typical feature
    ///                       size" on this body. = MinBboxDimMm by default.
    /// </summary>
    public class ModelScale
    {
        public class Scale
        {
            public double MinBboxDimMm;
            public double MidBboxDimMm;
            public double MaxBboxDimMm;
            public double DiagonalMm;
            public double DominantFeatureMm;
        }

        /// <summary>
        /// Compute the scale from the FeatureGraph's bbox size. Caller must
        /// have populated BboxSizeMm before calling.
        ///
        /// Degenerate input (null / zero / negative dims) is mapped to a
        /// "desktop CAD" default (100 mm) so ratio multiplies reproduce the
        /// legacy thresholds. We never want a ratio * 0 to silently disable
        /// a threshold.
        /// </summary>
        public static Scale Compute(FeatureGraph graph)
        {
            const double DefaultDimMm = 100.0;  // legacy desktop-CAD anchor

            double[] sizes = graph != null ? graph.BboxSizeMm : null;
            double bx = (sizes != null && sizes.Length >= 1 && sizes[0] > 0) ? sizes[0] : DefaultDimMm;
            double by = (sizes != null && sizes.Length >= 2 && sizes[1] > 0) ? sizes[1] : DefaultDimMm;
            double bz = (sizes != null && sizes.Length >= 3 && sizes[2] > 0) ? sizes[2] : DefaultDimMm;

            double[] sorted = new[] { bx, by, bz };
            Array.Sort(sorted);

            double minDim = sorted[0];
            double midDim = sorted[1];
            double maxDim = sorted[2];
            double diag = Math.Sqrt(bx * bx + by * by + bz * bz);

            return new Scale
            {
                MinBboxDimMm = minDim,
                MidBboxDimMm = midDim,
                MaxBboxDimMm = maxDim,
                DiagonalMm = diag,
                DominantFeatureMm = minDim,
            };
        }

        /// <summary>
        /// Compute scale from raw bbox-size triplet (mm). Convenience for
        /// detectors that only receive bboxSizeMm and not the full graph.
        /// </summary>
        public static Scale ComputeFromBboxSize(double[] bboxSizeMm)
        {
            var dummy = new FeatureGraph { BboxSizeMm = bboxSizeMm };
            return Compute(dummy);
        }
    }
}
