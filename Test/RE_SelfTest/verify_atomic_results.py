#!/usr/bin/env python
# Verify atomic_results/*.json: for each OK cell, check that the "after" value
# actually matches the requested mod (before * factor). Flag fake-OK cells
# where status=OK but |after - expected| > tolerance.
#
# Expected factors:
#   Hole:   new = before * 1.2  (enlarge)
#   Fillet: new = before * 1.2 (or 1.0 if before==0)
#   Wall:   new = before * 0.9  (shrink)
#
# Tolerance: 5% of expected, OR 0.01mm absolute (whichever larger).
#
# Outputs: a verification table to stdout.

import os
import json

RESULTS_DIR = r"D:\MXDigitalTwinModeller\Test\RealCAD\atomic_results"
TOL_PCT = 0.05
TOL_ABS_MM = 0.01

def expected_for(mod_kind, before):
    if before is None: return None
    if mod_kind == "Hole":   return before * 1.2
    if mod_kind == "Fillet": return before * 1.2 if before > 0 else 1.0
    if mod_kind == "Wall":   return before * 0.9
    return None

def is_close(after, expected):
    if after is None or expected is None: return None
    tol = max(abs(expected) * TOL_PCT, TOL_ABS_MM)
    return abs(after - expected) <= tol

rows = []
for fn in sorted(os.listdir(RESULTS_DIR)):
    if not fn.endswith(".json"): continue
    with open(os.path.join(RESULTS_DIR, fn), encoding="utf-8") as f:
        try: d = json.load(f)
        except Exception as e: print("PARSE FAIL", fn, e); continue
    rows.append(d)

# Verification table
print("%-32s %-7s %-7s %12s %12s %12s %s" % (
    "Model", "Mod", "Status", "Before", "Expected", "After", "Verdict"))
print("-" * 110)

fake_ok = []
real_ok = []
real_fail = []
no_feature = []

for r in rows:
    name = (r.get("name") or "")[:30]
    mod = r.get("mod_kind") or "?"
    status = r.get("status") or "?"
    before = r.get("before")
    after = r.get("after")
    expected = expected_for(mod, before)

    if status == "-" or (r.get("action") or "").startswith("(no"):
        no_feature.append(r); continue
    if status != "OK":
        # FAIL_VERIFY can still represent a real OK if after value matches
        # expected — the Python downgrade ran before MeasuredAfterMm was
        # propagated, OR the extractor lost the feature but the in-process
        # measurement was accurate.
        if status == "FAIL_VERIFY" and is_close(after, expected):
            real_ok.append(r)
            print("%-32s %-7s %-7s %12.3f %12.3f %12.3f %s" % (
                name, mod, status, before, expected, after,
                "VERIFIED (downgrade ignored, after matches)"))
            continue
        real_fail.append(r)
        print("%-32s %-7s %-7s %12s %12s %12s %s" % (
            name, mod, status,
            "%.3f" % before if before else "-",
            "%.3f" % expected if expected else "-",
            "%.3f" % after if after else "-",
            "FAIL"))
        continue

    # status == OK — check actual vs expected
    if after is None:
        # OK status but no verification — soft pass
        fake_ok.append((r, "no after value"))
        print("%-32s %-7s %-7s %12s %12s %12s %s" % (
            name, mod, status,
            "%.3f" % before if before else "-",
            "%.3f" % expected if expected else "-",
            "(null)",
            "UNVERIFIED"))
        continue

    ok = is_close(after, expected)
    if ok:
        real_ok.append(r)
        print("%-32s %-7s %-7s %12.3f %12.3f %12.3f %s" % (
            name, mod, status, before, expected, after, "VERIFIED"))
    else:
        diff = after - expected if (after is not None and expected is not None) else None
        fake_ok.append((r, "mismatch: after=%.3f expected=%.3f Δ=%.3f" % (after, expected, diff)))
        print("%-32s %-7s %-7s %12.3f %12.3f %12.3f %s" % (
            name, mod, status, before, expected, after,
            "FAKE-OK Δ=%.3f (%.0f%%)" % (diff, 100.0 * diff / expected if expected else 0)))

print()
print("=" * 110)
print("Real OK (status=OK + after matches expected):  %d" % len(real_ok))
print("Fake OK (status=OK but after wrong/unverified): %d" % len(fake_ok))
print("Real FAIL (status != OK):                       %d" % len(real_fail))
print("n/a (no feature on model):                      %d" % len(no_feature))
print("Total:                                          %d" % len(rows))

if fake_ok:
    print()
    print("=== FAKE OK DETAIL ===")
    for r, reason in fake_ok:
        print("  [%s %s] %s" % (r.get("name"), r.get("mod_kind"), reason))
