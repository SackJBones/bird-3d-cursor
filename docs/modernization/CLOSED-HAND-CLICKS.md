# Index-lever clicks at the closed-hand endpoint

Dana reports good clicking generally, but difficulty within a finger or two
of the palm and no useful clicking in a fist. The intended pose keeps the
other fingers closed while the index sticks out, then depresses the index
like a lever. The immediate code defect was a blanket `fistWeight > 0` click
disable in v0.5/v0.6, which suppressed clicks throughout the closing blend.

Version 0.7 removes that restriction. This is an interaction policy in the
port's existing click convenience API, separate from the geometric point.
The original `Bird.cs`, sphere fitting, range law, 45-degree aiming, Kalman
recurrence and closed-fist point are unchanged. A mandala can ignore clicks,
and an experience can disable them with `clicksAllowed`.

## Policy

Outside the closed-hand blend, the original sphere/cursor-centered selection
depth and 7 mm press / 5 mm release hysteresis remain unchanged.

In the closing region, compute an index-lever depth:

```text
lever depth = closeHandClickReach * mean(other three finger chain lengths)
              - dot(indexTip - indexBase, palmForward)
```

`palmForward` is the same knuckle-directed tangent derived solely from the
hand. The default `closeHandClickReach` is .7. An extended poking index is
outside the press plane; depressing it toward the palm crosses that plane.
The index does not define the reference chain length or affect the pointing
law. Mirroring/rotating the hand rotates the policy with it.

Blend from original depth toward lever depth using the greater of the fist
weight and fit-conditioning loss. This alternate policy is active only when
the fist weight is positive. A singular closed fit uses lever depth directly,
so a folded fist does not need a valid sphere to click. The same physical
7/5 mm hysteresis then produces one down/up edge. Invalid click parameters
release selection without invalidating a good geometric point. Tracking loss
and cancellation still release selection.

The `.7` fraction is an initial experience choice, not a recovered legacy
constant or anatomical calibration. It can be tuned independently of range.
Closing the other fingers while the index is already below the press plane
can select; this remains a pose-based click rule, not an intent classifier.

## Evidence

Real Unity C# and compiled Udon run the shared hand suite: 5549 assertions,
including the prior 2401 articulated sweep samples and new closed-click
fixtures. New cases cover regular and singular folded fists, 0.7/1/1.3 hand
scales, mirrored/rotated/translated poses, index-lever press/hold/release,
single edge pulses, unchanged closed-hand point, invalid policy parameters
and cancellation. The separate 114 compiled cursor/click/filter baseline
assertions also pass.

These establish implementation behavior, not physical lever comfort or the
best threshold for Dana's hand. The v0.7 physical test remains pending. The
existing optional recorder can help if a specific issue persists, but no
additional capture is required for continued work.
