using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// FeatureGraph → JSON string / file.
    /// .NET 4.7.2 환경이라 System.Text.Json 미사용. NuGet 의존 추가 안 하기 위해 hand-rolled.
    /// 데이터가 작아서 성능 무관.
    /// </summary>
    // @lat: [[reverse-engineer#FeatureGraph]]
    public static class FeatureGraphJsonWriter
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string ToJson(FeatureGraph graph)
        {
            var sb = new StringBuilder();
            WriteGraph(sb, graph, 0);
            return sb.ToString();
        }

        public static void WriteToFile(FeatureGraph graph, string path)
        {
            string json = ToJson(graph);
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        // ---- 그래프 전체 ----
        private static void WriteGraph(StringBuilder sb, FeatureGraph g, int indent)
        {
            sb.AppendLine("{");
            WriteKV(sb, "body_name", g.BodyName, indent + 1, false);
            WriteKV(sb, "extracted_at", g.ExtractedAt, indent + 1, false);
            WriteKV(sb, "extractor_version", g.ExtractorVersion, indent + 1, false);
            WriteKVArray(sb, "bbox_min_mm", g.BboxMinMm, indent + 1, false);
            WriteKVArray(sb, "bbox_max_mm", g.BboxMaxMm, indent + 1, false);
            WriteKVArray(sb, "bbox_size_mm", g.BboxSizeMm, indent + 1, false);
            WriteKVDict(sb, "face_type_counts", g.FaceTypeCounts, indent + 1, false);

            // faces (배열)
            Indent(sb, indent + 1);
            sb.AppendLine("\"faces\": [");
            for (int i = 0; i < g.Faces.Count; i++)
            {
                WriteFace(sb, g.Faces[i], indent + 2);
                sb.Append(i < g.Faces.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // walls
            Indent(sb, indent + 1);
            sb.AppendLine("\"walls\": [");
            for (int i = 0; i < g.Walls.Count; i++)
            {
                WriteWall(sb, g.Walls[i], indent + 2);
                sb.Append(i < g.Walls.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // fillets
            Indent(sb, indent + 1);
            sb.AppendLine("\"fillets\": [");
            for (int i = 0; i < g.Fillets.Count; i++)
            {
                WriteFillet(sb, g.Fillets[i], indent + 2);
                sb.Append(i < g.Fillets.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // holes
            Indent(sb, indent + 1);
            sb.AppendLine("\"holes\": [");
            for (int i = 0; i < g.Holes.Count; i++)
            {
                WriteHole(sb, g.Holes[i], indent + 2);
                sb.Append(i < g.Holes.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // bosses
            Indent(sb, indent + 1);
            sb.AppendLine("\"bosses\": [");
            for (int i = 0; i < g.Bosses.Count; i++)
            {
                WriteBoss(sb, g.Bosses[i], indent + 2);
                sb.Append(i < g.Bosses.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // slits
            Indent(sb, indent + 1);
            sb.AppendLine("\"slits\": [");
            for (int i = 0; i < g.Slits.Count; i++)
            {
                WriteSlit(sb, g.Slits[i], indent + 2);
                sb.Append(i < g.Slits.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // fillet_chains
            Indent(sb, indent + 1);
            sb.AppendLine("\"fillet_chains\": [");
            for (int i = 0; i < g.FilletChains.Count; i++)
            {
                WriteFilletChain(sb, g.FilletChains[i], indent + 2);
                sb.Append(i < g.FilletChains.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // hole_patterns
            Indent(sb, indent + 1);
            sb.AppendLine("\"hole_patterns\": [");
            for (int i = 0; i < g.HolePatterns.Count; i++)
            {
                WriteHolePattern(sb, g.HolePatterns[i], indent + 2);
                sb.Append(i < g.HolePatterns.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // boss_patterns
            Indent(sb, indent + 1);
            sb.AppendLine("\"boss_patterns\": [");
            for (int i = 0; i < g.BossPatterns.Count; i++)
            {
                WriteBossPattern(sb, g.BossPatterns[i], indent + 2);
                sb.Append(i < g.BossPatterns.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // symmetries
            Indent(sb, indent + 1);
            sb.AppendLine("\"symmetries\": [");
            for (int i = 0; i < g.Symmetries.Count; i++)
            {
                WriteSymmetry(sb, g.Symmetries[i], indent + 2);
                sb.Append(i < g.Symmetries.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // composites (mirrored_grid etc.)
            Indent(sb, indent + 1);
            sb.AppendLine("\"composites\": [");
            var composites = g.Composites ?? new List<CompositeFeature>();
            for (int i = 0; i < composites.Count; i++)
            {
                WriteComposite(sb, composites[i], indent + 2);
                sb.Append(i < composites.Count - 1 ? "," : "");
                sb.AppendLine();
            }
            Indent(sb, indent + 1);
            sb.AppendLine("],");

            // nurbs_face_count + nurbs_face_indices — NURBS / BSpline / procedural
            // free-form face 들의 인덱스. downstream (CAD RE, 사용자) 가 외장 surface
            // 분포를 파악하기 위한 flag. Hole/Boss/Fillet detector 에는 들어가지 않음.
            var nurbsIdx = g.NurbsFaceIndices ?? new List<int>();
            Indent(sb, indent + 1);
            sb.AppendFormat(Inv, "\"nurbs_face_count\": {0},\n", nurbsIdx.Count);
            Indent(sb, indent + 1);
            sb.Append("\"nurbs_face_indices\": [");
            for (int i = 0; i < nurbsIdx.Count; i++)
            {
                sb.Append(nurbsIdx[i]);
                if (i < nurbsIdx.Count - 1) sb.Append(", ");
            }
            sb.AppendLine("],");

            // other_subtype_histogram — diagnostic: actual runtime type name of
            // every face that fell into SurfaceType.Other. Empty {} when all
            // faces classified into a known SurfaceType.
            var otherHist = g.OtherSubtypeHistogram ?? new Dictionary<string, int>();
            WriteKVDict(sb, "other_subtype_histogram", otherHist, indent + 1, true);

            Indent(sb, indent);
            sb.Append("}");
        }

        private static void WriteComposite(StringBuilder sb, CompositeFeature c, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", c.Id);
            sb.AppendFormat(Inv, "\"type\": \"{0}\", ", Escape(c.Type ?? ""));
            sb.Append("\"member_pattern_ids\": [");
            for (int i = 0; i < c.MemberPatternIds.Count; i++)
            {
                sb.AppendFormat(Inv, "\"{0}\"", c.MemberPatternIds[i]);
                if (i < c.MemberPatternIds.Count - 1) sb.Append(", ");
            }
            sb.Append("], ");
            if (string.IsNullOrEmpty(c.SymmetryId))
                sb.Append("\"symmetry_id\": null");
            else
                sb.AppendFormat(Inv, "\"symmetry_id\": \"{0}\"", c.SymmetryId);
            sb.Append("}");
        }

        private static void WriteHolePattern(StringBuilder sb, HolePatternFeature p, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", p.Id);
            sb.AppendFormat(Inv, "\"pattern_type\": \"{0}\", ", p.PatternType);
            sb.AppendFormat(Inv, "\"count\": {0}, ", p.Count);
            sb.Append("\"member_hole_ids\": [");
            for (int i = 0; i < p.MemberHoleIds.Count; i++)
            {
                sb.AppendFormat(Inv, "\"{0}\"", p.MemberHoleIds[i]);
                if (i < p.MemberHoleIds.Count - 1) sb.Append(", ");
            }
            sb.Append("], ");
            sb.AppendFormat(Inv, "\"centroid_mm\": [{0}, {1}, {2}], ",
                Num(p.Centroid_mm[0]), Num(p.Centroid_mm[1]), Num(p.Centroid_mm[2]));
            sb.AppendFormat(Inv, "\"direction_axis\": [{0}, {1}, {2}], ",
                Num(p.DirectionAxis_mm[0]), Num(p.DirectionAxis_mm[1]), Num(p.DirectionAxis_mm[2]));
            sb.AppendFormat(Inv, "\"spacing_mm\": {0}, ", Num(p.Spacing_mm));
            sb.AppendFormat(Inv, "\"row_spacing_mm\": {0}, ", Num(p.RowSpacing_mm));
            sb.AppendFormat(Inv, "\"col_spacing_mm\": {0}, ", Num(p.ColSpacing_mm));
            sb.AppendFormat(Inv, "\"rows\": {0}, ", p.Rows);
            sb.AppendFormat(Inv, "\"cols\": {0}, ", p.Cols);
            sb.AppendFormat(Inv, "\"radius_mm\": {0}, ", Num(p.Radius_mm));
            sb.AppendFormat(Inv, "\"is_partial_match\": {0}, ", p.IsPartialMatch ? "true" : "false");
            sb.AppendFormat(Inv, "\"score_percent\": {0}, ", Num(p.ScorePercent));
            if (string.IsNullOrEmpty(p.MirroredPartnerId))
                sb.Append("\"mirrored_partner_id\": null");
            else
                sb.AppendFormat(Inv, "\"mirrored_partner_id\": \"{0}\"", p.MirroredPartnerId);
            sb.Append("}");
        }

        private static void WriteBossPattern(StringBuilder sb, BossPatternFeature p, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", p.Id);
            sb.AppendFormat(Inv, "\"pattern_type\": \"{0}\", ", p.PatternType);
            sb.AppendFormat(Inv, "\"count\": {0}, ", p.Count);
            sb.Append("\"member_boss_ids\": [");
            for (int i = 0; i < p.MemberBossIds.Count; i++)
            {
                sb.AppendFormat(Inv, "\"{0}\"", p.MemberBossIds[i]);
                if (i < p.MemberBossIds.Count - 1) sb.Append(", ");
            }
            sb.Append("], ");
            sb.AppendFormat(Inv, "\"centroid_mm\": [{0}, {1}, {2}], ",
                Num(p.Centroid_mm[0]), Num(p.Centroid_mm[1]), Num(p.Centroid_mm[2]));
            sb.AppendFormat(Inv, "\"direction_axis\": [{0}, {1}, {2}], ",
                Num(p.DirectionAxis_mm[0]), Num(p.DirectionAxis_mm[1]), Num(p.DirectionAxis_mm[2]));
            sb.AppendFormat(Inv, "\"spacing_mm\": {0}, ", Num(p.Spacing_mm));
            sb.AppendFormat(Inv, "\"row_spacing_mm\": {0}, ", Num(p.RowSpacing_mm));
            sb.AppendFormat(Inv, "\"col_spacing_mm\": {0}, ", Num(p.ColSpacing_mm));
            sb.AppendFormat(Inv, "\"rows\": {0}, ", p.Rows);
            sb.AppendFormat(Inv, "\"cols\": {0}, ", p.Cols);
            sb.AppendFormat(Inv, "\"radius_mm\": {0}, ", Num(p.Radius_mm));
            sb.AppendFormat(Inv, "\"is_partial_match\": {0}, ", p.IsPartialMatch ? "true" : "false");
            sb.AppendFormat(Inv, "\"score_percent\": {0}, ", Num(p.ScorePercent));
            if (string.IsNullOrEmpty(p.MirroredPartnerId))
                sb.Append("\"mirrored_partner_id\": null");
            else
                sb.AppendFormat(Inv, "\"mirrored_partner_id\": \"{0}\"", p.MirroredPartnerId);
            sb.Append("}");
        }

        private static void WriteSymmetry(StringBuilder sb, SymmetryFeature s, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", s.Id);
            sb.AppendFormat(Inv, "\"mirror_plane_normal\": [{0}, {1}, {2}], ",
                Num(s.MirrorPlaneNormal_mm[0]), Num(s.MirrorPlaneNormal_mm[1]), Num(s.MirrorPlaneNormal_mm[2]));
            sb.AppendFormat(Inv, "\"mirror_plane_origin_mm\": [{0}, {1}, {2}], ",
                Num(s.MirrorPlaneOrigin_mm[0]), Num(s.MirrorPlaneOrigin_mm[1]), Num(s.MirrorPlaneOrigin_mm[2]));
            sb.AppendFormat(Inv, "\"score_percent\": {0}", Num(s.ScorePercent));
            sb.Append("}");
        }

        private static void WriteHole(StringBuilder sb, HoleFeature h, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", h.Id);
            sb.AppendFormat(Inv, "\"face_index\": {0}, ", h.CylinderFaceIndex);
            sb.AppendFormat(Inv, "\"diameter_mm\": {0}, ", Num(h.DiameterMm));
            sb.AppendFormat(Inv, "\"depth_mm\": {0}, ", Num(h.DepthMm));
            sb.AppendFormat(Inv, "\"is_through\": {0}, ", h.IsThrough ? "true" : "false");
            sb.AppendFormat(Inv, "\"axis\": [{0}, {1}, {2}], ", Num(h.Axis[0]), Num(h.Axis[1]), Num(h.Axis[2]));
            sb.AppendFormat(Inv, "\"position_mm\": [{0}, {1}, {2}]",
                Num(h.PositionMm[0]), Num(h.PositionMm[1]), Num(h.PositionMm[2]));
            sb.AppendFormat(Inv, ", \"is_conical\": {0}", h.IsConical ? "true" : "false");
            if (h.IsConical)
                sb.AppendFormat(Inv, ", \"half_angle_deg\": {0}", Num(h.HalfAngleDeg));
            sb.Append("}");
        }

        private static void WriteBoss(StringBuilder sb, BossFeature b, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", b.Id);
            sb.AppendFormat(Inv, "\"face_index\": {0}, ", b.CylinderFaceIndex);
            sb.AppendFormat(Inv, "\"diameter_mm\": {0}, ", Num(b.DiameterMm));
            sb.AppendFormat(Inv, "\"height_mm\": {0}, ", Num(b.HeightMm));
            sb.AppendFormat(Inv, "\"axis\": [{0}, {1}, {2}], ", Num(b.Axis[0]), Num(b.Axis[1]), Num(b.Axis[2]));
            sb.AppendFormat(Inv, "\"base_position_mm\": [{0}, {1}, {2}]",
                Num(b.BasePositionMm[0]), Num(b.BasePositionMm[1]), Num(b.BasePositionMm[2]));
            sb.AppendFormat(Inv, ", \"is_conical\": {0}", b.IsConical ? "true" : "false");
            if (b.IsConical)
                sb.AppendFormat(Inv, ", \"half_angle_deg\": {0}", Num(b.HalfAngleDeg));
            sb.Append("}");
        }

        private static void WriteSlit(StringBuilder sb, SlitFeature s, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", s.Id);
            sb.AppendFormat(Inv, "\"face_a\": {0}, ", s.FaceA);
            sb.AppendFormat(Inv, "\"face_b\": {0}, ", s.FaceB);
            sb.AppendFormat(Inv, "\"width_mm\": {0}, ", Num(s.WidthMm));
            sb.AppendFormat(Inv, "\"length_mm\": {0}, ", Num(s.LengthMm));
            sb.AppendFormat(Inv, "\"aspect_ratio\": {0}", Num(s.AspectRatio));
            sb.Append("}");
        }

        private static void WriteFilletChain(StringBuilder sb, FilletChain fc, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", fc.Id);
            sb.AppendFormat(Inv, "\"radius_mm\": {0}, ", Num(fc.RadiusMm));
            sb.AppendFormat(Inv, "\"radius_min_mm\": {0}, ", Num(fc.RadiusMinMm));
            sb.AppendFormat(Inv, "\"radius_max_mm\": {0}, ", Num(fc.RadiusMaxMm));
            sb.AppendFormat(Inv, "\"edge_fillet_count\": {0}, ", fc.EdgeFilletCount);
            sb.AppendFormat(Inv, "\"corner_blend_count\": {0}, ", fc.CornerBlendCount);
            sb.AppendFormat(Inv, "\"is_variable_radius\": {0}, ", fc.IsVariableRadius ? "true" : "false");
            sb.Append("\"face_indices\": [");
            for (int i = 0; i < fc.FaceIndices.Count; i++)
            {
                sb.Append(fc.FaceIndices[i]);
                if (i < fc.FaceIndices.Count - 1) sb.Append(", ");
            }
            sb.Append("]");
            sb.Append("}");
        }

        // ---- face 한 개 ----
        private static void WriteFace(StringBuilder sb, FaceFeature f, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"face_index\": {0}, ", f.FaceIndex);
            sb.AppendFormat(Inv, "\"type\": \"{0}\", ", FaceClassifier.ToTypeKey(f.Type));
            sb.AppendFormat(Inv, "\"area_m2\": {0}, ", Num(f.AreaM2));
            sb.AppendFormat(Inv, "\"radius_m\": {0}, ", Num(f.RadiusM));
            sb.AppendFormat(Inv, "\"is_reversed\": {0}, ", f.IsReversed ? "true" : "false");
            sb.AppendFormat(Inv, "\"normal\": [{0}, {1}, {2}], ", Num(f.Normal[0]), Num(f.Normal[1]), Num(f.Normal[2]));
            sb.AppendFormat(Inv, "\"origin\": [{0}, {1}, {2}]", Num(f.Origin[0]), Num(f.Origin[1]), Num(f.Origin[2]));
            sb.Append("}");
        }

        // ---- wall 한 개 ----
        private static void WriteWall(StringBuilder sb, WallFeature w, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", w.Id);
            sb.AppendFormat(Inv, "\"face_a\": {0}, ", w.FaceA);
            sb.AppendFormat(Inv, "\"face_b\": {0}, ", w.FaceB);
            sb.AppendFormat(Inv, "\"thickness_mm\": {0}, ", Num(w.ThicknessMm));
            sb.AppendFormat(Inv, "\"area_mm2\": {0}, ", Num(w.AreaMm2));
            sb.AppendFormat(Inv, "\"normal\": [{0}, {1}, {2}], ", Num(w.Normal[0]), Num(w.Normal[1]), Num(w.Normal[2]));
            sb.AppendFormat(Inv, "\"is_inverted\": {0}", w.IsInverted ? "true" : "false");
            if (!string.IsNullOrEmpty(w.ConsistencyCheck))
            {
                sb.AppendFormat(Inv, ", \"consistency_check\": \"{0}\"", w.ConsistencyCheck);
            }
            sb.Append("}");
        }

        // ---- fillet 한 개 ----
        private static void WriteFillet(StringBuilder sb, FilletFeature fl, int indent)
        {
            Indent(sb, indent);
            sb.Append("{");
            sb.AppendFormat(Inv, "\"id\": \"{0}\", ", fl.Id);
            sb.AppendFormat(Inv, "\"face_index\": {0}, ", fl.FaceIndex);
            sb.AppendFormat(Inv, "\"radius_mm\": {0}, ", Num(fl.RadiusMm));
            sb.AppendFormat(Inv, "\"blend_type\": \"{0}\", ", fl.BlendType);
            sb.Append("\"adjacent_faces\": [");
            for (int i = 0; i < fl.AdjacentFaces.Count; i++)
            {
                sb.Append(fl.AdjacentFaces[i]);
                if (i < fl.AdjacentFaces.Count - 1) sb.Append(", ");
            }
            sb.Append("]");
            sb.Append("}");
        }

        // ---- 기본 유틸 ----
        private static void Indent(StringBuilder sb, int level)
        {
            for (int i = 0; i < level; i++) sb.Append("  ");
        }

        private static string Num(double d)
        {
            // 작은 값(<1e-12)은 0 으로 정규화
            if (Math.Abs(d) < 1e-12) return "0";
            return d.ToString("0.######", Inv);
        }

        private static string Escape(string s)
        {
            if (s == null) return "null";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteKV(StringBuilder sb, string key, string value, int indent, bool isLast)
        {
            Indent(sb, indent);
            sb.AppendFormat("\"{0}\": \"{1}\"{2}\n", key, Escape(value), isLast ? "" : ",");
        }

        private static void WriteKVArray(StringBuilder sb, string key, double[] arr, int indent, bool isLast)
        {
            Indent(sb, indent);
            sb.AppendFormat("\"{0}\": [", key);
            for (int i = 0; i < arr.Length; i++)
            {
                sb.Append(Num(arr[i]));
                if (i < arr.Length - 1) sb.Append(", ");
            }
            sb.AppendFormat("]{0}\n", isLast ? "" : ",");
        }

        private static void WriteKVDict(StringBuilder sb, string key, Dictionary<string, int> dict, int indent, bool isLast)
        {
            Indent(sb, indent);
            sb.AppendFormat("\"{0}\": {{", key);
            int i = 0;
            foreach (var kv in dict)
            {
                sb.AppendFormat("\"{0}\": {1}", Escape(kv.Key), kv.Value);
                if (i < dict.Count - 1) sb.Append(", ");
                i++;
            }
            sb.AppendFormat("}}{0}\n", isLast ? "" : ",");
        }
    }
}
