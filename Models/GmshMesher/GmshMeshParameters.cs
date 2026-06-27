using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.GmshMesher
{
    public enum ElementShape
    {
        Tet,
        Hex
    }

    public enum MeshAlgorithm
    {
        Auto = 0,
        Delaunay = 1,
        Frontal = 4,
        HXT = 10
    }

    public class BodyMeshOverride
    {
        public string BodyName { get; set; }
        public int BodyIndex { get; set; }
        public double ElementSizeMm { get; set; }

        public BodyMeshOverride(string bodyName, int bodyIndex, double elementSizeMm)
        {
            BodyName = bodyName;
            BodyIndex = bodyIndex;
            ElementSizeMm = elementSizeMm;
        }
    }

    public class GmshMeshParameters
    {
        public double GlobalElementSizeMm { get; set; }
        public double GrowthRate { get; set; }
        public ElementShape Shape { get; set; }
        public bool MidsideNodes { get; set; }
        public MeshAlgorithm Algorithm { get; set; }
        public List<BodyMeshOverride> BodyOverrides { get; set; }

        // Refinement box (future use)
        public bool UseRefinementBox { get; set; }
        public double RefinementBoxSizeMm { get; set; }
        public double RefinementBoxXMin { get; set; }
        public double RefinementBoxXMax { get; set; }
        public double RefinementBoxYMin { get; set; }
        public double RefinementBoxYMax { get; set; }
        public double RefinementBoxZMin { get; set; }
        public double RefinementBoxZMax { get; set; }

        public GmshMeshParameters()
        {
            GlobalElementSizeMm = 5.0;
            GrowthRate = 1.3;
            Shape = ElementShape.Tet;
            MidsideNodes = false;
            Algorithm = MeshAlgorithm.Auto;
            BodyOverrides = new List<BodyMeshOverride>();
            UseRefinementBox = false;
            RefinementBoxSizeMm = 2.0;
        }

        public bool Validate(out string errorMessage)
        {
            if (GlobalElementSizeMm <= 0)
            {
                errorMessage = "Element size must be greater than 0.";
                return false;
            }
            if (GrowthRate < 1.0 || GrowthRate > 3.0)
            {
                errorMessage = "Growth rate must be between 1.0 and 3.0.";
                return false;
            }
            foreach (var ovr in BodyOverrides)
            {
                if (ovr.ElementSizeMm <= 0)
                {
                    errorMessage = string.Format("Body '{0}' override size must be > 0.", ovr.BodyName);
                    return false;
                }
            }
            errorMessage = null;
            return true;
        }
    }
}
