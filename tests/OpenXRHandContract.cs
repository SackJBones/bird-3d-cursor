// Lightweight API doubles: these tests do not substitute for Unity/XR hardware tests.
using System;
using System.Collections.Generic;
using Bird3DCursor;
using UnityEngine;
using UnityEngine.XR.Hands;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 zero { get { return new Vector3(); } }
    }
    public struct Pose { public Vector3 position; }
    public static class Debug { public static void LogError(string message) { throw new Exception(message); } }
    public static class SubsystemManager
    {
        public static readonly List<XRHandSubsystem> Available = new List<XRHandSubsystem>();
        public static void GetSubsystems(List<XRHandSubsystem> target) { target.Clear(); target.AddRange(Available); }
    }
}
namespace UnityEngine.XR.Hands
{
    public enum Handedness { Left, Right }
    public enum XRHandJointID
    {
        ThumbMetacarpal, ThumbProximal, ThumbDistal, ThumbTip,
        IndexProximal, IndexIntermediate, IndexDistal, IndexTip,
        MiddleProximal, MiddleIntermediate, MiddleDistal, MiddleTip,
        RingProximal, RingIntermediate, RingDistal, RingTip,
        LittleProximal, LittleIntermediate, LittleDistal, LittleTip
    }
    public struct XRHandJoint
    {
        public bool valid;
        public Pose pose;
        public bool TryGetPose(out Pose result) { result = pose; return valid; }
    }
    public struct XRHand
    {
        public bool isTracked;
        public XRHandJoint joint;
        public XRHandJoint GetJoint(XRHandJointID id) { return joint; }
    }
    public class XRHandSubsystem
    {
        public bool running;
        public XRHand leftHand, rightHand;
    }
}
public static class OpenXRHandContract
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    public static string Run()
    {
        checks = 0;
        foreach (Hand.Chirality side in Enum.GetValues(typeof(Hand.Chirality)))
        {
            SubsystemManager.Available.Clear();
            var hand = new OpenXRHand(side);
            Check(!hand.IsTracking(), "No subsystem must mean no tracking");
            Check(hand.GetTipPosition(Finger.Index).x == 0, "Reading before subsystem startup must be safe");
            var sample = new XRHand { isTracked = true, joint = new XRHandJoint
                { valid = true, pose = new Pose { position = new Vector3 { x = 7 } } } };
            var subsystem = new XRHandSubsystem { running = true };
            if (side == Hand.Chirality.Left) subsystem.leftHand = sample;
            else subsystem.rightHand = sample;
            SubsystemManager.Available.Add(subsystem);
            Check(hand.IsTracking(), "Running tracked hand must be discovered");
            var readers = new Func<Finger, Vector3>[] { hand.GetBasePosition, hand.GetIntermediatePosition,
                hand.GetDistalPosition, hand.GetTipPosition };
            foreach (var read in readers) Check(read(Finger.Middle).x == 7, "Valid pose must be returned");
            sample.joint.valid = false; // Nonzero output on failure must not leak into Bird.
            if (side == Hand.Chirality.Left) subsystem.leftHand = sample;
            else subsystem.rightHand = sample;
            foreach (var read in readers) Check(read(Finger.Middle).x == 0, "Invalid pose must be rejected");
            subsystem.running = false;
            Check(!hand.IsTracking(), "Stopped subsystem must not retain tracked state");
            foreach (var read in readers) Check(read(Finger.Middle).x == 0, "Stopped subsystem reads must be safe");
            subsystem.running = true;
            Check(hand.IsTracking(), "Restarted subsystem must be rediscovered");
            sample.isTracked = false;
            if (side == Hand.Chirality.Left) subsystem.leftHand = sample;
            else subsystem.rightHand = sample;
            Check(!hand.IsTracking(), "Lost hand must report untracked");
        }
        return checks + " OpenXR contract checks passed (API doubles; no Unity or device validation).";
    }
}
