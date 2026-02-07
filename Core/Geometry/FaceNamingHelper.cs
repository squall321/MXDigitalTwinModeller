using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Face 이름 지정을 위한 헬퍼 클래스
    /// 시뮬레이션 경계조건 설정을 위해 Face를 식별 가능하게 명명
    /// </summary>
    public static class FaceNamingHelper
    {
        private const double Tolerance = 1e-6;  // 위치 비교 허용 오차 (미터 단위)

        /// <summary>
        /// 평면 Face 찾기 (법선 방향과 위치 기준)
        /// </summary>
        public static List<DesignFace> FindPlanarFaces(DesignBody body, Direction normalDirection, double? position = null, Axis? axis = null)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            var result = new List<DesignFace>();

            foreach (var designFace in body.Faces)
            {
                // Plane인지 확인
                if (!(designFace.Shape.Geometry is Plane plane))
                    continue;

                // 법선 방향 확인 (평행 또는 반평행)
                double dot = Math.Abs(Vector.Dot(plane.Frame.DirZ.UnitVector, normalDirection.UnitVector));
                if (dot < 0.99)  // 거의 평행하지 않으면 스킵
                    continue;

                // 위치 확인 (지정된 경우)
                if (position.HasValue && axis.HasValue)
                {
                    double facePosition = GetPositionOnAxis(plane.Frame.Origin, axis.Value);
                    if (Math.Abs(facePosition - position.Value) > Tolerance)
                        continue;
                }

                result.Add(designFace);
            }

            return result;
        }

        /// <summary>
        /// Z 좌표 기준 최대/최소 평면 Face 찾기
        /// </summary>
        public static DesignFace FindExtremePlanarFace(DesignBody body, Direction normalDirection, bool findMax)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            DesignFace extremeFace = null;
            double extremeZ = findMax ? double.MinValue : double.MaxValue;

            foreach (var designFace in body.Faces)
            {
                // Plane인지 확인
                if (!(designFace.Shape.Geometry is Plane plane))
                    continue;

                // 법선 방향 확인
                double dot = Math.Abs(Vector.Dot(plane.Frame.DirZ.UnitVector, normalDirection.UnitVector));
                if (dot < 0.99)
                    continue;

                double z = plane.Frame.Origin.Z;

                if ((findMax && z > extremeZ) || (!findMax && z < extremeZ))
                {
                    extremeZ = z;
                    extremeFace = designFace;
                }
            }

            return extremeFace;
        }

        /// <summary>
        /// Face에 이름 지정 (Group 생성)
        /// SpaceClaim의 Group 기능을 사용하여 Face를 명명된 그룹으로 만듦
        /// </summary>
        public static void NameFace(Part part, DesignFace face, string name)
        {
            if (part == null || face == null || string.IsNullOrEmpty(name))
                return;

            try
            {
                // Face를 포함하는 컬렉션 생성
                var faceCollection = new List<IDocObject> { face };

                // Group 생성 (Named Selection과 동일한 역할)
                Group.Create(part, name, faceCollection);

                System.Diagnostics.Debug.WriteLine($"Group created: '{name}' with 1 face");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create group '{name}': {ex.Message}");
            }
        }

        /// <summary>
        /// 여러 Face를 하나의 그룹으로 명명
        /// </summary>
        public static void NameFaces(Part part, IEnumerable<DesignFace> faces, string baseName)
        {
            if (part == null || faces == null || !faces.Any())
                return;

            try
            {
                // 모든 Face를 포함하는 컬렉션 생성
                var faceCollection = new List<IDocObject>();
                foreach (var face in faces)
                {
                    faceCollection.Add(face);
                }

                // Group 생성 (모든 Face 포함)
                Group.Create(part, baseName, faceCollection);

                System.Diagnostics.Debug.WriteLine($"Group created: '{baseName}' with {faceCollection.Count} face(s)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create group '{baseName}': {ex.Message}");
            }
        }

        /// <summary>
        /// 축 상의 위치 추출
        /// </summary>
        private static double GetPositionOnAxis(Point point, Axis axis)
        {
            switch (axis)
            {
                case Axis.X:
                    return point.X;
                case Axis.Y:
                    return point.Y;
                case Axis.Z:
                    return point.Z;
                default:
                    throw new ArgumentException("Invalid axis", nameof(axis));
            }
        }

        /// <summary>
        /// 좌표축 열거형
        /// </summary>
        public enum Axis
        {
            X,
            Y,
            Z
        }
    }
}
