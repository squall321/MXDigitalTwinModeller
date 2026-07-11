using System;
using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Odb
{
    /// <summary>One package (footprint) definition from steps/&lt;step&gt;/eda/data.</summary>
    public class OdbPackage
    {
        public string Name;
        public double PitchMm;
        public double XMinMm, YMinMm, XMaxMm, YMaxMm;      // declared bbox
        /// <summary>Package outline polygon (mm, package-local). Falls back to the
        /// declared bbox rectangle when the PKG record carries no outline records.</summary>
        public List<double[]> OutlineMm = new List<double[]>();
        public bool OutlineFromBBox;
        /// <summary>Hole contours (H islands) inside the package outline — ring/frame
        /// packages (shield frames etc.); subtracted from the extruded body.</summary>
        public List<List<double[]>> HolesMm = new List<List<double[]>>();
        public List<OdbPin> Pins = new List<OdbPin>();
    }

    public class OdbPin
    {
        public string Name;
        public double XMm, YMm;                            // package-local
    }

    /// <summary>One placed component (CMP record) from a COMPONENT layer.</summary>
    public class OdbComponent
    {
        public string RefDes;
        public int PkgIndex;                               // index into OdbStep.Packages
        public double XMm, YMm;
        public double RotDeg;                              // ODB++ rotation is CLOCKWISE
        public bool Mirrored;                              // bottom-side placement
        public string PartName;
        /// <summary>From the COMP_HEIGHT property when present; 0 = unknown.</summary>
        public double HeightMm;
    }

    public class OdbStep
    {
        public string Name;
        /// <summary>Board outline (profile island), arcs tessellated, mm.</summary>
        public List<double[]> OutlineMm = new List<double[]>();
        /// <summary>Profile holes -> board cutouts.</summary>
        public List<List<double[]>> CutoutsMm = new List<List<double[]>>();
        public List<OdbPackage> Packages = new List<OdbPackage>();
        public List<OdbComponent> Components = new List<OdbComponent>();
        public List<string> ComponentLayers = new List<string>();
    }

    public class OdbDesign
    {
        public string RootDir;
        public List<string> StepNames = new List<string>();
        public OdbStep Step;                               // the parsed step
        public List<string> Warnings = new List<string>();
    }
}
