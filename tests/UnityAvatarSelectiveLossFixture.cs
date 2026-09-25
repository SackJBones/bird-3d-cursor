#if UNITY_EDITOR
using System;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.Udon;

// Runtime-only SDK return-value injection. All unaffected queries use ClientSim.
// No production source, SDK source, avatar asset or saved scene is modified.
public static class UnityAvatarSelectiveLossFixture
{
    private static readonly string[] Required = {
        "ThumbIntermediate", "ThumbDistal", "IndexProximal",
        "MiddleProximal", "MiddleIntermediate", "MiddleDistal",
        "RingProximal", "RingIntermediate", "RingDistal",
        "LittleProximal", "LittleIntermediate", "LittleDistal",
        "ThumbProximal", "IndexDistal"
    };
    private static Func<VRCPlayerApi, HumanBodyBones, Vector3> original;
    private static Func<VRCPlayerApi, HumanBodyBones, Vector3> wrapper;
    private static HumanBodyBones missing;
    private static Vector3 injected;
    private static bool enabled;
    private static int scenario, stage;
    private static float next;
    private static bool Right { get { return scenario < 28 ? scenario >= 14 : scenario == 29; } }
    private static bool Unused { get { return scenario == 30; } }

    public static bool Tick(BirdAvatarInput[] inputs)
    {
        if (Time.time < next) return false;
        if (original == null)
        {
            original = VRCPlayerApi._GetBonePosition;
            if (original == null) throw new Exception("ClientSim bone provider unavailable");
            wrapper = Query;
            VRCPlayerApi._GetBonePosition = wrapper;
        }
        if (VRCPlayerApi._GetBonePosition != wrapper) throw new Exception("SDK bone provider changed during fixture");
        foreach (var proxy in inputs)
        {
            var input = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
            var cursor = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy.cursor);
            bool affected = proxy.rightHand == Right && !Unused;
            bool calibrated = Read<bool>(input, "calibrated");
            bool valid = Read<bool>(cursor, "poseValid");
            bool visible = Read<Transform>(cursor, "cursorVisual").gameObject.activeSelf;
            if (Read<bool>(cursor, "clicksAllowed") || Read<bool>(cursor, "selected") || Read<bool>(cursor, "down"))
                throw new Exception("Fault fixture enabled clicks");
            if (stage == 2 && affected)
            {
                if (Read<int>(input, "available") != 13 || Read<bool>(input, "dataReady") || calibrated || valid || visible || Read<bool>(cursor, "tracking"))
                    throw new Exception("Selective loss did not cancel " + missing);
                input.SendCustomEvent("CalibrateNeutral");
                if (Read<bool>(input, "calibrated")) throw new Exception("Calibration accepted missing bone " + missing);
            }
            else
            {
                if (Read<int>(input, "available") != 14 || !Read<bool>(input, "dataReady")) throw new Exception("Unexpected unaffected/recovered bone loss");
                if (stage == 0 || (stage == 3 && affected))
                {
                    if (calibrated || valid || visible) throw new Exception("Unexpected automatic calibration/recovery");
                    input.SendCustomEvent("CalibrateNeutral");
                    if (!Read<bool>(input, "calibrated")) throw new Exception("Explicit calibration failed");
                }
                else
                {
                    float range = Vector3.Distance(Read<Vector3>(cursor, "rawPosition"), Read<Vector3>(cursor, "handRoot"));
                    if (!calibrated || !valid || !visible || float.IsNaN(range) || Mathf.Abs(range - 0.3f) > 0.002f)
                        throw new Exception("Healthy/calibrated hand changed during scenario " + scenario + ", stage " + stage);
                }
            }
        }
        if (stage == 1)
        {
            string name = scenario < 28 ? Required[scenario % 14] : scenario == 28 ? "MiddleDistal" : scenario == 29 ? "ThumbProximal" : "IndexIntermediate";
            missing = (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), (Right ? "Right" : "Left") + name);
            injected = scenario == 28 ? new Vector3(float.NaN, 1, 1) : scenario == 29 ? new Vector3(1, float.PositiveInfinity, 1) : Vector3.zero;
            if (original(Networking.LocalPlayer, missing) == Vector3.zero) throw new Exception("Fault baseline bone is already missing");
            enabled = true;
        }
        else if (stage == 2) enabled = false;
        else if (stage == 4)
        {
            scenario++;
            if (scenario == 31) { Restore(); return true; }
            stage = 0; // next tick verifies calibration then injects the next fault
        }
        stage++;
        next = Time.time + 0.2f;
        return false;
    }

    private static T Read<T>(UdonBehaviour vm, string name) { return (T)vm.GetProgramVariable(name); }
    private static Vector3 Query(VRCPlayerApi player, HumanBodyBones bone)
    {
        return enabled && player != null && player.isLocal && bone == missing ? injected : original(player, bone);
    }
    public static void Restore()
    {
        enabled = false;
        if (original != null && VRCPlayerApi._GetBonePosition == wrapper) VRCPlayerApi._GetBonePosition = original;
        original = null; wrapper = null;
    }
}
#endif
