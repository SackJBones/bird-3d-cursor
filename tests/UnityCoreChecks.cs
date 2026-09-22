using System;
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
