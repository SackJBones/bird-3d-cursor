"""Audit the legacy polynomial and already-recorded synthetic avatar samples.

No geometry, runtime setting or scene is changed. Curl labels identify the
synthetic fixture only; they are not an openness measure or user calibration.
"""
import argparse
import csv
import math
from pathlib import Path


def reach(s, near=.02, far=.03):
    return s + s*s/near + near*(s/far)**6


def inverse_reach(r):
    lo, hi = 0., 2.
    while reach(hi) < r:
        hi *= 2
    for _ in range(80):
        mid = (lo+hi)/2
        if reach(mid) < r:
            lo = mid
        else:
            hi = mid
    return (lo+hi)/2 if r else 0.


def run(out, avatar_csv):
    out.mkdir(parents=True, exist_ok=True)
    with (out/'reference-range-curve.csv').open('w', newline='') as f:
        w = csv.writer(f)
        w.writerow(['sphere_distance_m', 'legacy_reach_m', 'legacy_gain',
                    'illustrative_far_characteristic_004_reach_m'])
        for s in [0,.01,.02,.03,.04,.05,.06,.07,.08,.09,.1,.12,.15,.2,.3,.5,1,2]:
            w.writerow([s, reach(s), 1+2*s/.02+4*(s/.03)**5, reach(s, far=.04)])
            assert math.isclose(inverse_reach(reach(s)), s, abs_tol=1e-14)
    if avatar_csv:
        with avatar_csv.open() as f:
            rows = list(csv.DictReader(f))
        with (out/'reference-range-normalization.csv').open('w', newline='') as f:
            w = csv.writer(f)
            w.writerow(['fixture_phase', 'side', 'fixture_curl_deg', 'finger_length_m',
                        'normalization', 'far_term_multiplier', 'mapped_input_m',
                        'effective_vector_length_m', 'observed_raw_reach_m',
                        'same_effective_vector_without_normalization_reach_m',
                        'flat_weight', 'fist_weight'])
            for row in rows:
                if row['phase'] != 'articulation' or row['scale'] != '1':
                    continue
                length = float(row['finger_length_m'])
                r = float(row['range_m'])
                scale = .09/length
                mapped = inverse_reach(r)
                w.writerow([row['phase'],row['side'],row['bend_deg'],length,scale,
                            scale**6,mapped,mapped/scale,r,reach(mapped/scale),
                            row['flat_weight'],row['fist_weight']])
    print('PASS: reference polynomial inversions; illustrative curve and synthetic normalization audit written.')


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--out', type=Path, required=True)
    p.add_argument('--avatar-csv', type=Path)
    args = p.parse_args()
    run(args.out, args.avatar_csv)
