using System;
using System.Collections.Generic;
using System.Linq;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.ReverseEngineer
{
    /// <summary>
    /// R 값 정규화: 미시 노이즈 + boolean cleanup 결과 같은 작은 변동을 제거.
    /// 두 단계:
    ///   1) Cluster — 비슷한 R 끼리 묶음 (절대 ε + 상대 5%)
    ///   2) Snap — 각 cluster 의 mean 을 nominal set 중 가장 가까운 값으로 (옵션)
    /// </summary>
    public class RadiusSnapper
    {
        /// <summary>
        /// Scale-invariant nominal R 후보 (mantissa 시퀀스).
        ///
        /// 사용 방법: cluster mean 으로부터 decade anchor 를 만들고 (10^round(log10(mean)))
        /// 그 decade 안의 nominal 값과만 비교한다. 이렇게 하면 nominal 후보가
        /// "현재 cluster 의 자릿수" 에 맞춰져 desktop CAD (R≈1~10mm), MEMS (R≈0.001~0.01mm),
        /// aerospace fixture (R≈10~100mm) 모두에서 자연스러운 snap 후보를 제공한다.
        /// 기존 DefaultNominalsMm 후보 {0.1..20.0} 은 desktop CAD 만 커버해서 다른 스케일에서
        /// 모두 잘못 snap 되었다.
        /// </summary>
        private static readonly double[] NominalMantissas = new[]
        {
            1.0, 1.25, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 8.0
        };

        /// <summary>
        /// Cluster 묶기 절대 tolerance: cluster mean 의 비율 (scale-invariant) with floor.
        /// effectiveTol = max(AbsoluteToleranceFloorMm, AbsoluteToleranceRatio × mean).
        /// 기본 0.005 = 0.5% — desktop CAD 에서 mean R=10mm 면 0.05mm (legacy AbsoluteToleranceMm).
        /// Floor 0.05mm: kernel/machining precision; prevents pathologically tight
        /// clustering at very small mean R (e.g. R=1mm → ratio gives 0.005mm, but
        /// boolean-cleanup noise can be ~10µm).
        /// </summary>
        public double AbsoluteToleranceRatio { get; set; } = 0.005;
        public double AbsoluteToleranceFloorMm { get; set; } = 0.05;
        private const double RelativeTolerance = 0.05;     // ±5% 안은 같은 cluster (already ratio)

        /// <summary>
        /// Nominal snap tolerance: cluster mean 의 비율.
        /// 기본 0.015 = 1.5% — mean=10mm 면 0.15mm (legacy NominalSnapToleranceMm).
        /// </summary>
        public double NominalSnapToleranceRatio { get; set; } = 0.015;

        /// <summary>
        /// Tight cluster 판정: stddev/mean (coefficient of variation) 이 이 값보다 작으면
        /// designer 의 의도된 non-nominal 값 → snap 안 함. 2% CV 가 기본.
        /// </summary>
        public double TightClusterRelStddev { get; set; } = 0.02;

        /// <summary>
        /// 한 cluster 의 결과.
        /// </summary>
        public class Cluster
        {
            public List<double> RawValues { get; set; }
            public double MeanMm { get; set; }
            public double MinMm { get; set; }
            public double MaxMm { get; set; }
            public double SnappedMm { get; set; }  // nominal 로 snap 된 값
            public List<int> MemberIndices { get; set; }  // 원래 입력 배열 내 인덱스

            public Cluster()
            {
                RawValues = new List<double>();
                MemberIndices = new List<int>();
            }
        }

        /// <summary>
        /// R 값 목록을 cluster 로 묶음 (Stage 1 보강의 핵심).
        /// </summary>
        /// <param name="radiiMm">입력 R 값 (mm)</param>
        /// <param name="snapToNominal">true 면 nominal R 후보 중 가까운 값으로 snap</param>
        public List<Cluster> ClusterRadii(IList<double> radiiMm, bool snapToNominal = true)
        {
            var clusters = new List<Cluster>();
            if (radiiMm == null || radiiMm.Count == 0) return clusters;

            // 안정성 위해 정렬된 인덱스
            var sortedIndices = Enumerable.Range(0, radiiMm.Count)
                .OrderBy(i => radiiMm[i])
                .ToList();

            foreach (int idx in sortedIndices)
            {
                double r = radiiMm[idx];
                if (r <= 0) continue;  // 0 또는 음수는 무시

                // Audit fix: "첫 매치로 break" 는 cluster 순서 의존적이어서
                //   두 cluster 모두 tolerance 안에 들면 가장 가까운 쪽이 아닌
                //   먼저 만들어진 쪽에 흡수되었다. 가장 가까운 cluster 를 선택.
                //
                // Scale-invariant: absolute tolerance = ratio × cluster mean.
                Cluster found = null;
                double bestClusterDiff = double.MaxValue;
                foreach (var c in clusters)
                {
                    double diff = Math.Abs(c.MeanMm - r);
                    double relDiff = diff / Math.Max(c.MeanMm, r);
                    double absToleranceMm = Math.Max(
                        AbsoluteToleranceFloorMm,
                        AbsoluteToleranceRatio * Math.Max(c.MeanMm, r));
                    if (diff <= absToleranceMm || relDiff <= RelativeTolerance)
                    {
                        if (diff < bestClusterDiff)
                        {
                            bestClusterDiff = diff;
                            found = c;
                        }
                    }
                }

                if (found == null)
                {
                    found = new Cluster
                    {
                        MeanMm = r,
                        MinMm = r,
                        MaxMm = r,
                    };
                    clusters.Add(found);
                }

                found.RawValues.Add(r);
                found.MemberIndices.Add(idx);
                found.MinMm = Math.Min(found.MinMm, r);
                found.MaxMm = Math.Max(found.MaxMm, r);
                found.MeanMm = found.RawValues.Average();
            }

            // snap to nominal (옵션)
            //
            // Scale-invariant: nominal 후보를 cluster 의 decade anchor 에서 동적으로 생성.
            //   anchor = 10^round(log10(meanMm))   — cluster mean 이 들어가는 decade.
            //   candidates = NominalMantissas × anchor (그리고 인접 decade 도 약간 보강).
            //   tolerance = NominalSnapToleranceRatio × meanMm  (mean 의 비율).
            //   tight cluster = stddev/mean < TightClusterRelStddev  (coefficient of variation).
            foreach (var c in clusters)
            {
                if (snapToNominal)
                {
                    double bestNominal = c.MeanMm;
                    double bestDiff = double.MaxValue;
                    double anchor = c.MeanMm > 0 ? Math.Pow(10.0, Math.Round(Math.Log10(c.MeanMm))) : 1.0;
                    // 인접 decade (×0.1, ×1, ×10) 까지 후보 확장: cluster mean 이 decade 경계
                    // 근처일 때 (예: 9.5mm) 잘못된 decade 만 후보로 잡히는 일을 방지.
                    double[] decadeFactors = new[] { 0.1, 1.0, 10.0 };
                    foreach (var df in decadeFactors)
                    {
                        foreach (var m in NominalMantissas)
                        {
                            double nom = m * anchor * df;
                            double d = Math.Abs(c.MeanMm - nom);
                            if (d < bestDiff)
                            {
                                bestDiff = d;
                                bestNominal = nom;
                            }
                        }
                    }
                    // Audit fix: 다수의 raw 값이 좁은 spread 로 같은 cluster 에 모였다면
                    //   그건 designer 의 의도된 non-nominal 값이다. tightly-clustered
                    //   결과를 silent 하게 nominal 로 덮어쓰지 않는다.
                    double stddev = 0.0;
                    if (c.RawValues.Count > 1)
                    {
                        double mean = c.MeanMm;
                        double sum2 = 0.0;
                        foreach (var v in c.RawValues) sum2 += (v - mean) * (v - mean);
                        stddev = Math.Sqrt(sum2 / c.RawValues.Count);
                    }
                    double cv = c.MeanMm > 0 ? stddev / c.MeanMm : 0.0;
                    bool tightCluster = c.RawValues.Count >= 3 && cv < TightClusterRelStddev;
                    double snapToleranceMm = NominalSnapToleranceRatio * c.MeanMm;
                    c.SnappedMm = (bestDiff <= snapToleranceMm && !tightCluster)
                        ? bestNominal
                        : c.MeanMm;
                }
                else
                {
                    c.SnappedMm = c.MeanMm;
                }
            }

            return clusters;
        }
    }
}
