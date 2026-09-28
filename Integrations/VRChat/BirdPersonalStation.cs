using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// An unlimited, local opt-in station. No pickup ownership, inventory or networking.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(-100)]
public class BirdPersonalStation : UdonSharpBehaviour
{
    public GameObject personalRig;
    public Transform touchPoint;
    public Text label;
    public BirdCursorState left;
    public BirdCursorState right;
    public float touchRadius = .16f;
    public float touchSeconds = .12f;
    [Tooltip("Leave this area with your body before returning can put Bird away.")]
    public float leaveRadius = 2.2f;
    public float returnRadius = 1.1f;
    [HideInInspector] public bool acquired;
    [HideInInspector] public bool returnArmed;
    private float touchingSince = -1;
    private float nextLabel;
    private bool mustWithdraw;
    private float nextAcquire;

    private void Start()
    {
        if (personalRig != null) personalRig.SetActive(acquired);
        RefreshLabel();
    }
    public override void Interact()
    {
        if (acquired)
        {
            var player = Networking.LocalPlayer;
            if (returnArmed && Utilities.IsValid(player) && BodyDistance(player) <= returnRadius) PutAway();
        }
        else if (!mustWithdraw && Time.time >= nextAcquire) TakeBird();
    }
    public void TakeBird()
    {
        if (personalRig == null || acquired) return;
        acquired = true;
        returnArmed = false;
        mustWithdraw = true;
        touchingSince = -1;
        personalRig.SetActive(true);
        RefreshLabel();
    }
    public void PutAway()
    {
        acquired = false;
        returnArmed = false;
        mustWithdraw = true;
        nextAcquire = Time.time + 1;
        touchingSince = -1;
        if (left != null) left.Cancel();
        if (right != null) right.Cancel();
        if (personalRig != null) personalRig.SetActive(false);
        RefreshLabel();
    }
    public override void PostLateUpdate()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        if (touchPoint != null)
        {
            // Anatomical palm origin, independent of controller grip pose and
            // cursor range. Merely pointing at the pedestal cannot collect Bird.
            bool touching = Touching(player, false) || Touching(player, true);
            float distance = BodyDistance(player);
            if (acquired && distance > leaveRadius) returnArmed = true;
            if (!touching) { mustWithdraw = false; touchingSince = -1; }
            else if (!mustWithdraw && (acquired ? returnArmed && distance <= returnRadius : Time.time >= nextAcquire))
            {
                if (touchingSince < 0) touchingSince = Time.time;
                if (Time.time - touchingSince >= touchSeconds)
                {
                    if (acquired) PutAway(); else TakeBird();
                }
            }
            else touchingSince = -1;
        }
        if (Time.time >= nextLabel) { nextLabel = Time.time + .25f; RefreshLabel(); }
    }
    private float BodyDistance(VRCPlayerApi player)
    {
        if (touchPoint == null) return float.MaxValue;
        // Track physical room-scale departure as well as locomotion. A hand
        // withdrawal alone must never arm the pedestal's put-away action.
        Vector3 delta = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position - touchPoint.position; delta.y = 0;
        return delta.magnitude;
    }
    private bool Touching(VRCPlayerApi player, bool rightHand)
    {
        Vector3 index = player.GetBonePosition(rightHand ? HumanBodyBones.RightIndexProximal : HumanBodyBones.LeftIndexProximal);
        Vector3 pinky = player.GetBonePosition(rightHand ? HumanBodyBones.RightLittleProximal : HumanBodyBones.LeftLittleProximal);
        Vector3 thumb = player.GetBonePosition(rightHand ? HumanBodyBones.RightThumbProximal : HumanBodyBones.LeftThumbProximal);
        if (index == Vector3.zero || pinky == Vector3.zero || thumb == Vector3.zero) return false;
        return Vector3.Distance(index * .3f + pinky * .3f + thumb * .4f, touchPoint.position) <= touchRadius;
    }
    private void RefreshLabel()
    {
        if (label == null) return;
        label.text = !acquired ? "Take a Bird\nTouch the light, or Use" :
            (left != null && left.poseValid) || (right != null && right.poseValid) ?
            "Bird is yours\nOpen your hands and explore\nReturn here to put it away" :
            "Bird is yours\nShow your hands to begin\nReturn here to put it away";
    }
}
