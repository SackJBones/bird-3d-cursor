using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

// Avatar bone diagnostic only. These are not raw tracked joints or fingertip endpoints.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdHandDataProbe : UdonSharpBehaviour
{
    public Transform[] markers;
    public Text status;
    [HideInInspector] public int leftAvailable;
    [HideInInspector] public int rightAvailable;
    private float nextSample;
    private int[] bones = new int[] {
        (int)HumanBodyBones.LeftHand,
        (int)HumanBodyBones.LeftThumbProximal,
        (int)HumanBodyBones.LeftThumbIntermediate,
        (int)HumanBodyBones.LeftThumbDistal,
        (int)HumanBodyBones.LeftIndexProximal,
        (int)HumanBodyBones.LeftIndexIntermediate,
        (int)HumanBodyBones.LeftIndexDistal,
        (int)HumanBodyBones.LeftMiddleProximal,
        (int)HumanBodyBones.LeftMiddleIntermediate,
        (int)HumanBodyBones.LeftMiddleDistal,
        (int)HumanBodyBones.LeftRingProximal,
        (int)HumanBodyBones.LeftRingIntermediate,
        (int)HumanBodyBones.LeftRingDistal,
        (int)HumanBodyBones.LeftLittleProximal,
        (int)HumanBodyBones.LeftLittleIntermediate,
        (int)HumanBodyBones.LeftLittleDistal,
        (int)HumanBodyBones.RightHand,
        (int)HumanBodyBones.RightThumbProximal,
        (int)HumanBodyBones.RightThumbIntermediate,
        (int)HumanBodyBones.RightThumbDistal,
        (int)HumanBodyBones.RightIndexProximal,
        (int)HumanBodyBones.RightIndexIntermediate,
        (int)HumanBodyBones.RightIndexDistal,
        (int)HumanBodyBones.RightMiddleProximal,
        (int)HumanBodyBones.RightMiddleIntermediate,
        (int)HumanBodyBones.RightMiddleDistal,
        (int)HumanBodyBones.RightRingProximal,
        (int)HumanBodyBones.RightRingIntermediate,
        (int)HumanBodyBones.RightRingDistal,
        (int)HumanBodyBones.RightLittleProximal,
        (int)HumanBodyBones.RightLittleIntermediate,
        (int)HumanBodyBones.RightLittleDistal
    };

    private void Update()
    {
        if (Time.time < nextSample) return;
        nextSample = Time.time + 0.1f;
        leftAvailable = 0;
        rightAvailable = 0;
        VRCPlayerApi player = Networking.LocalPlayer;
        bool available = Utilities.IsValid(player);
        for (int i = 0; i < bones.Length; i++)
        {
            Vector3 position = available ? player.GetBonePosition((HumanBodyBones)bones[i]) : Vector3.zero;
            // Zero is the API's missing-bone sentinel; a bone exactly at world origin is ambiguous.
            bool valid = position != Vector3.zero &&
                !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
                !float.IsNaN(position.y) && !float.IsInfinity(position.y) &&
                !float.IsNaN(position.z) && !float.IsInfinity(position.z);
            if (valid) { if (i < 16) leftAvailable++; else rightAvailable++; }
            if (markers != null && i < markers.Length && markers[i] != null)
            {
                markers[i].gameObject.SetActive(valid);
                if (valid) markers[i].position = position;
            }
        }
        if (status != null)
        {
            if (!available) status.text = "BIRD / HAND DATA PROBE\nWaiting for local player";
            else status.text = "BIRD / AVATAR BONE PROBE\nLeft " + leftAvailable + "/16  Right " + rightAvailable +
                "/16\nVR mode: " + player.IsUserInVR() + "\nBones are not raw tracked joints or fingertips";
        }
    }

    private void OnDisable()
    {
        leftAvailable = rightAvailable = 0;
        if (markers != null) for (int i = 0; i < markers.Length; i++)
            if (markers[i] != null) markers[i].gameObject.SetActive(false);
        if (status != null) status.text = "BIRD / HAND DATA PROBE\nDisabled";
    }
}
