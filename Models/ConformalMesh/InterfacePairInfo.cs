using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ConformalMesh
{
    /// <summary>
    /// 계면 타입
    /// </summary>
    public enum InterfaceType
    {
        /// <summary>평면 접촉</summary>
        Planar,

        /// <summary>원통 접촉</summary>
        Cylindrical,

        /// <summary>복합 접촉</summary>
        Mixed
    }

    /// <summary>
    /// 두 바디 간 계면 정보
    /// </summary>
    public class InterfacePairInfo
    {
        /// <summary>바디 A</summary>
        public DesignBody BodyA { get; set; }

        /// <summary>바디 B</summary>
        public DesignBody BodyB { get; set; }

        /// <summary>바디 A 측 접촉면 목록</summary>
        public List<DesignFace> FacesA { get; set; }

        /// <summary>바디 B 측 접촉면 목록</summary>
        public List<DesignFace> FacesB { get; set; }

        /// <summary>계면 타입</summary>
        public InterfaceType Type { get; set; }

        /// <summary>총 접촉 면적 [mm²]</summary>
        public double TotalAreaMm2 { get; set; }

        /// <summary>Named Selection 그룹 이름</summary>
        public string GroupName { get; set; }

        /// <summary>선택 여부 (UI)</summary>
        public bool IsSelected { get; set; }

        public InterfacePairInfo()
        {
            FacesA = new List<DesignFace>();
            FacesB = new List<DesignFace>();
            Type = InterfaceType.Planar;
            IsSelected = true;
        }

        public override string ToString()
        {
            string nameA = BodyA != null ? (BodyA.Name ?? "?") : "?";
            string nameB = BodyB != null ? (BodyB.Name ?? "?") : "?";
            return string.Format("{0} <-> {1} ({2}, {3:F2} mm2)", nameA, nameB, Type, TotalAreaMm2);
        }
    }
}
