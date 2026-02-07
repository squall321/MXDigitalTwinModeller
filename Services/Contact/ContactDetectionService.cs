using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Contact;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Contact
{
    /// <summary>
    /// 바디 간 면 대 면 접촉을 자동 감지하고 Named Selection 페어를 생성.
    /// 동일 크기뿐 아니라 포함/부분 겹침도 감지.
    /// </summary>
    public static class ContactDetectionService
    {
        private const double NormalTolerance = 0.985;  // anti-parallel dot threshold (≈±10°)
        private const double PlaneTolerance = 5e-5;   // 평면 거리 허용 오차 (m) = 0.05mm
        private const double VertexSnapTol = 1e-5;    // 꼭짓점 병합 허용 오차 (m) = 0.01mm

        // ─── 내부 데이터 구조 ───

        private class PlanarFaceInfo
        {
            public DesignFace Face;
            public DesignBody Body;
            public Vector Normal;       // 실제 법선 (reversed 반영)
            public Point PlaneOrigin;
            public double Area;
            public List<Point> Vertices; // 에지 꼭짓점 (순서 유지, 3D)
        }

        /// <summary>2D 점</summary>
        private struct Vec2
        {
            public double U, V;
            public Vec2(double u, double v) { U = u; V = v; }
        }

        // ─── Public API ───

        /// <summary>
        /// 진단 로그 (UI에서 표시)
        /// </summary>
        public static List<string> DiagnosticLog { get; private set; }

        /// <summary>
        /// 접촉면 페어를 감지하고 Named Selection을 생성
        /// </summary>
        public static List<ContactPairInfo> DetectAndCreateSelections(Part part, string keyword)
        {
            DiagnosticLog = new List<string>();

            var bodies = GetAllDesignBodies(part);
            DiagnosticLog.Add(string.Format("바디 수: {0}", bodies.Count));

            var faceInfos = CollectAllPlanarFaces(bodies);
            DiagnosticLog.Add(string.Format("평면 face 수: {0}", faceInfos.Count));

            // 바디별 face 수 로그
            var bodyFaceCounts = new Dictionary<string, int>();
            foreach (var fi in faceInfos)
            {
                string name = fi.Body.Name ?? "Unnamed";
                if (!bodyFaceCounts.ContainsKey(name))
                    bodyFaceCounts[name] = 0;
                bodyFaceCounts[name]++;
            }
            foreach (var kvp in bodyFaceCounts)
                DiagnosticLog.Add(string.Format("  {0}: {1}개 평면", kvp.Key, kvp.Value));

            var pairs = FindContactPairs(faceInfos, keyword);
            DiagnosticLog.Add(string.Format("감지된 면접촉 페어: {0}", pairs.Count));

            // 에지 접촉 감지 (서로 다른 바디의 면이 에지를 공유하는 경우)
            var edgePairs = FindEdgeContactPairs(bodies, pairs, keyword);
            DiagnosticLog.Add(string.Format("감지된 에지접촉 페어: {0}", edgePairs.Count));

            var allPairs = new List<ContactPairInfo>();
            allPairs.AddRange(pairs);
            allPairs.AddRange(edgePairs);

            CreateNamedSelections(part, allPairs);
            return allPairs;
        }

        // ─── Face 수집 ───

        private static List<PlanarFaceInfo> CollectAllPlanarFaces(List<DesignBody> bodies)
        {
            var result = new List<PlanarFaceInfo>();

            foreach (var body in bodies)
            {
                string bodyName = body.Name ?? "Unnamed";
                int totalFaces = 0;
                int planarFaces = 0;
                int areaFail = 0;
                int vertexFail = 0;
                var nonPlaneTypes = new Dictionary<string, int>();

                foreach (var face in body.Faces)
                {
                    totalFaces++;

                    var geom = face.Shape.Geometry;
                    if (geom == null)
                    {
                        if (!nonPlaneTypes.ContainsKey("null")) nonPlaneTypes["null"] = 0;
                        nonPlaneTypes["null"]++;
                        continue;
                    }

                    Plane plane = geom as Plane;
                    if (plane == null)
                    {
                        string typeName = geom.GetType().Name;
                        if (!nonPlaneTypes.ContainsKey(typeName)) nonPlaneTypes[typeName] = 0;
                        nonPlaneTypes[typeName]++;
                        continue;
                    }

                    planarFaces++;

                    Vector normalVec = plane.Frame.DirZ.UnitVector;
                    if (face.Shape.IsReversed)
                        normalVec = -normalVec;

                    double area;
                    try { area = face.Shape.Area; }
                    catch { areaFail++; continue; }

                    var vertices = CollectFaceVertices(face);
                    if (vertices.Count < 3)
                    {
                        vertexFail++;
                        DiagnosticLog.Add(string.Format("  [꼭짓점부족] {0}: face 꼭짓점 {1}개 (에지수={2})",
                            bodyName, vertices.Count, face.Edges.Count));
                        continue;
                    }

                    result.Add(new PlanarFaceInfo
                    {
                        Face = face,
                        Body = body,
                        Normal = normalVec,
                        PlaneOrigin = plane.Frame.Origin,
                        Area = area,
                        Vertices = vertices
                    });
                }

                // 바디별 상세 로그
                DiagnosticLog.Add(string.Format("[바디] {0}: 전체face={1}, 평면={2}, 면적실패={3}, 꼭짓점실패={4}",
                    bodyName, totalFaces, planarFaces, areaFail, vertexFail));
                foreach (var kvp in nonPlaneTypes)
                    DiagnosticLog.Add(string.Format("  비평면: {0} x{1}", kvp.Key, kvp.Value));

                // 수집된 평면 face의 법선/원점 상세 로그 (바디당 최대 10개)
                int logged = 0;
                foreach (var fi in result)
                {
                    if (fi.Body != body) continue;
                    if (logged >= 10) break;
                    DiagnosticLog.Add(string.Format("  평면face: n=({0:F4},{1:F4},{2:F4}) o=({3:F6},{4:F6},{5:F6}) area={6:E3}m² verts={7}",
                        fi.Normal.X, fi.Normal.Y, fi.Normal.Z,
                        fi.PlaneOrigin.X, fi.PlaneOrigin.Y, fi.PlaneOrigin.Z,
                        fi.Area, fi.Vertices.Count));
                    logged++;
                }
            }

            return result;
        }

        /// <summary>
        /// 곡선 에지 샘플링 수 (직선이면 0, 곡선이면 이 수만큼 중간점 생성)
        /// </summary>
        private const int CurveSamples = 16;

        /// <summary>
        /// 에지 정보: start, end, 중간 샘플점 포함
        /// </summary>
        private class EdgeInfo
        {
            public Point Start;
            public Point End;
            public List<Point> SampledPoints; // start → samples → end 순서
        }

        /// <summary>
        /// face 에지를 체이닝하여 순서 있는 폴리곤 꼭짓점 반환.
        /// 곡선 에지는 파라미터 공간에서 샘플링하여 실제 형상 반영.
        /// 구멍이 있는 면(inner loop)은 가장 큰 루프(외곽)만 사용.
        /// </summary>
        private static List<Point> CollectFaceVertices(DesignFace face)
        {
            // 1. 모든 에지를 샘플링하여 수집
            var edgeInfos = new List<EdgeInfo>();
            foreach (var edge in face.Edges)
            {
                try
                {
                    var ei = SampleEdge(edge);
                    if (ei != null)
                        edgeInfos.Add(ei);
                }
                catch { }
            }

            if (edgeInfos.Count < 2)
                return new List<Point>();

            // 2. 에지 체이닝으로 루프들 추출
            var used = new bool[edgeInfos.Count];
            var allLoops = new List<List<Point>>();

            while (true)
            {
                int startIdx = -1;
                for (int k = 0; k < used.Length; k++)
                {
                    if (!used[k]) { startIdx = k; break; }
                }
                if (startIdx < 0)
                    break;

                var loop = new List<Point>();
                used[startIdx] = true;

                // 첫 에지의 점들 추가 (start → ... → end)
                var firstEdge = edgeInfos[startIdx];
                Point firstPoint = firstEdge.Start;
                Point current = firstEdge.End;
                AddSampledPoints(loop, firstEdge, false);

                // 체이닝
                bool found = true;
                int safety = edgeInfos.Count + 2;
                while (found && !IsNear(current, firstPoint) && safety-- > 0)
                {
                    found = false;
                    for (int k = 0; k < edgeInfos.Count; k++)
                    {
                        if (used[k]) continue;

                        var ei = edgeInfos[k];

                        if (IsNear(current, ei.Start))
                        {
                            used[k] = true;
                            AddSampledPoints(loop, ei, false);
                            current = ei.End;
                            found = true;
                            break;
                        }
                        else if (IsNear(current, ei.End))
                        {
                            used[k] = true;
                            AddSampledPoints(loop, ei, true); // 역순
                            current = ei.Start;
                            found = true;
                            break;
                        }
                    }
                }

                // 마지막 점이 첫 점과 같으면 제거
                while (loop.Count > 1 && IsNear(loop[0], loop[loop.Count - 1]))
                    loop.RemoveAt(loop.Count - 1);

                if (loop.Count >= 3)
                    allLoops.Add(loop);
            }

            if (allLoops.Count == 0)
                return new List<Point>();

            // 3. 가장 큰 루프 반환 (외곽 경계)
            List<Point> largest = allLoops[0];
            double largestArea = 0;
            foreach (var loop in allLoops)
            {
                double a = ComputeLoop2DArea(loop);
                if (a > largestArea)
                {
                    largestArea = a;
                    largest = loop;
                }
            }

            return largest;
        }

        /// <summary>
        /// 에지를 샘플링. 직선이면 start/end만, 곡선이면 파라미터 공간에서 중간점 생성.
        /// </summary>
        private static EdgeInfo SampleEdge(DesignEdge edge)
        {
            Point start = edge.Shape.StartPoint;
            Point end = edge.Shape.EndPoint;

            var points = new List<Point>();
            points.Add(start);

            // 곡선 에지 중간점 샘플링
            try
            {
                var bounds = edge.Shape.Bounds;
                double tStart = bounds.Start;
                double tEnd = bounds.End;
                double range = tEnd - tStart;

                if (range > 1e-15)
                {
                    // 직선 여부 판단: 중간점이 start-end 직선에서 벗어나는지
                    double midT = tStart + range * 0.5;
                    Point midPoint = edge.Shape.Geometry.Evaluate(midT).Point;

                    Vector startToEnd = end - start;
                    double lineLen = Math.Sqrt(VecDot(startToEnd, startToEnd));

                    bool isCurved = false;
                    if (lineLen > 1e-12)
                    {
                        Vector startToMid = midPoint - start;
                        // 직선까지의 거리 = |cross(startToEnd, startToMid)| / |startToEnd|
                        Vector cross = Cross(startToEnd, startToMid);
                        double crossLen = Math.Sqrt(VecDot(cross, cross));
                        double deviation = crossLen / lineLen;
                        isCurved = deviation > VertexSnapTol;
                    }
                    else
                    {
                        isCurved = true; // 시작점=끝점인 루프 에지
                    }

                    if (isCurved)
                    {
                        for (int s = 1; s <= CurveSamples; s++)
                        {
                            double t = tStart + range * s / (CurveSamples + 1);
                            try
                            {
                                Point pt = edge.Shape.Geometry.Evaluate(t).Point;
                                points.Add(pt);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            points.Add(end);

            return new EdgeInfo
            {
                Start = start,
                End = end,
                SampledPoints = points
            };
        }

        /// <summary>
        /// EdgeInfo의 샘플점을 루프에 추가 (역순 가능).
        /// 첫 점은 이전 에지 끝과 중복되므로 건너뜀.
        /// </summary>
        private static void AddSampledPoints(List<Point> loop, EdgeInfo ei, bool reverse)
        {
            var pts = ei.SampledPoints;
            if (reverse)
            {
                // end → ... → start
                for (int i = pts.Count - 1; i >= 0; i--)
                {
                    if (loop.Count == 0 || !IsNear(loop[loop.Count - 1], pts[i]))
                        loop.Add(pts[i]);
                }
            }
            else
            {
                // start → ... → end
                for (int i = 0; i < pts.Count; i++)
                {
                    if (loop.Count == 0 || !IsNear(loop[loop.Count - 1], pts[i]))
                        loop.Add(pts[i]);
                }
            }
        }

        /// <summary>
        /// 3D 루프의 면적 근사 (Newell's method로 법선 크기 계산)
        /// </summary>
        private static double ComputeLoop2DArea(List<Point> loop)
        {
            double nx = 0, ny = 0, nz = 0;
            int n = loop.Count;
            for (int i = 0; i < n; i++)
            {
                Point cur = loop[i];
                Point next = loop[(i + 1) % n];
                nx += (cur.Y - next.Y) * (cur.Z + next.Z);
                ny += (cur.Z - next.Z) * (cur.X + next.X);
                nz += (cur.X - next.X) * (cur.Y + next.Y);
            }
            return 0.5 * Math.Sqrt(nx * nx + ny * ny + nz * nz);
        }

        private static bool IsNear(Point a, Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz < VertexSnapTol * VertexSnapTol;
        }

        // ─── 접촉 쌍 탐색 ───

        /// <summary>
        /// 후보 접촉 쌍 (최근접 필터 전)
        /// </summary>
        private class CandidatePair
        {
            public PlanarFaceInfo A;
            public PlanarFaceInfo B;
            public double PlaneDist; // 평면 간 거리 (절대값)
            public bool IsEdgeOnly;  // 동일 평면 에지만 공유 (면적 겹침 없음)
        }

        private static List<ContactPairInfo> FindContactPairs(List<PlanarFaceInfo> faceInfos, string keyword)
        {
            int checkedPairs = 0;
            int passedNormal = 0;
            int passedPlane = 0;
            int passedOverlap = 0;

            // Phase 1: 모든 후보 수집 (거리 포함)
            var candidates = new List<CandidatePair>();

            int detailLogCount = 0;
            const int MaxDetailLog = 20;

            for (int i = 0; i < faceInfos.Count; i++)
            {
                for (int j = i + 1; j < faceInfos.Count; j++)
                {
                    var a = faceInfos[i];
                    var b = faceInfos[j];

                    if (a.Body == b.Body)
                        continue;

                    checkedPairs++;

                    // 법선 반평행 체크
                    double dot = VecDot(a.Normal, b.Normal);
                    if (dot > -NormalTolerance)
                    {
                        if (detailLogCount < MaxDetailLog)
                        {
                            detailLogCount++;
                            DiagnosticLog.Add(string.Format("  [법선실패] {0} ↔ {1}: dot={2:F4} (need<{3:F2}), nA=({4:F3},{5:F3},{6:F3}), nB=({7:F3},{8:F3},{9:F3})",
                                a.Body.Name ?? "?", b.Body.Name ?? "?", dot, -NormalTolerance,
                                a.Normal.X, a.Normal.Y, a.Normal.Z,
                                b.Normal.X, b.Normal.Y, b.Normal.Z));
                        }
                        continue;
                    }
                    passedNormal++;

                    // 동일 평면 체크
                    Vector diff = b.PlaneOrigin - a.PlaneOrigin;
                    double planeDist = Math.Abs(VecDot(diff, Normalize(a.Normal)));
                    if (planeDist > PlaneTolerance)
                    {
                        if (planeDist < PlaneTolerance * 10)
                        {
                            DiagnosticLog.Add(string.Format("  Near-miss 평면거리: {0} ↔ {1}, dist={2:E3}m (tol={3:E3})",
                                a.Body.Name ?? "?", b.Body.Name ?? "?", planeDist, PlaneTolerance));
                        }
                        else if (detailLogCount < MaxDetailLog)
                        {
                            detailLogCount++;
                            DiagnosticLog.Add(string.Format("  [평면실패] {0} ↔ {1}: dist={2:E3}m (tol={3:E3})",
                                a.Body.Name ?? "?", b.Body.Name ?? "?", planeDist, PlaneTolerance));
                        }
                        continue;
                    }
                    passedPlane++;

                    DiagnosticLog.Add(string.Format("  [오버랩검사] {0}({1}pts) ↔ {2}({3}pts), planeDist={4:E3}",
                        a.Body.Name ?? "?", a.Vertices.Count,
                        b.Body.Name ?? "?", b.Vertices.Count, planeDist));

                    // 2D 오버랩 검사
                    if (!PolygonsOverlap(a, b))
                    {
                        DiagnosticLog.Add(string.Format("  [오버랩실패] {0}({1}pts) ↔ {2}({3}pts)",
                            a.Body.Name ?? "?", a.Vertices.Count,
                            b.Body.Name ?? "?", b.Vertices.Count));
                        // 추가: 바운딩박스 정보 로그
                        Vector normalN = Normalize(a.Normal);
                        Vector uA, vA;
                        BuildPlaneAxes(normalN, out uA, out vA);
                        var pA = ProjectToPlane(a.Vertices, a.PlaneOrigin, uA, vA);
                        var pB = ProjectToPlane(b.Vertices, a.PlaneOrigin, uA, vA);
                        double aMinU, aMaxU, aMinV, aMaxV, bMinU, bMaxU, bMinV, bMaxV;
                        GetBBox(pA, out aMinU, out aMaxU, out aMinV, out aMaxV);
                        GetBBox(pB, out bMinU, out bMaxU, out bMinV, out bMaxV);
                        DiagnosticLog.Add(string.Format("    bboxA: U[{0:F6},{1:F6}] V[{2:F6},{3:F6}]", aMinU, aMaxU, aMinV, aMaxV));
                        DiagnosticLog.Add(string.Format("    bboxB: U[{0:F6},{1:F6}] V[{2:F6},{3:F6}]", bMinU, bMaxU, bMinV, bMaxV));
                        continue;
                    }
                    passedOverlap++;

                    // 내부 영역 겹침 vs 경계만 닿음 판별
                    // 인접 면이 에지만 공유하는 경우 (같은 평면, 겹치는 면적 없음) 구분
                    bool edgeOnly = !HasInteriorOverlap(a, b);
                    if (edgeOnly)
                    {
                        DiagnosticLog.Add(string.Format("  [에지만접촉] {0} ↔ {1}: 동일 평면, 에지만 공유",
                            a.Body.Name ?? "?", b.Body.Name ?? "?"));
                    }

                    candidates.Add(new CandidatePair { A = a, B = b, PlaneDist = planeDist, IsEdgeOnly = edgeOnly });
                }
            }

            DiagnosticLog.Add(string.Format("필터 통계: 비교={0}, 법선통과={1}, 평면통과={2}, 오버랩통과={3}",
                checkedPairs, passedNormal, passedPlane, passedOverlap));

            // Phase 2: 각 face에 대해 가장 가까운 매칭만 유지
            // face i → 가장 가까운 anti-parallel face (다른 바디)만 접촉으로 인정
            // 이렇게 하면 얇은 바디를 뚫고 반대편 면과 매칭되는 것을 방지
            var bestForFace = new Dictionary<DesignFace, double>(); // face → 최소 거리

            foreach (var c in candidates)
            {
                double distA;
                if (!bestForFace.TryGetValue(c.A.Face, out distA))
                    distA = double.MaxValue;
                if (c.PlaneDist < distA)
                    bestForFace[c.A.Face] = c.PlaneDist;

                double distB;
                if (!bestForFace.TryGetValue(c.B.Face, out distB))
                    distB = double.MaxValue;
                if (c.PlaneDist < distB)
                    bestForFace[c.B.Face] = c.PlaneDist;
            }

            // 각 후보에 대해: A의 최근접 거리 또는 B의 최근접 거리와 같은 경우만 통과
            // (둘 중 하나라도 이 후보가 최근접이면 유지)
            var filtered = new List<CandidatePair>();
            int droppedByNearest = 0;
            foreach (var c in candidates)
            {
                double bestA = bestForFace[c.A.Face];
                double bestB = bestForFace[c.B.Face];

                // 이 후보의 거리가 양쪽 모두의 최근접보다 현저히 먼 경우 제외
                // (얇은 바디 관통 방지: 더 가까운 매칭이 있으면 이 매칭은 무효)
                bool aIsNearest = c.PlaneDist <= bestA + VertexSnapTol;
                bool bIsNearest = c.PlaneDist <= bestB + VertexSnapTol;

                if (aIsNearest || bIsNearest)
                {
                    filtered.Add(c);
                }
                else
                {
                    droppedByNearest++;
                    DiagnosticLog.Add(string.Format("  최근접 아님 (제외): {0} ↔ {1}, dist={2:E3}m (best={3:E3}/{4:E3})",
                        c.A.Body.Name ?? "?", c.B.Body.Name ?? "?",
                        c.PlaneDist, bestA, bestB));
                }
            }

            if (droppedByNearest > 0)
                DiagnosticLog.Add(string.Format("최근접 필터로 {0}개 제외", droppedByNearest));

            // Phase 3: 최종 페어 생성
            var pairs = new List<ContactPairInfo>();
            var pairedSet = new HashSet<string>();
            int pairIndex = 1;

            foreach (var c in filtered)
            {
                // 중복 체크
                string key = c.A.Face.GetHashCode() < c.B.Face.GetHashCode()
                    ? c.A.Face.GetHashCode() + "_" + c.B.Face.GetHashCode()
                    : c.B.Face.GetHashCode() + "_" + c.A.Face.GetHashCode();
                if (pairedSet.Contains(key))
                    continue;
                pairedSet.Add(key);

                // A/B 결정: 법선 성분 합 > 0 인 쪽이 A
                PlanarFaceInfo faceA, faceB;
                double sumN = c.A.Normal.X + c.A.Normal.Y + c.A.Normal.Z;
                if (sumN > 0)
                {
                    faceA = c.A;
                    faceB = c.B;
                }
                else
                {
                    faceA = c.B;
                    faceB = c.A;
                }

                // 접두사 및 타입 결정
                string prefix;
                ContactType contactType;

                if (c.IsEdgeOnly)
                {
                    // 동일 평면에서 에지만 공유 → EdgeContact
                    prefix = "EdgeContact";
                    contactType = ContactType.Edge;
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        string bna = faceA.Body.Name ?? "";
                        string bnb = faceB.Body.Name ?? "";
                        if (bna.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            bnb.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            prefix = keyword + "_Edge";
                        }
                    }
                }
                else
                {
                    // 면적 겹침 → NodeSet (면접촉)
                    prefix = "NodeSet";
                    contactType = ContactType.Face;
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        string bna = faceA.Body.Name ?? "";
                        string bnb = faceB.Body.Name ?? "";
                        if (bna.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            bnb.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            prefix = keyword;
                        }
                    }
                }

                pairs.Add(new ContactPairInfo
                {
                    FaceA = faceA.Face,
                    FaceB = faceB.Face,
                    BodyA = faceA.Body,
                    BodyB = faceB.Body,
                    Prefix = prefix,
                    PairIndex = pairIndex,
                    Area = c.IsEdgeOnly ? 0 : Math.Min(faceA.Area, faceB.Area),
                    Type = contactType
                });

                pairIndex++;
            }

            return pairs;
        }

        // ─── 에지 접촉 감지 ───

        /// <summary>
        /// 서로 다른 바디의 면이 에지를 공유하는 경우를 감지.
        /// 면 접촉(coplanar)이 아닌 에지 접촉(면이 각도를 이루며 만남).
        /// </summary>
        private static List<ContactPairInfo> FindEdgeContactPairs(
            List<DesignBody> bodies,
            List<ContactPairInfo> faceContactPairs,
            string keyword)
        {
            // 이미 면접촉으로 매칭된 face 쌍을 기록 (중복 방지)
            var facePairedSet = new HashSet<string>();
            foreach (var p in faceContactPairs)
            {
                int ha = p.FaceA.GetHashCode();
                int hb = p.FaceB.GetHashCode();
                string key = ha < hb ? ha + "_" + hb : hb + "_" + ha;
                facePairedSet.Add(key);
            }

            // 모든 바디에서 에지 정보 수집: (에지 시작점, 끝점, 소속 face, 소속 body)
            var edgeRecords = new List<EdgeRecord>();
            foreach (var body in bodies)
            {
                foreach (var face in body.Faces)
                {
                    foreach (var edge in face.Edges)
                    {
                        try
                        {
                            Point start = edge.Shape.StartPoint;
                            Point end = edge.Shape.EndPoint;
                            edgeRecords.Add(new EdgeRecord
                            {
                                Start = start,
                                End = end,
                                Face = face,
                                Body = body
                            });
                        }
                        catch { }
                    }
                }
            }

            DiagnosticLog.Add(string.Format("에지 접촉 검사: 총 에지 {0}개", edgeRecords.Count));

            // 서로 다른 바디 에지 간 공유(coincident) 검사
            // 에지가 공선(collinear)이고 구간이 겹치면 공유 에지
            var edgePairedSet = new HashSet<string>(); // face 쌍 중복 방지
            var result = new List<ContactPairInfo>();
            int edgePairIndex = 1;
            int edgeChecked = 0;
            int edgeCoincident = 0;

            for (int i = 0; i < edgeRecords.Count; i++)
            {
                for (int j = i + 1; j < edgeRecords.Count; j++)
                {
                    var a = edgeRecords[i];
                    var b = edgeRecords[j];

                    // 같은 바디는 스킵
                    if (a.Body == b.Body) continue;

                    // 같은 face 쌍이 이미 면접촉으로 잡혔으면 스킵
                    int hfa = a.Face.GetHashCode();
                    int hfb = b.Face.GetHashCode();
                    string faceKey = hfa < hfb ? hfa + "_" + hfb : hfb + "_" + hfa;
                    if (facePairedSet.Contains(faceKey)) continue;

                    // 이미 이 face 쌍으로 에지 접촉 잡혔으면 스킵
                    if (edgePairedSet.Contains(faceKey)) continue;

                    edgeChecked++;

                    // 에지 공선(collinear) + 겹침 검사
                    if (!EdgesCoincident(a.Start, a.End, b.Start, b.End))
                        continue;

                    edgeCoincident++;
                    edgePairedSet.Add(faceKey);

                    // 접두사 결정
                    string prefix = "EdgeContact";
                    if (!string.IsNullOrEmpty(keyword))
                    {
                        string bodyNameA = a.Body.Name ?? "";
                        string bodyNameB = b.Body.Name ?? "";
                        if (bodyNameA.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            bodyNameB.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            prefix = keyword + "_Edge";
                        }
                    }

                    result.Add(new ContactPairInfo
                    {
                        FaceA = a.Face,
                        FaceB = b.Face,
                        BodyA = a.Body,
                        BodyB = b.Body,
                        Prefix = prefix,
                        PairIndex = edgePairIndex,
                        Area = 0,
                        Type = ContactType.Edge
                    });
                    edgePairIndex++;
                }
            }

            DiagnosticLog.Add(string.Format("에지 비교: {0}쌍, 공선 에지: {1}개", edgeChecked, edgeCoincident));
            return result;
        }

        private class EdgeRecord
        {
            public Point Start;
            public Point End;
            public DesignFace Face;
            public DesignBody Body;
        }

        /// <summary>
        /// 두 에지가 공선(collinear)이고 구간이 겹치는지 판정.
        /// </summary>
        private static bool EdgesCoincident(Point a1, Point a2, Point b1, Point b2)
        {
            const double lineTol = 1e-5; // 0.01mm

            // 에지 A의 방향
            Vector dirA = a2 - a1;
            double lenA = Math.Sqrt(VecDot(dirA, dirA));
            if (lenA < 1e-12) return false;

            // 에지 B의 방향
            Vector dirB = b2 - b1;
            double lenB = Math.Sqrt(VecDot(dirB, dirB));
            if (lenB < 1e-12) return false;

            // 평행 검사: cross product 크기가 충분히 작아야 함
            Vector cross = Cross(dirA, dirB);
            double crossLen = Math.Sqrt(VecDot(cross, cross));
            double sinAngle = crossLen / (lenA * lenB);
            if (sinAngle > 0.05) return false; // ~3도 이상이면 비평행

            // 직선 거리 검사: b1이 에지 A의 직선으로부터 충분히 가까워야 함
            Vector a1ToB1 = b1 - a1;
            Vector crossDist = Cross(dirA, a1ToB1);
            double dist = Math.Sqrt(VecDot(crossDist, crossDist)) / lenA;
            if (dist > lineTol) return false;

            // 구간 겹침 검사: B의 양 끝점을 A 방향으로 투영
            Vector unitA = Vector.Create(dirA.X / lenA, dirA.Y / lenA, dirA.Z / lenA);
            double tB1 = VecDot(b1 - a1, unitA);
            double tB2 = VecDot(b2 - a1, unitA);
            double tBmin = Math.Min(tB1, tB2);
            double tBmax = Math.Max(tB1, tB2);

            // A의 구간은 [0, lenA]
            // 겹침: tBmax > tolerance && tBmin < lenA - tolerance
            double overlapStart = Math.Max(0, tBmin);
            double overlapEnd = Math.Min(lenA, tBmax);
            double overlapLen = overlapEnd - overlapStart;

            return overlapLen > lineTol;
        }

        // ─── 2D 폴리곤 오버랩 검사 ───

        /// <summary>
        /// 두 동일 평면 face의 2D 투영 후 오버랩 여부 판단.
        /// 포함, 부분 겹침, 동일 크기 모두 감지.
        /// </summary>
        private static bool PolygonsOverlap(PlanarFaceInfo a, PlanarFaceInfo b)
        {
            // 공유 평면의 로컬 좌표계 구축
            Vector normal = Normalize(a.Normal);
            Vector uAxis, vAxis;
            BuildPlaneAxes(normal, out uAxis, out vAxis);

            // 기준점: a의 PlaneOrigin
            Point origin = a.PlaneOrigin;

            // 3D → 2D 투영
            var polyA = ProjectToPlane(a.Vertices, origin, uAxis, vAxis);
            var polyB = ProjectToPlane(b.Vertices, origin, uAxis, vAxis);

            if (polyA.Count < 3 || polyB.Count < 3)
                return false;

            // 1단계: 바운딩박스 빠른 거부
            double aMinU, aMaxU, aMinV, aMaxV;
            double bMinU, bMaxU, bMinV, bMaxV;
            GetBBox(polyA, out aMinU, out aMaxU, out aMinV, out aMaxV);
            GetBBox(polyB, out bMinU, out bMaxU, out bMinV, out bMaxV);

            if (aMaxU < bMinU - VertexSnapTol || bMaxU < aMinU - VertexSnapTol ||
                aMaxV < bMinV - VertexSnapTol || bMaxV < aMinV - VertexSnapTol)
                return false;

            // 2단계: A의 꼭짓점이 B 내부에 있는지
            foreach (var pt in polyA)
            {
                if (PointInPolygon(pt, polyB))
                    return true;
            }

            // 3단계: B의 꼭짓점이 A 내부에 있는지
            foreach (var pt in polyB)
            {
                if (PointInPolygon(pt, polyA))
                    return true;
            }

            // 4단계: 에지 교차 검사 (별이나 L 형태 등에서 꼭짓점이 내부에 없어도 교차 가능)
            for (int ia = 0; ia < polyA.Count; ia++)
            {
                int ia2 = (ia + 1) % polyA.Count;
                for (int ib = 0; ib < polyB.Count; ib++)
                {
                    int ib2 = (ib + 1) % polyB.Count;
                    if (SegmentsIntersect(polyA[ia], polyA[ia2], polyB[ib], polyB[ib2]))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 평면 법선에서 직교 U/V 축 생성
        /// </summary>
        private static void BuildPlaneAxes(Vector normal, out Vector uAxis, out Vector vAxis)
        {
            // normal과 가장 적게 평행한 세계축을 골라 cross product
            double ax = Math.Abs(normal.X);
            double ay = Math.Abs(normal.Y);
            double az = Math.Abs(normal.Z);

            Vector seed;
            if (ax <= ay && ax <= az)
                seed = Vector.Create(1, 0, 0);
            else if (ay <= az)
                seed = Vector.Create(0, 1, 0);
            else
                seed = Vector.Create(0, 0, 1);

            uAxis = Normalize(Cross(normal, seed));
            vAxis = Normalize(Cross(normal, uAxis));
        }

        /// <summary>
        /// 3D 점 리스트를 평면 로컬 좌표계의 2D로 투영
        /// </summary>
        private static List<Vec2> ProjectToPlane(List<Point> pts, Point origin, Vector uAxis, Vector vAxis)
        {
            var result = new List<Vec2>(pts.Count);
            foreach (var pt in pts)
            {
                Vector d = pt - origin;
                double u = VecDot(d, uAxis);
                double v = VecDot(d, vAxis);
                result.Add(new Vec2(u, v));
            }
            return result;
        }

        // ─── 2D 기하 유틸 ───

        /// <summary>
        /// Ray casting 알고리즘으로 점이 폴리곤 내부인지 판정
        /// </summary>
        private static bool PointInPolygon(Vec2 pt, List<Vec2> polygon)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vec2 pi = polygon[i], pj = polygon[j];
                if ((pi.V > pt.V) != (pj.V > pt.V) &&
                    pt.U < (pj.U - pi.U) * (pt.V - pi.V) / (pj.V - pi.V) + pi.U)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// 두 선분이 교차하는지 판정
        /// </summary>
        private static bool SegmentsIntersect(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2)
        {
            double d1 = CrossSign(b1, b2, a1);
            double d2 = CrossSign(b1, b2, a2);
            double d3 = CrossSign(a1, a2, b1);
            double d4 = CrossSign(a1, a2, b2);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            // 공선(collinear) 케이스
            if (Math.Abs(d1) < 1e-12 && OnSegment(b1, b2, a1)) return true;
            if (Math.Abs(d2) < 1e-12 && OnSegment(b1, b2, a2)) return true;
            if (Math.Abs(d3) < 1e-12 && OnSegment(a1, a2, b1)) return true;
            if (Math.Abs(d4) < 1e-12 && OnSegment(a1, a2, b2)) return true;

            return false;
        }

        private static double CrossSign(Vec2 a, Vec2 b, Vec2 c)
        {
            return (b.U - a.U) * (c.V - a.V) - (b.V - a.V) * (c.U - a.U);
        }

        private static bool OnSegment(Vec2 p, Vec2 q, Vec2 r)
        {
            return Math.Min(p.U, q.U) <= r.U + 1e-12 && r.U <= Math.Max(p.U, q.U) + 1e-12 &&
                   Math.Min(p.V, q.V) <= r.V + 1e-12 && r.V <= Math.Max(p.V, q.V) + 1e-12;
        }

        private static void GetBBox(List<Vec2> poly,
            out double minU, out double maxU, out double minV, out double maxV)
        {
            minU = double.MaxValue; maxU = double.MinValue;
            minV = double.MaxValue; maxV = double.MinValue;
            foreach (var p in poly)
            {
                if (p.U < minU) minU = p.U;
                if (p.U > maxU) maxU = p.U;
                if (p.V < minV) minV = p.V;
                if (p.V > maxV) maxV = p.V;
            }
        }

        // ─── 내부 영역 겹침 판별 ───

        /// <summary>
        /// 두 동일 평면 폴리곤이 면적을 공유하는지 (경계만 닿는 경우 false).
        /// 인접한 면이 에지만 공유하는 경우를 면접촉에서 제외하기 위함.
        /// </summary>
        private static bool HasInteriorOverlap(PlanarFaceInfo a, PlanarFaceInfo b)
        {
            // 공유 평면의 로컬 좌표계 구축
            Vector normal = Normalize(a.Normal);
            Vector uAxis, vAxis;
            BuildPlaneAxes(normal, out uAxis, out vAxis);
            Point origin = a.PlaneOrigin;

            var polyA = ProjectToPlane(a.Vertices, origin, uAxis, vAxis);
            var polyB = ProjectToPlane(b.Vertices, origin, uAxis, vAxis);

            if (polyA.Count < 3 || polyB.Count < 3)
                return false;

            // 경계 거리 임계값: 이 거리 이내이면 "경계 위"로 간주
            const double boundaryTol = 1e-6; // 0.001mm

            // 1단계: 중심점(centroid) 검사
            // 동일한 면이 겹치는 경우, 꼭짓점은 경계 위에 놓이지만
            // 중심점은 상대 폴리곤의 깊은 내부에 위치
            Vec2 centroidA = PolygonCentroid(polyA);
            if (PointStrictlyInsidePolygon(centroidA, polyB, boundaryTol))
                return true;

            Vec2 centroidB = PolygonCentroid(polyB);
            if (PointStrictlyInsidePolygon(centroidB, polyA, boundaryTol))
                return true;

            // 2단계: 에지 중점 검사
            // 부분 겹침에서 양쪽 중심점이 모두 상대 바깥에 있어도
            // 겹치는 영역의 에지 중점은 상대 내부에 위치할 수 있음
            for (int i = 0; i < polyA.Count; i++)
            {
                int j = (i + 1) % polyA.Count;
                Vec2 mid = new Vec2(
                    (polyA[i].U + polyA[j].U) * 0.5,
                    (polyA[i].V + polyA[j].V) * 0.5);
                if (PointStrictlyInsidePolygon(mid, polyB, boundaryTol))
                    return true;
            }
            for (int i = 0; i < polyB.Count; i++)
            {
                int j = (i + 1) % polyB.Count;
                Vec2 mid = new Vec2(
                    (polyB[i].U + polyB[j].U) * 0.5,
                    (polyB[i].V + polyB[j].V) * 0.5);
                if (PointStrictlyInsidePolygon(mid, polyA, boundaryTol))
                    return true;
            }

            // 3단계: A의 꼭짓점이 B의 내부에 엄격히 위치하는지 (경계 제외)
            foreach (var pt in polyA)
            {
                if (PointStrictlyInsidePolygon(pt, polyB, boundaryTol))
                    return true;
            }

            // 4단계: B의 꼭짓점이 A의 내부에 엄격히 위치하는지
            foreach (var pt in polyB)
            {
                if (PointStrictlyInsidePolygon(pt, polyA, boundaryTol))
                    return true;
            }

            // 5단계: 에지 교차가 "진짜 교차"인지 (공유 에지/꼭짓점 접촉이 아닌)
            for (int ia = 0; ia < polyA.Count; ia++)
            {
                int ia2 = (ia + 1) % polyA.Count;
                for (int ib = 0; ib < polyB.Count; ib++)
                {
                    int ib2 = (ib + 1) % polyB.Count;
                    if (SegmentsProperlyIntersect(polyA[ia], polyA[ia2], polyB[ib], polyB[ib2], boundaryTol))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 폴리곤의 무게 중심 (꼭짓점 평균)
        /// </summary>
        private static Vec2 PolygonCentroid(List<Vec2> poly)
        {
            double sumU = 0, sumV = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                sumU += poly[i].U;
                sumV += poly[i].V;
            }
            return new Vec2(sumU / poly.Count, sumV / poly.Count);
        }

        /// <summary>
        /// 점이 폴리곤 내부에 엄격히 위치하는지 (경계에서 boundaryTol 이상 떨어져야 함)
        /// </summary>
        private static bool PointStrictlyInsidePolygon(Vec2 pt, List<Vec2> polygon, double tol)
        {
            // 먼저 ray casting으로 내부 판정
            if (!PointInPolygon(pt, polygon))
                return false;

            // 폴리곤의 모든 에지와의 최소 거리 계산
            double minDist = double.MaxValue;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                double d = PointToSegmentDist(pt, polygon[i], polygon[j]);
                if (d < minDist)
                    minDist = d;
            }

            // 경계에 너무 가까우면 "경계 위"로 판정
            return minDist > tol;
        }

        /// <summary>
        /// 점에서 선분까지의 최소 거리 (2D)
        /// </summary>
        private static double PointToSegmentDist(Vec2 pt, Vec2 a, Vec2 b)
        {
            double dx = b.U - a.U;
            double dy = b.V - a.V;
            double lenSq = dx * dx + dy * dy;
            if (lenSq < 1e-24)
            {
                double ex = pt.U - a.U, ey = pt.V - a.V;
                return Math.Sqrt(ex * ex + ey * ey);
            }

            double t = ((pt.U - a.U) * dx + (pt.V - a.V) * dy) / lenSq;
            t = Math.Max(0, Math.Min(1, t));

            double closestU = a.U + t * dx;
            double closestV = a.V + t * dy;
            double du = pt.U - closestU;
            double dv = pt.V - closestV;
            return Math.Sqrt(du * du + dv * dv);
        }

        /// <summary>
        /// 두 선분이 "진짜" 교차하는지 (공선/끝점 접촉이 아닌 진정한 내부 교차)
        /// </summary>
        private static bool SegmentsProperlyIntersect(Vec2 a1, Vec2 a2, Vec2 b1, Vec2 b2, double tol)
        {
            double d1 = CrossSign(b1, b2, a1);
            double d2 = CrossSign(b1, b2, a2);
            double d3 = CrossSign(a1, a2, b1);
            double d4 = CrossSign(a1, a2, b2);

            // 엄격한 교차: 양쪽 선분의 양 끝점이 상대 선분의 반대편에 있어야 함
            // 공선(collinear)이면 cross product ≈ 0 → 이 검사를 통과하지 못함
            // 끝점이 정확히 선분 위에 있으면(d ≈ 0) 이것도 경계 접촉이므로 제외
            if (Math.Abs(d1) < tol || Math.Abs(d2) < tol ||
                Math.Abs(d3) < tol || Math.Abs(d4) < tol)
                return false; // 경계/공선 접촉

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            return false;
        }

        // ─── 3D 벡터 유틸 ───

        private static double VecDot(Vector a, Vector b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        private static Vector Cross(Vector a, Vector b)
        {
            return Vector.Create(
                a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        }

        private static Vector Normalize(Vector v)
        {
            double len = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
            if (len < 1e-15) return v;
            return Vector.Create(v.X / len, v.Y / len, v.Z / len);
        }

        // ─── 셀프 테스트 (SpaceClaim API 불필요) ───

        /// <summary>
        /// 순수 기하 알고리즘 검증. SpaceClaim 객체 없이 내부 로직만 테스트.
        /// </summary>
        public static List<string> RunSelfTest()
        {
            var log = new List<string>();
            int pass = 0, fail = 0;

            // ── 1. PointInPolygon 테스트 ──
            // 단위 정사각형 (0,0)-(1,0)-(1,1)-(0,1)
            var square = new List<Vec2>
            {
                new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1)
            };

            Check(log, ref pass, ref fail, "PIP: 중심(0.5,0.5)은 내부",
                PointInPolygon(new Vec2(0.5, 0.5), square) == true);

            Check(log, ref pass, ref fail, "PIP: (2,2)는 외부",
                PointInPolygon(new Vec2(2, 2), square) == false);

            Check(log, ref pass, ref fail, "PIP: (-0.1,0.5)는 외부",
                PointInPolygon(new Vec2(-0.1, 0.5), square) == false);

            Check(log, ref pass, ref fail, "PIP: (0.01,0.01)은 내부",
                PointInPolygon(new Vec2(0.01, 0.01), square) == true);

            // ── 2. SegmentsIntersect 테스트 ──
            Check(log, ref pass, ref fail, "선분교차: X자 교차",
                SegmentsIntersect(new Vec2(0, 0), new Vec2(1, 1), new Vec2(0, 1), new Vec2(1, 0)) == true);

            Check(log, ref pass, ref fail, "선분교차: 평행 비교차",
                SegmentsIntersect(new Vec2(0, 0), new Vec2(1, 0), new Vec2(0, 1), new Vec2(1, 1)) == false);

            Check(log, ref pass, ref fail, "선분교차: T자 접촉",
                SegmentsIntersect(new Vec2(0, 0), new Vec2(1, 0), new Vec2(0.5, 0), new Vec2(0.5, 1)) == true);

            // ── 3. 폴리곤 오버랩 (PlanarFaceInfo 시뮬레이션) ──
            // 큰 사각형 (0,0,0)~(10mm,10mm,0) vs 작은 사각형 (2mm,2mm,0)~(5mm,5mm,0)
            // 단위: m (SpaceClaim 내부 단위)
            double s = 0.01; // 10mm = 0.01m

            var bigFace = MakeTestFaceInfo(
                Vector.Create(0, 0, 1), Point.Create(0, 0, 0),
                new List<Point>
                {
                    Point.Create(0, 0, 0), Point.Create(s, 0, 0),
                    Point.Create(s, s, 0), Point.Create(0, s, 0)
                });
            var smallFace = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(0.002, 0.002, 0),
                new List<Point>
                {
                    Point.Create(0.002, 0.002, 0), Point.Create(0.005, 0.002, 0),
                    Point.Create(0.005, 0.005, 0), Point.Create(0.002, 0.005, 0)
                });

            Check(log, ref pass, ref fail, "오버랩: 큰면 안에 작은면 (포함)",
                PolygonsOverlap(bigFace, smallFace) == true);

            // 완전히 분리된 면
            var farFace = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(0.02, 0.02, 0),
                new List<Point>
                {
                    Point.Create(0.02, 0.02, 0), Point.Create(0.03, 0.02, 0),
                    Point.Create(0.03, 0.03, 0), Point.Create(0.02, 0.03, 0)
                });

            Check(log, ref pass, ref fail, "오버랩: 분리된 면은 false",
                PolygonsOverlap(bigFace, farFace) == false);

            // 부분 겹침
            var partialFace = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(0.005, 0.005, 0),
                new List<Point>
                {
                    Point.Create(0.005, 0.005, 0), Point.Create(0.015, 0.005, 0),
                    Point.Create(0.015, 0.015, 0), Point.Create(0.005, 0.015, 0)
                });

            Check(log, ref pass, ref fail, "오버랩: 부분 겹침",
                PolygonsOverlap(bigFace, partialFace) == true);

            // ── 4. 법선 체크 시뮬레이션 ──
            Vector nUp = Vector.Create(0, 0, 1);
            Vector nDown = Vector.Create(0, 0, -1);
            Vector nSame = Vector.Create(0, 0, 1);
            Vector nTilted = Vector.Create(0, Math.Sin(Math.PI / 36), -Math.Cos(Math.PI / 36)); // 5도 기울임

            double dotAntiParallel = VecDot(nUp, nDown);
            Check(log, ref pass, ref fail,
                string.Format("법선: 완전 반대 dot={0:F4} → 통과해야함", dotAntiParallel),
                dotAntiParallel <= -NormalTolerance);

            double dotSame = VecDot(nUp, nSame);
            Check(log, ref pass, ref fail,
                string.Format("법선: 같은방향 dot={0:F4} → 거부해야함", dotSame),
                dotSame > -NormalTolerance);

            double dotTilted = VecDot(nUp, nTilted);
            Check(log, ref pass, ref fail,
                string.Format("법선: 5도 기울임 dot={0:F4} → 통과해야함(10도 허용)", dotTilted),
                dotTilted <= -NormalTolerance);

            // 11도 기울임 → 거부해야함
            Vector nTilted11 = Vector.Create(0, Math.Sin(11 * Math.PI / 180), -Math.Cos(11 * Math.PI / 180));
            double dotTilted11 = VecDot(nUp, nTilted11);
            Check(log, ref pass, ref fail,
                string.Format("법선: 11도 기울임 dot={0:F4} → 거부해야함(10도 허용)", dotTilted11),
                dotTilted11 > -NormalTolerance);

            // ── 5. 평면 거리 체크 ──
            Vector diff1 = Point.Create(0, 0, 0.00003) - Point.Create(0, 0, 0); // 0.03mm
            double dist1 = Math.Abs(VecDot(diff1, Normalize(nUp)));
            Check(log, ref pass, ref fail,
                string.Format("평면거리: 0.03mm={0:E3}m → 통과(tol=0.05mm)", dist1),
                dist1 <= PlaneTolerance);

            Vector diff2 = Point.Create(0, 0, 0.0001) - Point.Create(0, 0, 0); // 0.1mm
            double dist2 = Math.Abs(VecDot(diff2, Normalize(nUp)));
            Check(log, ref pass, ref fail,
                string.Format("평면거리: 0.1mm={0:E3}m → 거부(tol=0.05mm)", dist2),
                dist2 > PlaneTolerance);

            // ── 6. BuildPlaneAxes 직교성 검증 ──
            Vector[] testNormals = {
                Vector.Create(0, 0, 1),
                Vector.Create(1, 0, 0),
                Vector.Create(0, 1, 0),
                Normalize(Vector.Create(1, 1, 1))
            };
            foreach (var tn in testNormals)
            {
                Vector u, v;
                BuildPlaneAxes(tn, out u, out v);
                double dotNU = Math.Abs(VecDot(tn, u));
                double dotNV = Math.Abs(VecDot(tn, v));
                double dotUV = Math.Abs(VecDot(u, v));
                bool ortho = dotNU < 1e-10 && dotNV < 1e-10 && dotUV < 1e-10;
                Check(log, ref pass, ref fail,
                    string.Format("직교축: n=({0:F2},{1:F2},{2:F2}) → n·u={3:E1}, n·v={4:E1}, u·v={5:E1}",
                        tn.X, tn.Y, tn.Z, dotNU, dotNV, dotUV),
                    ortho);
            }

            // ── 7. EdgesCoincident 테스트 ──
            // 완전히 같은 에지
            Check(log, ref pass, ref fail, "에지공선: 동일 에지",
                EdgesCoincident(
                    Point.Create(0, 0, 0), Point.Create(0.01, 0, 0),
                    Point.Create(0, 0, 0), Point.Create(0.01, 0, 0)) == true);

            // 부분 겹침 (같은 직선, 구간 일부 겹침)
            Check(log, ref pass, ref fail, "에지공선: 부분 겹침",
                EdgesCoincident(
                    Point.Create(0, 0, 0), Point.Create(0.01, 0, 0),
                    Point.Create(0.005, 0, 0), Point.Create(0.02, 0, 0)) == true);

            // 역방향 에지
            Check(log, ref pass, ref fail, "에지공선: 역방향 동일",
                EdgesCoincident(
                    Point.Create(0, 0, 0), Point.Create(0.01, 0, 0),
                    Point.Create(0.01, 0, 0), Point.Create(0, 0, 0)) == true);

            // 평행하지만 떨어진 에지
            Check(log, ref pass, ref fail, "에지공선: 평행 but 떨어짐 → false",
                EdgesCoincident(
                    Point.Create(0, 0, 0), Point.Create(0.01, 0, 0),
                    Point.Create(0, 0.001, 0), Point.Create(0.01, 0.001, 0)) == false);

            // 같은 직선이지만 겹치지 않는 구간
            Check(log, ref pass, ref fail, "에지공선: 같은 직선, 비겹침 구간 → false",
                EdgesCoincident(
                    Point.Create(0, 0, 0), Point.Create(0.01, 0, 0),
                    Point.Create(0.02, 0, 0), Point.Create(0.03, 0, 0)) == false);

            // 수직 에지 (비평행)
            Check(log, ref pass, ref fail, "에지공선: 수직 → false",
                EdgesCoincident(
                    Point.Create(0, 0, 0), Point.Create(0.01, 0, 0),
                    Point.Create(0, 0, 0), Point.Create(0, 0.01, 0)) == false);

            // ── 8. HasInteriorOverlap 테스트 ──
            // 인접 사각형: (0,0)~(10mm,10mm) 와 (10mm,0)~(20mm,10mm) → 에지만 공유
            var adjFaceA = MakeTestFaceInfo(
                Vector.Create(0, 0, 1), Point.Create(0, 0, 0),
                new List<Point>
                {
                    Point.Create(0, 0, 0), Point.Create(s, 0, 0),
                    Point.Create(s, s, 0), Point.Create(0, s, 0)
                });
            var adjFaceB = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(s, 0, 0),
                new List<Point>
                {
                    Point.Create(s, 0, 0), Point.Create(2 * s, 0, 0),
                    Point.Create(2 * s, s, 0), Point.Create(s, s, 0)
                });

            Check(log, ref pass, ref fail, "내부겹침: 인접 면(에지 공유) → false",
                HasInteriorOverlap(adjFaceA, adjFaceB) == false);

            // 겹치는 사각형: (0,0)~(10mm,10mm) 와 (5mm,0)~(15mm,10mm) → 면적 겹침
            var overlapFaceB = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(0.005, 0, 0),
                new List<Point>
                {
                    Point.Create(0.005, 0, 0), Point.Create(0.015, 0, 0),
                    Point.Create(0.015, s, 0), Point.Create(0.005, s, 0)
                });

            Check(log, ref pass, ref fail, "내부겹침: 부분 겹침 → true",
                HasInteriorOverlap(adjFaceA, overlapFaceB) == true);

            // 포함 관계: 큰 면 안에 작은 면
            Check(log, ref pass, ref fail, "내부겹침: 포함 → true",
                HasInteriorOverlap(bigFace, smallFace) == true);

            // 꼭짓점만 공유 (모서리 접촉): (0,0)~(10mm,10mm) 와 (10mm,10mm)~(20mm,20mm)
            var cornerFaceB = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(s, s, 0),
                new List<Point>
                {
                    Point.Create(s, s, 0), Point.Create(2 * s, s, 0),
                    Point.Create(2 * s, 2 * s, 0), Point.Create(s, 2 * s, 0)
                });

            Check(log, ref pass, ref fail, "내부겹침: 꼭짓점만 공유 → false",
                HasInteriorOverlap(adjFaceA, cornerFaceB) == false);

            // 동일한 면 (완전히 같은 위치) → 면접촉! (가장 흔한 케이스)
            var identicalFaceB = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(0, 0, 0),
                new List<Point>
                {
                    Point.Create(0, 0, 0), Point.Create(s, 0, 0),
                    Point.Create(s, s, 0), Point.Create(0, s, 0)
                });

            Check(log, ref pass, ref fail, "내부겹침: 동일 면(identical) → true",
                HasInteriorOverlap(adjFaceA, identicalFaceB) == true);

            // 부분 겹침 (경계가 일치하는 케이스): (0,0)~(6,5) 와 (4,0)~(10,5)
            // 겹침 영역 (4,0)~(6,5)의 경계가 양쪽 폴리곤 에지와 일치
            double s6 = 0.006, s4 = 0.004;
            var alignedA = MakeTestFaceInfo(
                Vector.Create(0, 0, 1), Point.Create(0, 0, 0),
                new List<Point>
                {
                    Point.Create(0, 0, 0), Point.Create(s6, 0, 0),
                    Point.Create(s6, 0.005, 0), Point.Create(0, 0.005, 0)
                });
            var alignedB = MakeTestFaceInfo(
                Vector.Create(0, 0, -1), Point.Create(s4, 0, 0),
                new List<Point>
                {
                    Point.Create(s4, 0, 0), Point.Create(s, 0, 0),
                    Point.Create(s, 0.005, 0), Point.Create(s4, 0.005, 0)
                });

            Check(log, ref pass, ref fail, "내부겹침: 부분겹침(경계일치) → true",
                HasInteriorOverlap(alignedA, alignedB) == true);

            // PointStrictlyInsidePolygon 직접 테스트
            Check(log, ref pass, ref fail, "엄격내부: (0.5,0.5) in square → true",
                PointStrictlyInsidePolygon(new Vec2(0.5, 0.5), square, 1e-6) == true);

            Check(log, ref pass, ref fail, "엄격내부: (0,0.5) 경계 위 → false",
                PointStrictlyInsidePolygon(new Vec2(0, 0.5), square, 1e-6) == false);

            Check(log, ref pass, ref fail, "엄격내부: (1e-7,0.5) 경계 매우 근접 → false",
                PointStrictlyInsidePolygon(new Vec2(1e-7, 0.5), square, 1e-6) == false);

            log.Insert(0, string.Format("=== 셀프 테스트 결과: {0} PASS, {1} FAIL ===\r\n", pass, fail));
            return log;
        }

        private static PlanarFaceInfo MakeTestFaceInfo(Vector normal, Point origin, List<Point> vertices)
        {
            return new PlanarFaceInfo
            {
                Face = null,
                Body = null,
                Normal = normal,
                PlaneOrigin = origin,
                Area = 0,
                Vertices = vertices
            };
        }

        private static void Check(List<string> log, ref int pass, ref int fail, string desc, bool condition)
        {
            if (condition)
            {
                pass++;
                log.Add(string.Format("[PASS] {0}", desc));
            }
            else
            {
                fail++;
                log.Add(string.Format("[FAIL] {0}", desc));
            }
        }

        // ─── Named Selection 생성 ───

        private static void CreateNamedSelections(Part part, List<ContactPairInfo> pairs)
        {
            // 개별 페어 Named Selection 생성
            foreach (var pair in pairs)
            {
                FaceNamingHelper.NameFace(part, pair.FaceA, pair.NameA);
                FaceNamingHelper.NameFace(part, pair.FaceB, pair.NameB);
            }

            // 면접촉(Face) 전체를 A/B로 묶은 그룹 Named Selection 생성
            // 접두사별로 그룹화: 같은 접두사의 모든 A면, 모든 B면
            var prefixGroups = new Dictionary<string, List<ContactPairInfo>>();
            foreach (var pair in pairs)
            {
                if (pair.Type != Models.Contact.ContactType.Face)
                    continue;

                List<ContactPairInfo> group;
                if (!prefixGroups.TryGetValue(pair.Prefix, out group))
                {
                    group = new List<ContactPairInfo>();
                    prefixGroups[pair.Prefix] = group;
                }
                group.Add(pair);
            }

            foreach (var kv in prefixGroups)
            {
                string prefix = kv.Key;
                var group = kv.Value;
                if (group.Count == 0) continue;

                var allAFaces = new List<DesignFace>();
                var allBFaces = new List<DesignFace>();
                foreach (var pair in group)
                {
                    allAFaces.Add(pair.FaceA);
                    allBFaces.Add(pair.FaceB);
                }

                // 접두사_1 = 모든 A면 (+ 법선), 접두사_2 = 모든 B면 (- 법선)
                FaceNamingHelper.NameFaces(part, allAFaces, prefix + "_1");
                FaceNamingHelper.NameFaces(part, allBFaces, prefix + "_2");
            }
        }

        // ─── 바디 수집 ───

        public static List<DesignBody> GetAllDesignBodies(Part rootPart)
        {
            var result = new List<DesignBody>();
            CollectBodies(rootPart, result);
            return result;
        }

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
