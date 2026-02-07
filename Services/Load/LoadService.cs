using System;
using System.Collections.Generic;
using System.Globalization;
using SpaceClaim.Api.V252.MXDigitalTwinModeller.Models.Load;

namespace SpaceClaim.Api.V252.MXDigitalTwinModeller.Services.Load
{
    public static class LoadService
    {
        // ─── Static cache ───
        private static readonly List<LoadDefinition> _loads = new List<LoadDefinition>();

        public static int Count { get { return _loads.Count; } }
        public static LoadDefinition Get(int index) { return _loads[index]; }

        public static void Add(LoadDefinition ld) { _loads.Add(ld); }

        public static void Update(int index, LoadDefinition ld)
        {
            if (index >= 0 && index < _loads.Count)
                _loads[index] = ld;
        }

        public static void RemoveAt(int index)
        {
            if (index >= 0 && index < _loads.Count)
                _loads.RemoveAt(index);
        }

        public static List<LoadDefinition> GetAll()
        {
            return new List<LoadDefinition>(_loads);
        }

        // ─── Time series generation ───

        public static void GenerateTimeSeries(LoadDefinition ld)
        {
            if (ld.InputMode == LoadInputMode.Expression)
            {
                if (ld.DeltaTime <= 0 || ld.EndTime <= 0)
                    throw new ArgumentException("EndTime and dt must be positive.");

                int n = (int)(ld.EndTime / ld.DeltaTime) + 1;
                if (n > 1000000) n = 1000000; // safety cap
                ld.ComputedTime = new double[n];
                ld.ComputedAmplitude = new double[n];
                ld.ComputedTarget = null;

                for (int i = 0; i < n; i++)
                {
                    double t = i * ld.DeltaTime;
                    ld.ComputedTime[i] = t;
                    ld.ComputedAmplitude[i] = EvaluateExpression(ld.Expression, t);
                }
            }
            else if (ld.InputMode == LoadInputMode.Tabular)
            {
                if (ld.TimeValues == null || ld.AmplitudeValues == null)
                    throw new ArgumentException("Tabular data is empty.");

                int n = Math.Min(ld.TimeValues.Length, ld.AmplitudeValues.Length);
                ld.ComputedTime = new double[n];
                ld.ComputedAmplitude = new double[n];
                ld.ComputedTarget = null;
                Array.Copy(ld.TimeValues, ld.ComputedTime, n);
                Array.Copy(ld.AmplitudeValues, ld.ComputedAmplitude, n);

                if (n >= 2)
                    ld.DeltaTime = ld.ComputedTime[1] - ld.ComputedTime[0];
                ld.EndTime = ld.ComputedTime[n - 1];
            }
            else if (ld.InputMode == LoadInputMode.Pwm)
            {
                GeneratePwm(ld);
            }
        }

        // ─── PWM Generation ───

        private static void GeneratePwm(LoadDefinition ld)
        {
            if (ld.DeltaTime <= 0 || ld.EndTime <= 0)
                throw new ArgumentException("EndTime and dt must be positive.");
            if (ld.PwmHarmonics == null || ld.PwmHarmonics.Count == 0)
                throw new ArgumentException("At least one harmonic is required.");
            if (ld.PwmCarrierFrequency <= 0)
                throw new ArgumentException("Carrier frequency must be positive.");

            int n = (int)(ld.EndTime / ld.DeltaTime) + 1;
            if (n > 1000000) n = 1000000;

            ld.ComputedTime = new double[n];
            ld.ComputedTarget = new double[n];
            ld.ComputedAmplitude = new double[n];

            double outputAmp = ld.PwmOutputAmplitude;
            double carrierPeriod = 1.0 / ld.PwmCarrierFrequency;

            // Compute max target amplitude for normalization
            double maxTargetAmp = 0;
            foreach (var h in ld.PwmHarmonics)
                maxTargetAmp += Math.Abs(h.Amplitude);
            if (maxTargetAmp == 0) maxTargetAmp = 1;

            for (int i = 0; i < n; i++)
            {
                double t = i * ld.DeltaTime;
                ld.ComputedTime[i] = t;

                // 1) Target signal: sum of sine waves
                double target = 0;
                foreach (var h in ld.PwmHarmonics)
                {
                    double phaseRad = h.Phase * Math.PI / 180.0;
                    target += h.Amplitude * Math.Sin(2.0 * Math.PI * h.Frequency * t + phaseRad);
                }
                ld.ComputedTarget[i] = target;

                // 2) Triangle carrier: range [-maxTargetAmp, +maxTargetAmp]
                double carrierPhase = (t % carrierPeriod) / carrierPeriod; // 0~1
                double carrier;
                if (carrierPhase < 0.5)
                    carrier = -maxTargetAmp + 4.0 * maxTargetAmp * carrierPhase;
                else
                    carrier = 3.0 * maxTargetAmp - 4.0 * maxTargetAmp * carrierPhase;

                // 3) PWM comparison
                if (ld.PwmBipolar)
                {
                    ld.ComputedAmplitude[i] = target >= carrier ? outputAmp : -outputAmp;
                }
                else
                {
                    ld.ComputedAmplitude[i] = target >= carrier ? outputAmp : 0;
                }
            }
        }

        // ─── PWM Optimization ───

        /// <summary>
        /// 타겟 주파수의 순수 사인파를 가장 잘 모사하는 소스 하모닉 조합을 탐색.
        /// nHarmonics개의 주파수를 선정하여 PWM 출력과 이상적 사인파의 RMS 오차를 최소화.
        /// </summary>
        public static void OptimizePwmHarmonics(LoadDefinition ld, int nHarmonics)
        {
            if (nHarmonics < 1 || ld.PwmTargetFrequency <= 0 || ld.DeltaTime <= 0 || ld.EndTime <= 0)
                return;

            double targetFreq = ld.PwmTargetFrequency;
            double outputAmp = ld.PwmOutputAmplitude;
            double dt = ld.DeltaTime;

            int n = (int)(ld.EndTime / dt) + 1;
            if (n > 500000) n = 500000; // optimization speed cap

            // Ideal output: pure sine at target frequency
            double[] ideal = new double[n];
            for (int i = 0; i < n; i++)
                ideal[i] = outputAmp * Math.Sin(2.0 * Math.PI * targetFreq * i * dt);

            // FFT setup
            int fftN = 1;
            while (fftN < n) fftN <<= 1;
            double freqRes = 1.0 / (fftN * dt);
            int half = fftN / 2;

            // Track used frequency bins (± tolerance)
            int fundBin = (int)Math.Round(targetFreq / freqRes);
            var usedBins = new HashSet<int>();
            for (int b = Math.Max(0, fundBin - 3); b <= Math.Min(half - 1, fundBin + 3); b++)
                usedBins.Add(b);

            // Phase 1: Start with fundamental
            ld.PwmHarmonics = new List<PwmHarmonic>();
            ld.PwmHarmonics.Add(new PwmHarmonic(targetFreq, outputAmp * 0.85, 0));
            GeneratePwm(ld);

            // Phase 2: Iteratively add harmonics to cancel dominant error components
            for (int h = 1; h < nHarmonics; h++)
            {
                // Error = PWM output - ideal
                double[] errRe = new double[fftN];
                int len = Math.Min(n, ld.ComputedAmplitude.Length);
                for (int i = 0; i < len; i++)
                    errRe[i] = ld.ComputedAmplitude[i] - ideal[i];
                double[] errIm = new double[fftN];
                FFT(errRe, errIm, false);

                // Find largest error peak (skip used bins)
                double bestMagSq = 0;
                int bestK = -1;
                for (int k = 1; k < half; k++)
                {
                    if (usedBins.Contains(k)) continue;
                    double magSq = errRe[k] * errRe[k] + errIm[k] * errIm[k];
                    if (magSq > bestMagSq) { bestMagSq = magSq; bestK = k; }
                }
                if (bestK < 0) break;

                // Mark ±3 bins as used
                for (int b = Math.Max(0, bestK - 3); b <= Math.Min(half - 1, bestK + 3); b++)
                    usedBins.Add(b);

                double peakFreq = bestK * freqRes;
                double errAmp = Math.Sqrt(bestMagSq) * 2.0 / fftN;

                // Cancellation phase: for sin representation, φ = atan2(re, -im)
                // Opposite phase = φ + 180°
                double cancelPhaseDeg = Math.Atan2(errRe[bestK], -errIm[bestK]) * 180.0 / Math.PI + 180.0;

                // Grid search: find best amplitude for this harmonic
                double baseErr = RmsError(ld.ComputedAmplitude, ideal, n);
                double bestAmp = errAmp * 0.3;
                double bestNewErr = baseErr;

                for (int ai = 1; ai <= 15; ai++)
                {
                    double tryAmp = errAmp * ai * 0.1; // 0.1x ~ 1.5x
                    ld.PwmHarmonics.Add(new PwmHarmonic(peakFreq, tryAmp, cancelPhaseDeg));
                    GeneratePwm(ld);
                    double err = RmsError(ld.ComputedAmplitude, ideal, n);
                    if (err < bestNewErr) { bestNewErr = err; bestAmp = tryAmp; }
                    ld.PwmHarmonics.RemoveAt(ld.PwmHarmonics.Count - 1);
                }

                ld.PwmHarmonics.Add(new PwmHarmonic(peakFreq, bestAmp, cancelPhaseDeg));
                GeneratePwm(ld);
            }

            // Phase 3: Fine-tune fundamental amplitude
            {
                double bestErr = RmsError(ld.ComputedAmplitude, ideal, n);
                double bestAmp = ld.PwmHarmonics[0].Amplitude;
                for (int s = 0; s < 20; s++)
                {
                    double tryAmp = outputAmp * (0.4 + s * 0.06);
                    ld.PwmHarmonics[0].Amplitude = tryAmp;
                    GeneratePwm(ld);
                    double err = RmsError(ld.ComputedAmplitude, ideal, n);
                    if (err < bestErr) { bestErr = err; bestAmp = tryAmp; }
                }
                ld.PwmHarmonics[0].Amplitude = bestAmp;
            }

            // Phase 4: Coordinate descent on all harmonics (2 rounds)
            for (int round = 0; round < 2; round++)
            {
                for (int hi = 0; hi < ld.PwmHarmonics.Count; hi++)
                {
                    var harm = ld.PwmHarmonics[hi];
                    double origAmp = harm.Amplitude;
                    double origPhase = harm.Phase;
                    double bestErr = RmsError(ld.ComputedAmplitude, ideal, n);

                    // Tune amplitude (±30% in 7 steps)
                    double bestA = origAmp;
                    for (int s = -3; s <= 3; s++)
                    {
                        if (s == 0) continue;
                        double tryA = origAmp * (1.0 + s * 0.1);
                        if (tryA <= 0) continue;
                        harm.Amplitude = tryA;
                        GeneratePwm(ld);
                        double err = RmsError(ld.ComputedAmplitude, ideal, n);
                        if (err < bestErr) { bestErr = err; bestA = tryA; }
                    }
                    harm.Amplitude = bestA;

                    // Tune phase (±30° in 6 steps)
                    double bestP = harm.Phase;
                    for (int s = -3; s <= 3; s++)
                    {
                        if (s == 0) continue;
                        harm.Phase = bestP + s * 10.0;
                        GeneratePwm(ld);
                        double err = RmsError(ld.ComputedAmplitude, ideal, n);
                        if (err < bestErr) { bestErr = err; harm.Phase = bestP + s * 10.0; bestP = harm.Phase; }
                    }
                    harm.Phase = bestP;
                    GeneratePwm(ld);
                }
            }

            // Final generation
            GeneratePwm(ld);
            ComputeFFT(ld);
        }

        private static double RmsError(double[] actual, double[] ideal, int n)
        {
            double sum = 0;
            int len = Math.Min(Math.Min(actual.Length, ideal.Length), n);
            for (int i = 0; i < len; i++)
            {
                double d = actual[i] - ideal[i];
                sum += d * d;
            }
            return Math.Sqrt(sum / len);
        }

        // ─── FFT (Cooley-Tukey Radix-2) ───

        public static void ComputeFFT(LoadDefinition ld)
        {
            if (ld.ComputedAmplitude == null || ld.ComputedAmplitude.Length < 2)
                return;

            int dataLen = ld.ComputedAmplitude.Length;

            // Zero-pad to next power of 2
            int n = 1;
            while (n < dataLen) n <<= 1;

            double[] re = new double[n];
            double[] im = new double[n];
            Array.Copy(ld.ComputedAmplitude, re, dataLen);

            FFT(re, im, false);

            // Compute magnitude spectrum (positive frequencies only)
            int half = n / 2;
            ld.FftMagnitude = new double[half];
            ld.FftFrequency = new double[half];

            double dt = ld.DeltaTime > 0 ? ld.DeltaTime : 1.0;
            double freqRes = 1.0 / (n * dt);

            for (int k = 0; k < half; k++)
            {
                ld.FftFrequency[k] = k * freqRes;
                ld.FftMagnitude[k] = Math.Sqrt(re[k] * re[k] + im[k] * im[k]) * 2.0 / n;
            }
            // DC component
            if (half > 0)
                ld.FftMagnitude[0] /= 2.0;
        }

        private static void FFT(double[] re, double[] im, bool inverse)
        {
            int n = re.Length;
            if (n <= 1) return;

            // Bit-reversal permutation
            int bits = 0;
            int temp = n;
            while (temp > 1) { bits++; temp >>= 1; }

            for (int i = 0; i < n; i++)
            {
                int j = BitReverse(i, bits);
                if (j > i)
                {
                    double tr = re[i]; re[i] = re[j]; re[j] = tr;
                    double ti = im[i]; im[i] = im[j]; im[j] = ti;
                }
            }

            // Cooley-Tukey iterative FFT
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = 2.0 * Math.PI / len * (inverse ? -1.0 : 1.0);
                double wRe = Math.Cos(angle);
                double wIm = Math.Sin(angle);

                for (int i = 0; i < n; i += len)
                {
                    double curRe = 1.0, curIm = 0.0;
                    int halfLen = len / 2;

                    for (int j = 0; j < halfLen; j++)
                    {
                        int u = i + j;
                        int v = i + j + halfLen;

                        double tRe = curRe * re[v] - curIm * im[v];
                        double tIm = curRe * im[v] + curIm * re[v];

                        re[v] = re[u] - tRe;
                        im[v] = im[u] - tIm;
                        re[u] += tRe;
                        im[u] += tIm;

                        double newCurRe = curRe * wRe - curIm * wIm;
                        curIm = curRe * wIm + curIm * wRe;
                        curRe = newCurRe;
                    }
                }
            }

            if (inverse)
            {
                for (int i = 0; i < n; i++)
                {
                    re[i] /= n;
                    im[i] /= n;
                }
            }
        }

        private static int BitReverse(int x, int bits)
        {
            int result = 0;
            for (int i = 0; i < bits; i++)
            {
                result = (result << 1) | (x & 1);
                x >>= 1;
            }
            return result;
        }

        // ─── Expression Evaluator (Recursive Descent) ───

        public static double EvaluateExpression(string expr, double t)
        {
            var parser = new ExprParser(expr, t);
            double result = parser.ParseExpression();
            return result;
        }

        /// <summary>
        /// Recursive descent parser for math expressions.
        /// Supports: +, -, *, /, ^
        /// Functions: sin, cos, tan, exp, log, sqrt, abs, asin, acos, atan, ceil, floor
        /// Waveforms: square(x[,duty]), pulse(t,start,width), step(t[,t0]), saw(x), tri(x)
        /// Multi-arg: min(a,b), max(a,b)
        /// Constants: pi, e  Variable: t
        /// </summary>
        private class ExprParser
        {
            private readonly string _expr;
            private readonly double _t;
            private int _pos;

            public ExprParser(string expr, double t)
            {
                _expr = expr != null ? expr.Trim() : "";
                _t = t;
                _pos = 0;
            }

            public double ParseExpression()
            {
                double result = ParseTerm();
                while (_pos < _expr.Length)
                {
                    char c = _expr[_pos];
                    if (c == '+') { _pos++; result += ParseTerm(); }
                    else if (c == '-') { _pos++; result -= ParseTerm(); }
                    else break;
                }
                return result;
            }

            private double ParseTerm()
            {
                double result = ParsePower();
                while (_pos < _expr.Length)
                {
                    char c = _expr[_pos];
                    if (c == '*') { _pos++; result *= ParsePower(); }
                    else if (c == '/') { _pos++; double d = ParsePower(); result /= d; }
                    else break;
                }
                return result;
            }

            private double ParsePower()
            {
                double result = ParseUnary();
                while (_pos < _expr.Length && _expr[_pos] == '^')
                {
                    _pos++;
                    double exp = ParseUnary();
                    result = Math.Pow(result, exp);
                }
                return result;
            }

            private double ParseUnary()
            {
                SkipSpaces();
                if (_pos < _expr.Length && _expr[_pos] == '-')
                {
                    _pos++;
                    return -ParseAtom();
                }
                if (_pos < _expr.Length && _expr[_pos] == '+')
                {
                    _pos++;
                }
                return ParseAtom();
            }

            private double ParseAtom()
            {
                SkipSpaces();
                if (_pos >= _expr.Length)
                    return 0;

                // Parentheses
                if (_expr[_pos] == '(')
                {
                    _pos++;
                    double result = ParseExpression();
                    SkipSpaces();
                    if (_pos < _expr.Length && _expr[_pos] == ')')
                        _pos++;
                    return result;
                }

                // Number
                if (char.IsDigit(_expr[_pos]) || _expr[_pos] == '.')
                {
                    return ParseNumber();
                }

                // Identifier (function or constant)
                if (char.IsLetter(_expr[_pos]))
                {
                    string name = ParseIdentifier();
                    SkipSpaces();

                    // Check for function call (supports multi-argument)
                    if (_pos < _expr.Length && _expr[_pos] == '(')
                    {
                        _pos++;
                        var args = new List<double>();
                        args.Add(ParseExpression());
                        SkipSpaces();
                        while (_pos < _expr.Length && _expr[_pos] == ',')
                        {
                            _pos++;
                            args.Add(ParseExpression());
                            SkipSpaces();
                        }
                        if (_pos < _expr.Length && _expr[_pos] == ')')
                            _pos++;
                        return CallFunction(name, args);
                    }

                    // Constants and variable
                    switch (name.ToLowerInvariant())
                    {
                        case "t": return _t;
                        case "pi": return Math.PI;
                        case "e": return Math.E;
                        default:
                            throw new FormatException("Unknown identifier: " + name);
                    }
                }

                return 0;
            }

            private double ParseNumber()
            {
                int start = _pos;
                while (_pos < _expr.Length && (char.IsDigit(_expr[_pos]) || _expr[_pos] == '.'))
                    _pos++;

                // Scientific notation (e.g., 1e-3, 2.5E+6)
                if (_pos < _expr.Length && (_expr[_pos] == 'e' || _expr[_pos] == 'E'))
                {
                    _pos++;
                    if (_pos < _expr.Length && (_expr[_pos] == '+' || _expr[_pos] == '-'))
                        _pos++;
                    while (_pos < _expr.Length && char.IsDigit(_expr[_pos]))
                        _pos++;
                }

                string numStr = _expr.Substring(start, _pos - start);
                double val;
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                    return val;
                throw new FormatException("Invalid number: " + numStr);
            }

            private string ParseIdentifier()
            {
                int start = _pos;
                while (_pos < _expr.Length && (char.IsLetterOrDigit(_expr[_pos]) || _expr[_pos] == '_'))
                    _pos++;
                return _expr.Substring(start, _pos - start);
            }

            private static double CallFunction(string name, List<double> args)
            {
                if (args.Count == 0)
                    throw new FormatException("Function requires arguments: " + name);
                double x = args[0];

                switch (name.ToLowerInvariant())
                {
                    // Standard math
                    case "sin": return Math.Sin(x);
                    case "cos": return Math.Cos(x);
                    case "tan": return Math.Tan(x);
                    case "exp": return Math.Exp(x);
                    case "log": return Math.Log(x);
                    case "ln": return Math.Log(x);
                    case "log10": return Math.Log10(x);
                    case "sqrt": return Math.Sqrt(x);
                    case "abs": return Math.Abs(x);
                    case "asin": return Math.Asin(x);
                    case "acos": return Math.Acos(x);
                    case "atan": return Math.Atan(x);
                    case "ceil": return Math.Ceiling(x);
                    case "floor": return Math.Floor(x);
                    case "min": return args.Count > 1 ? Math.Min(x, args[1]) : x;
                    case "max": return args.Count > 1 ? Math.Max(x, args[1]) : x;
                    case "pow": return args.Count > 1 ? Math.Pow(x, args[1]) : x;

                    // square: two modes
                    // 1~2 args: square(x [, duty])  — period=2π (sin-like)
                    //   Ex) 1000*square(2*pi*50*t)  or  1000*square(2*pi*50*t, 0.3)
                    // 3~4 args: square(t, period, duty [, delay])  — period in seconds
                    //   Ex) 1000*square(t, 0.02, 0.5)  or  1000*square(t, 0.02, 0.3, 0.001)
                    case "square":
                    {
                        double period, duty, delay;
                        if (args.Count <= 2)
                        {
                            period = 2.0 * Math.PI;
                            duty = args.Count > 1 ? args[1] : 0.5;
                            delay = 0;
                        }
                        else
                        {
                            period = args[1];
                            duty = args[2];
                            delay = args.Count > 3 ? args[3] : 0;
                        }
                        if (period <= 0) return 0;
                        if (duty <= 0) return -1.0;
                        if (duty >= 1) return 1.0;
                        double phase = (x - delay) % period;
                        if (phase < 0) phase += period;
                        return phase < period * duty ? 1.0 : -1.0;
                    }

                    // pulse(t, start, width)
                    // Single non-periodic rectangular pulse. Returns 1 for start ≤ t < start+width, else 0
                    // Usage: 500*pulse(t, 0.001, 0.003)
                    case "pulse":
                    {
                        if (args.Count < 3) throw new FormatException("pulse(t, start, width) requires 3 args");
                        double start = args[1];
                        double width = args[2];
                        return (x >= start && x < start + width) ? 1.0 : 0.0;
                    }

                    // step(t [, t0])
                    // Heaviside step. Returns 1 for t >= t0, else 0. Default t0=0
                    // Usage: 1000*step(t, 0.005)
                    case "step":
                    {
                        double t0 = args.Count > 1 ? args[1] : 0;
                        return x >= t0 ? 1.0 : 0.0;
                    }

                    // saw: two modes
                    // 1 arg:  saw(x) — period=2π (sin-like)
                    // 2~3 args: saw(t, period [, delay]) — period in seconds
                    case "saw":
                    {
                        double period, delay;
                        if (args.Count <= 1)
                        {
                            period = 2.0 * Math.PI;
                            delay = 0;
                        }
                        else
                        {
                            period = args[1];
                            delay = args.Count > 2 ? args[2] : 0;
                        }
                        if (period <= 0) return 0;
                        double phase = (x - delay) % period;
                        if (phase < 0) phase += period;
                        return 2.0 * phase / period - 1.0;
                    }

                    // tri: two modes
                    // 1 arg:  tri(x) — period=2π (sin-like)
                    // 2~3 args: tri(t, period [, delay]) — period in seconds
                    case "tri":
                    {
                        double period, delay;
                        if (args.Count <= 1)
                        {
                            period = 2.0 * Math.PI;
                            delay = 0;
                        }
                        else
                        {
                            period = args[1];
                            delay = args.Count > 2 ? args[2] : 0;
                        }
                        if (period <= 0) return 0;
                        double halfP = period * 0.5;
                        double phase = (x - delay) % period;
                        if (phase < 0) phase += period;
                        return phase < halfP
                            ? 2.0 * phase / halfP - 1.0
                            : 3.0 - 2.0 * phase / halfP;
                    }

                    default:
                        throw new FormatException("Unknown function: " + name);
                }
            }

            private void SkipSpaces()
            {
                while (_pos < _expr.Length && _expr[_pos] == ' ')
                    _pos++;
            }
        }

        // ─── Tabular data parsing ───

        public static bool ParseTabularData(string text, out double[] time, out double[] amplitude, out string error)
        {
            time = null;
            amplitude = null;
            error = null;

            if (string.IsNullOrEmpty(text))
            {
                error = "No data to parse.";
                return false;
            }

            var timeList = new List<double>();
            var ampList = new List<double>();

            string[] lines = text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Split by tab, comma, or multiple spaces
                string[] parts;
                if (line.Contains("\t"))
                    parts = line.Split('\t');
                else if (line.Contains(","))
                    parts = line.Split(',');
                else
                    parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 2)
                {
                    error = string.Format("Line {0}: expected 2 columns, got {1}", i + 1, parts.Length);
                    return false;
                }

                double t, a;
                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out t))
                {
                    error = string.Format("Line {0}: cannot parse time value '{1}'", i + 1, parts[0].Trim());
                    return false;
                }
                if (!double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out a))
                {
                    error = string.Format("Line {0}: cannot parse amplitude value '{1}'", i + 1, parts[1].Trim());
                    return false;
                }

                timeList.Add(t);
                ampList.Add(a);
            }

            if (timeList.Count < 2)
            {
                error = "Need at least 2 data points.";
                return false;
            }

            time = timeList.ToArray();
            amplitude = ampList.ToArray();
            return true;
        }
    }
}
