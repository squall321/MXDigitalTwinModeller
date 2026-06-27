using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Core.Geometry;

#if V251
using SpaceClaim.Api.V251.Geometry;
using SpaceClaim.Api.V251.Modeler;
#elif V252
using SpaceClaim.Api.V252.Geometry;
using SpaceClaim.Api.V252.Modeler;
#endif

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.VoidCut
{
    public enum BooleanMode
    {
        Subtract,
        Unite,
        Intersect
    }

    public enum CutterShape
    {
        Cuboid,
        Cylinder,
        Sphere
    }

    public class VoidCutResult
    {
        public int CandidateCount { get; set; }
        public int SubtractedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
        public double TotalVolumeBeforeMm3 { get; set; }
        public double TotalVolumeAfterMm3 { get; set; }
        public int NamedSelectionsCreated { get; set; }
        public List<string> Log { get; set; } = new List<string>();
    }

    /// <summary>
    /// Void Cut 서비스 (확장판)
    /// Boolean Subtract/Unite/Intersect, 다중 커터/타겟, 오프셋,
    /// 체적 표시, NS 자동 생성, 배치 모드 지원
    /// </summary>
    public static class VoidCutService
    {
        #region Main Execute

        /// <summary>
        /// 메인 실행 메서드: 모든 옵션 지원
        /// </summary>
        public static VoidCutResult Execute(
            Part part,
            List<DesignBody> cutterBodies,
            List<DesignBody> targetBodies,
            BooleanMode mode,
            double offsetMm,
            bool deleteCutters,
            bool createNS,
            bool showVolume)
        {
            var result = new VoidCutResult();

            if (part == null)
            {
                result.Log.Add("[ERROR] Part가 null입니다.");
                return result;
            }
            if (cutterBodies == null || cutterBodies.Count == 0)
            {
                result.Log.Add("[ERROR] 커터 바디가 없습니다.");
                return result;
            }
            if (targetBodies == null || targetBodies.Count == 0)
            {
                result.Log.Add("[ERROR] 타겟 바디가 없습니다.");
                return result;
            }

            string modeName = GetModeName(mode);
            result.Log.Add(string.Format("모드: {0}, 커터: {1}개, 대상: {2}개",
                modeName, cutterBodies.Count, targetBodies.Count));

            if (offsetMm != 0)
                result.Log.Add(string.Format("오프셋: {0:F2} mm", offsetMm));

            // 체적 (실행 전)
            if (showVolume)
            {
                double volBefore = ComputeTotalVolume(targetBodies);
                result.TotalVolumeBeforeMm3 = volBefore * 1e9;
                result.Log.Add(string.Format("대상 체적 (실행 전): {0:F1} mm\u00B3",
                    result.TotalVolumeBeforeMm3));
            }

            var modifiedBodies = new List<DesignBody>();
            double offsetM = GeometryUtils.MmToMeters(offsetMm);

            // 커터 셋 (자기 참조 방지용)
            var cutterSet = new HashSet<DesignBody>(cutterBodies);

            // 각 커터에 대해 Boolean 수행
            foreach (var cutterBody in cutterBodies)
            {
                string cutterName = GetBodyName(cutterBody);
                result.Log.Add(string.Format("\n--- 커터: {0} ---", cutterName));

                AABB cutterBBox;
                try
                {
                    cutterBBox = ComputeAABB(cutterBody);
                }
                catch
                {
                    result.Log.Add("  [WARN] 커터 AABB 계산 실패 (바디가 유효하지 않음)");
                    continue;
                }

                // 오프셋 적용 시 AABB 확장
                if (offsetM > 0)
                {
                    cutterBBox.MinX -= offsetM;
                    cutterBBox.MinY -= offsetM;
                    cutterBBox.MinZ -= offsetM;
                    cutterBBox.MaxX += offsetM;
                    cutterBBox.MaxY += offsetM;
                    cutterBBox.MaxZ += offsetM;
                }

                foreach (var targetBody in targetBodies)
                {
                    // 커터==타겟 자기 참조 방지 (같은 바디에 자기 자신을 빼면 소멸됨)
                    if (cutterSet.Contains(targetBody) && targetBody == cutterBody)
                        continue;

                    AABB bodyBBox;
                    try
                    {
                        bodyBBox = ComputeAABB(targetBody);
                    }
                    catch
                    {
                        continue; // 이전 Boolean으로 무효화된 바디 스킵
                    }

                    if (!AABBOverlap(cutterBBox, bodyBBox))
                        continue;

                    result.CandidateCount++;
                    string bodyName = GetBodyName(targetBody);

                    try
                    {
                        Body cutterCopy = cutterBody.Shape.Copy();

                        // 오프셋 적용 (스케일)
                        if (offsetM > 0)
                            ScaleBody(cutterCopy, cutterBody, offsetM);

                        ApplyBoolean(targetBody, cutterCopy, mode);
                        result.SubtractedCount++;
                        result.Log.Add(string.Format("  [OK] {0}", bodyName));

                        if (!modifiedBodies.Contains(targetBody))
                            modifiedBodies.Add(targetBody);
                    }
                    catch (Exception ex)
                    {
                        result.SkippedCount++;
                        result.Log.Add(string.Format("  [SKIP] {0}: {1}", bodyName, ex.Message));
                    }
                }
            }

            // 체적 (실행 후)
            if (showVolume)
            {
                double volAfter = ComputeTotalVolume(targetBodies);
                result.TotalVolumeAfterMm3 = volAfter * 1e9;
                double delta = result.TotalVolumeAfterMm3 - result.TotalVolumeBeforeMm3;
                result.Log.Add(string.Format("\n대상 체적 (실행 후): {0:F1} mm\u00B3",
                    result.TotalVolumeAfterMm3));
                double pct = result.TotalVolumeBeforeMm3 > 0
                    ? delta / result.TotalVolumeBeforeMm3 * 100 : 0;
                result.Log.Add(string.Format("체적 변화: {0:+0.1;-0.1;0} mm\u00B3 ({1:F1}%)",
                    delta, pct));
            }

            // Named Selection 생성
            if (createNS && modifiedBodies.Count > 0)
                CreateNamedSelection(part, modifiedBodies, modeName, result);

            // 커터 삭제 (타겟이기도 한 커터는 보존)
            if (deleteCutters)
                DeleteCutters(cutterBodies, targetBodies, result);

            result.Log.Add("");
            result.Log.Add(string.Format("=== 완료: 성공 {0}, 스킵 {1} ===",
                result.SubtractedCount, result.SkippedCount));

            return result;
        }

        #endregion

        #region Legacy Wrappers

        public static VoidCutResult ExecuteWithBody(
            Part part, DesignBody cutterBody, bool deleteCutter)
        {
            var cutters = new List<DesignBody> { cutterBody };
            var targets = new List<DesignBody>();
            foreach (DesignBody body in part.Bodies)
            {
                if (body != cutterBody)
                    targets.Add(body);
            }
            return Execute(part, cutters, targets, BooleanMode.Subtract,
                0, deleteCutter, false, false);
        }

        public static VoidCutResult ExecuteWithBodies(
            Part part, List<DesignBody> cutterBodies, bool deleteCutters)
        {
            var cutterSet = new HashSet<DesignBody>(cutterBodies);
            var targets = new List<DesignBody>();
            foreach (DesignBody body in part.Bodies)
            {
                if (!cutterSet.Contains(body))
                    targets.Add(body);
            }
            return Execute(part, cutterBodies, targets, BooleanMode.Subtract,
                0, deleteCutters, false, false);
        }

        public static int CountCandidates(Part part, DesignBody cutterBody)
        {
            if (part == null || cutterBody == null) return 0;
            var cutterBBox = ComputeAABB(cutterBody);
            int count = 0;
            foreach (DesignBody body in part.Bodies)
            {
                if (body == cutterBody) continue;
                if (AABBOverlap(cutterBBox, ComputeAABB(body)))
                    count++;
            }
            return count;
        }

        #endregion

        #region Shape Creation

        public static Body CreateCuboidBody(
            double wMm, double hMm, double dMm,
            double pxMm, double pyMm, double pzMm, double offsetMm)
        {
            double w = GeometryUtils.MmToMeters(wMm + offsetMm * 2);
            double h = GeometryUtils.MmToMeters(hMm + offsetMm * 2);
            double d = GeometryUtils.MmToMeters(dMm + offsetMm * 2);
            double px = GeometryUtils.MmToMeters(pxMm);
            double py = GeometryUtils.MmToMeters(pyMm);
            double pz = GeometryUtils.MmToMeters(pzMm);

            Point center = Point.Create(px, py, pz);
            Frame frame = Frame.Create(center, Direction.DirX, Direction.DirY);
            Plane basePlane = Plane.Create(frame);

            Profile profile = new RectangleProfile(basePlane, w, h);
            Body body = Body.ExtrudeProfile(profile, d);
            body.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, -d / 2.0)));
            return body;
        }

        public static Body CreateCylinderBody(
            double radiusMm, double heightMm,
            double pxMm, double pyMm, double pzMm, double offsetMm)
        {
            double r = GeometryUtils.MmToMeters(radiusMm + offsetMm);
            double ht = GeometryUtils.MmToMeters(heightMm + offsetMm * 2);
            double px = GeometryUtils.MmToMeters(pxMm);
            double py = GeometryUtils.MmToMeters(pyMm);
            double pz = GeometryUtils.MmToMeters(pzMm);

            Point center = Point.Create(px, py, pz);
            Frame frame = Frame.Create(center, Direction.DirX, Direction.DirY);
            Plane basePlane = Plane.Create(frame);

            var builder = new ProfileBuilder(basePlane);
            builder.AddCircle(center, r);
            Profile profile = builder.Build();

            Body body = Body.ExtrudeProfile(profile, ht);
            body.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, -ht / 2.0)));
            return body;
        }

        public static Body CreateSphereBody(
            double radiusMm,
            double pxMm, double pyMm, double pzMm, double offsetMm)
        {
            double r = GeometryUtils.MmToMeters(radiusMm + offsetMm);
            double px = GeometryUtils.MmToMeters(pxMm);
            double py = GeometryUtils.MmToMeters(pyMm);
            double pz = GeometryUtils.MmToMeters(pzMm);

            // 구 생성: 정육면체 → 모든 엣지 라운딩 (radius = side/2)
            // 이 방법은 정확한 구를 생성함
            double side = 2 * r;
            Point center = Point.Create(px, py, pz);
            Frame frame = Frame.Create(center, Direction.DirX, Direction.DirY);
            Plane basePlane = Plane.Create(frame);

            Profile profile = new RectangleProfile(basePlane, side, side);
            Body body = Body.ExtrudeProfile(profile, side);
            body.Transform(Matrix.CreateTranslation(Vector.Create(0, 0, -side / 2.0)));

            // 모든 엣지를 반지름 r로 라운딩 → 구
            // 미세하게 줄여 CAD 커널 경계값 수치 오류 방지 (99.9% 반경)
            double filletR = r * 0.999;
            var edgeRounds = new List<KeyValuePair<Edge, EdgeRound>>();
            var round = new FixedRadiusRound(filletR);
            foreach (Edge edge in body.Edges)
                edgeRounds.Add(new KeyValuePair<Edge, EdgeRound>(edge, round));
            body.RoundEdges(edgeRounds);

            return body;
        }

        /// <summary>
        /// 형상 정의로 Void Cut 실행
        /// </summary>
        public static VoidCutResult ExecuteWithShape(
            Part part,
            CutterShape shape,
            double dim1Mm, double dim2Mm, double dim3Mm,
            double pxMm, double pyMm, double pzMm,
            List<DesignBody> targetBodies,
            BooleanMode mode, double offsetMm,
            bool deleteCutter, bool createNS, bool showVolume)
        {
            Body cutterRaw;
            switch (shape)
            {
                case CutterShape.Cylinder:
                    cutterRaw = CreateCylinderBody(dim1Mm, dim2Mm, pxMm, pyMm, pzMm, offsetMm);
                    break;
                case CutterShape.Sphere:
                    cutterRaw = CreateSphereBody(dim1Mm, pxMm, pyMm, pzMm, offsetMm);
                    break;
                default:
                    cutterRaw = CreateCuboidBody(dim1Mm, dim2Mm, dim3Mm, pxMm, pyMm, pzMm, offsetMm);
                    break;
            }

            string shapeName = string.Format("VoidCut_{0}", shape);
            DesignBody cutterDesign = DesignBody.Create(part, shapeName, cutterRaw);

            var cutters = new List<DesignBody> { cutterDesign };
            // 형상 생성 시 오프셋은 이미 치수에 반영됨
            return Execute(part, cutters, targetBodies, mode,
                0, deleteCutter, createNS, showVolume);
        }

        #endregion

        #region Batch Mode

        /// <summary>
        /// CSV 파일에서 커터 정의를 읽어 일괄 실행
        /// CSV 형식: shape,dim1,dim2,dim3,posX,posY,posZ
        /// </summary>
        public static VoidCutResult ExecuteBatch(
            Part part, string csvPath,
            List<DesignBody> targetBodies,
            BooleanMode mode, double offsetMm,
            bool createNS, bool showVolume)
        {
            var result = new VoidCutResult();

            if (!File.Exists(csvPath))
            {
                result.Log.Add(string.Format("[ERROR] 파일을 찾을 수 없습니다: {0}", csvPath));
                return result;
            }

            string[] lines = File.ReadAllLines(csvPath);
            result.Log.Add(string.Format("배치 파일: {0} ({1}행)", csvPath, lines.Length));

            var allCutters = new List<DesignBody>();
            int lineNum = 0;

            foreach (string rawLine in lines)
            {
                lineNum++;
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("shape"))
                    continue;

                string[] parts = line.Split(',');
                if (parts.Length < 7)
                {
                    result.Log.Add(string.Format("[WARN] Line {0}: 컬럼 부족 (필요: 7)", lineNum));
                    continue;
                }

                try
                {
                    string shapeStr = parts[0].Trim().ToLower();
                    var ci = CultureInfo.InvariantCulture;
                    double d1 = double.Parse(parts[1].Trim(), ci);
                    double d2 = double.Parse(parts[2].Trim(), ci);
                    double d3 = double.Parse(parts[3].Trim(), ci);
                    double px = double.Parse(parts[4].Trim(), ci);
                    double py = double.Parse(parts[5].Trim(), ci);
                    double pz = double.Parse(parts[6].Trim(), ci);

                    Body cutterRaw;
                    string name;

                    if (shapeStr == "cylinder" || shapeStr == "cyl")
                    {
                        cutterRaw = CreateCylinderBody(d1, d2, px, py, pz, offsetMm);
                        name = string.Format("Batch_Cyl_{0}", lineNum);
                    }
                    else if (shapeStr == "sphere" || shapeStr == "sph")
                    {
                        cutterRaw = CreateSphereBody(d1, px, py, pz, offsetMm);
                        name = string.Format("Batch_Sph_{0}", lineNum);
                    }
                    else
                    {
                        cutterRaw = CreateCuboidBody(d1, d2, d3, px, py, pz, offsetMm);
                        name = string.Format("Batch_Box_{0}", lineNum);
                    }

                    DesignBody db = DesignBody.Create(part, name, cutterRaw);
                    allCutters.Add(db);
                    result.Log.Add(string.Format("  Line {0}: {1} 생성", lineNum, name));
                }
                catch (Exception ex)
                {
                    result.Log.Add(string.Format("[WARN] Line {0}: {1}", lineNum, ex.Message));
                }
            }

            if (allCutters.Count == 0)
            {
                result.Log.Add("[ERROR] 생성된 커터가 없습니다.");
                return result;
            }

            result.Log.Add(string.Format("\n배치 커터 {0}개 생성 완료. Boolean 실행...\n", allCutters.Count));

            // 배치 커터는 항상 삭제
            var batchResult = Execute(part, allCutters, targetBodies, mode,
                0, true, createNS, showVolume);

            // 배치 결과 합산
            result.CandidateCount += batchResult.CandidateCount;
            result.SubtractedCount += batchResult.SubtractedCount;
            result.SkippedCount += batchResult.SkippedCount;
            result.FailedCount += batchResult.FailedCount;
            result.TotalVolumeBeforeMm3 = batchResult.TotalVolumeBeforeMm3;
            result.TotalVolumeAfterMm3 = batchResult.TotalVolumeAfterMm3;
            result.NamedSelectionsCreated += batchResult.NamedSelectionsCreated;
            result.Log.AddRange(batchResult.Log);

            return result;
        }

        #endregion

        #region Preview

        /// <summary>
        /// AABB 기반 교차 후보 목록 반환 (미리보기용)
        /// </summary>
        public static List<string> GetCandidateInfo(
            Part part,
            List<DesignBody> cutterBodies,
            List<DesignBody> targetBodies)
        {
            var info = new List<string>();
            int totalCandidates = 0;
            var cutterSet = new HashSet<DesignBody>(cutterBodies);

            foreach (var cutter in cutterBodies)
            {
                string cutterName = GetBodyName(cutter);
                var cutterBBox = ComputeAABB(cutter);
                int count = 0;
                var candidateNames = new List<string>();

                foreach (var target in targetBodies)
                {
                    // 자기 자신 스킵
                    if (cutterSet.Contains(target) && target == cutter)
                        continue;

                    if (AABBOverlap(cutterBBox, ComputeAABB(target)))
                    {
                        count++;
                        candidateNames.Add(GetBodyName(target));
                    }
                }

                totalCandidates += count;
                info.Add(string.Format("커터 [{0}]: AABB 교차 {1}개", cutterName, count));
                foreach (var cn in candidateNames)
                    info.Add(string.Format("  -> {0}", cn));
            }

            info.Add("");
            info.Add(string.Format("총 교차 후보: {0}개", totalCandidates));
            return info;
        }

        #endregion

        #region Boolean Operations

        private static void ApplyBoolean(DesignBody target, Body cutterCopy, BooleanMode mode)
        {
            var tools = new Body[] { cutterCopy };
            switch (mode)
            {
                case BooleanMode.Unite:
                    target.Shape.Unite(tools);
                    break;
                case BooleanMode.Intersect:
                    target.Shape.Intersect(tools);
                    break;
                default:
                    target.Shape.Subtract(tools);
                    break;
            }
        }

        #endregion

        #region Helpers

        private static string GetModeName(BooleanMode mode)
        {
            switch (mode)
            {
                case BooleanMode.Unite: return "Unite";
                case BooleanMode.Intersect: return "Intersect";
                default: return "Subtract";
            }
        }

        private static string GetBodyName(DesignBody body)
        {
            return string.IsNullOrEmpty(body.Name) ? body.ToString() : body.Name;
        }

        private static double ComputeTotalVolume(List<DesignBody> bodies)
        {
            double total = 0;
            foreach (var b in bodies)
            {
                try { total += b.Shape.Volume; }
                catch { }
            }
            return total;
        }

        private static void ScaleBody(Body cutterCopy, DesignBody original, double offsetM)
        {
            // 바운딩 박스 기반 균일 스케일 (근사)
            try
            {
                var bb = original.Shape.GetBoundingBox(Matrix.Identity);
                double sizeX = bb.MaxCorner.X - bb.MinCorner.X;
                double sizeY = bb.MaxCorner.Y - bb.MinCorner.Y;
                double sizeZ = bb.MaxCorner.Z - bb.MinCorner.Z;
                double avgSize = (sizeX + sizeY + sizeZ) / 3.0;

                if (avgSize > 1e-10)
                {
                    double scale = 1.0 + (offsetM * 2.0) / avgSize;
                    Point center = Point.Create(
                        (bb.MinCorner.X + bb.MaxCorner.X) / 2.0,
                        (bb.MinCorner.Y + bb.MaxCorner.Y) / 2.0,
                        (bb.MinCorner.Z + bb.MaxCorner.Z) / 2.0);

                    // 중심 기준 스케일
                    cutterCopy.Transform(Matrix.CreateScale(scale, center));
                }
            }
            catch
            {
                // 스케일 실패 시 원본 크기 사용
            }
        }

        private static void CreateNamedSelection(
            Part part, List<DesignBody> modifiedBodies, string modeName,
            VoidCutResult result)
        {
            try
            {
                var docObjects = new List<IDocObject>();
                foreach (var mb in modifiedBodies)
                    docObjects.Add(mb);

                string nsName = string.Format("VoidCut_{0}_{1:yyyyMMdd_HHmmss}",
                    modeName, DateTime.Now);
                Group.Create(part, nsName, docObjects);
                result.NamedSelectionsCreated++;
                result.Log.Add(string.Format("\n[NS] '{0}' 생성됨 ({1}개 바디)",
                    nsName, modifiedBodies.Count));
            }
            catch (Exception ex)
            {
                result.Log.Add(string.Format("[NS] Named Selection 생성 실패: {0}", ex.Message));
            }
        }

        private static void DeleteCutters(
            List<DesignBody> cutterBodies, List<DesignBody> targetBodies,
            VoidCutResult result)
        {
            // 타겟이기도 한 커터는 삭제하면 안 됨
            var targetSet = new HashSet<DesignBody>(targetBodies);
            int deleted = 0;
            int skippedAsTarget = 0;
            foreach (var cutterBody in cutterBodies)
            {
                if (targetSet.Contains(cutterBody))
                {
                    skippedAsTarget++;
                    continue;
                }
                try { cutterBody.Delete(); deleted++; }
                catch (Exception ex)
                {
                    result.Log.Add(string.Format("[WARN] 커터 삭제 실패: {0}", ex.Message));
                }
            }
            if (skippedAsTarget > 0)
                result.Log.Add(string.Format("[INFO] 커터 {0}개 삭제, {1}개는 타겟이므로 보존",
                    deleted, skippedAsTarget));
            else
                result.Log.Add(string.Format("[INFO] 커터 {0}개 삭제 완료", deleted));
        }

        #endregion

        #region AABB

        private struct AABB
        {
            public double MinX, MaxX;
            public double MinY, MaxY;
            public double MinZ, MaxZ;
        }

        private static AABB ComputeAABB(DesignBody body)
        {
            var bb = body.Shape.GetBoundingBox(Matrix.Identity);
            var minC = bb.MinCorner;
            var maxC = bb.MaxCorner;
            return new AABB
            {
                MinX = minC.X, MaxX = maxC.X,
                MinY = minC.Y, MaxY = maxC.Y,
                MinZ = minC.Z, MaxZ = maxC.Z
            };
        }

        private static bool AABBOverlap(AABB a, AABB b)
        {
            if (a.MaxX < b.MinX || b.MaxX < a.MinX) return false;
            if (a.MaxY < b.MinY || b.MaxY < a.MinY) return false;
            if (a.MaxZ < b.MinZ || b.MaxZ < a.MinZ) return false;
            return true;
        }

        #endregion
    }
}
