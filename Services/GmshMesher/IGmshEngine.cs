using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.GmshMesher
{
    public interface IGmshEngine
    {
        bool IsAvailable { get; }
        string Version { get; }
        bool RunMesh(string geoPath, string mshPath, List<string> log);
    }
}
