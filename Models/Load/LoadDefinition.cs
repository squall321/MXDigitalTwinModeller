using System;
using System.Collections.Generic;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Load
{
    public enum LoadInputMode
    {
        Expression,
        Tabular,
        Pwm
    }

    /// <summary>
    /// PWM 소스 주파수 성분 하나를 정의
    /// </summary>
    public class PwmHarmonic
    {
        public double Frequency { get; set; }   // Hz
        public double Amplitude { get; set; }   // 진폭
        public double Phase { get; set; }       // 위상 (degree)

        public PwmHarmonic() { }
        public PwmHarmonic(double freq, double amp, double phase)
        {
            Frequency = freq;
            Amplitude = amp;
            Phase = phase;
        }

        public PwmHarmonic Clone()
        {
            return new PwmHarmonic(Frequency, Amplitude, Phase);
        }
    }

    public class LoadDefinition
    {
        public string Name { get; set; }
        public string GroupName { get; set; }
        public double EndTime { get; set; }
        public double DeltaTime { get; set; }
        public LoadInputMode InputMode { get; set; }

        // Expression mode
        public string Expression { get; set; }

        // Tabular mode
        public double[] TimeValues { get; set; }
        public double[] AmplitudeValues { get; set; }

        // PWM mode
        public double PwmCarrierFrequency { get; set; }    // 캐리어 주파수 (Hz)
        public double PwmOutputAmplitude { get; set; }      // 출력 진폭
        public bool PwmBipolar { get; set; }                // true=양극성(+/-), false=단극성(0/+)
        public List<PwmHarmonic> PwmHarmonics { get; set; } // 소스 주파수 성분 리스트
        public double PwmTargetFrequency { get; set; }     // 최적화 타겟 주파수 (Hz)

        // Computed results (cache)
        public double[] ComputedTime { get; set; }
        public double[] ComputedAmplitude { get; set; }     // PWM: 사각파 출력
        public double[] ComputedTarget { get; set; }        // PWM: 타겟 사인파 합성
        public double[] FftFrequency { get; set; }
        public double[] FftMagnitude { get; set; }

        public LoadDefinition()
        {
            PwmHarmonics = new List<PwmHarmonic>();
            PwmCarrierFrequency = 10000;
            PwmOutputAmplitude = 1000;
            PwmBipolar = true;
            PwmTargetFrequency = 50;
        }

        public LoadDefinition Clone()
        {
            var clone = new LoadDefinition
            {
                Name = Name,
                GroupName = GroupName,
                EndTime = EndTime,
                DeltaTime = DeltaTime,
                InputMode = InputMode,
                Expression = Expression,
                PwmCarrierFrequency = PwmCarrierFrequency,
                PwmOutputAmplitude = PwmOutputAmplitude,
                PwmBipolar = PwmBipolar,
                PwmTargetFrequency = PwmTargetFrequency
            };

            if (PwmHarmonics != null)
            {
                clone.PwmHarmonics = new List<PwmHarmonic>();
                foreach (var h in PwmHarmonics)
                    clone.PwmHarmonics.Add(h.Clone());
            }

            if (TimeValues != null)
                clone.TimeValues = (double[])TimeValues.Clone();
            if (AmplitudeValues != null)
                clone.AmplitudeValues = (double[])AmplitudeValues.Clone();
            if (ComputedTime != null)
                clone.ComputedTime = (double[])ComputedTime.Clone();
            if (ComputedAmplitude != null)
                clone.ComputedAmplitude = (double[])ComputedAmplitude.Clone();
            if (ComputedTarget != null)
                clone.ComputedTarget = (double[])ComputedTarget.Clone();
            if (FftFrequency != null)
                clone.FftFrequency = (double[])FftFrequency.Clone();
            if (FftMagnitude != null)
                clone.FftMagnitude = (double[])FftMagnitude.Clone();

            return clone;
        }
    }
}
