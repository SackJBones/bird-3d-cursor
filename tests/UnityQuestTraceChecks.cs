#if BIRD_OPENXR_ENABLED && UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Bird3DCursor;
using UnityEngine;

// Exercise actual capture/snapshot/pause-save code with injected joint data.
// This does not claim a headset fingertip hit test or device file-write check.
public static class UnityQuestTraceChecks
{
    public static string Run()
    {
        var go = new GameObject("Joint trace persistence check");
        try
        {
            var app = go.AddComponent<UnityQuestHands>();
            var type = typeof(UnityQuestHands);
            var sideType = type.GetNestedType("Side", BindingFlags.NonPublic);
            var handType = type.GetNestedType("CapturedHand", BindingFlags.NonPublic);
            object hand = Activator.CreateInstance(handType, new object[] { Hand.Chirality.Left });
            var joints = (Vector3[])handType.GetField("joints").GetValue(hand);
            for (int i = 0; i < 20; i++) joints[i] = new Vector3(i*.01f, .2f, .3f);
            var port = go.AddComponent<BirdCursorState>();
            port.fitter = go.AddComponent<BirdSphereFit>();
            port.handRoot = new Vector3(.1f, .2f, .3f);
            port.palmNormal = Vector3.forward;
            object side = Activator.CreateInstance(sideType);
            sideType.GetField("name").SetValue(side, "Left");
            sideType.GetField("hand").SetValue(side, hand);
            sideType.GetField("port").SetValue(side, port);
            sideType.GetField("jointTracking").SetValue(side, true);
            var sides = Array.CreateInstance(sideType, 1); sides.SetValue(side, 0);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("sides", flags).SetValue(app, sides);
            app.BeginCapture();
            type.GetMethod("CaptureJoints", flags).Invoke(app, null);
            if ((int)type.GetField("captureSamples", flags).GetValue(app) != 0) throw new Exception("Countdown captured prematurely");
            type.GetField("captureStart", flags).SetValue(app, Time.unscaledTime-1);
            type.GetField("captureEnd", flags).SetValue(app, Time.unscaledTime+20);
            type.GetMethod("CaptureJoints", flags).Invoke(app, null);
            joints[0] = Vector3.one;
            sideType.GetField("jointTracking").SetValue(side, false);
            type.GetMethod("CaptureJoints", flags).Invoke(app, null);
            type.GetMethod("OnApplicationPause", flags).Invoke(app, new object[] { true });
            if (type.GetField("capture", flags).GetValue(app) != null || string.IsNullOrEmpty(app.LastCapturePath)) throw new Exception("Pause failed to save/stop capture");
            string[] lines = File.ReadAllLines(app.LastCapturePath);
            if (lines.Length != 2) throw new Exception("Trace row count mismatch");
            var first = JsonUtility.FromJson<UnityQuestHands.JointTrace>(lines[0]);
            var second = JsonUtility.FromJson<UnityQuestHands.JointTrace>(lines[1]);
            if (first.schema != 1 || first.appVersion != "0.7" || first.hand != "Left" || first.joints.Length != 20 ||
                first.joints[0] != new Vector3(0,.2f,.3f) || second.joints[0] != Vector3.one || !first.tracked || second.tracked ||
                first.root != port.handRoot || first.palmNormal != Vector3.forward || first.time < 0)
                throw new Exception("Saved joint snapshot/schema/loss information differs");
            Directory.CreateDirectory("JointTraceChecks");
            File.Copy(app.LastCapturePath, "JointTraceChecks/synthetic.jsonl", true);
            File.Delete(app.LastCapturePath); // Only this test's exact newly returned file.
            return "PASS: actual joint capture countdown, immutable 20-joint snapshots, tracking loss, pause/save and JSONL readback (synthetic editor input).";
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
#endif
