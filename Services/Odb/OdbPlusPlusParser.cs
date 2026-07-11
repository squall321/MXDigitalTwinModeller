using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Odb;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Odb
{
    /// <summary>
    /// Tolerant ODB++ reader for the geometry a CAE model needs:
    ///
    ///   steps/&lt;step&gt;/profile            -> board outline (islands) + holes (cutouts)
    ///   steps/&lt;step&gt;/eda/data           -> PKG package outlines + PIN sites
    ///   steps/&lt;step&gt;/layers/&lt;comp&gt;/components -> CMP placements (+ COMP_HEIGHT)
    ///   matrix/matrix                    -> step names + COMPONENT layer names
    ///   steps/&lt;step&gt;/stephdr            -> step-and-repeat detection (panels)
    ///
    /// Scope (documented, loud): the input must be an UNPACKED ODB++ directory (extract
    /// .tgz first — the error says so); units follow the per-file UNITS directive with
    /// the ODB++ default of INCH when absent (COMP_HEIGHT included); ';attribute'
    /// suffixes are stripped, and indexed '@n .comp_height' attributes are honored;
    /// package outlines come from CT contours, RC/CR/SQ records, or the declared bbox;
    /// contours following a PIN record belong to the pin and are NOT package outlines;
    /// arcs are tessellated at max 15 deg per chord (near-coincident endpoints = full
    /// circle); unknown record types are skipped with a deduped warning; '&amp;'
    /// continuation lines are joined; copper toeprints are NOT read — pads come from
    /// PIN sites. Step-and-repeat panels are NOT instantiated (loud warning).
    /// </summary>
    public static class OdbPlusPlusParser
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const double InchToMm = 25.4;
        private const double ArcChordDeg = 15.0;

        public static OdbDesign Parse(string rootDir, string stepName)
        {
            if (string.IsNullOrEmpty(rootDir))
                throw new ArgumentException("path is null/empty");
            // accept the root itself or its odb/ child as the tree root
            string root = rootDir;
            if (!Directory.Exists(Path.Combine(root, "steps"))
                && Directory.Exists(Path.Combine(root, "odb", "steps")))
                root = Path.Combine(root, "odb");
            if (!Directory.Exists(root))
            {
                if (File.Exists(rootDir))
                    throw new ArgumentException(
                        "path is a file - ODB++ archives (.tgz) must be EXTRACTED first; " +
                        "pass the unpacked directory that contains steps/ and matrix/");
                throw new DirectoryNotFoundException("ODB++ directory not found: " + rootDir);
            }
            string stepsDir = Path.Combine(root, "steps");
            if (!Directory.Exists(stepsDir))
                throw new ArgumentException(
                    "not an ODB++ tree (no steps/ directory under " + root + ")");

            var design = new OdbDesign { RootDir = root };
            foreach (var d in Directory.GetDirectories(stepsDir))
                design.StepNames.Add(Path.GetFileName(d));
            if (design.StepNames.Count == 0)
                throw new ArgumentException("ODB++ tree has no steps");

            string step = stepName;
            if (string.IsNullOrEmpty(step))
            {
                // default: prefer a real board step (has eda/data, no step-and-repeat)
                // over panels — alphabetical order would pick 'array' before 'pcb'
                step = design.StepNames[0];
                foreach (var cand in design.StepNames)
                {
                    if (File.Exists(Path.Combine(stepsDir, cand, "eda", "data"))
                        && ReadStepRepeatRefs(Path.Combine(stepsDir, cand)).Count == 0)
                    { step = cand; break; }
                }
                if (design.StepNames.Count > 1)
                    design.Warnings.Add("multiple steps - auto-selected '" + step
                        + "' (pass step=... to override)");
            }
            else if (!design.StepNames.Contains(step))
                throw new ArgumentException(string.Format(Inv,
                    "step '{0}' not found - available: {1}",
                    step, string.Join(", ", design.StepNames)));

            var s = new OdbStep { Name = step };
            design.Step = s;
            string stepDir = Path.Combine(stepsDir, step);

            var srRefs = ReadStepRepeatRefs(stepDir);
            if (srRefs.Count > 0)
                design.Warnings.Add("step '" + step + "' is a step-and-repeat panel referencing: "
                    + string.Join(", ", srRefs)
                    + " - sub-steps are NOT instantiated; pass step=... to import the board directly");

            s.ComponentLayers = FindComponentLayers(root, stepDir, design.Warnings);
            ParseProfile(Path.Combine(stepDir, "profile"), s, design.Warnings);
            ParseEdaData(Path.Combine(stepDir, "eda", "data"), s, design.Warnings);
            foreach (var lay in s.ComponentLayers)
                ParseComponents(Path.Combine(stepDir, "layers", lay, "components"),
                    s, design.Warnings);
            return design;
        }

        // ------------------------------------------------------------------
        // stephdr: step-and-repeat sub-step names (SR / STEP-REPEAT blocks)
        // ------------------------------------------------------------------
        private static List<string> ReadStepRepeatRefs(string stepDir)
        {
            var refs = new List<string>();
            string hdr = Path.Combine(stepDir, "stephdr");
            if (!File.Exists(hdr)) return refs;
            bool inSr = false;
            foreach (var raw in File.ReadAllLines(hdr))
            {
                string line = raw.Trim();
                if (!inSr)
                {
                    int brace = line.IndexOf('{');
                    if (brace >= 0)
                    {
                        string tok = line.Substring(0, brace).Trim().ToUpperInvariant();
                        if (tok == "SR" || tok == "STEP-REPEAT") inSr = true;
                    }
                    continue;
                }
                if (line.StartsWith("}")) { inSr = false; continue; }
                int eq = line.IndexOf('=');
                if (eq > 0 && line.Substring(0, eq).Trim().ToUpperInvariant() == "NAME")
                {
                    string v = line.Substring(eq + 1).Trim();
                    if (v.Length > 0 && !refs.Contains(v)) refs.Add(v);
                }
            }
            return refs;
        }

        // ------------------------------------------------------------------
        // matrix: step + COMPONENT layer discovery (directory fallback)
        // ------------------------------------------------------------------
        private static List<string> FindComponentLayers(string root, string stepDir,
            List<string> warnings)
        {
            var layers = new List<string>();
            string matrix = Path.Combine(root, "matrix", "matrix");
            if (File.Exists(matrix))
            {
                string name = null;
                bool isComponent = false;
                foreach (var raw in File.ReadAllLines(matrix))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("LAYER", StringComparison.OrdinalIgnoreCase))
                    { name = null; isComponent = false; }
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        string k = line.Substring(0, eq).Trim().ToUpperInvariant();
                        string v = line.Substring(eq + 1).Trim().ToLowerInvariant();
                        if (k == "NAME") name = v;
                        if (k == "TYPE" && v == "component") isComponent = true;
                    }
                    if (line.StartsWith("}") && isComponent && name != null)
                    { layers.Add(name); name = null; isComponent = false; }
                }
            }
            if (layers.Count == 0)
            {
                // fallback: any layer directory whose name contains "comp"
                string layDir = Path.Combine(stepDir, "layers");
                if (Directory.Exists(layDir))
                    foreach (var d in Directory.GetDirectories(layDir))
                    {
                        string n = Path.GetFileName(d).ToLowerInvariant();
                        if (n.Contains("comp")) layers.Add(n);
                    }
                if (layers.Count > 0)
                    warnings.Add("matrix missing/empty - component layers guessed from names: "
                        + string.Join(", ", layers));
            }
            return layers;
        }

        // ------------------------------------------------------------------
        // profile: OB x y I|H / OS x y / OC x y cx cy Y|N / OE
        // ------------------------------------------------------------------
        private static void ParseProfile(string path, OdbStep s, List<string> warnings)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("ODB++ profile not found (board outline): " + path);
            double scale = InchToMm;
            List<double[]> cur = null;
            bool curIsHole = false;
            var unknown = new HashSet<string>();
            foreach (var raw in JoinContinuations(File.ReadAllLines(path)))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("UNITS=", StringComparison.OrdinalIgnoreCase))
                { scale = UnitsScale(line); continue; }
                line = StripAttrSuffix(line);
                if (line.Length == 0) continue;
                var t = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                switch (t[0].ToUpperInvariant())
                {
                    case "OB":
                        cur = new List<double[]> { new[] { Num(t[1]) * scale, Num(t[2]) * scale } };
                        curIsHole = t.Length > 3 && t[3].ToUpperInvariant() == "H";
                        break;
                    case "OS":
                        if (cur != null)
                            cur.Add(new[] { Num(t[1]) * scale, Num(t[2]) * scale });
                        break;
                    case "OC":
                        if (cur != null)
                            TessellateArc(cur,
                                Num(t[1]) * scale, Num(t[2]) * scale,
                                Num(t[3]) * scale, Num(t[4]) * scale,
                                t.Length > 5 && t[5].ToUpperInvariant() == "Y");
                        break;
                    case "OE":
                        if (cur != null && cur.Count >= 3)
                        {
                            TrimClosingPoint(cur);
                            if (curIsHole) s.CutoutsMm.Add(cur);
                            else if (s.OutlineMm.Count == 0) s.OutlineMm = cur;
                            else warnings.Add("profile has multiple islands - only the first is the board outline");
                        }
                        else if (cur != null)
                            warnings.Add(string.Format(Inv,
                                "profile {0} contour with {1} point(s) dropped (degenerate)",
                                curIsHole ? "hole" : "island", cur.Count));
                        cur = null;
                        break;
                    case "S":
                    case "SE":
                        break; // surface record markers - structural, no geometry of their own
                    default:
                        if (unknown.Add(t[0].ToUpperInvariant()))
                            warnings.Add("profile: unknown record '" + t[0] + "' skipped");
                        break;
                }
            }
            if (s.OutlineMm.Count < 3)
                throw new ArgumentException("profile contains no island polygon (board outline)");
        }

        /// <summary>Circular arc from the current last point to (x, y) about (cx, cy),
        /// cw = clockwise, appended as chords of at most ArcChordDeg. Endpoints that
        /// coincide with the start within coordinate rounding (0.1 um) are a FULL
        /// circle — real exports round to ~6 decimals, so exact-angle comparison
        /// would collapse the circle to a single chord.</summary>
        internal static void TessellateArc(List<double[]> pts, double x, double y,
            double cx, double cy, bool cw)
        {
            var p0 = pts[pts.Count - 1];
            double a0 = Math.Atan2(p0[1] - cy, p0[0] - cx);
            double a1 = Math.Atan2(y - cy, x - cx);
            double r = Math.Sqrt((p0[0] - cx) * (p0[0] - cx) + (p0[1] - cy) * (p0[1] - cy));
            double gap = Math.Sqrt((x - p0[0]) * (x - p0[0]) + (y - p0[1]) * (y - p0[1]));
            bool fullCircle = gap < 1e-4;
            double sweep;
            if (fullCircle) sweep = cw ? -2 * Math.PI : 2 * Math.PI;
            else
            {
                sweep = a1 - a0;
                if (cw) { while (sweep > -1e-12) sweep -= 2 * Math.PI; }
                else { while (sweep < 1e-12) sweep += 2 * Math.PI; }
            }
            int n = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweep) / (ArcChordDeg * Math.PI / 180)));
            for (int i = 1; i <= n; i++)
            {
                double a = a0 + sweep * i / n;
                pts.Add(new[] { cx + r * Math.Cos(a), cy + r * Math.Sin(a) });
            }
            // honor the record's exact endpoint (start/end radii can differ by file
            // rounding); a full circle ends at its start - TrimClosingPoint removes it
            if (!fullCircle) pts[pts.Count - 1] = new[] { x, y };
        }

        // ------------------------------------------------------------------
        // eda/data: PKG <name> <pitch> <xmin> <ymin> <xmax> <ymax>
        //           outline = CT block (OB/OS/OC/OE islands + H holes) or RC/CR/SQ
        //           PIN <name> <type> <x> <y> ...  (pin contours are NOT the package)
        // ------------------------------------------------------------------
        private static void ParseEdaData(string path, OdbStep s, List<string> warnings)
        {
            if (!File.Exists(path))
            {
                warnings.Add("eda/data missing - no package geometry (components will be skipped)");
                return;
            }
            double scale = InchToMm;
            OdbPackage pkg = null;
            List<double[]> poly = null;
            bool inPin = false, polyIsHole = false;
            var unknown = new HashSet<string>();
            foreach (var raw in JoinContinuations(File.ReadAllLines(path)))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("UNITS=", StringComparison.OrdinalIgnoreCase))
                { scale = UnitsScale(line); continue; }
                // real exports glue the ';attr=val' list to the last field with no space
                line = StripAttrSuffix(line);
                if (line.Length == 0) continue;
                var t = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                switch (t[0].ToUpperInvariant())
                {
                    case "PKG":
                        FinishPackage(s, pkg, warnings);
                        pkg = new OdbPackage
                        {
                            Name = t.Length > 1 ? t[1] : ("pkg" + s.Packages.Count.ToString(Inv)),
                            PitchMm = t.Length > 2 ? Num(t[2]) * scale : 0,
                            XMinMm = t.Length > 3 ? Num(t[3]) * scale : 0,
                            YMinMm = t.Length > 4 ? Num(t[4]) * scale : 0,
                            XMaxMm = t.Length > 5 ? Num(t[5]) * scale : 0,
                            YMaxMm = t.Length > 6 ? Num(t[6]) * scale : 0,
                        };
                        poly = null;
                        inPin = false;
                        break;
                    case "PIN":
                        if (pkg != null && t.Length >= 5)
                            pkg.Pins.Add(new OdbPin
                            {
                                Name = t[1],
                                XMm = Num(t[3]) * scale,
                                YMm = Num(t[4]) * scale,
                            });
                        inPin = true;  // contours that follow belong to the PIN, not the package
                        poly = null;
                        break;
                    case "CT":
                    case "CE":
                        break; // contour block markers
                    case "OB":
                        poly = new List<double[]> { new[] { Num(t[1]) * scale, Num(t[2]) * scale } };
                        polyIsHole = t.Length > 3 && t[3].ToUpperInvariant() == "H";
                        break;
                    case "OS":
                        if (poly != null) poly.Add(new[] { Num(t[1]) * scale, Num(t[2]) * scale });
                        break;
                    case "OC":
                        if (poly != null)
                            TessellateArc(poly, Num(t[1]) * scale, Num(t[2]) * scale,
                                Num(t[3]) * scale, Num(t[4]) * scale,
                                t.Length > 5 && t[5].ToUpperInvariant() == "Y");
                        break;
                    case "OE":
                        if (!inPin && pkg != null && poly != null && poly.Count >= 3)
                        {
                            TrimClosingPoint(poly);
                            if (polyIsHole) pkg.HolesMm.Add(poly);
                            else if (pkg.OutlineMm.Count == 0) pkg.OutlineMm = poly;
                            else warnings.Add("PKG " + pkg.Name + ": extra island contour ignored");
                        }
                        poly = null;
                        break;
                    case "RC": // rectangle: lower-left x y, width, height
                        if (!inPin && pkg != null && pkg.OutlineMm.Count == 0 && t.Length >= 5)
                        {
                            double rx = Num(t[1]) * scale, ry = Num(t[2]) * scale;
                            double rw = Num(t[3]) * scale, rh = Num(t[4]) * scale;
                            pkg.OutlineMm = new List<double[]>
                            {
                                new[] { rx, ry }, new[] { rx + rw, ry },
                                new[] { rx + rw, ry + rh }, new[] { rx, ry + rh },
                            };
                        }
                        break;
                    case "CR": // circle: center x y, radius
                        if (!inPin && pkg != null && pkg.OutlineMm.Count == 0 && t.Length >= 4)
                        {
                            double ccx = Num(t[1]) * scale, ccy = Num(t[2]) * scale;
                            double cr = Num(t[3]) * scale;
                            var circ = new List<double[]> { new[] { ccx + cr, ccy } };
                            TessellateArc(circ, ccx + cr, ccy, ccx, ccy, false); // full CCW circle
                            TrimClosingPoint(circ);
                            pkg.OutlineMm = circ;
                        }
                        break;
                    case "SQ": // square: center x y, half-side
                        if (!inPin && pkg != null && pkg.OutlineMm.Count == 0 && t.Length >= 4)
                        {
                            double sx = Num(t[1]) * scale, sy = Num(t[2]) * scale;
                            double hs = Num(t[3]) * scale;
                            pkg.OutlineMm = new List<double[]>
                            {
                                new[] { sx - hs, sy - hs }, new[] { sx + hs, sy - hs },
                                new[] { sx + hs, sy + hs }, new[] { sx - hs, sy + hs },
                            };
                        }
                        break;
                    default:
                        if (unknown.Add(t[0].ToUpperInvariant()))
                            warnings.Add("eda/data: unknown record '" + t[0] + "' skipped");
                        break;
                }
            }
            FinishPackage(s, pkg, warnings);
        }

        private static void FinishPackage(OdbStep s, OdbPackage pkg, List<string> warnings)
        {
            if (pkg == null) return;
            if (pkg.OutlineMm.Count < 3)
            {
                pkg.OutlineMm = new List<double[]>
                {
                    new[] { pkg.XMinMm, pkg.YMinMm }, new[] { pkg.XMaxMm, pkg.YMinMm },
                    new[] { pkg.XMaxMm, pkg.YMaxMm }, new[] { pkg.XMinMm, pkg.YMaxMm },
                };
                pkg.OutlineFromBBox = true;
                if (pkg.XMaxMm - pkg.XMinMm <= 0 || pkg.YMaxMm - pkg.YMinMm <= 0)
                    warnings.Add("PKG " + pkg.Name
                        + ": no outline and degenerate bbox - components using it will be skipped");
            }
            s.Packages.Add(pkg);
        }

        // ------------------------------------------------------------------
        // components: @n <attr_name> lookup header
        //             CMP <pkg_ref> <x> <y> <rot> <mirror N|M> <name> <part> ;n=v,...
        //             PRP COMP_HEIGHT '<v>'   (v follows the file's UNITS)
        // ------------------------------------------------------------------
        private static void ParseComponents(string path, OdbStep s, List<string> warnings)
        {
            if (!File.Exists(path))
            {
                warnings.Add("components file missing: " + path);
                return;
            }
            double scale = InchToMm;
            OdbComponent cur = null;
            var attrNames = new Dictionary<int, string>();
            var unknown = new HashSet<string>();
            foreach (var raw in JoinContinuations(File.ReadAllLines(path)))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                if (line.StartsWith("UNITS=", StringComparison.OrdinalIgnoreCase))
                { scale = UnitsScale(line); continue; }
                // keep the ';attribute' suffix - .comp_height rides in it
                string attrs = null;
                int semi = line.IndexOf(';');
                if (semi >= 0)
                {
                    attrs = line.Substring(semi + 1).Trim();
                    line = line.Substring(0, semi).Trim();
                    if (line.Length == 0) continue;
                }
                var t = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length == 0) continue;
                // '@n <name>' attribute-name lookup header
                if (t[0].Length > 1 && t[0][0] == '@')
                {
                    int ai;
                    if (t.Length >= 2 && int.TryParse(t[0].Substring(1), NumberStyles.Integer, Inv, out ai))
                        attrNames[ai] = t[1].ToLowerInvariant();
                    continue;
                }
                switch (t[0].ToUpperInvariant())
                {
                    case "CMP":
                        cur = new OdbComponent
                        {
                            PkgIndex = (int)Num(t[1]),
                            XMm = Num(t[2]) * scale,
                            YMm = Num(t[3]) * scale,
                            RotDeg = t.Length > 4 ? Num(t[4]) : 0,
                            Mirrored = t.Length > 5 && t[5].ToUpperInvariant() == "M",
                            RefDes = t.Length > 6 ? t[6] : ("C" + s.Components.Count.ToString(Inv)),
                            PartName = t.Length > 7 ? t[7] : "",
                        };
                        if (cur.PkgIndex < 0 || cur.PkgIndex >= s.Packages.Count)
                        {
                            warnings.Add("CMP " + cur.RefDes + " references unknown package index "
                                + cur.PkgIndex.ToString(Inv) + " - skipped");
                            cur = null;
                        }
                        else
                        {
                            s.Components.Add(cur);
                            ApplyCmpAttrs(cur, attrs, attrNames, scale);
                        }
                        break;
                    case "PRP":
                        if (cur != null && t.Length >= 3
                            && t[1].Equals("COMP_HEIGHT", StringComparison.OrdinalIgnoreCase))
                        {
                            double h;
                            if (double.TryParse(t[2].Trim('\''), NumberStyles.Float, Inv, out h))
                                cur.HeightMm = h * scale;   // follows the file's UNITS
                        }
                        break;
                    default:
                        if (unknown.Add(t[0].ToUpperInvariant()))
                            warnings.Add("components: unknown record '" + t[0]
                                + "' skipped (toeprints/attributes not imported)");
                        break;
                }
            }
        }

        /// <summary>Indexed CMP attributes (';0=1,1=1.75'): resolve ids through the
        /// '@n name' lookup and honor '.comp_height' (value follows the file UNITS).
        /// A later PRP COMP_HEIGHT record overrides this.</summary>
        private static void ApplyCmpAttrs(OdbComponent cmp, string attrs,
            Dictionary<int, string> attrNames, double scale)
        {
            if (cmp == null || string.IsNullOrEmpty(attrs)) return;
            foreach (var pair in attrs.Split(','))
            {
                int eq = pair.IndexOf('=');
                string key = (eq >= 0 ? pair.Substring(0, eq) : pair).Trim();
                string val = eq >= 0 ? pair.Substring(eq + 1).Trim().Trim('\'') : "";
                int id; string name; double v;
                if (!int.TryParse(key, NumberStyles.Integer, Inv, out id)) continue;
                if (!attrNames.TryGetValue(id, out name)) continue;
                if (name == ".comp_height"
                    && double.TryParse(val, NumberStyles.Float, Inv, out v) && v > 0)
                    cmp.HeightMm = v * scale;
            }
        }

        // ------------------------------------------------------------------
        /// <summary>Strip a ';attribute' suffix (glued or spaced) from a record line.</summary>
        private static string StripAttrSuffix(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi).Trim() : line;
        }

        private static IEnumerable<string> JoinContinuations(string[] lines)
        {
            string pending = null;
            foreach (var l in lines)
            {
                if (l.TrimStart().StartsWith("&"))
                {
                    pending = (pending ?? "") + " " + l.TrimStart().Substring(1);
                    continue;
                }
                if (pending != null) yield return pending;
                pending = l;
            }
            if (pending != null) yield return pending;
        }

        private static double UnitsScale(string unitsLine)
        {
            string v = unitsLine.Substring(unitsLine.IndexOf('=') + 1).Trim().ToUpperInvariant();
            if (v.StartsWith("MM")) return 1.0;
            if (v.StartsWith("INCH") || v.StartsWith("IN")) return InchToMm;
            throw new ArgumentException("unknown UNITS directive: " + unitsLine);
        }

        private static void TrimClosingPoint(List<double[]> poly)
        {
            var a = poly[0];
            var b = poly[poly.Count - 1];
            if (Math.Abs(a[0] - b[0]) < 1e-9 && Math.Abs(a[1] - b[1]) < 1e-9)
                poly.RemoveAt(poly.Count - 1);
        }

        private static double Num(string s)
        {
            return double.Parse(s, NumberStyles.Float, Inv);
        }
    }
}
