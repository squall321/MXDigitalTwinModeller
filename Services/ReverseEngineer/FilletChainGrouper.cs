using System;
using System.Collections.Generic;
using System.Linq;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.ReverseEngineer;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// Tangent 로 연결된 fillet face 들을 하나의 chain 으로 묶음.
    ///
    /// 알고리즘 (3-단계):
    ///   1) 같은 snapped R 버킷의 fillet 끼리만 BFS connected component 을 찾는다.
    ///      tangent edge 만 통과 (rel.IsTangent). corner blend (Torus/Sphere) face 도
    ///      fillet 리스트에 포함되어 있으므로 자연스럽게 hop 노드 역할을 한다.
    ///   2) 같은 R 버킷의 다중 component 들이 "다른 R 의 fillet face" 를 다리로
    ///      연결돼 있으면 합친다 (spec 14: 상하 R=1 ring 이 R=8 vertical corner fillet
    ///      들을 통해 토폴로지상 같은 디자인 의도로 묶인다).
    ///   3) 같은 R 버킷의 singleton (1-face component) 들은 멀티 component 가 정확히
    ///      한 개 있을 때 거기 흡수 (spec 61: boss base 코너블렌드가 박스 edge fillet
    ///      과 같은 R=0.5 → 한 chain). 멀티가 없거나 여러 개일 때는 singleton 끼리만
    ///      모아 별도 chain.
    ///
    /// 왜 R-별 버킷팅이 정확한가:
    ///   이전 동작은 R-비율 3x 이하면 인접 fillet 을 같은 chain 에 넣었지만, spec 51
    ///   (R=2/R=3/R=5, 비율 1.5~2.5x) 의 3 종류 design intent 가 한 chain 으로
    ///   뭉쳐버렸다. snapped R 가 정확히 같은 face 끼리만 묶는 게 (1) downstream "이
    ///   chain 의 R 을 한꺼번에 변경" 같은 작업, (2) feature semantics ("이 4 개
    ///   corner R 은 한 feature") 양쪽 모두에 부합한다.
    /// </summary>
    public class FilletChainGrouper
    {
        // chain 안에서 max/min 이 이 값을 넘으면 IsVariableRadius=true.
        // 같은 snapped R 버킷이면 raw R 변동이 미미할 것이지만, snapping 직전의 raw R
        // 변동이 큰 경우 대비.
        private const double VariableRadiusRatio = 1.5;

        /// <summary>
        /// fillets 리스트를 받아 chain 으로 그룹화. fillets[i].ChainId 가 설정됨.
        /// </summary>
        public List<FilletChain> Group(
            List<FilletFeature> fillets,
            Dictionary<int, FaceAdjacency> adjGraph,
            RadiusSnapper.Cluster[] radiusClusters)
        {
            var chains = new List<FilletChain>();
            if (fillets.Count == 0) return chains;

            // face_index → fillet 매핑
            var faceToFillet = fillets.ToDictionary(f => f.FaceIndex);

            // face_index → snapped R 매핑
            var faceToSnappedR = new Dictionary<int, double>();
            for (int i = 0; i < fillets.Count; i++)
            {
                double snapped = fillets[i].SnappedRadiusMm > 0
                    ? fillets[i].SnappedRadiusMm
                    : fillets[i].RadiusMm;
                faceToSnappedR[fillets[i].FaceIndex] = snapped;
            }

            // ---- 1) snapped R 버킷으로 분류 ----
            //
            // 왜 concavity 를 키에 안 넣는가:
            //   Torus / Sphere corner-blend face 의 IsReversed 플래그는 CAD 커널이 face 의
            //   surface-natural normal 방향을 표현하는 토폴로지 비트이지, 직관적인
            //   convex/concave 의미와 1:1 대응하지 않는다. 같은 convex 외부 fillet 에서도
            //   인접한 cylinder face 는 IsReversed=false, torus corner blend 는 IsReversed=true
            //   가 나올 수 있다 (spec 61 boss base 가 그 예: torus 가 Concave 로 잘못 분류돼서
            //   cylinder 들과 다른 버킷에 떨어진다). 같은 R 끼리는 사실상 같은 design intent
            //   이므로 concavity 를 키에서 빼고 R 만으로 묶는다. (concavity 가 정말 다른 fillet
            //   은 거의 항상 R 도 다르므로 R 으로도 자연스럽게 분리된다 — spec 10 의 R=3 만
            //   concave 케이스가 그런 예.)
            // key = "R" rounded, value = list of fillets in that bucket
            //
            // Scale-invariant refactor: 고정 4 자리 (0.0001mm) 대신 adaptive precision.
            //   rDigits = max(2, -log10(medianR) + 3) 형태로, median R 이 아주 클 때 (R=1000)
            //   는 2자리, 아주 작을 때 (R=0.001) 는 6자리. 이렇게 하면 (1) 미터 스케일에서
            //   R=1000 / R=1000.001 같은 spurious split 이 줄고, (2) 마이크로 스케일에서
            //   모든 R 이 0 으로 collapse 되는 일이 없다.
            var rValues = fillets.Select(f => faceToSnappedR[f.FaceIndex]).Where(v => v > 0).ToList();
            double medianR = 0.0;
            if (rValues.Count > 0)
            {
                rValues.Sort();
                medianR = rValues[rValues.Count / 2];
            }
            int rDigits;
            if (medianR > 0)
            {
                int proposed = (int)Math.Ceiling(-Math.Log10(medianR) + 3.0);
                rDigits = Math.Max(2, Math.Min(8, proposed));
            }
            else
            {
                rDigits = 4;
            }
            string rFormat = "F" + rDigits;
            var buckets = new Dictionary<string, List<FilletFeature>>();
            foreach (var f in fillets)
            {
                double r = faceToSnappedR[f.FaceIndex];
                string key = string.Format("{0:" + rFormat + "}", Math.Round(r, rDigits));
                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<FilletFeature>();
                    buckets[key] = list;
                }
                list.Add(f);
            }

            int chainCounter = 1;
            // 모든 fillet face index (어느 버킷이든) — "fillet network" 판정에 사용.
            var allFilletFaces = new HashSet<int>(fillets.Select(f => f.FaceIndex));

            // ---- 2) 각 버킷 안에서 connected component 찾기 + 같은 R 끼리 통합 ----
            foreach (var bucketEntry in buckets)
            {
                var bucketFillets = bucketEntry.Value;
                var bucketFaceSet = new HashSet<int>(bucketFillets.Select(x => x.FaceIndex));

                // 각 버킷 안에서 BFS connected components (tangent + same-bucket only)
                var visited = new HashSet<int>();
                var components = new List<List<int>>();

                foreach (var seed in bucketFillets)
                {
                    if (visited.Contains(seed.FaceIndex)) continue;

                    var comp = new List<int>();
                    var queue = new Queue<int>();
                    queue.Enqueue(seed.FaceIndex);
                    visited.Add(seed.FaceIndex);

                    while (queue.Count > 0)
                    {
                        int currentIdx = queue.Dequeue();
                        comp.Add(currentIdx);

                        if (!adjGraph.TryGetValue(currentIdx, out var adj)) continue;

                        foreach (var neighborIdx in adj.Neighbors)
                        {
                            if (visited.Contains(neighborIdx)) continue;
                            // 같은 버킷 멤버만 hop
                            if (!bucketFaceSet.Contains(neighborIdx)) continue;
                            // tangent 연결만 (perpendicular 는 다른 feature)
                            if (!adj.EdgeRelations.TryGetValue(neighborIdx, out var rel)) continue;
                            if (!rel.IsTangent) continue;

                            visited.Add(neighborIdx);
                            queue.Enqueue(neighborIdx);
                        }
                    }
                    components.Add(comp);
                }

                // 3) Component merging — 두 단계.
                //
                //    (a) 같은 버킷 안에서 component A와 B가 "다른 R 버킷의 fillet face"
                //        를 통해 연결돼 있으면 같은 chain. 예: spec 14 의 top R=1 ring 과
                //        bottom R=1 ring 은 vertical R=8 cylinder fillet 으로 분리돼 있다.
                //        R=8 fillet face 가 양쪽 R=1 component 멤버와 tangent edge 로
                //        붙어 있으면 (실제 그렇다) bridge 로 인정해서 합친다.
                //        반면 spec 66 의 4 × R=5 component 들은 "other"(NURBS) face 로
                //        분리돼 있다 — NURBS 는 fillet 이 아니므로 bridge 아님 → 합치지 않음.
                //
                //    (b) singleton (1-face component) 은 같은 버킷 안의 멀티 component 가
                //        정확히 1 개일 때 그 멀티에 흡수. 멀티가 여러 개거나 없으면
                //        singleton 들끼리 별도 chain.
                var multiComponents = components.Where(c => c.Count > 1).ToList();
                var singletonFaces = components.Where(c => c.Count == 1).Select(c => c[0]).ToList();

                // (a) bridge merge — 다른 R 버킷의 fillet face 를 다리로 같은 R 의
                //     멀티 component 들을 합친다. Union-Find pattern.
                if (multiComponents.Count > 1)
                {
                    // face → component-index 맵
                    var faceToCompIdx = new Dictionary<int, int>();
                    for (int ci = 0; ci < multiComponents.Count; ci++)
                        foreach (var fIdx in multiComponents[ci])
                            faceToCompIdx[fIdx] = ci;

                    // Union-Find roots
                    int[] uf = new int[multiComponents.Count];
                    for (int i = 0; i < uf.Length; i++) uf[i] = i;
                    Func<int, int> Find = null;
                    Find = x => { while (uf[x] != x) { uf[x] = uf[uf[x]]; x = uf[x]; } return x; };
                    Action<int, int> Union = (a, b) => { int ra = Find(a); int rb = Find(b); if (ra != rb) uf[ra] = rb; };

                    // 모든 "다른 R 버킷의 fillet face" 를 순회. 그 face 의 이웃 중
                    // bucketFaceSet 멤버가 2 개 이상이면 그 멤버들이 속한 component 를 union.
                    foreach (var bridgeFace in allFilletFaces)
                    {
                        if (bucketFaceSet.Contains(bridgeFace)) continue; // 같은 R 이면 bridge 아님
                        if (!adjGraph.TryGetValue(bridgeFace, out var bridgeAdj)) continue;

                        int? firstCompIdx = null;
                        foreach (var nb in bridgeAdj.Neighbors)
                        {
                            if (!faceToCompIdx.TryGetValue(nb, out int compIdx)) continue;
                            // bridgeFace → nb 가 tangent edge 여야 의미 있는 다리.
                            if (!bridgeAdj.EdgeRelations.TryGetValue(nb, out var rel)) continue;
                            if (!rel.IsTangent) continue;

                            if (firstCompIdx == null) firstCompIdx = compIdx;
                            else if (firstCompIdx.Value != compIdx) Union(firstCompIdx.Value, compIdx);
                        }
                    }

                    // root 별로 component 재구성
                    var merged = new Dictionary<int, List<int>>();
                    for (int ci = 0; ci < multiComponents.Count; ci++)
                    {
                        int root = Find(ci);
                        if (!merged.TryGetValue(root, out var list))
                        {
                            list = new List<int>();
                            merged[root] = list;
                        }
                        list.AddRange(multiComponents[ci]);
                    }
                    multiComponents = merged.Values.ToList();
                }

                // (b) singleton 흡수
                if (singletonFaces.Count > 0 && multiComponents.Count == 1)
                {
                    multiComponents[0].AddRange(singletonFaces);
                    singletonFaces.Clear();
                }

                // 멀티-component 각각 → 별개 chain
                foreach (var comp in multiComponents)
                {
                    var chain = BuildChain(chainCounter, comp, faceToFillet, faceToSnappedR);
                    chains.Add(chain);
                    chainCounter++;
                }

                // 남은 singleton 들 → 하나의 chain (멀티 없거나 멀티가 여러 개일 때)
                if (singletonFaces.Count > 0)
                {
                    var chain = BuildChain(chainCounter, singletonFaces, faceToFillet, faceToSnappedR);
                    chains.Add(chain);
                    chainCounter++;
                }
            }

            return chains;
        }

        // 한 face index 리스트로부터 FilletChain 객체를 만든다.
        // fillets[i].ChainId 도 함께 설정.
        private FilletChain BuildChain(
            int chainNumber,
            List<int> faceIndices,
            Dictionary<int, FilletFeature> faceToFillet,
            Dictionary<int, double> faceToSnappedR)
        {
            var chain = new FilletChain
            {
                Id = "FC" + chainNumber,
                RadiusMinMm = double.PositiveInfinity,
                RadiusMaxMm = double.NegativeInfinity,
            };

            var rHistogram = new Dictionary<double, int>();

            foreach (int fIdx in faceIndices)
            {
                chain.FaceIndices.Add(fIdx);
                if (!faceToFillet.TryGetValue(fIdx, out var f)) continue;

                double rs = faceToSnappedR.TryGetValue(fIdx, out var sR) ? sR : f.RadiusMm;
                if (rs > 0)
                {
                    chain.RadiusMinMm = Math.Min(chain.RadiusMinMm, rs);
                    chain.RadiusMaxMm = Math.Max(chain.RadiusMaxMm, rs);
                    if (rHistogram.ContainsKey(rs)) rHistogram[rs]++;
                    else rHistogram[rs] = 1;
                }

                if (f.BlendType == "edge") chain.EdgeFilletCount++;
                else if (f.BlendType == "corner") chain.CornerBlendCount++;

                f.ChainId = chain.Id;
            }

            // 대표 R: mode (가장 흔한 snapped R). 같은 버킷이라 사실상 단일값.
            if (rHistogram.Count > 0)
            {
                int bestCount = -1;
                double bestR = 0;
                foreach (var kv in rHistogram)
                {
                    if (kv.Value > bestCount || (kv.Value == bestCount && kv.Key > bestR))
                    {
                        bestCount = kv.Value;
                        bestR = kv.Key;
                    }
                }
                chain.RadiusMm = bestR;
            }

            if (double.IsPositiveInfinity(chain.RadiusMinMm)) chain.RadiusMinMm = chain.RadiusMm;
            if (double.IsNegativeInfinity(chain.RadiusMaxMm)) chain.RadiusMaxMm = chain.RadiusMm;

            // 1e-6: WARNING — kernel epsilon (mathematical-zero guard against division by
            // zero in the ratio test). Not a feature threshold; do not refactor to ratio.
            if (chain.RadiusMinMm > 1e-6 && chain.RadiusMaxMm / chain.RadiusMinMm > VariableRadiusRatio)
                chain.IsVariableRadius = true;

            return chain;
        }
    }
}
