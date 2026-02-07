using System;
using System.Collections.Generic;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.BendingFixture;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Simplify;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.BendingFixture;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Simplify
{
    public static class SimplifyService
    {
        // ==========================================
        //  Main entry point
        // ==========================================

        public static SimplifyResult Execute(Part part, string keyword, SimplifyMode mode)
        {
            var rules = new List<SimplifyRule> { new SimplifyRule(keyword, mode) };
            return ExecuteBatch(part, rules);
        }

        public static SimplifyResult ExecuteBatch(Part part, List<SimplifyRule> rules)
        {
            var result = new SimplifyResult();

            if (rules == null || rules.Count == 0)
            {
                result.Log.Add("룰이 없습니다.");
                return result;
            }

            var validRules = new List<SimplifyRule>();
            foreach (var rule in rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.Keyword))
                    validRules.Add(rule);
            }

            if (validRules.Count == 0)
            {
                result.Log.Add("유효한 키워드가 없습니다.");
                return result;
            }

            result.Log.Add(string.Format("룰 {0}개 실행", validRules.Count));
            result.Log.Add("");

            foreach (var rule in validRules)
            {
                string keyword = rule.Keyword.Trim();

                var allBodies = new List<DesignBody>();
                CollectBodies(part, allBodies);

                var matchedBodies = new List<DesignBody>();
                foreach (DesignBody body in allBodies)
                {
                    string name = body.Name ?? "";
                    if (name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        matchedBodies.Add(body);
                }

                result.Log.Add(string.Format("── \"{0}\" ({1}) ── 매칭: {2}개",
                    keyword, rule.Mode, matchedBodies.Count));
                result.MatchedCount += matchedBodies.Count;

                if (matchedBodies.Count == 0)
                {
                    result.Log.Add("");
                    continue;
                }

                switch (rule.Mode)
                {
                    case SimplifyMode.BoundingBox:
                        ReplaceBodiesWithBoundingBoxes(part, matchedBodies, result);
                        break;
                    case SimplifyMode.SolidToShell:
                        ReplaceBodiesWithMidSurface(part, matchedBodies, result);
                        break;
                }

                result.Log.Add("");
            }

            return result;
        }

        // ==========================================
        //  BoundingBox 모드
        // ==========================================

        private static void ReplaceBodiesWithBoundingBoxes(
            Part part, List<DesignBody> bodies, SimplifyResult result)
        {
            var bboxService = new BendingFixtureService();

            foreach (var body in bodies)
            {
                string bodyName = body.Name ?? "Unnamed";
                try
                {
                    // 1. AABB 계산
                    AxisAlignedBoundingBox bbox = bboxService.ComputeBoundingBox(body);

                    double extX = bbox.ExtentX;
                    double extY = bbox.ExtentY;
                    double extZ = bbox.ExtentZ;

                    if (extX < 1e-12 || extY < 1e-12 || extZ < 1e-12)
                    {
                        result.FailedCount++;
                        result.Log.Add(string.Format("  [스킵] {0}: 바운딩 박스 크기 0", bodyName));
                        continue;
                    }

                    // 2. 위치 맞춘 박스 생성
                    Point baseCenter = Point.Create(bbox.CenterX, bbox.CenterY, bbox.MinZ);
                    Frame frame = Frame.Create(baseCenter, Direction.DirX, Direction.DirY);
                    Plane basePlane = Plane.Create(frame);
                    Profile profile = new RectangleProfile(basePlane, extX, extY);
                    Body boxBody = Body.ExtrudeProfile(profile, extZ);

                    // 3. 원본 삭제
                    body.Delete();

                    // 4. 대체 바디 생성
                    string newName = bodyName + "_simplified";
                    BodyBuilder.CreateDesignBody(part, newName, boxBody);

                    result.ProcessedCount++;
                    result.Log.Add(string.Format("  [BB] {0}: {1:F1} x {2:F1} x {3:F1} mm",
                        bodyName,
                        GeometryUtils.MetersToMm(extX),
                        GeometryUtils.MetersToMm(extY),
                        GeometryUtils.MetersToMm(extZ)));
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Log.Add(string.Format("  [실패] {0}: {1}", bodyName, ex.Message));
                }
            }
        }

        // ==========================================
        //  SolidToShell 모드
        // ==========================================

        private static void ReplaceBodiesWithMidSurface(
            Part part, List<DesignBody> bodies, SimplifyResult result)
        {
            foreach (var body in bodies)
            {
                string bodyName = body.Name ?? "Unnamed";
                try
                {
                    // 1. 평면 수집
                    var planarFaces = CollectPlanarFaces(body);

                    if (planarFaces.Count < 2)
                    {
                        result.FailedCount++;
                        result.Log.Add(string.Format("  [스킵] {0}: 평면 2개 미만 ({1}개)",
                            bodyName, planarFaces.Count));
                        continue;
                    }

                    // 2. 면적 내림차순 정렬
                    planarFaces.Sort((a, b) => b.Area.CompareTo(a.Area));

                    // 3. 최대면
                    var largest = planarFaces[0];

                    // 4. 대향면 탐색
                    PlanarFaceData opposing = FindOpposingFace(largest, planarFaces);

                    if (opposing == null)
                    {
                        result.FailedCount++;
                        result.Log.Add(string.Format("  [스킵] {0}: 대향면 없음", bodyName));
                        continue;
                    }

                    // 5. 중간면 계산
                    double dist1 = DotPointNormal(largest.Origin, largest.Normal);
                    double dist2 = DotPointNormal(opposing.Origin, largest.Normal);
                    double midDist = (dist1 + dist2) / 2.0;
                    double thickness = Math.Abs(dist1 - dist2);

                    // midPlane origin 계산
                    double offset = midDist - dist1;
                    Point midOrigin = Point.Create(
                        largest.Origin.X + largest.Normal.X * offset,
                        largest.Origin.Y + largest.Normal.Y * offset,
                        largest.Origin.Z + largest.Normal.Z * offset);

                    Direction midNormal = Direction.Create(
                        largest.Normal.X, largest.Normal.Y, largest.Normal.Z);

                    // 탄젠트 프레임 구성
                    Direction tangentU = GetTangent(midNormal);
                    Vector tangentVVec = Vector.Cross(midNormal.UnitVector, tangentU.UnitVector);
                    Direction tangentV = Direction.Create(tangentVVec.X, tangentVVec.Y, tangentVVec.Z);

                    Frame midFrame = Frame.Create(midOrigin, tangentU, tangentV);
                    Plane midPlane = Plane.Create(midFrame);

                    // 6. 에지 투영
                    var projectedCurves = ProjectFaceEdgesToPlane(largest.Face, midFrame);

                    if (projectedCurves.Count < 3)
                    {
                        result.FailedCount++;
                        result.Log.Add(string.Format("  [스킵] {0}: 투영 에지 부족 ({1}개)",
                            bodyName, projectedCurves.Count));
                        continue;
                    }

                    // 7. 쉘 바디 생성
                    Body shellBody = Body.CreatePlanarBody(midPlane, projectedCurves);

                    // 8. 원본 삭제 → 대체
                    body.Delete();
                    string newName = bodyName + "_shell";
                    BodyBuilder.CreateDesignBody(part, newName, shellBody);

                    result.ProcessedCount++;
                    result.Log.Add(string.Format("  [Shell] {0}: 두께 {1:F2} mm, 면적 {2:F1} mm²",
                        bodyName,
                        GeometryUtils.MetersToMm(thickness),
                        GeometryUtils.MetersToMm(Math.Sqrt(largest.Area)) *
                            GeometryUtils.MetersToMm(Math.Sqrt(largest.Area))));
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Log.Add(string.Format("  [실패] {0}: {1}", bodyName, ex.Message));
                }
            }
        }

        // ==========================================
        //  내부 데이터 구조
        // ==========================================

        private class PlanarFaceData
        {
            public DesignFace Face;
            public double Area;
            public Vector Normal;
            public Point Origin;
        }

        // ==========================================
        //  헬퍼: 평면 수집
        // ==========================================

        private static List<PlanarFaceData> CollectPlanarFaces(DesignBody body)
        {
            var list = new List<PlanarFaceData>();
            foreach (DesignFace face in body.Faces)
            {
                if (!(face.Shape.Geometry is Plane plane))
                    continue;

                Vector normal = plane.Frame.DirZ.UnitVector;
                if (face.Shape.IsReversed)
                    normal = -normal;

                double area;
                try { area = face.Shape.Area; }
                catch { continue; }

                list.Add(new PlanarFaceData
                {
                    Face = face,
                    Area = area,
                    Normal = normal,
                    Origin = plane.Frame.Origin
                });
            }
            return list;
        }

        // ==========================================
        //  헬퍼: 대향면 탐색
        // ==========================================

        private const double AntiParallelThreshold = -0.985;

        private static PlanarFaceData FindOpposingFace(
            PlanarFaceData reference, List<PlanarFaceData> candidates)
        {
            PlanarFaceData best = null;
            double bestArea = 0;

            foreach (var c in candidates)
            {
                if (c == reference) continue;

                double dot = Vector.Dot(reference.Normal, c.Normal);
                if (dot > AntiParallelThreshold)
                    continue;

                if (c.Area > bestArea)
                {
                    bestArea = c.Area;
                    best = c;
                }
            }

            return best;
        }

        // ==========================================
        //  헬퍼: 점과 법선 내적
        // ==========================================

        private static double DotPointNormal(Point pt, Vector normal)
        {
            return pt.X * normal.X + pt.Y * normal.Y + pt.Z * normal.Z;
        }

        // ==========================================
        //  헬퍼: 탄젠트 방향 계산
        // ==========================================

        private static Direction GetTangent(Direction normal)
        {
            double absX = Math.Abs(normal.UnitVector.X);
            double absY = Math.Abs(normal.UnitVector.Y);
            double absZ = Math.Abs(normal.UnitVector.Z);

            Vector seed;
            if (absX <= absY && absX <= absZ)
                seed = Vector.Create(1, 0, 0);
            else if (absY <= absZ)
                seed = Vector.Create(0, 1, 0);
            else
                seed = Vector.Create(0, 0, 1);

            Vector tangent = Vector.Cross(normal.UnitVector, seed);
            return Direction.Create(tangent.X, tangent.Y, tangent.Z);
        }

        // ==========================================
        //  헬퍼: 에지를 평면에 투영
        // ==========================================

        private static List<ITrimmedCurve> ProjectFaceEdgesToPlane(
            DesignFace face, Frame targetFrame)
        {
            var curves = new List<ITrimmedCurve>();

            foreach (DesignEdge edge in face.Edges)
            {
                Point start = edge.Shape.StartPoint;
                Point end = edge.Shape.EndPoint;

                Point projStart = ProjectPointToPlane(start, targetFrame);
                Point projEnd = ProjectPointToPlane(end, targetFrame);

                double dist = (projStart - projEnd).Magnitude;
                if (dist < 1e-9) continue;

                curves.Add(CurveSegment.Create(projStart, projEnd));
            }

            return curves;
        }

        private static Point ProjectPointToPlane(Point pt, Frame frame)
        {
            Vector v = pt - frame.Origin;
            double signedDist = Vector.Dot(v, frame.DirZ.UnitVector);
            return Point.Create(
                pt.X - signedDist * frame.DirZ.UnitVector.X,
                pt.Y - signedDist * frame.DirZ.UnitVector.Y,
                pt.Z - signedDist * frame.DirZ.UnitVector.Z);
        }
        // ==========================================
        //  헬퍼: 재귀적 바디 수집 (컴포넌트 포함)
        // ==========================================

        private static void CollectBodies(IPart part, List<DesignBody> result)
        {
            if (part == null) return;

            foreach (var body in part.Bodies)
            {
                var db = body as DesignBody;
                if (db != null)
                    result.Add(db);
            }

            foreach (var comp in part.Components)
            {
                try
                {
                    if (comp.Content != null)
                        CollectBodies(comp.Content, result);
                }
                catch { }
            }
        }
    }
}
