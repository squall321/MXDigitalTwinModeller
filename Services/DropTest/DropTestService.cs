using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.DropTest
{
    /// <summary>
    /// Drop-test environment: pose the whole device (every body) into a corner / edge /
    /// face drop attitude, drop in a rigid floor slab under it, and place ball / pen
    /// impactors over a target point — the standard mobile drop-CAE pre-processing that
    /// engineers hand-model today.
    ///
    /// Pose model: a drop feature names a DEVICE direction (face = axis, edge = sum of
    /// two faces, corner = sum of three, e.g. "bottom_front_left"); the device is rotated
    /// rigidly about its combined bbox center so that direction points straight DOWN,
    /// then translated so its lowest point sits gap_mm above the floor plane. Rigid
    /// transforms only — total volume is invariant (the gate asserts it).
    ///
    /// Only verified kernel APIs: Transform (rotation about Line / translation),
    /// ExtrudeProfile floor, the VoidCut cube->RoundEdges sphere for the ball, and the
    /// fastener stacked-disc taper for the pen cone.
    /// </summary>
    public class DropTestService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------
        // feature naming: face tokens combine into edges and corners
        // ------------------------------------------------------------------
        /// <summary>Unit DEVICE direction for a drop feature: face ("bottom"), edge
        /// ("bottom_front"), corner ("bottom_front_left") — underscore-joined face
        /// tokens summed and normalized. Throws on unknown/contradictory tokens.</summary>
        public static double[] FeatureDirection(string feature)
        {
            if (string.IsNullOrEmpty(feature)) throw new ArgumentException("feature is empty");
            double[] d = { 0, 0, 0 };
            foreach (var tokRaw in feature.ToLowerInvariant().Split('_'))
            {
                var tok = tokRaw.Trim();
                switch (tok)
                {
                    case "top": d[2] += 1; break;
                    case "bottom": d[2] -= 1; break;
                    case "left": d[0] -= 1; break;
                    case "right": d[0] += 1; break;
                    case "front": d[1] -= 1; break;
                    case "back": d[1] += 1; break;
                    case "": break;
                    default:
                        throw new ArgumentException(
                            "unknown feature token '" + tok +
                            "' (use top|bottom|left|right|front|back joined by _)");
                }
            }
            double m = Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]);
            if (m < 1e-9)
                throw new ArgumentException("feature '" + feature + "' cancels out to no direction");
            return new[] { d[0] / m, d[1] / m, d[2] / m };
        }

        public class PoseResult
        {
            public int BodiesPosed;
            public double RotationDeg;
            public double[] RotAxis = new double[3];
            public double MinZMm;
            public double VolumeBeforeMm3;
            public double VolumeAfterMm3;
        }

        /// <summary>Rigidly rotate ALL bodies so deviceDir points down, then drop the
        /// stack so its lowest bbox point sits at floorTopZ + gap.</summary>
        public PoseResult Pose(List<DesignBody> bodies, double[] deviceDir,
            double gapMm, double floorTopZMm)
        {
            if (bodies == null || bodies.Count == 0)
                throw new ArgumentException("no bodies to pose");
            var res = new PoseResult { BodiesPosed = bodies.Count };
            foreach (var b in bodies) res.VolumeBeforeMm3 += b.Shape.Volume * 1e9;

            // rotation mapping deviceDir -> (0,0,-1) about the combined bbox center
            double[] lo, hi;
            CombinedBoxMm(bodies, out lo, out hi);
            double[] cMm = { (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, (lo[2] + hi[2]) / 2 };
            var center = Point.Create(cMm[0] / 1000, cMm[1] / 1000, cMm[2] / 1000);

            double[] down = { 0, 0, -1 };
            double dot = deviceDir[0] * down[0] + deviceDir[1] * down[1] + deviceDir[2] * down[2];
            dot = Math.Max(-1, Math.Min(1, dot));
            double ang = Math.Acos(dot);
            double[] axis =
            {
                deviceDir[1] * down[2] - deviceDir[2] * down[1],
                deviceDir[2] * down[0] - deviceDir[0] * down[2],
                deviceDir[0] * down[1] - deviceDir[1] * down[0],
            };
            double am = Math.Sqrt(axis[0] * axis[0] + axis[1] * axis[1] + axis[2] * axis[2]);
            if (am < 1e-9) axis = new double[] { 1, 0, 0 };  // parallel/antiparallel: any axis
            else { axis[0] /= am; axis[1] /= am; axis[2] /= am; }
            res.RotAxis = axis;
            res.RotationDeg = ang * 180 / Math.PI;

            if (ang > 1e-9)
            {
                var rot = Matrix.CreateRotation(
                    Line.Create(center, Direction.Create(axis[0], axis[1], axis[2])), ang);
                foreach (var b in bodies) b.Shape.Transform(rot);
            }

            // settle: lowest bbox point -> floorTopZ + gap
            CombinedBoxMm(bodies, out lo, out hi);
            double dz = (floorTopZMm + gapMm) - lo[2];
            if (Math.Abs(dz) > 1e-9)
            {
                var tr = Matrix.CreateTranslation(Vector.Create(0, 0, GeometryUtils.MmToMeters(dz)));
                foreach (var b in bodies) b.Shape.Transform(tr);
            }
            CombinedBoxMm(bodies, out lo, out hi);
            res.MinZMm = lo[2];
            foreach (var b in bodies) res.VolumeAfterMm3 += b.Shape.Volume * 1e9;
            return res;
        }

        /// <summary>Rigid floor slab under the (already posed) bodies: spans their
        /// combined XY footprint + margin, top face at topZ.</summary>
        public DesignBody AddFloor(Part part, List<DesignBody> bodies,
            double marginMm, double thicknessMm, double topZMm, string name)
        {
            if (bodies == null || bodies.Count == 0)
                throw new ArgumentException("no device bodies - the floor sizes from their footprint");
            double[] lo, hi;
            CombinedBoxMm(bodies, out lo, out hi);
            double w = hi[0] - lo[0] + 2 * marginMm;
            double l = hi[1] - lo[1] + 2 * marginMm;
            Body slab = BodyBuilder.CreateBlock(
                GeometryUtils.MmToMeters(w), GeometryUtils.MmToMeters(l),
                GeometryUtils.MmToMeters(thicknessMm));
            slab.Transform(Matrix.CreateTranslation(Vector.Create(
                GeometryUtils.MmToMeters((lo[0] + hi[0]) / 2),
                GeometryUtils.MmToMeters((lo[1] + hi[1]) / 2),
                GeometryUtils.MmToMeters(topZMm - thicknessMm))));
            return BodyBuilder.CreateDesignBody(part, name, slab);
        }

        /// <summary>Steel-ball impactor: sphere of diaMm resting clearanceMm above the
        /// target point (center at target + dia/2 + clearance along +Z).</summary>
        public DesignBody AddBall(Part part, double diaMm, double[] targetMm,
            double clearanceMm, string name)
        {
            if (diaMm <= 0) throw new ArgumentException("ball_dia_mm must be > 0");
            Body ball = VoidCut.VoidCutService.CreateSphereBody(diaMm / 2,
                targetMm[0], targetMm[1], targetMm[2] + diaMm / 2 + clearanceMm, 0);
            return BodyBuilder.CreateDesignBody(part, name, ball);
        }

        /// <summary>Pen impactor pointing straight down at the target: rounded-tip cone
        /// nose (tipR -> shank radius over the cone angle) + cylindrical shank, lowest
        /// point clearanceMm above the target.</summary>
        public DesignBody AddPen(Part part, double tipRMm, double coneFullDeg,
            double shankDiaMm, double lenMm, double[] targetMm, double clearanceMm,
            string name, out double coneHMm)
        {
            if (tipRMm <= 0 || shankDiaMm <= 0 || lenMm <= 0)
                throw new ArgumentException("pen dimensions must be > 0");
            double rs = shankDiaMm / 2;
            if (tipRMm >= rs)
                throw new ArgumentException("pen_tip_r_mm must be < pen_shank_dia_mm/2");
            if (coneFullDeg <= 0 || coneFullDeg >= 180)
                throw new ArgumentException("pen_cone_deg must be in (0, 180)");
            double half = coneFullDeg / 2 * Math.PI / 180;
            coneHMm = (rs - tipRMm) / Math.Tan(half);
            if (coneHMm >= lenMm)
                throw new ArgumentException(string.Format(Inv,
                    "pen_len_mm {0:0.##} shorter than the cone nose {1:0.##}", lenMm, coneHMm));

            // local +Z build, tip at z=0: cone frustum then shank; sequential unite
            Body pen = Fastener.FastenerGenerationService.TaperStack(tipRMm, rs, 0, coneHMm, 1);
            Body shank = Fastener.FastenerGenerationService.Disc(rs, coneHMm - 0.02, lenMm - coneHMm + 0.02);
            pen.Unite(new List<Body> { shank });
            pen.Transform(Matrix.CreateTranslation(Vector.Create(
                GeometryUtils.MmToMeters(targetMm[0]),
                GeometryUtils.MmToMeters(targetMm[1]),
                GeometryUtils.MmToMeters(targetMm[2] + clearanceMm))));
            return BodyBuilder.CreateDesignBody(part, name, pen);
        }

        private static void CombinedBoxMm(List<DesignBody> bodies, out double[] lo, out double[] hi)
        {
            lo = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
            hi = new[] { double.MinValue, double.MinValue, double.MinValue };
            foreach (var b in bodies)
            {
                var bb = b.Shape.GetBoundingBox(Matrix.Identity);
                lo[0] = Math.Min(lo[0], bb.MinCorner.X * 1000);
                lo[1] = Math.Min(lo[1], bb.MinCorner.Y * 1000);
                lo[2] = Math.Min(lo[2], bb.MinCorner.Z * 1000);
                hi[0] = Math.Max(hi[0], bb.MaxCorner.X * 1000);
                hi[1] = Math.Max(hi[1], bb.MaxCorner.Y * 1000);
                hi[2] = Math.Max(hi[2], bb.MaxCorner.Z * 1000);
            }
        }
    }
}
