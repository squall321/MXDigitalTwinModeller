namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Contact
{
    /// <summary>
    /// 접촉 타입
    /// </summary>
    public enum ContactType
    {
        /// <summary>면 대 면 접촉 (coplanar overlap)</summary>
        Face,
        /// <summary>에지 접촉 (공유 에지, tied 미적용)</summary>
        Edge
    }

    /// <summary>
    /// 접촉 쌍 정보
    /// </summary>
    public class ContactPairInfo
    {
        /// <summary>+ 방향 법선을 가진 면</summary>
        public DesignFace FaceA { get; set; }

        /// <summary>- 방향 법선을 가진 면</summary>
        public DesignFace FaceB { get; set; }

        /// <summary>FaceA가 속한 바디</summary>
        public DesignBody BodyA { get; set; }

        /// <summary>FaceB가 속한 바디</summary>
        public DesignBody BodyB { get; set; }

        /// <summary>Named Selection 접두사 ("NodeSet", "EdgeContact" 또는 키워드)</summary>
        public string Prefix { get; set; }

        /// <summary>페어 인덱스 (1부터 시작)</summary>
        public int PairIndex { get; set; }

        /// <summary>접촉 면적 (㎡, Edge 접촉은 0)</summary>
        public double Area { get; set; }

        /// <summary>접촉 타입</summary>
        public ContactType Type { get; set; }

        /// <summary>A면 Named Selection 이름</summary>
        public string NameA
        {
            get { return string.Format("{0}_{1}", Prefix, 100000 + PairIndex); }
        }

        /// <summary>B면 Named Selection 이름</summary>
        public string NameB
        {
            get { return string.Format("{0}_{1}", Prefix, 200000 + PairIndex); }
        }
    }
}
