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
    public float neutralPreviewRange = 0.3f;
    [HideInInspector] public bool calibrated;
    [HideInInspector] public bool dataReady;
    [HideInInspector] public int available;
    [HideInInspector] public bool rangeRejected;
    [HideInInspector] public float measuredRange;
    private float nextSample;
    private float neutralShape;
    private float mappedNeutralDistance;
    private bool calibratedRightHand;
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
            ResetCalibration();
        }
        else
        {
            dataReady = true;
            cursor.points = samples;
            cursor.handRoot = samples[2] * 0.6f + thumbRoot * 0.4f;
            // Required finite input only; clicks are disabled because this is not a tip.
            cursor.indexTip = indexDistal;
            cursor.tracking = true;
            if (calibrated && calibratedRightHand != rightHand) ResetCalibration();
            float length = HandLength();
            if (calibrated && (!Positive(length) || !Positive(neutralShape))) ResetCalibration();
            cursor.rangeDistanceMultiplier = calibrated ? mappedNeutralDistance / (length * neutralShape) : 1;
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
    // Explicitly anchor this pose; never silently calibrate the first observed avatar pose.
    public void CalibrateNeutral()
    {
        ResetCalibration();
        if (!dataReady || cursor == null || cursor.fitter == null || !cursor.fitter.fitValid ||
            !Positive(neutralPreviewRange) || neutralPreviewRange > 3 ||
            !Positive(maximumPreviewRange) || neutralPreviewRange > maximumPreviewRange) return;
        float length = HandLength();
        float distance = (cursor.fitter.center - cursor.handRoot).magnitude;
        if (!Positive(length) || !Positive(distance)) return;
        // Invert the monotonic original range law with a fixed work budget.
        float low = 0, high = 0.2f;
        for (int i = 0; i < 24; i++)
        {
            float middle = (low + high) * 0.5f;
            float far = middle / 0.03f;
            float range = middle + middle * middle / 0.02f + 0.02f * far * far * far * far * far * far;
            if (range < neutralPreviewRange) low = middle; else high = middle;
        }
        mappedNeutralDistance = (low + high) * 0.5f;
        neutralShape = distance / length;
        calibratedRightHand = rightHand;
        calibrated = true;
        nextSample = 0;
    }
    public void ResetCalibration()
    {
        calibrated = false;
        if (cursor != null) { cursor.rangeDistanceMultiplier = 1; cursor.Cancel(); }
    }
    public override void OnAvatarChanged(VRCPlayerApi player)
    {
        if (Utilities.IsValid(player) && player.isLocal) { dataReady = false; ResetCalibration(); }
    }
    private float HandLength()
    {
        // Sum segment lengths, not the bend-dependent proximal-to-distal chord.
        return (samples[3] - samples[4]).magnitude + (samples[4] - samples[5]).magnitude;
    }
    private bool Positive(float value) { return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value); }
    private bool Valid(Vector3 p)
    {
        return p != Vector3.zero && !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}
