using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Package;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Package
{
    /// <summary>
    /// Parser for package description files (Examples/packages/*.txt):
    ///
    ///   *Layer,PCB
    ///   Location,0,0,0          — layer center XY (mm); optional 3rd value = absolute Z base
    ///   Length,30.0,30.0        — in-plane size (mm)
    ///   Thickness,0.512         — layer thickness (mm)
    ///   MeshGenerationType,...  — mesh-only keys: FILTERED into MeshOptions (no CAD effect)
    ///   Cylinder,x,y,r          — ball-map entry (solder ball / via), repeated
    ///   Box,x,y,w,h             — embedded rectangular die/attach, repeated
    ///
    /// Layers stack bottom-up in file order. Unknown keys produce warnings, never failures —
    /// the format is tool-generated and may grow keys we do not model.
    /// </summary>
    public static class PackageFileParser
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Mesh-configuration keys that must NOT influence the CAD build. Anything
        /// else starting with "Mesh" is treated the same way (forward compatibility).</summary>
        private static readonly HashSet<string> MeshKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "MeshGenerationType", "MeshSizeInPlane", "MeshSizeInThickness",
            "NumberofElementinThickness", "ConformalBufferThickness", "MeshPath", "MeshType",
        };

        public static PackageSpec ParseFile(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is null/empty");
            if (!File.Exists(path)) throw new FileNotFoundException("package file not found", path);
            return Parse(File.ReadAllLines(path));
        }

        public static PackageSpec Parse(string[] lines)
        {
            var spec = new PackageSpec();
            PackageLayerSpec layer = null;
            int autoIdx = 0;

            for (int ln = 0; ln < lines.Length; ln++)
            {
                string raw = lines[ln];
                if (raw == null) continue;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//")) continue;

                string[] parts = line.Split(',');
                string key = parts[0].Trim();

                if (key.StartsWith("*"))
                {
                    // *Layer,<name> — any *-record opens a layer block; tolerate a missing name.
                    autoIdx++;
                    layer = new PackageLayerSpec
                    {
                        Name = parts.Length > 1 && parts[1].Trim().Length > 0
                            ? parts[1].Trim() : ("Layer_" + autoIdx.ToString(Inv)),
                    };
                    spec.Layers.Add(layer);
                    continue;
                }

                if (layer == null)
                {
                    Warn(spec, ln, "content before the first *Layer ignored: " + Snip(line));
                    continue;
                }

                if (MeshKeys.Contains(key) || key.StartsWith("Mesh", StringComparison.OrdinalIgnoreCase))
                {
                    layer.MeshOptions[key] = parts.Length > 1
                        ? line.Substring(line.IndexOf(',') + 1).Trim() : "";
                    continue;
                }

                switch (key.ToLowerInvariant())
                {
                    case "location":
                    {
                        double x, y;
                        if (TryNum(parts, 1, out x) && TryNum(parts, 2, out y))
                        {
                            layer.LocXMm = x; layer.LocYMm = y;
                            double z;
                            if (parts.Length > 3 && TryNum(parts, 3, out z)) layer.LocZMm = z;
                        }
                        else Warn(spec, ln, "bad Location: " + Snip(line));
                        break;
                    }
                    case "length":
                    {
                        double lx, ly;
                        if (TryNum(parts, 1, out lx) && TryNum(parts, 2, out ly))
                        { layer.LenXMm = lx; layer.LenYMm = ly; }
                        else Warn(spec, ln, "bad Length: " + Snip(line));
                        break;
                    }
                    case "thickness":
                    {
                        double t;
                        if (TryNum(parts, 1, out t) && t > 0) layer.ThicknessMm = t;
                        else Warn(spec, ln, "bad Thickness: " + Snip(line));
                        break;
                    }
                    case "cylinder":
                    {
                        double x, y, r;
                        if (TryNum(parts, 1, out x) && TryNum(parts, 2, out y)
                            && TryNum(parts, 3, out r) && r > 0)
                            layer.Balls.Add(new PackageBallSpec { XMm = x, YMm = y, RadiusMm = r });
                        else Warn(spec, ln, "bad Cylinder: " + Snip(line));
                        break;
                    }
                    case "box":
                    {
                        double x, y, w, h;
                        if (TryNum(parts, 1, out x) && TryNum(parts, 2, out y)
                            && TryNum(parts, 3, out w) && TryNum(parts, 4, out h) && w > 0 && h > 0)
                            layer.Boxes.Add(new PackageBoxSpec { XMm = x, YMm = y, WidthMm = w, HeightMm = h });
                        else Warn(spec, ln, "bad Box: " + Snip(line));
                        break;
                    }
                    default:
                        Warn(spec, ln, "unknown key '" + key + "' ignored");
                        break;
                }
            }

            Validate(spec);
            return spec;
        }

        private static void Validate(PackageSpec spec)
        {
            if (spec.Layers.Count == 0)
            {
                spec.Warnings.Add("no *Layer blocks found");
                return;
            }
            foreach (var l in spec.Layers)
            {
                if (l.LenXMm <= 0 || l.LenYMm <= 0)
                    spec.Warnings.Add("layer '" + l.Name + "' has no valid Length (skipped at build)");
                if (l.ThicknessMm <= 0)
                    spec.Warnings.Add("layer '" + l.Name + "' has no valid Thickness (skipped at build)");
                // inclusion-outside-slab is a warning, not an error: tool exports sometimes
                // carry corner balls exactly on the edge; the Boolean still resolves.
                double hx = l.LenXMm / 2, hy = l.LenYMm / 2;
                int outside = 0;
                foreach (var b in l.Balls)
                    if (Math.Abs(b.XMm) - b.RadiusMm > hx + 1e-9 || Math.Abs(b.YMm) - b.RadiusMm > hy + 1e-9)
                        outside++;
                if (outside > 0)
                    spec.Warnings.Add("layer '" + l.Name + "': " + outside.ToString(Inv) +
                        " ball(s) fully outside the layer rectangle");
            }
        }

        private static bool TryNum(string[] parts, int idx, out double val)
        {
            val = 0;
            return idx < parts.Length &&
                   double.TryParse(parts[idx].Trim(), NumberStyles.Float, Inv, out val);
        }

        private static void Warn(PackageSpec spec, int line0, string msg)
        {
            const int MaxWarnings = 40;
            if (spec.Warnings.Count < MaxWarnings)
                spec.Warnings.Add("line " + (line0 + 1).ToString(Inv) + ": " + msg);
            else if (spec.Warnings.Count == MaxWarnings)
                spec.Warnings.Add("(further warnings suppressed)");
        }

        private static string Snip(string s)
        {
            return s.Length <= 60 ? s : s.Substring(0, 57) + "...";
        }
    }
}
