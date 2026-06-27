using System;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// face.Shape.Geometry 의 런타임 타입을 검사해 SurfaceType + 핵심 메트릭을 추출한다.
    /// 모든 길이는 SI 미터.
    /// </summary>
    // @lat: [[reverse-engineer#FaceClassifier]]
    public static class FaceClassifier
    {
        /// <summary>
        /// 한 face 를 분류해 FaceFeature 로 변환.
        /// </summary>
        /// <param name="designFace">대상 face</param>
        /// <param name="faceIndex">body 내 0-based 인덱스</param>
        public static FaceFeature Classify(DesignFace designFace, int faceIndex)
        {
            string ignored;
            return Classify(designFace, faceIndex, out ignored);
        }

        /// <summary>
        /// Classify overload that ALSO emits the runtime type name when the face
        /// falls into <see cref="SurfaceType.Other"/>. Caller can aggregate these
        /// names into a histogram for diagnostic / SDK-discovery purposes.
        ///
        /// otherSubtypeName == null  → face was classified as a known type.
        /// otherSubtypeName != null  → face is Other; value = face.Geometry.GetType().FullName
        ///                              (or "(null)" if Geometry itself was null).
        /// </summary>
        public static FaceFeature Classify(DesignFace designFace, int faceIndex, out string otherSubtypeName)
        {
            otherSubtypeName = null;
            if (designFace == null)
                throw new ArgumentNullException(nameof(designFace));

            var face = designFace.Shape;
            var feature = new FaceFeature
            {
                FaceIndex = faceIndex,
                AreaM2 = face.Area,
                IsReversed = face.IsReversed,
                Normal = new double[] { 0, 0, 0 },
                Origin = new double[] { 0, 0, 0 },
                RadiusM = 0.0,
            };

            // 1) Plane 우선 (가장 흔함)
            if (face.Geometry is Plane plane)
            {
                feature.Type = SurfaceType.Plane;
                var n = plane.Frame.DirZ.UnitVector;
                // IsReversed 면 방향 뒤집어서 "outward" 표현
                double sign = face.IsReversed ? -1.0 : 1.0;
                feature.Normal = new double[] { n.X * sign, n.Y * sign, n.Z * sign };
                feature.Origin = new double[] { plane.Frame.Origin.X, plane.Frame.Origin.Y, plane.Frame.Origin.Z };
                return feature;
            }

            // 2) Cylinder
            if (face.Geometry is Cylinder cyl)
            {
                feature.Type = SurfaceType.Cylinder;
                feature.RadiusM = cyl.Radius;
                var axisZ = cyl.Frame.DirZ.UnitVector;
                feature.Normal = new double[] { axisZ.X, axisZ.Y, axisZ.Z };  // 축 방향
                feature.Origin = new double[] { cyl.Frame.Origin.X, cyl.Frame.Origin.Y, cyl.Frame.Origin.Z };
                return feature;
            }

            // 3) Sphere
            if (face.Geometry is Sphere sphere)
            {
                feature.Type = SurfaceType.Sphere;
                // SpaceClaim Sphere 의 Radius 접근자는 .Radius 가 표준이지만,
                // V252 XML 문서에서 명시 안 됨. 안전하게 빈 처리.
                try
                {
                    var radiusProp = sphere.GetType().GetProperty("Radius");
                    if (radiusProp != null)
                        feature.RadiusM = (double)radiusProp.GetValue(sphere, null);
                }
                catch
                {
                    feature.RadiusM = 0.0;
                }
                feature.Origin = new double[] { sphere.Frame.Origin.X, sphere.Frame.Origin.Y, sphere.Frame.Origin.Z };
                return feature;
            }

            // 4) Cone
            //    SpaceClaim 의 Cone 정의:
            //      - Frame.DirZ = 축 방향 (radius 가 증가하는 쪽)
            //      - Radius 는 frame 의 XY 평면 (V=0) 에서 측정
            //      - V 파라미터 = 축 방향 거리, R(V) = Radius + V * tan(HalfAngle)
            //      - HalfAngle 은 양수면 DirZ 쪽으로 갈수록 넓어진다
            //    Cylinder 와 같은 의미로 AxisDirection / Origin / RadiusM 을 채워서
            //    downstream (CylinderRoleClassifier, HoleDetector, BossDetector) 가
            //    동일 코드 경로로 처리할 수 있도록 한다.
            if (face.Geometry is Cone cone)
            {
                feature.Type = SurfaceType.Cone;

                double baseRadius = 0.0;
                double halfAngleRad = 0.0;
                bool radiusOk = false;
                bool angleOk = false;
                try
                {
                    var radiusProp = cone.GetType().GetProperty("Radius");
                    if (radiusProp != null)
                    {
                        baseRadius = (double)radiusProp.GetValue(cone, null);
                        radiusOk = true;
                    }
                }
                catch { /* radius prop missing — keep baseRadius=0 */ }
                try
                {
                    var halfAngleProp = cone.GetType().GetProperty("HalfAngle");
                    if (halfAngleProp != null)
                    {
                        halfAngleRad = (double)halfAngleProp.GetValue(cone, null);
                        angleOk = true;
                    }
                }
                catch { /* HalfAngle missing — leave 0 */ }

                // Face 의 V-range mid 에서의 radius (face midpoint).
                //   R_mid = baseRadius + V_mid * tan(halfAngle)
                // BoxUV.Center 로부터 V_mid 를 추출. 실패해도 graceful: baseRadius 사용.
                double midRadius = baseRadius;
                try
                {
                    var boxUvProp = face.GetType().GetProperty("BoxUV");
                    if (boxUvProp != null)
                    {
                        object boxUv = boxUvProp.GetValue(face, null);
                        if (boxUv != null)
                        {
                            var centerProp = boxUv.GetType().GetProperty("Center");
                            if (centerProp != null)
                            {
                                object pointUv = centerProp.GetValue(boxUv, null);
                                if (pointUv != null)
                                {
                                    // PointUV 는 V 프로퍼티가 있다 (대부분의 SC 버전).
                                    var vProp = pointUv.GetType().GetProperty("V");
                                    if (vProp != null)
                                    {
                                        double vMid = (double)vProp.GetValue(pointUv, null);
                                        if (angleOk)
                                            midRadius = baseRadius + vMid * Math.Tan(halfAngleRad);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { /* reflection failed — midRadius stays = baseRadius */ }

                feature.RadiusM = Math.Abs(midRadius);
                feature.HalfAngleDeg = halfAngleRad * 180.0 / Math.PI;

                var axisZ = cone.Frame.DirZ.UnitVector;
                feature.Normal = new double[] { axisZ.X, axisZ.Y, axisZ.Z };
                feature.AxisDirection = new double[] { axisZ.X, axisZ.Y, axisZ.Z };
                // Origin = Frame.Origin (= apex 가 NOT, V=0 plane 위의 점).
                //   downstream 의 along-axis 계산 (HoleDetector 의 plane projection 등)
                //   은 origin 위치 자체보다 axis 방향에 의존하므로 충분히 안전하다.
                feature.Origin = new double[] { cone.Frame.Origin.X, cone.Frame.Origin.Y, cone.Frame.Origin.Z };

                if (!radiusOk || !angleOk)
                {
                    // Honest fallback log: API surface 가 cone properties 를 노출하지 않을 때.
                    // (현재 v252 에서는 안전하지만, 미래 SDK 호환성 보호.)
                    System.Diagnostics.Trace.WriteLine(string.Format(
                        "[FaceClassifier] Cone face #{0}: incomplete cone properties (radiusOk={1}, angleOk={2}). HalfAngleDeg=0.",
                        faceIndex, radiusOk, angleOk));
                }
                return feature;
            }

            // 5) Torus (corner blend = 코너 fillet)
            if (face.Geometry is Torus torus)
            {
                feature.Type = SurfaceType.Torus;
                // Torus 의 MinorRadius = blend radius
                try
                {
                    var minorProp = torus.GetType().GetProperty("MinorRadius");
                    if (minorProp != null)
                        feature.RadiusM = (double)minorProp.GetValue(torus, null);
                }
                catch
                {
                    feature.RadiusM = 0.0;
                }
                feature.Origin = new double[] { torus.Frame.Origin.X, torus.Frame.Origin.Y, torus.Frame.Origin.Z };
                return feature;
            }

            // 6) NURBS / BSpline / free-form surface
            //    SpaceClaim 의 Geometry namespace 에는 두 종류 free-form 가 있다:
            //      - NurbsSurface       : 외부에서 (STEP import 등) 들어온 NURBS
            //      - ProceduralSurface  : fillet/offset/swept/ruled 등 절차적 정의
            //    둘 다 SurfaceType.Nurbs 로 묶는다 (downstream 시각화/통계 용).
            //    Analytic 타입 (Plane/Cylinder/Cone/Sphere/Torus) 은 위에서 이미 처리.
            //
            //    실패 처리: reflection-based name check 로 fallback (SDK 가 type 을
            //    노출하지 않는 경우 대비). 그래도 안 잡힐 경우 다음 6b 단계로 fall-through.
            if (TryClassifyAsNurbs(face, feature, faceIndex))
            {
                return feature;
            }

            // 6b) Base "Surface" runtime type — V252 SDK type erasure.
            //     Real-world STEP imports (ANSYS SampleModel4, Ventilator.stp, ...)
            //     surface bodies whose .NET runtime type is the ABSTRACT base
            //     `SpaceClaim.Api.V252.Geometry.Surface` itself. The native CADdb
            //     surface is not surfaced as a concrete subclass (no Revolved/Swept/
            //     Extruded/Offset .NET type exists in the public XML), so `is X`
            //     against every concrete subclass above fails.
            //
            //     Diagnostic evidence (run 2026-06-06): the OtherSubtypeHistogram
            //     for SampleModel4 was {"SpaceClaim.Api.V252.Geometry.Surface": 12}
            //     and for Ventilator.stp it was {"SpaceClaim.Api.V252.Geometry.Surface": 30}
            //     — 100 % of "Other" faces were the bare base type, NOT a hidden
            //     RevolvedSurface / SweptSurface / etc.
            //
            //     Behaviorally these faces are free-form / non-analytic (otherwise
            //     the analytic checks above would have hit). Fold them into Nurbs
            //     so downstream consumers (NurbsFaceIndices list, hole/boss/fillet
            //     detectors that skip free-form) treat them consistently.
            //
            //     We DO NOT add RevolvedSurface / SweptSurface / ExtrudedSurface
            //     to the SurfaceType enum because no such concrete .NET runtime
            //     type was actually discovered — adding them would be dishonest
            //     speculation.
            if (face.Geometry != null)
            {
                var t = face.Geometry.GetType();
                if (t == typeof(Surface) || string.Equals(t.FullName,
                        "SpaceClaim.Api.V252.Geometry.Surface", StringComparison.Ordinal))
                {
                    feature.Type = SurfaceType.Nurbs;
                    // Best-effort origin extraction shares the NURBS path's logic.
                    PopulateFreeFormOrigin(face, feature, faceIndex);
                    return feature;
                }
            }

            // 7) Other (정말로 분류 불가)
            //    Diagnostic: record the actual runtime type so the caller can
            //    aggregate a histogram (see FeatureGraph.OtherSubtypeHistogram).
            //    This is how we discover what new surface subtypes the SDK has
            //    started returning in newer versions / from real-world STEPs.
            try
            {
                var geom = face.Geometry;
                otherSubtypeName = (geom == null) ? "(null)" : (geom.GetType().FullName ?? "(unknown)");
            }
            catch
            {
                otherSubtypeName = "(threw)";
            }
            feature.Type = SurfaceType.Other;
            return feature;
        }

        /// <summary>
        /// face.Geometry 가 NurbsSurface 또는 ProceduralSurface 인지 검사.
        /// is/as 로 직접 매치 안 되면 (SDK 가 type 을 internal 로 숨긴 경우),
        /// runtime type name 의 fallback 검사를 시도한다.
        /// </summary>
        private static bool TryClassifyAsNurbs(Face face, FaceFeature feature, int faceIndex)
        {
            if (face == null || face.Geometry == null) return false;

            // Primary path: direct is-check against the public Surface subclasses.
            // NurbsSurface 와 ProceduralSurface 는 SpaceClaim.Api.V252.Geometry 에
            // 둘 다 명시적으로 존재 (verified via SpaceClaim.Api.V252.xml).
            bool isNurbs = (face.Geometry is NurbsSurface)
                        || (face.Geometry is ProceduralSurface);

            // Fallback path: runtime type name match for SDKs that hide one of the
            // concrete types. We match defensively against the well-known shapes.
            if (!isNurbs)
            {
                try
                {
                    string typeName = face.Geometry.GetType().Name ?? "";
                    if (typeName.IndexOf("Nurbs", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("BSpline", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("Procedural", StringComparison.OrdinalIgnoreCase) >= 0
                        || typeName.IndexOf("Spline", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isNurbs = true;
                    }
                }
                catch { /* leave isNurbs = false → falls through to Other */ }
            }

            if (!isNurbs) return false;

            feature.Type = SurfaceType.Nurbs;
            PopulateFreeFormOrigin(face, feature, faceIndex);
            return true;
        }

        /// <summary>
        /// Free-form surface 의 best-effort 3D origin 채우기.
        /// face 의 UV-box centroid 를 Surface.Evaluate 로 3D 변환.
        /// 실패해도 non-fatal (Origin stays at (0,0,0)).
        /// </summary>
        private static void PopulateFreeFormOrigin(Face face, FaceFeature feature, int faceIndex)
        {
            if (face == null || face.Geometry == null) return;

            // Metadata: face center on the surface (approximate origin). Free-form
            // surfaces don't have a single canonical "origin" or "normal", so we
            // emit the face's UV-box centroid as a best-effort representative point.
            // Failure here is non-fatal — Origin stays at (0,0,0).
            try
            {
                var boxUvProp = face.GetType().GetProperty("BoxUV");
                if (boxUvProp != null)
                {
                    object boxUv = boxUvProp.GetValue(face, null);
                    if (boxUv != null)
                    {
                        var centerProp = boxUv.GetType().GetProperty("Center");
                        if (centerProp != null)
                        {
                            object pointUv = centerProp.GetValue(boxUv, null);
                            if (pointUv != null)
                            {
                                // Try Surface.Evaluate(PointUV) to get a 3D point.
                                var evalMethod = face.Geometry.GetType().GetMethod("Evaluate", new[] { pointUv.GetType() });
                                if (evalMethod != null)
                                {
                                    object evalResult = evalMethod.Invoke(face.Geometry, new object[] { pointUv });
                                    if (evalResult != null)
                                    {
                                        // SurfaceEvaluation has a Point property (typed).
                                        var ptProp = evalResult.GetType().GetProperty("Point");
                                        if (ptProp != null)
                                        {
                                            object pt = ptProp.GetValue(evalResult, null);
                                            if (pt != null)
                                            {
                                                var xProp = pt.GetType().GetProperty("X");
                                                var yProp = pt.GetType().GetProperty("Y");
                                                var zProp = pt.GetType().GetProperty("Z");
                                                if (xProp != null && yProp != null && zProp != null)
                                                {
                                                    feature.Origin = new double[]
                                                    {
                                                        (double)xProp.GetValue(pt, null),
                                                        (double)yProp.GetValue(pt, null),
                                                        (double)zProp.GetValue(pt, null),
                                                    };
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // origin stays (0,0,0). Type = Nurbs is still useful on its own.
                System.Diagnostics.Trace.WriteLine(string.Format(
                    "[FaceClassifier] Nurbs face #{0}: Origin extraction failed (non-fatal).",
                    faceIndex));
            }
        }

        /// <summary>
        /// SurfaceType enum → 사람 읽을 수 있는 string (JSON 출력용)
        /// </summary>
        public static string ToTypeKey(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Plane: return "plane";
                case SurfaceType.Cylinder: return "cylinder";
                case SurfaceType.Cone: return "cone";
                case SurfaceType.Sphere: return "sphere";
                case SurfaceType.Torus: return "torus";
                case SurfaceType.Nurbs: return "nurbs";
                default: return "other";
            }
        }
    }
}
