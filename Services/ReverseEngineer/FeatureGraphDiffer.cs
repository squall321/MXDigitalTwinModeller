using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// FeatureGraph before/after diff.
    ///
    /// Used by RoundTripTestRunner (each RT spec emits {name}_diff.json) and intended
    /// to be reused by ModificationService callers that want a structured summary
    /// of "what actually changed" between two extraction snapshots.
    ///
    /// Matching rules:
    ///   - Walls / Holes / Bosses / FilletChains are matched by their stable Id.
    ///   - For shared Ids we compare a small set of "key fields" (R, D, thickness,
    ///     position, ...) and emit a "Changed" entry whenever any of them moved
    ///     beyond a small numeric tolerance.
    ///   - Fillet face indices are NOT stable across modifications (OffsetFaces
    ///     creates/destroys faces), so individual FilletFeatures are compared by
    ///     count only.
    ///   - BboxDeltaMm = after.BboxSizeMm - before.BboxSizeMm (axis-wise).
    ///   - Summary is a single line, terse and human-skimmable.
    /// </summary>
    // @lat: [[reverse-engineer#FeatureGraphDiffer]]
    public static class FeatureGraphDiffer
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Tolerances — small enough to catch real changes, large enough to ignore
        // numerical noise from re-extraction of the same geometry.
        private const double LenTolMm = 0.05;   // thickness, depth, height, position
        private const double DiaTolMm = 0.05;   // diameter
        private const double RadTolMm = 0.02;   // fillet radius

        public class GraphDiff
        {
            /// <summary>Added features — "H2: hole D=4.00", "B1: boss D=6.00 H=5.00", "FC1: fillet R=2.00".</summary>
            public List<string> AddedFeatures { get; set; }

            /// <summary>Removed features — same format as Added.</summary>
            public List<string> RemovedFeatures { get; set; }

            /// <summary>Changed features — "F1: R 2.00 → 3.00", "H1: D 5.00 → 8.00, pos (0,0)→(10,15)".</summary>
            public List<string> ChangedFeatures { get; set; }

            /// <summary>(after - before) bbox size per axis (mm). Always length 3.</summary>
            public double[] BboxDeltaMm { get; set; }

            /// <summary>after.Faces.Count - before.Faces.Count.</summary>
            public int FaceCountDelta { get; set; }

            /// <summary>Single-line summary, e.g. "before=14f / after=18f / +1H, +1F, R(F1)2→3".</summary>
            public string Summary { get; set; }

            public GraphDiff()
            {
                AddedFeatures = new List<string>();
                RemovedFeatures = new List<string>();
                ChangedFeatures = new List<string>();
                BboxDeltaMm = new double[3];
            }
        }

        // -------------------------------------------------------------------
        // Diff
        // -------------------------------------------------------------------
        public static GraphDiff Diff(FeatureGraph before, FeatureGraph after)
        {
            var diff = new GraphDiff();
            if (before == null || after == null)
            {
                diff.Summary = "diff: " + (before == null ? "before=null" : "before-ok")
                    + " / " + (after == null ? "after=null" : "after-ok");
                return diff;
            }

            // ---- bbox delta ----
            if (before.BboxSizeMm != null && after.BboxSizeMm != null
                && before.BboxSizeMm.Length == 3 && after.BboxSizeMm.Length == 3)
            {
                for (int i = 0; i < 3; i++)
                    diff.BboxDeltaMm[i] = after.BboxSizeMm[i] - before.BboxSizeMm[i];
            }

            // ---- face count delta ----
            int faceBefore = before.Faces != null ? before.Faces.Count : 0;
            int faceAfter = after.Faces != null ? after.Faces.Count : 0;
            diff.FaceCountDelta = faceAfter - faceBefore;

            // ---- Walls ----
            int wallChanged = 0;
            DiffById(
                before.Walls, after.Walls,
                w => w.Id,
                w => string.Format(Inv, "{0}: wall T={1:F2}", w.Id, w.ThicknessMm),
                (b, a) =>
                {
                    var parts = new List<string>();
                    if (Math.Abs(b.ThicknessMm - a.ThicknessMm) > LenTolMm)
                        parts.Add(string.Format(Inv, "T {0:F2}→{1:F2}", b.ThicknessMm, a.ThicknessMm));
                    if (parts.Count == 0) return null;
                    return b.Id + ": " + string.Join(", ", parts.ToArray());
                },
                diff.AddedFeatures, diff.RemovedFeatures, diff.ChangedFeatures,
                ref wallChanged);

            // ---- Holes ----
            int holeChanged = 0;
            DiffById(
                before.Holes, after.Holes,
                h => h.Id,
                h => string.Format(Inv, "{0}: hole D={1:F2}", h.Id, h.DiameterMm),
                (b, a) =>
                {
                    var parts = new List<string>();
                    if (Math.Abs(b.DiameterMm - a.DiameterMm) > DiaTolMm)
                        parts.Add(string.Format(Inv, "D {0:F2}→{1:F2}", b.DiameterMm, a.DiameterMm));
                    if (Math.Abs(b.DepthMm - a.DepthMm) > LenTolMm)
                        parts.Add(string.Format(Inv, "depth {0:F2}→{1:F2}", b.DepthMm, a.DepthMm));
                    if (b.IsThrough != a.IsThrough)
                        parts.Add(string.Format(Inv, "through {0}→{1}", b.IsThrough, a.IsThrough));
                    string posChange = PosDelta(b.PositionMm, a.PositionMm);
                    if (posChange != null) parts.Add("pos " + posChange);
                    if (parts.Count == 0) return null;
                    return b.Id + ": " + string.Join(", ", parts.ToArray());
                },
                diff.AddedFeatures, diff.RemovedFeatures, diff.ChangedFeatures,
                ref holeChanged);

            // ---- Bosses ----
            int bossChanged = 0;
            DiffById(
                before.Bosses, after.Bosses,
                b => b.Id,
                b => string.Format(Inv, "{0}: boss D={1:F2} H={2:F2}", b.Id, b.DiameterMm, b.HeightMm),
                (b, a) =>
                {
                    var parts = new List<string>();
                    if (Math.Abs(b.DiameterMm - a.DiameterMm) > DiaTolMm)
                        parts.Add(string.Format(Inv, "D {0:F2}→{1:F2}", b.DiameterMm, a.DiameterMm));
                    if (Math.Abs(b.HeightMm - a.HeightMm) > LenTolMm)
                        parts.Add(string.Format(Inv, "H {0:F2}→{1:F2}", b.HeightMm, a.HeightMm));
                    string posChange = PosDelta(b.BasePositionMm, a.BasePositionMm);
                    if (posChange != null) parts.Add("pos " + posChange);
                    if (parts.Count == 0) return null;
                    return b.Id + ": " + string.Join(", ", parts.ToArray());
                },
                diff.AddedFeatures, diff.RemovedFeatures, diff.ChangedFeatures,
                ref bossChanged);

            // ---- FilletChains (matched by Id; R + face count are the key fields) ----
            int chainChanged = 0;
            DiffById(
                before.FilletChains, after.FilletChains,
                c => c.Id,
                c => string.Format(Inv, "{0}: fillet R={1:F2} (faces={2})",
                    c.Id, c.RadiusMm, c.FaceIndices != null ? c.FaceIndices.Count : 0),
                (b, a) =>
                {
                    var parts = new List<string>();
                    if (Math.Abs(b.RadiusMm - a.RadiusMm) > RadTolMm)
                        parts.Add(string.Format(Inv, "R {0:F2}→{1:F2}", b.RadiusMm, a.RadiusMm));
                    int bFaces = b.FaceIndices != null ? b.FaceIndices.Count : 0;
                    int aFaces = a.FaceIndices != null ? a.FaceIndices.Count : 0;
                    if (bFaces != aFaces)
                        parts.Add(string.Format(Inv, "faces {0}→{1}", bFaces, aFaces));
                    if (parts.Count == 0) return null;
                    return b.Id + ": " + string.Join(", ", parts.ToArray());
                },
                diff.AddedFeatures, diff.RemovedFeatures, diff.ChangedFeatures,
                ref chainChanged);

            // ---- Fillets (individual) — count only, no per-id diff. ----
            int filletBefore = before.Fillets != null ? before.Fillets.Count : 0;
            int filletAfter = after.Fillets != null ? after.Fillets.Count : 0;
            int filletDelta = filletAfter - filletBefore;

            // ---- Build summary ----
            diff.Summary = BuildSummary(
                faceBefore, faceAfter,
                diff.AddedFeatures, diff.RemovedFeatures, diff.ChangedFeatures,
                filletDelta);

            return diff;
        }

        // -------------------------------------------------------------------
        // WriteJson — terse, hand-rolled (matches FeatureGraphJsonWriter style).
        // -------------------------------------------------------------------
        public static void WriteJson(GraphDiff diff, string path)
        {
            if (diff == null) throw new ArgumentNullException(nameof(diff));
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path empty");
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendFormat(Inv, "  \"summary\": \"{0}\",\n", Escape(diff.Summary));
            sb.AppendFormat(Inv, "  \"face_count_delta\": {0},\n", diff.FaceCountDelta);
            sb.AppendFormat(Inv, "  \"bbox_delta_mm\": [{0}, {1}, {2}],\n",
                Num(diff.BboxDeltaMm[0]), Num(diff.BboxDeltaMm[1]), Num(diff.BboxDeltaMm[2]));
            WriteStringArray(sb, "added", diff.AddedFeatures, false);
            WriteStringArray(sb, "removed", diff.RemovedFeatures, false);
            WriteStringArray(sb, "changed", diff.ChangedFeatures, true);
            sb.AppendLine("}");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        // -------------------------------------------------------------------
        // internals
        // -------------------------------------------------------------------
        private static void DiffById<T>(
            List<T> before, List<T> after,
            Func<T, string> idOf,
            Func<T, string> describe,
            Func<T, T, string> changeOf,
            List<string> added, List<string> removed, List<string> changed,
            ref int changedCount)
        {
            var beforeMap = new Dictionary<string, T>(StringComparer.Ordinal);
            var afterMap = new Dictionary<string, T>(StringComparer.Ordinal);
            if (before != null)
            {
                foreach (var item in before)
                {
                    string id = idOf(item);
                    if (!string.IsNullOrEmpty(id) && !beforeMap.ContainsKey(id))
                        beforeMap[id] = item;
                }
            }
            if (after != null)
            {
                foreach (var item in after)
                {
                    string id = idOf(item);
                    if (!string.IsNullOrEmpty(id) && !afterMap.ContainsKey(id))
                        afterMap[id] = item;
                }
            }
            // Removed: in before, not in after.
            foreach (var kv in beforeMap)
            {
                if (!afterMap.ContainsKey(kv.Key))
                    removed.Add(describe(kv.Value));
            }
            // Added: in after, not in before.
            foreach (var kv in afterMap)
            {
                if (!beforeMap.ContainsKey(kv.Key))
                    added.Add(describe(kv.Value));
            }
            // Changed: in both, key fields differ.
            foreach (var kv in beforeMap)
            {
                T afterItem;
                if (!afterMap.TryGetValue(kv.Key, out afterItem)) continue;
                string entry = changeOf(kv.Value, afterItem);
                if (!string.IsNullOrEmpty(entry))
                {
                    changed.Add(entry);
                    changedCount++;
                }
            }
        }

        /// <summary>Returns "(x1,y1)→(x2,y2)" if XY position moved by more than LenTolMm,
        /// else null. Z-axis change is folded into the same comparison (only reported
        /// if any component differs).</summary>
        private static string PosDelta(double[] beforePos, double[] afterPos)
        {
            if (beforePos == null || afterPos == null) return null;
            int n = Math.Min(beforePos.Length, afterPos.Length);
            if (n < 2) return null;
            bool moved = false;
            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(beforePos[i] - afterPos[i]) > LenTolMm) { moved = true; break; }
            }
            if (!moved) return null;
            if (n >= 3)
            {
                return string.Format(Inv, "({0:F1},{1:F1},{2:F1})→({3:F1},{4:F1},{5:F1})",
                    beforePos[0], beforePos[1], beforePos[2],
                    afterPos[0], afterPos[1], afterPos[2]);
            }
            return string.Format(Inv, "({0:F1},{1:F1})→({2:F1},{3:F1})",
                beforePos[0], beforePos[1], afterPos[0], afterPos[1]);
        }

        private static string BuildSummary(
            int faceBefore, int faceAfter,
            List<string> added, List<string> removed, List<string> changed,
            int filletDelta)
        {
            // Bucket added/removed by feature kind prefix (Walls=W, Holes=H, Bosses=B, FilletChains=FC).
            int addH = 0, addB = 0, addW = 0, addFC = 0;
            int remH = 0, remB = 0, remW = 0, remFC = 0;
            foreach (var s in added)
            {
                if (StartsWithPrefix(s, "H")) addH++;
                else if (StartsWithPrefix(s, "B")) addB++;
                else if (StartsWithPrefix(s, "W")) addW++;
                else if (StartsWithPrefix(s, "FC")) addFC++;
            }
            foreach (var s in removed)
            {
                if (StartsWithPrefix(s, "H")) remH++;
                else if (StartsWithPrefix(s, "B")) remB++;
                else if (StartsWithPrefix(s, "W")) remW++;
                else if (StartsWithPrefix(s, "FC")) remFC++;
            }

            var parts = new List<string>();
            if (addH > 0) parts.Add("+" + addH + "H");
            if (remH > 0) parts.Add("-" + remH + "H");
            if (addB > 0) parts.Add("+" + addB + "B");
            if (remB > 0) parts.Add("-" + remB + "B");
            if (addW > 0) parts.Add("+" + addW + "W");
            if (remW > 0) parts.Add("-" + remW + "W");
            if (addFC > 0) parts.Add("+" + addFC + "FC");
            if (remFC > 0) parts.Add("-" + remFC + "FC");
            if (filletDelta != 0) parts.Add((filletDelta > 0 ? "+" : "") + filletDelta + "F");
            // Highlight up to 2 first changed entries verbatim (often the most informative).
            foreach (var c in changed.Take(2))
                parts.Add(Compact(c));

            string changesStr = parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "no-change";
            return string.Format(Inv, "before={0}f / after={1}f / {2}", faceBefore, faceAfter, changesStr);
        }

        /// <summary>True when s starts with prefix and the next char is a digit (e.g. "H2" but not "Hxx").</summary>
        private static bool StartsWithPrefix(string s, string prefix)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= prefix.Length) return false;
            if (!s.StartsWith(prefix, StringComparison.Ordinal)) return false;
            return char.IsDigit(s[prefix.Length]);
        }

        /// <summary>Compact a "Fx: R 2.00→3.00" entry to "R(Fx)2.00→3.00" for the summary line.</summary>
        private static string Compact(string changeEntry)
        {
            if (string.IsNullOrEmpty(changeEntry)) return changeEntry;
            int colon = changeEntry.IndexOf(':');
            if (colon <= 0 || colon >= changeEntry.Length - 1) return changeEntry;
            string id = changeEntry.Substring(0, colon).Trim();
            string body = changeEntry.Substring(colon + 1).Trim();
            // body like "R 2.00→3.00, faces 5→6" — keep first field, prefix with (id).
            int firstComma = body.IndexOf(',');
            string firstField = firstComma > 0 ? body.Substring(0, firstComma).Trim() : body;
            // Squeeze the space between field name and value (e.g., "R 2→3" → "R(F1)2→3").
            int sp = firstField.IndexOf(' ');
            if (sp > 0)
                return firstField.Substring(0, sp) + "(" + id + ")" + firstField.Substring(sp + 1);
            return firstField + "(" + id + ")";
        }

        private static void WriteStringArray(StringBuilder sb, string key, List<string> items, bool isLast)
        {
            sb.AppendFormat(Inv, "  \"{0}\": [", key);
            if (items == null || items.Count == 0)
            {
                sb.Append("]");
            }
            else
            {
                sb.AppendLine();
                for (int i = 0; i < items.Count; i++)
                {
                    sb.AppendFormat(Inv, "    \"{0}\"{1}", Escape(items[i]), i < items.Count - 1 ? "," : "");
                    sb.AppendLine();
                }
                sb.Append("  ]");
            }
            sb.AppendLine(isLast ? "" : ",");
        }

        private static string Num(double d)
        {
            if (Math.Abs(d) < 1e-12) return "0";
            return d.ToString("0.######", Inv);
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
