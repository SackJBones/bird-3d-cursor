using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Inspection only: no fitting, avatar mutation, calibration, networking or recording.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdLabStatus : UdonSharpBehaviour
{
    public Text status;
    public Transform leftOrigin;
    public Transform rightOrigin;
    private float nextSample;
    private bool announced;

    public override void PostLateUpdate()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        VRCPlayerApi player = Networking.LocalPlayer;
        if (!Utilities.IsValid(player)) return;
        Vector3 left = player.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
        Vector3 right = player.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
        if (leftOrigin != null) leftOrigin.position = left;
        if (rightOrigin != null) rightOrigin.position = right;
        // Text allocation is throttled independently from the live hand markers.
        if (Time.time < nextSample) return;
        nextSample = Time.time + 0.2f;
        if (status != null)
            status.text = "LOCAL PLAYER / " + (player.IsUserInVR() ? "VR" : "DESKTOP") +
                "\nAvatar eye height: " + player.GetAvatarEyeHeightAsMeters().ToString("F2") + " m" +
                "\nTracking origin / wrist separation" +
                "\nL " + Vector3.Distance(left, player.GetBonePosition(HumanBodyBones.LeftHand)).ToString("F3") +
                " m    R " + Vector3.Distance(right, player.GetBonePosition(HumanBodyBones.RightHand)).ToString("F3") +
                " m\nAvailability is not tracking confidence.";
        if (!announced)
        {
            announced = true;
            Debug.Log("BIRD_TRACKING_LAB_READY: v0.2; per-frame post-IK markers; standard world diagnostics active; Bird solver not enabled.");
        }
    }
}
