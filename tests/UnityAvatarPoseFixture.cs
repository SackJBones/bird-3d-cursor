#if UNITY_EDITOR
using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;

// Controlled simulator articulation only; never persisted into SDK assets or a scene.
public static class UnityAvatarPoseFixture
{
    private static Animator animator;
    private static bool wasEnabled;
    private static Transform[] joints;
    private static Quaternion[] rotations;
    private static Vector3[] originalDistal = new Vector3[2];
    private static float[] lengths = new float[2];
    private static bool[] responded = new bool[2];
    private static int stage;
    private static float next;
    private const string Csv = "../Validation/AvatarPose/metrics.csv";

    public static bool Tick(BirdAvatarInput[] inputs)
    {
        if (Time.time < next) return false;
        var provider = Networking.LocalPlayer.GetClientSimPlayer().GetAvatarDataProvider() as ClientSimPlayerAvatarManager;
        if (provider == null) throw new Exception("Unexpected ClientSim avatar provider");
        if (stage == 0)
        {
            var field = typeof(ClientSimPlayerAvatarManager).GetField("avatarAnimator", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new Exception("SDK animator fixture changed");
            animator = field.GetValue(provider) as Animator;
            if (animator == null) throw new Exception("Missing avatar animator");
            wasEnabled = animator.enabled;
            joints = new Transform[6]; rotations = new Quaternion[6];
            HumanBodyBones[] bones = { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftLittleProximal,
                HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightRingProximal, HumanBodyBones.RightLittleProximal };
            for (int i = 0; i < bones.Length; i++)
            {
                joints[i] = provider.GetBoneTransform(bones[i]);
                if (joints[i] == null) throw new Exception("Missing fixture joint");
                rotations[i] = joints[i].localRotation;
            }
            animator.enabled = false;
            Directory.CreateDirectory("../Validation/AvatarPose");
            File.WriteAllText(Csv, "hand,pose,chain_length_m,distal_displacement_m,fit_valid,cursor_valid,range_rejected,raw_target_m\n");
            foreach (var input in inputs)
            {
                int hand = input.rightHand ? 1 : 0;
                originalDistal[hand] = Bone(input.rightHand, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal);
                lengths[hand] = Chain(input.rightHand);
                var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(input);
                vm.SendCustomEvent("CalibrateNeutral");
                if (!(bool)vm.GetProgramVariable("calibrated")) throw new Exception("Fixture could not calibrate neutral");
            }
        }
        else
        {
            foreach (var input in inputs)
            {
                int hand = input.rightHand ? 1 : 0;
                var vm = UdonSharpEditorUtility.GetBackingUdonBehaviour(input);
                var cursor = UdonSharpEditorUtility.GetBackingUdonBehaviour(input.cursor);
                var fit = UdonSharpEditorUtility.GetBackingUdonBehaviour(input.cursor.fitter);
                if (!(bool)vm.GetProgramVariable("dataReady") || !(bool)vm.GetProgramVariable("calibrated")) throw new Exception("Articulation lost data/calibration");
                if ((bool)cursor.GetProgramVariable("clicksAllowed") || (bool)cursor.GetProgramVariable("selected")) throw new Exception("Articulation enabled clicks");
                float length = Chain(input.rightHand);
                if (Mathf.Abs(length - lengths[hand]) > 0.0002f) throw new Exception("Fixture changed bone-segment lengths");
                float displacement = Vector3.Distance(originalDistal[hand], Bone(input.rightHand, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal));
                bool fitValid = (bool)fit.GetProgramVariable("fitValid");
                bool valid = (bool)cursor.GetProgramVariable("poseValid");
                bool rejected = (bool)vm.GetProgramVariable("rangeRejected");
                float range = Vector3.Distance((Vector3)cursor.GetProgramVariable("rawPosition"), (Vector3)cursor.GetProgramVariable("handRoot"));
                if (float.IsNaN(range) || float.IsInfinity(range)) throw new Exception("Nonfinite articulation result");
                if (((Transform)cursor.GetProgramVariable("cursorVisual")).gameObject.activeSelf != valid || (rejected && valid)) throw new Exception("Preview visibility inconsistent");
                if (stage == 1 || stage == 4)
                {
                    if (!valid || Mathf.Abs(range - 0.3f) > 0.002f || displacement > 0.0002f) throw new Exception("Neutral target/pose failed to restore");
                }
                else
                {
                    if (displacement < 0.001f) throw new Exception("Finger bend did not change SDK bone positions");
                    if (!fitValid || rejected || Mathf.Abs(range - 0.3f) > 0.001f) responded[hand] = true;
                }
                string pose = stage == 1 ? "neutral" : stage == 2 ? "plus15_localZ" : stage == 3 ? "minus15_localZ" : "restored";
                File.AppendAllText(Csv, (input.rightHand ? "right" : "left") + "," + pose + "," + length.ToString("R", CultureInfo.InvariantCulture) + "," +
                    displacement.ToString("R", CultureInfo.InvariantCulture) + "," + fitValid + "," + valid + "," + rejected + "," + range.ToString("R", CultureInfo.InvariantCulture) + "\n");
            }
            if (stage == 4)
            {
                if (!responded[0] || !responded[1]) throw new Exception("One cursor ignored articulated input");
                Restore(); return true;
            }
            float angle = stage == 1 ? 15 : stage == 2 ? -15 : 0;
            for (int i = 0; i < joints.Length; i++) joints[i].localRotation = rotations[i] * Quaternion.Euler(0, 0, angle);
        }
        stage++; next = Time.time + 0.6f;
        return false;
    }
    private static Vector3 Bone(bool right, HumanBodyBones leftBone, HumanBodyBones rightBone)
    {
        return Networking.LocalPlayer.GetBonePosition(right ? rightBone : leftBone);
    }
    private static float Chain(bool right)
    {
        Vector3 p = Bone(right, HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal);
        Vector3 m = Bone(right, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.RightMiddleIntermediate);
        Vector3 d = Bone(right, HumanBodyBones.LeftMiddleDistal, HumanBodyBones.RightMiddleDistal);
        return Vector3.Distance(p, m) + Vector3.Distance(m, d);
    }
    public static void Restore()
    {
        if (joints != null) for (int i = 0; i < joints.Length; i++) if (joints[i] != null) joints[i].localRotation = rotations[i];
        if (animator != null) animator.enabled = wasEnabled;
        animator = null; joints = null; rotations = null;
    }
}
#endif
