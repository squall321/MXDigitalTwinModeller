using System;
using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Package
{
    /// <summary>One solder ball / via in a layer's ball map: center (x, y) mm relative to the
    /// layer Location, pad radius r mm. The ball occupies the full layer thickness.</summary>
    public class PackageBallSpec
    {
        public double XMm { get; set; }
        public double YMm { get; set; }
        public double RadiusMm { get; set; }
    }

    /// <summary>One embedded rectangular inclusion (die/attach) in a layer: center (x, y) mm
    /// relative to the layer Location, full size w x h mm, full layer thickness.</summary>
    public class PackageBoxSpec
    {
        public double XMm { get; set; }
        public double YMm { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
    }

    /// <summary>
    /// One *Layer block of a package description file. Geometric fields drive CAD; every
    /// Mesh* key is filtered into MeshOptions untouched (the CAD build ignores them, a later
    /// meshing stage can consume them).
    /// </summary>
    public class PackageLayerSpec
    {
        public string Name { get; set; }
        public double LocXMm { get; set; }
        public double LocYMm { get; set; }
        /// <summary>Optional explicit absolute Z base (3rd Location value). When absent the
        /// layer sits on top of the previous layer (cumulative stacking).</summary>
        public double? LocZMm { get; set; }
        public double LenXMm { get; set; }
        public double LenYMm { get; set; }
        public double ThicknessMm { get; set; }
        public List<PackageBallSpec> Balls { get; } = new List<PackageBallSpec>();
        public List<PackageBoxSpec> Boxes { get; } = new List<PackageBoxSpec>();
        /// <summary>Mesh-only keys (MeshGenerationType, MeshSizeInPlane, NumberofElementinThickness,
        /// ConformalBufferThickness, MeshPath, ...) preserved verbatim: key -> raw value string.</summary>
        public Dictionary<string, string> MeshOptions { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool HasInclusions { get { return Balls.Count > 0 || Boxes.Count > 0; } }
    }

    /// <summary>A parsed package description (bottom-up layer stack).</summary>
    public class PackageSpec
    {
        public List<PackageLayerSpec> Layers { get; } = new List<PackageLayerSpec>();
        public List<string> Warnings { get; } = new List<string>();

        public double GetTotalThicknessMm()
        {
            double t = 0;
            foreach (var l in Layers) t += l.ThicknessMm;
            return t;
        }

        /// <summary>Absolute Z base per layer (mm), bottom-up cumulative; an explicit 3rd
        /// Location value on a layer resets the cursor. The SAME rule feeds parse summaries
        /// and generation so the two can never drift.</summary>
        public List<double> ComputeZBasesMm()
        {
            var z = new List<double>();
            double cur = 0.0;
            foreach (var l in Layers)
            {
                if (l.LocZMm.HasValue) cur = l.LocZMm.Value;
                z.Add(cur);
                cur += l.ThicknessMm;
            }
            return z;
        }
    }

    /// <summary>Options for the CAD build of a parsed package spec.</summary>
    public class PackageGenOptions
    {
        /// <summary>"cylinder" (ball-map default, straight joint) or "barrel"
        /// (convex reflowed joint: radius follows a circular arc, bulging at mid-height).</summary>
        public string BallShape = "cylinder";
        /// <summary>Barrel mid-height radius = pad radius * this ratio (>1 bulges outward).</summary>
        public double BarrelBulgeRatio = 1.25;
        /// <summary>Stacked-disc slices approximating the barrel arc (same idiom as the
        /// phone pipeline's curved-back stack; no revolve API in this codebase).</summary>
        public int BarrelSlices = 8;
        /// <summary>Create the "resin" matrix body per inclusion layer:
        /// layer slab MINUS all balls/boxes (underfill / molding compound).</summary>
        public bool FillMatrix = true;
        /// <summary>Only build layers whose name is in this set (null/empty = all).
        /// Skipped layers still advance the Z cursor so the stack stays true.</summary>
        public HashSet<string> LayerFilter = null;
    }

    /// <summary>Per-layer build telemetry.</summary>
    public class PackageLayerBuildInfo
    {
        public string Name;
        public double ZBaseMm;
        public double ThicknessMm;
        public int BallBodies;
        public int BoxBodies;
        public bool MatrixCreated;
        public bool PlainCreated;
        public bool Skipped;
    }

    /// <summary>Result of PackageGenerationService.BuildStack.</summary>
    public class PackageGenResult
    {
        public bool Success;
        public string Error;
        public List<PackageLayerBuildInfo> Layers = new List<PackageLayerBuildInfo>();
        public double TotalThicknessMm;
        public int TotalBodies;
        public string BoundBodyName;
        public List<string> Log = new List<string>();
    }
}
