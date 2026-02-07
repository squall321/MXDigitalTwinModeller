using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Laminate;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Laminate
{
    /// <summary>
    /// 직사각형 적층 모델 생성 서비스
    /// - 각 레이어를 개별 DesignBody로 생성
    /// - 인접 레이어 간 면이 기하학적으로 일치 (conformal mesh 가능)
    /// - 계면 Named Selection 자동 생성
    /// </summary>
    public class RectangularLaminateService
    {
        /// <summary>
        /// 직사각형 적층 모델 생성
        /// </summary>
        public List<DesignBody> CreateRectangularLaminate(Part part, RectangularLaminateParameters p)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));

            var result = new List<DesignBody>();
            double widthM = GeometryUtils.MmToMeters(p.WidthMm);
            double lengthM = GeometryUtils.MmToMeters(p.LengthMm);
            double currentOffsetM = 0.0;

            for (int i = 0; i < p.Layers.Count; i++)
            {
                var layer = p.Layers[i];
                double thicknessM = GeometryUtils.MmToMeters(layer.ThicknessMm);

                DesignBody layerBody = CreateLayerBody(
                    part, widthM, lengthM, thicknessM,
                    currentOffsetM, p.Direction, layer.Name);

                result.Add(layerBody);
                currentOffsetM += thicknessM;
            }

            // 계면 Named Selection 생성
            if (p.CreateInterfaceNamedSelections && result.Count > 1)
            {
                CreateInterfaceGroups(part, result, p);
            }

            return result;
        }

        /// <summary>
        /// 개별 레이어 바디 생성
        /// </summary>
        private DesignBody CreateLayerBody(Part part,
            double widthM, double lengthM, double thicknessM,
            double offsetM, StackingDirection dir, string name)
        {
            double halfW = widthM / 2.0;
            double halfL = lengthM / 2.0;

            Plane profilePlane;
            Point p1, p2, p3, p4;

            switch (dir)
            {
                case StackingDirection.Z:
                    profilePlane = Plane.Create(Frame.Create(
                        Point.Create(0, 0, offsetM), Direction.DirX, Direction.DirY));
                    p1 = Point.Create(-halfW, -halfL, offsetM);
                    p2 = Point.Create(halfW, -halfL, offsetM);
                    p3 = Point.Create(halfW, halfL, offsetM);
                    p4 = Point.Create(-halfW, halfL, offsetM);
                    break;

                case StackingDirection.X:
                    profilePlane = Plane.Create(Frame.Create(
                        Point.Create(offsetM, 0, 0), Direction.DirY, Direction.DirZ));
                    p1 = Point.Create(offsetM, -halfW, -halfL);
                    p2 = Point.Create(offsetM, halfW, -halfL);
                    p3 = Point.Create(offsetM, halfW, halfL);
                    p4 = Point.Create(offsetM, -halfW, halfL);
                    break;

                case StackingDirection.Y:
                    profilePlane = Plane.Create(Frame.Create(
                        Point.Create(0, offsetM, 0), Direction.DirZ, Direction.DirX));
                    p1 = Point.Create(-halfL, offsetM, -halfW);
                    p2 = Point.Create(halfL, offsetM, -halfW);
                    p3 = Point.Create(halfL, offsetM, halfW);
                    p4 = Point.Create(-halfL, offsetM, halfW);
                    break;

                default:
                    throw new ArgumentException("Invalid stacking direction");
            }

            var curves = new List<ITrimmedCurve>
            {
                CurveSegment.Create(p1, p2),
                CurveSegment.Create(p2, p3),
                CurveSegment.Create(p3, p4),
                CurveSegment.Create(p4, p1)
            };

            Profile profile = new Profile(profilePlane, curves);
            Body body = Body.ExtrudeProfile(profile, thicknessM);
            return BodyBuilder.CreateDesignBody(part, name, body);
        }

        /// <summary>
        /// 레이어 간 계면 Named Selection 생성
        /// </summary>
        private void CreateInterfaceGroups(Part part,
            List<DesignBody> bodies, RectangularLaminateParameters p)
        {
            double cumulativeOffsetM = 0.0;

            for (int i = 0; i < p.Layers.Count - 1; i++)
            {
                cumulativeOffsetM += GeometryUtils.MmToMeters(p.Layers[i].ThicknessMm);

                Direction normalDir;
                FaceNamingHelper.Axis axis;

                switch (p.Direction)
                {
                    case StackingDirection.X:
                        normalDir = Direction.DirX;
                        axis = FaceNamingHelper.Axis.X;
                        break;
                    case StackingDirection.Y:
                        normalDir = Direction.DirY;
                        axis = FaceNamingHelper.Axis.Y;
                        break;
                    case StackingDirection.Z:
                    default:
                        normalDir = Direction.DirZ;
                        axis = FaceNamingHelper.Axis.Z;
                        break;
                }

                // layer[i]의 상면 + layer[i+1]의 하면 (같은 위치)
                var topFaces = FaceNamingHelper.FindPlanarFaces(
                    bodies[i], normalDir, cumulativeOffsetM, axis);
                var bottomFaces = FaceNamingHelper.FindPlanarFaces(
                    bodies[i + 1], normalDir, cumulativeOffsetM, axis);

                var interfaceFaces = new List<DesignFace>();
                interfaceFaces.AddRange(topFaces);
                interfaceFaces.AddRange(bottomFaces);

                if (interfaceFaces.Count > 0)
                {
                    string groupName = string.Format("Interface_{0}_{1}",
                        p.Layers[i].Name, p.Layers[i + 1].Name);
                    FaceNamingHelper.NameFaces(part, interfaceFaces, groupName);
                }
            }
        }
    }
}
