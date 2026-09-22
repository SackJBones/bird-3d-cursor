using System;
using System.Collections.Generic;
using System.IO;
using Bird3DCursor;
using UnityEditor;
using UnityEngine;

// Runs the production solver inside Unity, with deterministic synthetic hand input.
public static class UnityCoreChecks
{
    private static int checks;

    public static void Run()
    {
        try
        {
            CheckTrackingLoss();
            CheckBatchFiltering();
            CheckInvalidPoses();
            File.WriteAllText("core-checks-result.txt", "PASS: " + checks + " core checks; Unity " + Application.unityVersion);
            Debug.Log("BIRD_CORE_CHECKS_PASS: " + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            File.WriteAllText("core-checks-result.txt", "FAIL: " + exception);
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void CheckTrackingLoss()
    {
        var hand = new SyntheticHand();
        var bird = new Bird(hand);
        bird.Update();
        Check(!bird.GetClick() && !bird.GetClickDown() && !bird.GetClickUp(), "Startup without tracking must be idle");
        hand.tracked = true;
        bird.Update();
        Check(Vector3.Distance(bird.GetSphereFitCenter(), SyntheticHand.Center) < 0.0001f, "Synthetic sphere center must be recovered");
        Check(Mathf.Abs(bird.GetSphereFitRadius() - SyntheticHand.Radius) < 0.0001f, "Synthetic sphere radius must be recovered");
        Check(!bird.GetClick(), "Extended index must not click");
        hand.pressed = true;
        bird.Update();
        Check(bird.GetClick() && bird.GetClickDown() && !bird.GetClickUp(), "Index penetration must produce one press");
        var heldPosition = bird.GetPosition();
        hand.tracked = false;
        bird.Update();
        Check(!bird.GetClickDown(), "Tracking loss must clear the prior press edge");
        Check(!bird.GetClick() && bird.GetClickUp(), "Tracking loss must release a held click");
        Check(bird.GetPosition() == heldPosition, "Tracking loss must hold the last cursor position");
        Check(bird.GetPrevPosition() == bird.GetPosition(), "Tracking loss must clear stale motion");
        bird.Update();
        Check(!bird.GetClick() && !bird.GetClickDown() && !bird.GetClickUp(), "Continued tracking loss must not repeat edges");
        hand.tracked = true;
        hand.pressed = false;
        bird.Update();
        Check(!bird.GetClick() && !bird.GetClickDown() && !bird.GetClickUp(), "Recovery with extended index must be idle");
        hand.pressed = true;
        bird.Update();
        Check(bird.GetClickDown(), "Press after recovery must work");
        bird.Update();
        Check(bird.GetClick() && !bird.GetClickDown(), "Holding must not repeat press edges");
        hand.pressed = false;
        bird.Update();
        Check(bird.GetClickUp(), "Normal release must still work");
        hand.tracked = false;
        bird.Update();
        Check(!bird.GetClickUp(), "Tracking loss must clear the prior release edge");
    }

    private static void CheckBatchFiltering()
    {
        var chronological = new List<Vector3> {
            new Vector3(1, -2, 3), new Vector3(-4, 5, 0.5f),
            new Vector3(0.2f, -0.7f, 8), new Vector3(2, 3, -1)
        };
        foreach (int count in new[] { 1, chronological.Count })
        foreach (bool newestFirst in new[] { false, true })
        foreach (bool overrideNoise in new[] { false, true })
        {
            var expected = new KalmanFilterVector3();
            var actual = new KalmanFilterVector3();
            var initial = new Vector3(-2, 1, 0.5f);
            expected.Reset(initial);
            actual.Reset(initial);
            float? q = overrideNoise ? (float?)0.02f : null;
            float? r = overrideNoise ? (float?)0.07f : null;
            Vector3 expectedResult = Vector3.zero;
            for (int i = 0; i < count; i++) expectedResult = expected.Update(chronological[i], q, r);
            var batch = chronological.GetRange(0, count);
            if (newestFirst) batch.Reverse();
            Vector3 actualResult = actual.Update(batch, newestFirst, q, r);
            Check(Vector3.Distance(expectedResult, actualResult) < 0.000001f,
                "Batch must match chronological single updates: count=" + count + ", newestFirst=" + newestFirst + ", overrideNoise=" + overrideNoise);
            Vector3 next = new Vector3(9, -3, 2);
            Check(Vector3.Distance(expected.Update(next), actual.Update(next)) < 0.000001f,
                "Batch must leave the same filter state for the next measurement");
        }

        var emptyFilter = new KalmanFilterVector3();
        var control = new KalmanFilterVector3();
        emptyFilter.Update(chronological[0]);
        control.Update(chronological[0]);
        Check(emptyFilter.Update(new List<Vector3>()) == Vector3.zero,
            "Empty batch preserves the existing zero-result contract");
        Check(emptyFilter.Update(chronological[1]) == control.Update(chronological[1]),
            "Empty batch must not change filter state");
    }

    private static void CheckInvalidPoses()
    {
        for (int mode = 0; mode < 4; mode++)
        {
            var hand = new FaultHand();
            var bird = new Bird(hand);
            var reference = new Bird(new SyntheticHand { tracked = true, pressed = true });
            bird.Update();
            reference.Update();
            Vector3 position = bird.GetPosition();
            Vector3 center = bird.GetSphereFitCenter();
            Vector3 root = bird.GetHandRoot();
            hand.mode = mode;
            bird.Update();
            Check(bird.GetPosition() == position, "Invalid pose must hold cursor position; mode=" + mode);
            Check(bird.GetSphereFitCenter() == center && bird.GetHandRoot() == root,
                "Invalid pose must preserve last valid geometry; mode=" + mode);
            Check(!bird.GetClick() && !bird.GetClickDown() && bird.GetClickUp(),
                "Invalid pose must release the selection once; mode=" + mode);
            Check(bird.GetPrevPosition() == position, "Invalid pose must clear stale motion");
            bird.Update();
            Check(!bird.GetClickUp(), "Repeated invalid poses must not repeat release");
            hand.mode = -1;
            bird.Update();
            reference.Update();
            Check(Vector3.Distance(bird.GetPosition(), reference.GetPosition()) < 0.000001f,
                "Valid pose must recover without poisoning the filter; mode=" + mode);
            Check(bird.GetClick() && bird.GetClickDown(), "Valid pose must resume index selection");
        }
    }

    private sealed class FaultHand : Hand
    {
        private readonly SyntheticHand source = new SyntheticHand { tracked = true, pressed = true };
        public int mode = -1;
        public FaultHand() : base(Chirality.Right) { }
        public override bool IsTracking() { return true; }
        private Vector3 Sample(Vector3 point, Finger finger, int joint)
        {
            if (mode == 0 && finger == Finger.Middle && joint == 1) point.x = float.NaN;
            if (mode == 1 && finger == Finger.Index && joint == 3) point.y = float.PositiveInfinity;
            if (mode == 2 && finger == Finger.Thumb && joint == 0) point.z = float.NegativeInfinity;
            if (mode == 3) return Vector3.zero;
            return point;
        }
        public override Vector3 GetBasePosition(Finger finger) { return Sample(source.GetBasePosition(finger), finger, 0); }
        public override Vector3 GetIntermediatePosition(Finger finger) { return Sample(source.GetIntermediatePosition(finger), finger, 1); }
        public override Vector3 GetDistalPosition(Finger finger) { return Sample(source.GetDistalPosition(finger), finger, 2); }
        public override Vector3 GetTipPosition(Finger finger) { return Sample(source.GetTipPosition(finger), finger, 3); }
    }

    private sealed class SyntheticHand : Hand
    {
        public static readonly Vector3 Center = new Vector3(0, 0, 0.04f);
        public const float Radius = 0.03f;
        private readonly Vector3[] points = new Vector3[16];
        public bool tracked, pressed;

        public SyntheticHand() : base(Chirality.Right)
        {
            // Evenly distributed non-coplanar points on a known sphere.
            for (int i = 0; i < points.Length; i++)
            {
                float z = 1f - 2f * (i + 0.5f) / points.Length;
                float radial = Mathf.Sqrt(1f - z * z);
                float angle = i * 2.39996323f;
                points[i] = Center + Radius * new Vector3(radial * Mathf.Cos(angle), radial * Mathf.Sin(angle), z);
            }
        }

        public override bool IsTracking() { return tracked; }
        public override Vector3 GetBasePosition(Finger finger)
        {
            if (finger == Finger.Thumb) return points[3] + new Vector3(0, 0.025f, -0.025f);
            if (finger == Finger.Index) return points[3];
            return points[4 + ((int)finger - 2) * 4];
        }
        public override Vector3 GetIntermediatePosition(Finger finger) { return Joint(finger, 1); }
        public override Vector3 GetDistalPosition(Finger finger) { return Joint(finger, 2); }
        public override Vector3 GetTipPosition(Finger finger)
        {
            if (finger == Finger.Index) return Center + (pressed ? Vector3.zero : Vector3.right * 0.15f);
            return Joint(finger, 3);
        }
        private Vector3 Joint(Finger finger, int joint)
        {
            if (finger == Finger.Thumb) return points[joint - 1];
            if (finger == Finger.Index) throw new Exception("Index intermediate/distal joints must not enter sphere fit");
            return points[4 + ((int)finger - 2) * 4 + joint];
        }
    }
}
