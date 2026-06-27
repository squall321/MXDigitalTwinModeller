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
