using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Avatar skeleton adapter. Estimated distal endpoints are never raw tracked joints.
// Calibration and size compensation belong here, outside the geometric Bird solver.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(0)]
public class BirdAvatarHandInput : UdonSharpBehaviour
{
    public BirdCursorState cursor;
    public bool rightHand;
    public Text status;
    [Tooltip("Estimated distal segment length / preceding segment length. Avatar-dependent, not measured fingertips.")]
    [Range(.2f, 1.5f)] public float tipLengthRatio = .8f;
    [Tooltip("Normalize the mean middle/ring/little finger length before the original range law.")]
    public float referenceFingerLength = .09f;
    [HideInInspector] public bool calibrated, dataReady;
    [HideInInspector] public int available, sampledFrame = -1;
    [HideInInspector] public float fingerLength;
    [HideInInspector] public Vector3[] bonePositions = new Vector3[16];
    [HideInInspector] public Vector3[] estimatedTips = new Vector3[5];
    [HideInInspector] public Vector3 normal;
    private Vector3[] fitPoints = new Vector3[16];
    private Quaternion[] distalRotations = new Quaternion[5];
    private Vector3[] tipAxes = new Vector3[5];
    private bool calibratedSide;
    private float calibratedRatio, calibratedReference, nextText;
    private string calibrationMessage = "Open this hand, then SET with the other hand.";
    private int[] bones = new int[] {
        (int)HumanBodyBones.LeftHand,
        (int)HumanBodyBones.LeftThumbProximal, (int)HumanBodyBones.LeftThumbIntermediate, (int)HumanBodyBones.LeftThumbDistal,
        (int)HumanBodyBones.LeftIndexProximal, (int)HumanBodyBones.LeftIndexIntermediate, (int)HumanBodyBones.LeftIndexDistal,
        (int)HumanBodyBones.LeftMiddleProximal, (int)HumanBodyBones.LeftMiddleIntermediate, (int)HumanBodyBones.LeftMiddleDistal,
        (int)HumanBodyBones.LeftRingProximal, (int)HumanBodyBones.LeftRingIntermediate, (int)HumanBodyBones.LeftRingDistal,
        (int)HumanBodyBones.LeftLittleProximal, (int)HumanBodyBones.LeftLittleIntermediate, (int)HumanBodyBones.LeftLittleDistal,
        (int)HumanBodyBones.RightHand,
        (int)HumanBodyBones.RightThumbProximal, (int)HumanBodyBones.RightThumbIntermediate, (int)HumanBodyBones.RightThumbDistal,
        (int)HumanBodyBones.RightIndexProximal, (int)HumanBodyBones.RightIndexIntermediate, (int)HumanBodyBones.RightIndexDistal,
        (int)HumanBodyBones.RightMiddleProximal, (int)HumanBodyBones.RightMiddleIntermediate, (int)HumanBodyBones.RightMiddleDistal,
        (int)HumanBodyBones.RightRingProximal, (int)HumanBodyBones.RightRingIntermediate, (int)HumanBodyBones.RightRingDistal,
        (int)HumanBodyBones.RightLittleProximal, (int)HumanBodyBones.RightLittleIntermediate, (int)HumanBodyBones.RightLittleDistal
    };

    public override void PostLateUpdate()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        sampledFrame = Time.frameCount;
        ReadBones();
        if (calibrated && (!dataReady || calibratedSide != rightHand || calibratedRatio != tipLengthRatio || calibratedReference != referenceFingerLength))
            ResetCalibration();
        if (cursor != null)
        {
            cursor.clicksAllowed = false; // Estimated index endpoint has not been validated for clicking.
            cursor.tracking = calibrated && dataReady;
            if (cursor.tracking)
            {
                BuildPoints();
                cursor.points = fitPoints;
                cursor.handRoot = bonePositions[4] * .6f + bonePositions[1] * .4f;
                cursor.indexTip = estimatedTips[1];
                cursor.palmNormal = normal;
                cursor.useHandLimits = true;
                cursor.rangeDistanceMultiplier = referenceFingerLength / fingerLength;
                // Express the accepted limit-law endpoint in the same normalized units.
                cursor.maximumLimitDistance = 2f / cursor.rangeDistanceMultiplier;
                cursor.Step();
            }
            else cursor.Cancel();
        }
        RefreshText();
    }

    private void ReadBones()
    {
        available = 0; dataReady = false;
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || !Positive(referenceFingerLength) || referenceFingerLength > 1 ||
            !Positive(tipLengthRatio) || tipLengthRatio < .2f || tipLengthRatio > 1.5f) return;
        int start = rightHand ? 16 : 0;
        for (int i = 0; i < 16; i++)
        {
            Vector3 p = player.GetBonePosition((HumanBodyBones)bones[start+i]);
            bonePositions[i] = p;
            if (ValidPosition(p)) available++;
        }
        if (available != 16) return;
        for (int f = 0; f < 5; f++)
        {
            int first = 1+f*3;
            float a = (bonePositions[first+1]-bonePositions[first]).magnitude;
            float b = (bonePositions[first+2]-bonePositions[first+1]).magnitude;
            if (!Positive(a) || !Positive(b) || a < .0001f || b < .0001f) return;
            Quaternion q = player.GetBoneRotation((HumanBodyBones)bones[start+first+2]);
            float sq = q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w;
            if (!Finite(sq) || sq < .5f || sq > 1.5f) return;
            distalRotations[f] = Quaternion.Normalize(q);
        }
        normal = Vector3.Cross(bonePositions[4]-bonePositions[1], bonePositions[13]-bonePositions[1]);
        if (!FiniteVector(normal) || normal.sqrMagnitude < .000000000001f) return;
        normal = normal.normalized * (rightHand ? 1 : -1);
        dataReady = true;
    }

    // Hold all fingers straight: the preceding phalanx gives a calibration axis
    // in each distal bone's local rotation frame. Arbitrary bone axes are supported.
    public void CalibrateOpenHand()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        ResetCalibration(); ReadBones();
        if (!dataReady || cursor == null) { calibrationMessage = "Missing or invalid avatar bones."; return; }
        // Use the wrist for the open-palm check: the thumb base is lateral and
        // can falsely classify a wide, straight hand as folded.
        Vector3 forward = (bonePositions[4]+bonePositions[7]+bonePositions[10]+bonePositions[13])*.25f-bonePositions[0];
        forward -= normal * Vector3.Dot(forward,normal); forward = forward.normalized;
        for (int f = 0; f < 5; f++)
        {
            int first = 1+f*3;
            Vector3 a = (bonePositions[first+1]-bonePositions[first]).normalized;
            Vector3 b = (bonePositions[first+2]-bonePositions[first+1]).normalized;
            if (Vector3.Dot(a,b) < .9f || (f > 0 && (Vector3.Dot(a,forward) < .65f || Mathf.Abs(Vector3.Dot(a,normal)) > .4f)))
            { calibrationMessage = "Straighten fingers and thumb, then SET again."; return; }
            tipAxes[f] = Quaternion.Inverse(distalRotations[f]) * b;
        }
        calibratedSide = rightHand; calibratedRatio = tipLengthRatio; calibratedReference = referenceFingerLength;
        calibrated = true;
        calibrationMessage = "Estimated fingertips / clicks disabled";
        nextText = 0;
    }

    private void BuildPoints()
    {
        fingerLength = 0;
        for (int f = 0; f < 5; f++)
        {
            int first = 1+f*3;
            float preceding = (bonePositions[first+2]-bonePositions[first+1]).magnitude;
            estimatedTips[f] = bonePositions[first+2] + distalRotations[f]*tipAxes[f]*(preceding*tipLengthRatio);
            if (f >= 2) fingerLength += (bonePositions[first+1]-bonePositions[first]).magnitude + preceding*(1+tipLengthRatio);
        }
        fingerLength /= 3;
        fitPoints[0] = bonePositions[2]; fitPoints[1] = bonePositions[3]; fitPoints[2] = estimatedTips[0];
        fitPoints[3] = bonePositions[4];
        for (int f = 2; f < 5; f++)
        {
            int first = 1+f*3, point = 4+(f-2)*4;
            fitPoints[point] = bonePositions[first]; fitPoints[point+1] = bonePositions[first+1];
            fitPoints[point+2] = bonePositions[first+2]; fitPoints[point+3] = estimatedTips[f];
        }
    }

    public void ResetCalibration()
    {
        calibrated = false; nextText = 0;
        calibrationMessage = "Open this hand, then SET with the other hand.";
        if (cursor != null) { cursor.tracking = false; cursor.Cancel(); }
    }
    public override void OnAvatarChanged(VRCPlayerApi player)
    { if (Utilities.IsValid(player) && player.isLocal) { dataReady = false; ResetCalibration(); } }
    private void OnDisable() { dataReady = false; available = 0; sampledFrame = -1; ResetCalibration(); RefreshText(); }
    private void RefreshText()
    {
        if (status == null || Time.time < nextText) return;
        nextText = Time.time + .2f;
        status.text = (rightHand ? "RIGHT" : "LEFT")+" / AVATAR BIRD\n"+available+"/16 bones / "+
            (!calibrated ? "Awaiting calibration" : cursor != null && cursor.poseValid ? "Bird active" : "No valid Bird point")+"\n"+calibrationMessage;
    }
    private bool Positive(float f) { return Finite(f) && f > 0; }
    private bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    private bool FiniteVector(Vector3 p) { return Finite(p.x) && Finite(p.y) && Finite(p.z); }
    private bool ValidPosition(Vector3 p) { return p != Vector3.zero && FiniteVector(p); }
}
