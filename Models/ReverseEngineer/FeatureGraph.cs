using System;
using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 한 body 의 추출된 feature graph.
    /// JSON 직렬화 대상.
    /// </summary>
    // @lat: [[reverse-engineer#FeatureGraph]]
    public class FeatureGraph
    {
        /// <summary>body 이름 (DesignBody.Name)</summary>
        public string BodyName { get; set; }

        /// <summary>AABB minimum corner (mm)</summary>
        public double[] BboxMinMm { get; set; }

        /// <summary>AABB maximum corner (mm)</summary>
        public double[] BboxMaxMm { get; set; }

        /// <summary>AABB 크기 (mm) — Lx, Ly, Lz</summary>
        public double[] BboxSizeMm { get; set; }

        /// <summary>면 통계 (type 별 count)</summary>
        public Dictionary<string, int> FaceTypeCounts { get; set; }

        /// <summary>전체 face 정보</summary>
        public List<FaceFeature> Faces { get; set; }

        /// <summary>검출된 wall (평행 평면 쌍)</summary>
        public List<WallFeature> Walls { get; set; }

        /// <summary>검출된 fillet (blend face)</summary>
        public List<FilletFeature> Fillets { get; set; }

        /// <summary>검출된 hole (Stage 1 보강: cylinder discrimination)</summary>
        public List<HoleFeature> Holes { get; set; }

        /// <summary>검출된 boss (평면에서 솟은 외부 cylinder)</summary>
        public List<BossFeature> Bosses { get; set; }

        /// <summary>검출된 slit (얇은 평행면 쌍, 안테나 라인 등)</summary>
        public List<SlitFeature> Slits { get; set; }

        /// <summary>Tangent + same-R 로 묶인 fillet chain</summary>
        public List<FilletChain> FilletChains { get; set; }

        /// <summary>Stage 3: hole 패턴 (Linear / Grid / Circular)</summary>
        public List<HolePatternFeature> HolePatterns { get; set; }

        /// <summary>Stage 3: boss 패턴 (Linear / Grid / Circular)</summary>
        public List<BossPatternFeature> BossPatterns { get; set; }

        /// <summary>Stage 3: mirror symmetry plane 후보</summary>
        public List<SymmetryFeature> Symmetries { get; set; }

        /// <summary>Stage 3: composite (mirrored grid 등) — pattern + symmetry 의 상위 그룹</summary>
        public List<CompositeFeature> Composites { get; set; }

        /// <summary>
        /// NURBS/BSpline (free-form) face 인덱스 목록.
        /// 미관용/외장용 surface 로 hole/boss/fillet detector 대상에서 제외되지만,
        /// downstream (CAD RE, 사용자) 가 가시화 / 통계를 위해 식별할 수 있도록 노출.
        /// </summary>
        public List<int> NurbsFaceIndices { get; set; }

        /// <summary>
        /// Diagnostic: 'Other' 로 분류된 face 들의 actual runtime type name 빈도.
        /// Key = face.Geometry.GetType().FullName (또는 "(null)").
        /// Value = count.
        /// FaceClassifier 가 분류 cascade 의 마지막 단계 (Other) 에서 채운다.
        /// 새 SDK 버전에서 등장한 surface subtype 을 발견 → 검출 분기 추가에 사용.
        /// </summary>
        public Dictionary<string, int> OtherSubtypeHistogram { get; set; }

        /// <summary>
        /// Face-level adjacency map. Built by AdjacencyBuilder during extraction.
        /// Key = face_index, Value = neighbor list + edge relations.
        /// Cycle 32: exposed on the graph so ModificationService can introspect
        /// (e.g. detect adjacent fillets before OffsetFaces).
        /// NOTE: not JSON-serialised (Edge / EdgeRelation are SC native).
        /// </summary>
        public Dictionary<int, FaceAdjacency> Adjacency { get; set; }

        /// <summary>분석 메타데이터</summary>
        public string ExtractedAt { get; set; }
        public string ExtractorVersion { get; set; }

        // -------------------------------------------------------------------
        // Multi-shell extension (opt-in via RealModelPipeline.ExtractAllBodies).
        // When ExtractAllBodies = false (default), Shells stays null/empty and
        // the graph above represents ONLY the first DesignBody — preserving
        // the original single-shell behavior bit-for-bit.
        //
        // When ExtractAllBodies = true, the top-level FeatureGraph still holds
        // the first body's features (so existing consumers see no change), but
        // <see cref="Shells"/> additionally carries one FeatureGraph per body
        // discovered by PartBodyTraversal.FindAllDesignBodies. The "aggregate"
        // counters (<see cref="AggregateFaceCount"/> etc.) and union bbox
        // (<see cref="AggregateBboxMinMm"/> / Max / Size) are written by
        // RealModelPipeline after each shell has been extracted.
        // -------------------------------------------------------------------

        /// <summary>per-shell graphs (null when single-shell). Index 0 ==
        /// the same DesignBody this top-level graph was extracted from.</summary>
        public List<FeatureGraph> Shells { get; set; }

        /// <summary>Aggregate face count across every shell in <see cref="Shells"/>.
        /// Zero when not in multi-shell mode.</summary>
        public int AggregateFaceCount { get; set; }

        /// <summary>Aggregate wall count across all shells.</summary>
        public int AggregateWallCount { get; set; }

        /// <summary>Aggregate fillet count across all shells.</summary>
        public int AggregateFilletCount { get; set; }

        /// <summary>Aggregate hole count across all shells.</summary>
        public int AggregateHoleCount { get; set; }

        /// <summary>Aggregate boss count across all shells.</summary>
        public int AggregateBossCount { get; set; }

        /// <summary>Aggregate slit count across all shells.</summary>
        public int AggregateSlitCount { get; set; }

        /// <summary>Number of shells (== Shells.Count when populated).</summary>
        public int ShellCount { get; set; }

        /// <summary>Union AABB minimum across every shell (mm). Null when not multi-shell.</summary>
        public double[] AggregateBboxMinMm { get; set; }

        /// <summary>Union AABB maximum across every shell (mm). Null when not multi-shell.</summary>
        public double[] AggregateBboxMaxMm { get; set; }

        /// <summary>Union AABB size (mm) — AggregateBboxMaxMm - AggregateBboxMinMm.</summary>
        public double[] AggregateBboxSizeMm { get; set; }

        public FeatureGraph()
        {
            FaceTypeCounts = new Dictionary<string, int>();
            Faces = new List<FaceFeature>();
            Walls = new List<WallFeature>();
            Fillets = new List<FilletFeature>();
            Holes = new List<HoleFeature>();
            Bosses = new List<BossFeature>();
            Slits = new List<SlitFeature>();
            FilletChains = new List<FilletChain>();
            HolePatterns = new List<HolePatternFeature>();
            BossPatterns = new List<BossPatternFeature>();
            Symmetries = new List<SymmetryFeature>();
            Composites = new List<CompositeFeature>();
            NurbsFaceIndices = new List<int>();
            OtherSubtypeHistogram = new Dictionary<string, int>();
            ExtractedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ExtractorVersion = "0.3.3-other-histogram";
            // Multi-shell fields left null/0 by default — populated only when
            // RealModelPipeline.ExtractAllBodies = true.
        }
    }
}
