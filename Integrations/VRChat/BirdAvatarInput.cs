using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Experimental bone-origin approximation, NOT raw hand tracking. No fingertip inference/clicks.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdAvatarInput : UdonSharpBehaviour
{
    public BirdCursorState cursor;
    public bool rightHand;
    public Text label;
    public float maximumPreviewRange = 3;
    [HideInInspector] public bool dataReady;
    [HideInInspector] public int available;
    [HideInInspector] public bool rangeRejected;
    [HideInInspector] public float measuredRange;
    private float nextSample;
    private Vector3[] samples = new Vector3[12];
    private int[] bones = new int[] {
        (int)HumanBodyBones.LeftThumbIntermediate, (int)HumanBodyBones.LeftThumbDistal,
        (int)HumanBodyBones.LeftIndexProximal,
        (int)HumanBodyBones.LeftMiddleProximal, (int)HumanBodyBones.LeftMiddleIntermediate, (int)HumanBodyBones.LeftMiddleDistal,
        (int)HumanBodyBones.LeftRingProximal, (int)HumanBodyBones.LeftRingIntermediate, (int)HumanBodyBones.LeftRingDistal,
        (int)HumanBodyBones.LeftLittleProximal, (int)HumanBodyBones.LeftLittleIntermediate, (int)HumanBodyBones.LeftLittleDistal,
        (int)HumanBodyBones.LeftThumbProximal, (int)HumanBodyBones.LeftIndexDistal,
        (int)HumanBodyBones.RightThumbIntermediate, (int)HumanBodyBones.RightThumbDistal,
        (int)HumanBodyBones.RightIndexProximal,
        (int)HumanBodyBones.RightMiddleProximal, (int)HumanBodyBones.RightMiddleIntermediate, (int)HumanBodyBones.RightMiddleDistal,
        (int)HumanBodyBones.RightRingProximal, (int)HumanBodyBones.RightRingIntermediate, (int)HumanBodyBones.RightRingDistal,
        (int)HumanBodyBones.RightLittleProximal, (int)HumanBodyBones.RightLittleIntermediate, (int)HumanBodyBones.RightLittleDistal,
        (int)HumanBodyBones.RightThumbProximal, (int)HumanBodyBones.RightIndexDistal
    };
    private void Update()
    {
        if (Time.time < nextSample) return;
        nextSample = Time.time + 0.03f;
        dataReady = false;
        rangeRejected = false;
        measuredRange = 0;
        available = 0;
        if (cursor == null) return;
        cursor.clicksAllowed = false;
        VRCPlayerApi player = Networking.LocalPlayer;
        int start = rightHand ? 14 : 0;
        Vector3 thumbRoot = Vector3.zero;
        Vector3 indexDistal = Vector3.zero;
        if (Utilities.IsValid(player))
        {
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = player.GetBonePosition((HumanBodyBones)bones[start + i]);
                if (Valid(p)) available++;
                if (i < 12) samples[i] = p;
                else if (i == 12) thumbRoot = p;
                else indexDistal = p;
            }
        }
        if (available != 14)
        {
            cursor.tracking = false;
            cursor.Cancel();
        }
        else
        {
            dataReady = true;
            cursor.points = samples;
            cursor.handRoot = samples[2] * 0.6f + thumbRoot * 0.4f;
            // Required finite input only; clicks are disabled because this is not a tip.
            cursor.indexTip = indexDistal;
            cursor.tracking = true;
            cursor.Step();
            if (cursor.poseValid)
            {
                measuredRange = (cursor.position - cursor.handRoot).magnitude;
                // Preview guard, not a replacement for avatar calibration or Bird's range law.
                if (float.IsNaN(maximumPreviewRange) || float.IsInfinity(maximumPreviewRange) || maximumPreviewRange <= 0 ||
                    float.IsNaN(measuredRange) || float.IsInfinity(measuredRange) || measuredRange > maximumPreviewRange)
                { rangeRejected = true; cursor.Cancel(); }
            }
        }
        if (label != null) label.text = "EXPERIMENTAL AVATAR BONES\n" + (rightHand ? "Right " : "Left ") + available +
            "/14  " + (rangeRejected ? "Range rejected: calibrate avatar" : cursor.poseValid ? "Fit accepted" : "No valid fit") + "\nClicks disabled / no tracked fingertips";
    }
    private bool Valid(Vector3 p)
    {
        return p != Vector3.zero && !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}
