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
    [Tooltip("Use estimated fingertips immediately, then learn each distal bone axis as that finger naturally straightens. SET remains an optional override.")]
    public bool automaticSetup = true;
    [Tooltip("Geometric distal-joint continuation before its axis is learned. This is an endpoint estimate, not a Bird openness signal.")]
    [Range(0,1)] public float estimatedDistalBendRatio = .7f;
    [Tooltip("Estimated distal segment length / preceding segment length. Avatar-dependent, not measured fingertips.")]
    [Range(.2f, 1.5f)] public float tipLengthRatio = .8f;
    [Tooltip("Normalize the mean middle/ring/little finger length before the original range law.")]
    public float referenceFingerLength = .09f;
    [Tooltip("Pinky share of the 60% knuckle contribution to the ray origin. 0 = classic index/thumb; .5 = 30% index, 30% pinky, 40% thumb.")]
    [Range(0, 1)] public float littleFingerRootShare;
    [HideInInspector] public bool calibrated, dataReady, tipsReady;
    [HideInInspector] public int available, sampledFrame = -1;
    [HideInInspector] public int calibrationRevision;
    [HideInInspector] public int learnedFingers;
    [HideInInspector] public float fingerLength;
    [HideInInspector] public Vector3[] bonePositions = new Vector3[16];
    [HideInInspector] public Vector3[] estimatedTips = new Vector3[5];
    [HideInInspector] public Vector3 normal;
    private Vector3[] fitPoints = new Vector3[16];
    private Quaternion[] distalRotations = new Quaternion[5];
    private Vector3[] tipAxes = new Vector3[5];
    private bool[] learnedAxes = new bool[5];
    private bool settingsBound, boundAutomatic;
    private float boundBendRatio;
    private bool calibratedSide;
    private float calibratedRatio, calibratedReference, nextText;
    private float boundRootShare;
    private float lastSampleTime = -1;
    private string calibrationMessage = "Estimated fingertips / clicks disabled";
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
        float now = Time.realtimeSinceStartup;
        // Suspension can stop Udon entirely, so no invalid sample is guaranteed.
        // Re-seed the old filter after a long gap while keeping avatar calibration.
        bool gap=lastSampleTime>=0 && (now<lastSampleTime || now-lastSampleTime>.25f);
        if(cursor!=null)
        {
            if(gap) cursor.Cancel();
            // A fresh seed has no elapsed filter interval. Do not integrate a suspension.
            cursor.sampleDeltaTime=lastSampleTime<0 || gap?1f/72:now-lastSampleTime;
        }
        lastSampleTime = now;
        ReadBones();
        // Origin selection changes geometry, not fingertip calibration. Rebase
        // temporal and contact history so it cannot become a gesture sweep.
        if(boundRootShare!=littleFingerRootShare)
        { boundRootShare=littleFingerRootShare; if(cursor!=null) cursor.Cancel(); }
        // Axis estimates belong to one avatar/hand/settings binding. Automatic
        // startup is usable immediately; it never records a curled first pose as
        // an open-hand calibration.
        if (!settingsBound || calibratedSide != rightHand || calibratedRatio != tipLengthRatio ||
            calibratedReference != referenceFingerLength || boundAutomatic != automaticSetup || boundBendRatio != estimatedDistalBendRatio)
        {
            ClearCalibration("Estimated fingertips / clicks disabled");
            settingsBound=true; calibratedSide=rightHand; calibratedRatio=tipLengthRatio;
            calibratedReference=referenceFingerLength; boundAutomatic=automaticSetup; boundBendRatio=estimatedDistalBendRatio;
        }
        tipsReady=dataReady && (calibrated || automaticSetup);
        if (cursor != null)
        {
            cursor.clicksAllowed = false; // Estimated index endpoint has not been validated for clicking.
            cursor.tracking = tipsReady;
            if (cursor.tracking)
            {
                BuildPoints();
                cursor.points = fitPoints;
                cursor.handRoot = (bonePositions[4]*(1-littleFingerRootShare)+bonePositions[13]*littleFingerRootShare)*.6f+bonePositions[1]*.4f;
                cursor.useExplicitThumbBase=true;
                cursor.thumbBase=bonePositions[1];
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
            !Positive(tipLengthRatio) || tipLengthRatio < .2f || tipLengthRatio > 1.5f ||
            !Finite(littleFingerRootShare) || littleFingerRootShare<0 || littleFingerRootShare>1 ||
            !Finite(estimatedDistalBendRatio) || estimatedDistalBendRatio<0 || estimatedDistalBendRatio>1) return;
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
        // Use the rigid palm, not the opposable thumb, to define its normal.
        // Thumb motion still contributes to the reference sphere and root; it
        // must not independently rotate the far-limit reference frame.
        Vector3 knuckles=(bonePositions[4]+bonePositions[7]+bonePositions[10]+bonePositions[13])*.25f;
        normal = Vector3.Cross(knuckles-bonePositions[0], bonePositions[13]-bonePositions[4]);
        if (!FiniteVector(normal) || normal.sqrMagnitude < .000000000001f) return;
        // Ordered wrist/knuckle winding faces the BACK of a right hand.
        // Unlike the OpenXR host, avatar input has no tracked palm rotation to
        // repair a reversed sign. Keep this anatomical handedness explicit.
        normal = normal.normalized * (rightHand ? -1 : 1);
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
            { calibrationMessage = automaticSetup?"Automatic estimates active / optional REFINE needs straight fingers":"Straighten fingers and thumb, then SET again."; return; }
            tipAxes[f] = Quaternion.Inverse(distalRotations[f]) * b;
        }
        calibratedSide = rightHand; calibratedRatio = tipLengthRatio; calibratedReference = referenceFingerLength;
        settingsBound=true; boundAutomatic=automaticSetup; boundBendRatio=estimatedDistalBendRatio;
        calibrated = true; learnedFingers=5; tipsReady=true;
        calibrationMessage = "Estimated fingertips / clicks disabled";
        nextText = 0;
    }

    private void BuildPoints()
    {
        fingerLength = 0;
        bool learned=false;
        Vector3 forward=(bonePositions[4]+bonePositions[7]+bonePositions[10]+bonePositions[13])*.25f-bonePositions[0];
        forward=(forward-normal*Vector3.Dot(forward,normal)).normalized;
        for (int f = 0; f < 5; f++)
        {
            int first = 1+f*3;
            float preceding = (bonePositions[first+2]-bonePositions[first+1]).magnitude;
            Vector3 direction;
            if(calibrated || learnedAxes[f]) direction=distalRotations[f]*tipAxes[f];
            else
            {
                Vector3 a=(bonePositions[first+1]-bonePositions[first]).normalized;
                Vector3 b=(bonePositions[first+2]-bonePositions[first+1]).normalized;
                direction=EstimateDirection(a,b);
                // Learn independently, only near extension. MCP checks prevent
                // a folded hand with nearly parallel phalanges from qualifying.
                if(Vector3.Dot(a,b)>.995f && (f==0 || (Vector3.Dot(a,forward)>.65f && Mathf.Abs(Vector3.Dot(a,normal))<.4f)))
                {
                    // The learning frame uses the same geometric endpoint, so
                    // adopting the axis cannot jump the current point. Thereafter
                    // actual distal rotation also captures independent DIP motion.
                    tipAxes[f]=Quaternion.Inverse(distalRotations[f])*direction;
                    learnedAxes[f]=true; learnedFingers++; learned=true;
                }
            }
            estimatedTips[f] = bonePositions[first+2] + direction*(preceding*tipLengthRatio);
            if (f >= 2) fingerLength += (bonePositions[first+1]-bonePositions[first]).magnitude + preceding*(1+tipLengthRatio);
        }
        if(learned) AdvanceCalibrationHistory();
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

    private Vector3 EstimateDirection(Vector3 a,Vector3 b)
    {
        // Continue the observed joint bend on the unit sphere. No Euler chart,
        // assumed bone-local axis, wrist/torso extrapolation or world-up axis.
        float cosine=Mathf.Clamp(Vector3.Dot(a,b),-1,1);
        Vector3 tangent=b*cosine-a;
        float sine=tangent.magnitude;
        // Coincident segments are straight; an exact fold has no unique bend
        // plane, so retain the last observed segment as the conservative estimate.
        if(sine<.000001f) return b;
        float turn=estimatedDistalBendRatio*Mathf.Atan2(sine,cosine);
        return (b*Mathf.Cos(turn)+tangent*(Mathf.Sin(turn)/sine)).normalized;
    }
    public void ResetCalibration()
    { ClearCalibration(automaticSetup?"Automatic fingertips resumed / clicks disabled":"Open this hand, then SET with the other hand."); }
    private void AdvanceCalibrationHistory()
    {
        calibrationRevision=calibrationRevision==int.MaxValue?0:calibrationRevision+1;
        if(cursor!=null) cursor.Cancel();
    }
    private void ClearCalibration(string message)
    {
        AdvanceCalibrationHistory();
        calibrated=tipsReady=false; learnedFingers=0;
        for(int f=0;f<5;f++) learnedAxes[f]=false;
        nextText = 0; lastSampleTime = -1; calibrationMessage = message;
        if(cursor!=null) cursor.tracking=false;
    }
    public override void OnAvatarChanged(VRCPlayerApi player)
    { if (Utilities.IsValid(player) && player.isLocal) { dataReady = false; ClearCalibration("New avatar / fingertip estimates restarted"); } }
    private void OnDisable() { dataReady = false; available = 0; sampledFrame = -1; ClearCalibration("Input disabled"); RefreshText(); }
    private void RefreshText()
    {
        if (status == null || Time.time < nextText) return;
        nextText = Time.time + .2f;
        string state=!dataReady?(calibrated?"Paused / correction retained":"Waiting for avatar bones"):
            cursor!=null && cursor.poseValid?"Bird active":automaticSetup?"Estimating fingertips":"Awaiting optional SET";
        string mode=calibrated?"Manual correction":automaticSetup?"Automatic / "+learnedFingers+" of 5 axes learned":"Manual-only input";
        status.text=(rightHand?"RIGHT":"LEFT")+" / AVATAR BIRD\n"+available+"/16 bones / "+state+"\n"+mode+"\n"+calibrationMessage;
        if (tipsReady && cursor != null && cursor.poseValid)
        {
            status.text += "\nDesired "+RangeLabel((cursor.rawPosition-cursor.handRoot).magnitude)+" / shown "+RangeLabel((cursor.position-cursor.handRoot).magnitude);
            status.text += cursor.fitter.fitValid ? "\nFit radius "+cursor.fitter.radius.ToString("F3")+" m" : "\nFit singular";
            status.text += " / range limit "+(cursor.limitWeight*100).ToString("F0")+"% / aim fix "+(cursor.insideOutWeight*100).ToString("F0")+"%";
        }
    }
    private string RangeLabel(float value)
    {
        if (value >= 1000000) return (value/1000000).ToString("F1")+" Mm";
        if (value >= 1000) return (value/1000).ToString("F1")+" km";
        return value.ToString("F2")+" m";
    }
    private bool Positive(float f) { return Finite(f) && f > 0; }
    private bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
    private bool FiniteVector(Vector3 p) { return Finite(p.x) && Finite(p.y) && Finite(p.z); }
    private bool ValidPosition(Vector3 p) { return p != Vector3.zero && FiniteVector(p); }
}
