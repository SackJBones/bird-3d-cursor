"""Bounded numerical study; changes no Unity runtime, source defaults or APK.

Run with Python 3. Output is CSV, suitable for redirection to a measurement file.
Stationary hand origin and exact synthetic inputs deliberately isolate angular
response. This is not a tracking replay, Udon test or perceptual validation.
"""
import csv
import math
import sys


def range_input(radius):
    lo, hi = 0.0, 2.0
    for _ in range(70):
        m = (lo + hi) / 2
        if m + m*m/.02 + .02*(m/.03)**6 < radius:
            lo = m
        else:
            hi = m
    return (lo + hi) / 2


def warp(point, inverse=False):
    radius = math.hypot(*point)
    if radius <= 4:
        return point
    # Identity through 4 m, C2 join, asymptotically logarithmic, explicit inverse.
    length = 4 + 4*(math.sinh((radius-4)/4) if inverse
                   else math.asinh((radius-4)/4))
    return tuple(v*length/radius for v in point)


class Filter:
    def __init__(self, policy, seed):
        self.policy, self.position, self.variance = policy, seed, 1.0

    def step(self, raw, dt):
        radius = math.hypot(*raw)
        noise = 270*range_input(radius)**3
        predicted = self.variance + .001
        if "responsive" in self.policy:
            t = min(1, max(0, math.log(max(radius, 4)/4)/math.log(5)))
            weight = t*t*(3-2*t)
            minimum_gain = -math.expm1(-dt*weight/.05)
            predicted = max(predicted, noise*minimum_gain/(1-minimum_gain))
        gain = predicted/(predicted+noise)
        self.variance = noise*predicted/(predicted+noise)
        mapped = "mapped" in self.policy
        previous = warp(self.position) if mapped else self.position
        target = warp(raw) if mapped else raw
        result = tuple(a*(1-gain)+b*gain for a, b in zip(previous, target))
        self.position = warp(result, True) if mapped else result
        return self.position


def run():
    policies = ("legacy", "mapped", "responsive", "mapped-responsive")
    for r in (0, .001, 1, 4, 4.0001, 5, 20, 1000, 1e9):
        result = warp(warp((r, 0)), True)
        assert abs(result[0]-r) <= 1e-6*max(r, 1)
    # Independent ordinary-volume trajectories must retain the recurrence.
    for hz in (30, 72, 120):
        filters = [Filter(p, (1, 0)) for p in policies]
        for i in range(hz*10):
            t = i/hz
            raw = (2+math.sin(t*2), .2*math.cos(t*3))
            values = [f.step(raw, 1/hz) for f in filters]
            assert all(math.dist(v, values[0]) < 1e-12 for v in values)
    writer = csv.writer(sys.stdout, lineterminator="\n")
    writer.writerow(("policy", "hz", "range_m", "stationary_history_s",
                     "90deg_turn_90pct_s", "minimum_range_fraction"))
    for hz in (30, 72, 120):
        for radius in (4, 1000, 1e9):
            for history in (1, 60):
                for policy in policies:
                    f = Filter(policy, (radius, 0))
                    for _ in range(hz*history):
                        f.step((radius, 0), 1/hz)
                    minimum, arrival = 1, None
                    for i in range(hz*60):
                        point = f.step((0, radius), 1/hz)
                        minimum = min(minimum, math.hypot(*point)/radius)
                        if math.atan2(point[1], point[0]) >= math.radians(81):
                            arrival = (i+1)/hz
                            break
                    writer.writerow((policy, hz, radius, history,
                                     "over60" if arrival is None else f"{arrival:.6f}",
                                     f"{minimum:.9g}"))


if __name__ == "__main__":
    run()
