using System;
using System.Collections.Generic;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry
{
    /// <summary>
    /// 프로파일 생성을 위한 헬퍼 클래스
    /// 2D 스케치 곡선 생성 유틸리티 제공
    /// </summary>
    public class ProfileBuilder
    {
        private readonly List<ITrimmedCurve> curves;
        private readonly Plane plane;

        public ProfileBuilder(Plane plane)
        {
            this.plane = plane;
            this.curves = new List<ITrimmedCurve>();
        }

        public ProfileBuilder() : this(Plane.PlaneXY)
        {
        }

        /// <summary>
        /// 두 점을 연결하는 직선 추가
        /// </summary>
        public ProfileBuilder AddLine(Point start, Point end)
        {
            curves.Add(CurveSegment.Create(start, end));
            return this;
        }

        /// <summary>
        /// 원호 추가
        /// </summary>
        public ProfileBuilder AddArc(Point center, double radius, double startAngle, double endAngle)
        {
            Circle circle = Circle.Create(Frame.Create(center, Direction.DirX, Direction.DirY), radius);
            curves.Add(CurveSegment.Create(circle, Interval.Create(startAngle, endAngle)));
            return this;
        }

        /// <summary>
        /// 원 추가
        /// </summary>
        public ProfileBuilder AddCircle(Point center, double radius)
        {
            Circle circle = Circle.Create(Frame.Create(center, Direction.DirX, Direction.DirY), radius);
            curves.Add(CurveSegment.Create(circle));
            return this;
        }

        /// <summary>
        /// 직사각형 추가
        /// </summary>
        public ProfileBuilder AddRectangle(Point center, double width, double height)
        {
            double halfWidth = width / 2;
            double halfHeight = height / 2;

            Point p1 = Point.Create(center.X - halfWidth, center.Y - halfHeight, center.Z);
            Point p2 = Point.Create(center.X + halfWidth, center.Y - halfHeight, center.Z);
            Point p3 = Point.Create(center.X + halfWidth, center.Y + halfHeight, center.Z);
            Point p4 = Point.Create(center.X - halfWidth, center.Y + halfHeight, center.Z);

            AddLine(p1, p2);
            AddLine(p2, p3);
            AddLine(p3, p4);
            AddLine(p4, p1);

            return this;
        }

        /// <summary>
        /// ITrimmedCurve 직접 추가
        /// </summary>
        public ProfileBuilder AddCurve(ITrimmedCurve curve)
        {
            curves.Add(curve);
            return this;
        }

        /// <summary>
        /// Profile 생성
        /// </summary>
        public Profile Build()
        {
            return new Profile(plane, curves);
        }

        /// <summary>
        /// 곡선 리스트 반환
        /// </summary>
        public List<ITrimmedCurve> GetCurves()
        {
            return new List<ITrimmedCurve>(curves);
        }

        /// <summary>
        /// 초기화
        /// </summary>
        public void Clear()
        {
            curves.Clear();
        }
    }
}
