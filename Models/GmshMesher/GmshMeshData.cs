using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.GmshMesher
{
    public enum GmshElementType
    {
        Tri3 = 2,
        Tet4 = 4,
        Hex8 = 5,
        Tet10 = 9
    }

    public class MeshNode
    {
        public int Id { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public MeshNode(int id, double x, double y, double z)
        {
            Id = id;
            X = x;
            Y = y;
            Z = z;
        }
    }

    public class MeshElement
    {
        public int Id { get; set; }
        public GmshElementType Type { get; set; }
        public int EntityTag { get; set; }
        public int[] NodeIds { get; set; }

        public MeshElement(int id, GmshElementType type, int entityTag, int[] nodeIds)
        {
            Id = id;
            Type = type;
            EntityTag = entityTag;
            NodeIds = nodeIds;
        }
    }

    public class PhysicalGroup
    {
        public int Dimension { get; set; }
        public int Tag { get; set; }
        public string Name { get; set; }

        public PhysicalGroup(int dimension, int tag, string name)
        {
            Dimension = dimension;
            Tag = tag;
            Name = name;
        }
    }

    public class GmshMeshData
    {
        public Dictionary<int, MeshNode> Nodes { get; set; }
        public List<MeshElement> VolumeElements { get; set; }
        public List<MeshElement> SurfaceElements { get; set; }
        public List<PhysicalGroup> PhysicalGroups { get; set; }

        public GmshMeshData()
        {
            Nodes = new Dictionary<int, MeshNode>();
            VolumeElements = new List<MeshElement>();
            SurfaceElements = new List<MeshElement>();
            PhysicalGroups = new List<PhysicalGroup>();
        }

        public int TotalNodeCount { get { return Nodes.Count; } }
        public int TotalElementCount { get { return VolumeElements.Count + SurfaceElements.Count; } }
    }
}
