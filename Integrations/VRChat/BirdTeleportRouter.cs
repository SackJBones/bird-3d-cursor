using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Local finite-point targeting and locomotion policy, separate from Bird geometry.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(150)]
public class BirdTeleportRouter : UdonSharpBehaviour
{
    public BirdPersonalStation station;
    public BirdUiPointer[] pointers = new BirdUiPointer[0];
    public BirdTeleportBeacon[] beacons = new BirdTeleportBeacon[0];
    [Tooltip("Leave off until this input source's clicks are validated. Hover remains available.")]
    public bool allowTeleport;
    public bool automatic = true;
    public float minimumClearanceHeight = 1.75f;
    [HideInInspector] public int travelRequests;
    [HideInInspector] public BirdTeleportBeacon lastRequested;
    private BirdUiPointer[] bindings = new BirdUiPointer[0];
    private BirdTeleportBeacon[] targets = new BirdTeleportBeacon[0];
    private int[] revisions = new int[0];
    private bool[] released = new bool[0];
    private int automaticFrame = -1;
    private bool boundPermission;
    private float nextTravel;

    public override void PostLateUpdate()
    {
        if (!automatic || automaticFrame == Time.frameCount) return;
        automaticFrame = Time.frameCount;
        Process();
    }
    public void ResetContact()
    {
        if (bindings.Length != pointers.Length)
        {
            bindings = new BirdUiPointer[pointers.Length];
            targets = new BirdTeleportBeacon[pointers.Length];
            revisions = new int[pointers.Length];
            released = new bool[pointers.Length];
        }
        boundPermission = allowTeleport;
        for (int i = 0; i < pointers.Length; i++)
        {
            bindings[i] = pointers[i]; targets[i] = null; released[i] = false;
            revisions[i] = pointers[i] == null ? 0 : pointers[i].revision;
        }
        foreach (var beacon in beacons) if (beacon != null) beacon.ShowState(0);
    }
    public void Process()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        if (bindings.Length != pointers.Length || boundPermission != allowTeleport) ResetContact();
        foreach (var beacon in beacons) if (beacon != null) beacon.ShowState(0);
        var player = Networking.LocalPlayer;
        if (station == null || !station.enabled || !station.gameObject.activeInHierarchy || !station.acquired || !Utilities.IsValid(player))
        { ResetContact(); return; }
        float eyeHeight = player.GetAvatarEyeHeightAsMeters();
        float height = Mathf.Max(minimumClearanceHeight, eyeHeight + .25f);
        if (!Finite(eyeHeight) || eyeHeight <= 0 || !Finite(minimumClearanceHeight) || minimumClearanceHeight < 1.75f || !Finite(height))
        { ResetContact(); return; }
        for (int i = 0; i < pointers.Length; i++)
        {
            var pointer = pointers[i];
            bool bindingChanged = bindings[i] != pointer;
            bool fresh = !bindingChanged && pointer != null && revisions[i] != pointer.revision;
            bindings[i] = pointer;
            if (pointer != null) revisions[i] = pointer.revision;
            if (!fresh || !pointer.IsTracked() || pointer.uiConsumed)
            { targets[i] = null; released[i] = false; continue; }
            BirdTeleportBeacon candidate = null;
            float nearest = float.PositiveInfinity;
            foreach (var beacon in beacons)
            {
                if (beacon != null && beacon.PointThrough(pointer.origin, pointer.position) && beacon.hitDistance < nearest)
                { candidate = beacon; nearest = beacon.hitDistance; }
            }
            bool same = candidate != null && candidate == targets[i] && pointer.hasHistory;
            if (!same) released[i] = false;
            targets[i] = candidate;
            if (candidate == null) continue;
            pointer.uiConsumed = true;
            bool safe = candidate.CheckLanding(height);
            candidate.ShowState(safe ? 1 : 2);
            // A held press, first tracked sample, target switch or stale frame
            // cannot turn a hover into a trip. Require a released hover first.
            bool activate = same && released[i] && pointer.pressedThisSample;
            if (!pointer.pressed) released[i] = true;
            else if (pointer.pressedThisSample) released[i] = false;
            if (!activate || !safe || !allowTeleport || Time.realtimeSinceStartup < nextTravel) continue;
            // Recheck at the action boundary; another consumer may move geometry.
            if (!candidate.CheckLanding(height)) continue;
            lastRequested = candidate;
            travelRequests++;
            nextTravel = Time.realtimeSinceStartup + .8f;
            player.TeleportTo(candidate.landingPoint, player.GetRotation(), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
            player.SetVelocity(Vector3.zero);
            if (station.left != null) station.left.Cancel();
            if (station.right != null) station.right.Cancel();
            foreach (var p in pointers) if (p != null) p.Cancel();
            ResetContact();
            return; // At most one local trip even if both hands press together.
        }
    }
    private void OnDisable() { ResetContact(); }
    private bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
}
