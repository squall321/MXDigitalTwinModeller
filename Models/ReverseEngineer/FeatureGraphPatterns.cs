using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer
{
    /// <summary>
    /// 패턴 종류. Stage 3 — feature grouping.
    /// </summary>
    public enum PatternType
    {
        /// <summary>패턴 미발견 (단독 feature)</summary>
        Single = 0,
        /// <summary>3개 이상 collinear + 등간격</summary>
        Linear = 1,
        /// <summary>rows * cols 격자</summary>
        Grid = 2,
        /// <summary>같은 중심, 같은 반경, 등각도</summary>
        Circular = 3,
    }

    /// <summary>
    /// 여러 HoleFeature 를 묶은 패턴 (Linear / Grid / Circular).
    /// </summary>
    public class HolePatternFeature
    {
        /// <summary>식별자 (예: "HP1")</summary>
        public string Id { get; set; }

        /// <summary>패턴 종류</summary>
        public PatternType PatternType { get; set; }

        /// <summary>이 패턴에 속한 hole id 목록</summary>
        public List<string> MemberHoleIds { get; set; }

        /// <summary>멤버 hole 위치 평균 (mm)</summary>
        public double[] Centroid_mm { get; set; }

        /// <summary>패턴 방향 (Linear/Grid 의 row 방향, Circular 의 normal). 단위 벡터.</summary>
        public double[] DirectionAxis_mm { get; set; }

        /// <summary>Linear 의 간격 (mm). Grid/Circular 에선 0.</summary>
        public double Spacing_mm { get; set; }

        /// <summary>Grid 의 row 간격 (mm).</summary>
        public double RowSpacing_mm { get; set; }

        /// <summary>Grid 의 col 간격 (mm).</summary>
        public double ColSpacing_mm { get; set; }

        /// <summary>Grid 의 row 개수.</summary>
        public int Rows { get; set; }

        /// <summary>Grid 의 col 개수.</summary>
        public int Cols { get; set; }

        /// <summary>Circular 의 반경 (centroid → member) (mm).</summary>
        public double Radius_mm { get; set; }

        /// <summary>멤버 개수.</summary>
        public int Count { get; set; }

        /// <summary>
        /// 부분 매칭 여부. true 면 ideal pattern 의 outlier 가 1개 이상 제외된 부분집합으로 인식됨.
        /// (e.g., 6 holes on a circle 중 5 만 등각이고 1 outlier 인 경우 → Circular(5) + IsPartialMatch=true).
        /// </summary>
        public bool IsPartialMatch { get; set; }

        /// <summary>
        /// Mirror symmetry pair 의 상대 패턴 id. null 이면 mirror partner 없음.
        /// PatternDetector.DetectComposites() 에서 채움.
        /// </summary>
        public string MirroredPartnerId { get; set; }

        /// <summary>
        /// 0-100. 패턴 매칭 score = matched_members / candidate_members * 100.
        /// Full match 시 100. Partial match 시 80~99 정도.
        /// </summary>
        public double ScorePercent { get; set; }

        public HolePatternFeature()
        {
            MemberHoleIds = new List<string>();
            Centroid_mm = new[] { 0.0, 0.0, 0.0 };
            DirectionAxis_mm = new[] { 0.0, 0.0, 0.0 };
            IsPartialMatch = false;
            MirroredPartnerId = null;
            ScorePercent = 100.0;
        }
    }

    /// <summary>
    /// 여러 BossFeature 를 묶은 패턴. 필드 의미는 HolePatternFeature 와 동일.
    /// </summary>
    public class BossPatternFeature
    {
        public string Id { get; set; }
        public PatternType PatternType { get; set; }
        public List<string> MemberBossIds { get; set; }
        public double[] Centroid_mm { get; set; }
        public double[] DirectionAxis_mm { get; set; }
        public double Spacing_mm { get; set; }
        public double RowSpacing_mm { get; set; }
        public double ColSpacing_mm { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public double Radius_mm { get; set; }
        public int Count { get; set; }

        /// <summary>HolePatternFeature.IsPartialMatch 와 동일 의미 (boss 버전).</summary>
        public bool IsPartialMatch { get; set; }

        /// <summary>HolePatternFeature.MirroredPartnerId 와 동일 의미 (boss 버전).</summary>
        public string MirroredPartnerId { get; set; }

        /// <summary>HolePatternFeature.ScorePercent 와 동일 의미 (boss 버전).</summary>
        public double ScorePercent { get; set; }

        public BossPatternFeature()
        {
            MemberBossIds = new List<string>();
            Centroid_mm = new[] { 0.0, 0.0, 0.0 };
            DirectionAxis_mm = new[] { 0.0, 0.0, 0.0 };
            IsPartialMatch = false;
            MirroredPartnerId = null;
            ScorePercent = 100.0;
        }
    }

    /// <summary>
    /// Mirror symmetry feature. 좌우 / 상하 / 앞뒤 등.
    /// </summary>
    public class SymmetryFeature
    {
        /// <summary>식별자 (예: "S1")</summary>
        public string Id { get; set; }

        /// <summary>mirror plane normal (단위 벡터)</summary>
        public double[] MirrorPlaneNormal_mm { get; set; }

        /// <summary>mirror plane 위의 한 점 (mm). 보통 bbox center.</summary>
        public double[] MirrorPlaneOrigin_mm { get; set; }

        /// <summary>0-100. 미러링 했을 때 매칭되는 feature 비율.</summary>
        public double ScorePercent { get; set; }

        public SymmetryFeature()
        {
            MirrorPlaneNormal_mm = new[] { 0.0, 0.0, 0.0 };
            MirrorPlaneOrigin_mm = new[] { 0.0, 0.0, 0.0 };
        }
    }

    /// <summary>
    /// 복합 (composite) 패턴 — 여러 개의 lower-level pattern 을 하나의 named cluster 로 묶는다.
    /// 예: "mirrored_grid" = symmetry plane 양쪽에 동일 grid 2개. "concentric_circles" = 같은 중심
    /// 다른 반경의 circular 패턴들.
    /// </summary>
    public class CompositeFeature
    {
        /// <summary>식별자 (예: "C1")</summary>
        public string Id { get; set; }

        /// <summary>composite 종류. 자유 문자열. (예: "mirrored_grid", "mirrored_circular", "mirrored_linear")</summary>
        public string Type { get; set; }

        /// <summary>이 composite 에 속한 lower-level pattern id 목록 (HP*/BP*).</summary>
        public List<string> MemberPatternIds { get; set; }

        /// <summary>관련 symmetry feature id (있으면).</summary>
        public string SymmetryId { get; set; }

        public CompositeFeature()
        {
            MemberPatternIds = new List<string>();
            Type = "";
            SymmetryId = null;
        }
    }
}
