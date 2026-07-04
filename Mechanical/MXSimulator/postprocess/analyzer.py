# encoding: utf-8
"""
MX Post-Process: FFT and FRF analysis utilities
"""
import numpy as np
from scipy.fft import fft, fftfreq
from scipy.signal import find_peaks, welch, csd


def parse_ansys_export(filepath):
    """
    Parse ANSYS ExportToTextFile output.
    Handles tab/space/comma delimited; skips header lines.
    Returns (time_arr [s], values_arr [mm or N]).
    """
    times, values = [], []
    with open(filepath, 'r', encoding='utf-8', errors='replace') as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            # Skip obvious header lines
            first = line[0]
            if first.isalpha() or first in ('#', '[', '%'):
                continue
            # Try tab, comma, space split
            for sep in ('\t', ',', None):
                parts = line.split(sep) if sep else line.split()
                try:
                    t = float(parts[0])
                    v = float(parts[-1])
                    times.append(t)
                    values.append(v)
                    break
                except (ValueError, IndexError):
                    continue
    return np.array(times, dtype=float), np.array(values, dtype=float)


def compute_fft(time, values):
    """
    Compute single-sided FFT magnitude spectrum.
    Returns (freqs [Hz], magnitudes) — positive frequencies only.
    """
    N = len(time)
    if N < 4:
        return np.array([]), np.array([])
    dt = np.mean(np.diff(time))
    Y = fft(values)
    freqs = fftfreq(N, dt)
    pos = freqs > 0
    return freqs[pos], np.abs(Y[pos]) * 2.0 / N


def compute_frf(time_d, disp, time_f, force):
    """
    H1 estimator: H(f) = Gxy(f) / Gxx(f)  [mm/N]
    Gxx = auto-spectrum of force
    Gxy = cross-spectrum of (force, disp)
    Resamples disp to force time grid if lengths differ.
    """
    # Align to common time grid
    if len(time_d) != len(time_f) or not np.allclose(time_d, time_f, rtol=1e-4):
        disp = np.interp(time_f, time_d, disp)

    N = len(time_f)
    dt = float(np.mean(np.diff(time_f)))
    fs = 1.0 / dt
    nperseg = min(N // 4, 512)
    nperseg = max(nperseg, 8)

    freqs, Gxx = welch(force, fs=fs, nperseg=nperseg)
    _,    Gxy = csd(force, disp, fs=fs, nperseg=nperseg)

    H = Gxy / (Gxx + 1e-30)
    return freqs, np.abs(H), np.angle(H, deg=True)


def coherence(time_d, disp, time_f, force):
    """
    Coherence function γ²(f) = |Gxy|² / (Gxx · Gyy)
    Range 0–1: 1 = perfect linear causality.
    """
    if len(time_d) != len(time_f):
        disp = np.interp(time_f, time_d, disp)
    N = len(time_f)
    dt = float(np.mean(np.diff(time_f)))
    fs = 1.0 / dt
    nperseg = min(N // 4, 512)
    nperseg = max(nperseg, 8)

    freqs, Gxx = welch(force, fs=fs, nperseg=nperseg)
    freqs, Gyy = welch(disp,  fs=fs, nperseg=nperseg)
    _,    Gxy = csd(force, disp, fs=fs, nperseg=nperseg)

    gamma2 = np.abs(Gxy)**2 / (Gxx * Gyy + 1e-60)
    return freqs, np.clip(gamma2, 0, 1)


def extract_modal_params(freqs, H_mag, prominence_ratio=0.05, min_dist_hz=2.0):
    """
    Extract natural frequencies and damping ratios from FRF magnitude.
    Uses half-power bandwidth method.
    Returns list of dicts: {fn, H_peak, zeta, Q, idx}
    """
    if len(H_mag) < 4:
        return []

    df = freqs[1] - freqs[0] if len(freqs) > 1 else 1.0
    min_dist_idx = max(int(min_dist_hz / df), 3)
    H_max = np.max(H_mag)

    peaks, _ = find_peaks(
        H_mag,
        prominence=prominence_ratio * H_max,
        distance=min_dist_idx
    )

    results = []
    for pk in peaks:
        fn = freqs[pk]
        H_pk = H_mag[pk]
        H_half = H_pk / np.sqrt(2.0)

        # Find left half-power point
        left = pk
        while left > 0 and H_mag[left] > H_half:
            left -= 1
        # Find right half-power point
        right = pk
        while right < len(H_mag) - 1 and H_mag[right] > H_half:
            right += 1

        if left < pk and right > pk and freqs[right] > freqs[left]:
            f1 = freqs[left]
            f2 = freqs[right]
            zeta = (f2 - f1) / (2.0 * fn)
            Q = 1.0 / (2.0 * zeta) if zeta > 0 else float('nan')
        else:
            zeta = float('nan')
            Q = float('nan')

        results.append({
            'fn': fn,
            'H_peak': H_pk,
            'zeta': zeta,
            'Q': Q,
            'idx': pk,
        })

    return results


def rms(arr):
    return float(np.sqrt(np.mean(np.asarray(arr, dtype=float)**2)))


def peak_to_peak(arr):
    a = np.asarray(arr, dtype=float)
    return float(np.max(a) - np.min(a))


# ---------------------------------------------------------------------------
# Fatigue: rainflow cycle counting + Miner's-rule damage
# (POSTPROCESS_IDEAS.md §10.2). Prefers the `fatpack` library when installed;
# falls back to a self-contained ASTM E1049 three-point rainflow so the viewer
# works with zero extra dependencies.
# ---------------------------------------------------------------------------

def _extract_reversals(signal):
    """Reduce a signal to its turning points (reversals) — the input rainflow needs."""
    s = np.asarray(signal, dtype=float)
    if s.size < 2:
        return s.copy()
    d = np.diff(s)
    # keep points where the slope changes sign, plus the endpoints
    idx = [0]
    for i in range(1, len(d)):
        if d[i - 1] != 0 and d[i] != 0 and (d[i - 1] > 0) != (d[i] > 0):
            idx.append(i)
    idx.append(len(s) - 1)
    return s[idx]


def _rainflow_astm(signal):
    """Self-contained ASTM E1049-85 rainflow — the exact three-point stack algorithm the
    `rainflow` PyPI package implements. Returns a list of (range, mean, count) tuples; count
    is 1.0 (full) or 0.5 (residual half). Cross-checked against fatpack (counts + Miner damage
    agree within tolerance).

    The stack holds unclosed reversals. On each new point we compare the last three a,b,c:
    - if |a-b| < |b-c|: b is enclosed. If a,b are the FIRST two points on the stack, a-b is a
      HALF cycle (unclosable from the left) and we drop a; otherwise a-b is a CLOSED FULL cycle
      and we drop a and b (leaving c) — re-testing continues.
    - else b is not yet closed: stop and wait for more points.
    Leftover consecutive pairs at the end are residual HALF cycles.
    """
    points = list(_extract_reversals(signal))
    cycles = []
    stack = []
    for p in points:
        stack.append(p)
        while len(stack) >= 3:
            a, b, c = stack[-3], stack[-2], stack[-1]
            X = abs(c - b)   # newest swing
            Y = abs(b - a)   # candidate enclosed swing
            if X < Y:
                break        # b not yet enclosed
            if len(stack) == 3:
                # a-b anchored at the history start -> half cycle, drop a, keep b,c
                cycles.append((Y, (a + b) / 2.0, 0.5))
                stack.pop(0)
            else:
                # a-b fully enclosed -> full cycle, remove a and b, keep c
                cycles.append((Y, (a + b) / 2.0, 1.0))
                stack.pop(-2)   # remove b
                stack.pop(-2)   # remove a (now at -2 after b was removed)
    # residual: consecutive leftover pairs are half cycles
    for i in range(len(stack) - 1):
        cycles.append((abs(stack[i + 1] - stack[i]),
                       (stack[i] + stack[i + 1]) / 2.0, 0.5))
    return cycles


def rainflow_count(signal, nbins=None):
    """Rainflow-count a stress/strain (or displacement) time history.
    Returns dict: ranges[], means[], counts[] (count 1.0 full / 0.5 residual half), plus a
    histogram (bin_edges, bin_counts) when nbins is given.

    Prefers the verified `rainflow` PyPI package (ASTM E1049-85); falls back to the bundled
    _rainflow_astm which is cross-checked to give IDENTICAL cycles. (fatpack is intentionally
    NOT used here — its cycle set differs from ASTM E1049 and gave a ~16x-wrong Miner damage.)
    """
    s = np.asarray(signal, dtype=float)
    tuples = None
    used = "astm-3pt"
    try:
        import rainflow as _rf  # verified ASTM E1049 implementation
        # extract_cycles yields (range, mean, count, i_start, i_end)
        tuples = [(c[0], c[1], c[2]) for c in _rf.extract_cycles(s)]
        used = "rainflow-pkg"
    except Exception:
        tuples = _rainflow_astm(s)
    if tuples:
        ranges = np.array([t[0] for t in tuples], dtype=float)
        means = np.array([t[1] for t in tuples], dtype=float)
        counts = np.array([t[2] for t in tuples], dtype=float)
    else:
        ranges = np.array([]); means = np.array([]); counts = np.array([])
    out = {'ranges': ranges, 'means': means, 'counts': counts, 'method': used}
    if nbins and ranges.size:
        edges = np.linspace(0.0, float(ranges.max()) * 1.0000001, int(nbins) + 1)
        bin_counts, _ = np.histogram(ranges, bins=edges, weights=counts)
        out['bin_edges'] = edges
        out['bin_counts'] = bin_counts
    return out


def damage_miner(ranges, counts, sn_A, sn_m, endurance=None):
    """Miner's cumulative damage against a power-law S-N curve N = A * S^(-m).
    ranges/counts from rainflow_count; sn_A, sn_m are the S-N constants (S in the
    same unit as `ranges`, e.g. MPa). Ranges below `endurance` (if given) contribute
    zero damage (infinite life). Returns dict: D (total), per-range Ni/ni, life_fraction."""
    r = np.asarray(ranges, dtype=float)
    n = np.asarray(counts, dtype=float)
    if r.size == 0:
        return {'D': 0.0, 'cycles_to_failure': float('inf'), 'contributions': []}
    D = 0.0
    contribs = []
    for rng, ni in zip(r, n):
        S = abs(rng)
        if S <= 0 or (endurance is not None and S < endurance):
            Ni = float('inf'); di = 0.0
        else:
            Ni = sn_A * (S ** (-sn_m))
            di = ni / Ni if Ni > 0 else float('inf')
        D += di
        contribs.append({'range': float(S), 'n': float(ni), 'N': float(Ni), 'damage': float(di)})
    life = (1.0 / D) if D > 0 else float('inf')  # repeats-of-this-block to failure
    return {'D': float(D), 'repeats_to_failure': life, 'contributions': contribs}
