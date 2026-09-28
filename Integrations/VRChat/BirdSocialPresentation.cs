using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// Optional observer-only replication. Put this on a VRCPlayerObject template.
// Its owner publishes the local station's geometric points; each remote copy
// feeds passive point states. It never writes to the owner's input or solver.
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
[DefaultExecutionOrder(50)]
public class BirdSocialPresentation : UdonSharpBehaviour
{
    public BirdPersonalStation station;
    public BirdCursorState remoteLeft;
    public BirdCursorState remoteRight;
    public BirdPointPresentation localLeftView, localRightView;
    public BirdPointPresentation remoteLeftView, remoteRightView;
    [Range(.05f, .5f)] public float sendInterval = .1f;
    [Range(.5f, 5f)] public float staleSeconds = 1.5f;
    [Range(0, .2f)] public float interpolationSeconds = .1f;

    [UdonSynced] private int visibleMask;
    [UdonSynced] private Vector3 leftRoot, rightRoot, leftPoint, rightPoint;
    [UdonSynced] private int leftRevision, rightRevision;
    [UdonSynced] private double sampleTime;
    [UdonSynced] private int sequence;
    private float nextSend, expires, blendStart;
    private bool forcePublish = true;
    private int sentMask = -1, acceptedSequence, acceptedMask;
    private int paletteOwner = -1;
    private int[] revisions = new int[2];
    private Vector3[] fromRoots = new Vector3[2], toRoots = new Vector3[2];
    private Vector3[] fromPoints = new Vector3[2], toPoints = new Vector3[2];

    public override void PostLateUpdate()
    {
        if (!Utilities.IsValid(Networking.LocalPlayer)) { Hide(); return; }
        var owner = Networking.GetOwner(gameObject);
        if (!Utilities.IsValid(owner)) { Hide(); return; }
        ApplyPalette(owner);
        if (owner.isLocal)
        {
            // The original local presentation remains immediate. No duplicate.
            Hide();
            int mask = LocalMask();
            if (Time.realtimeSinceStartup >= nextSend && !Networking.IsClogged &&
                (mask != 0 || mask != sentMask || forcePublish))
            {
                nextSend = Time.realtimeSinceStartup + Mathf.Max(.05f, sendInterval);
                RequestSerialization();
            }
            return;
        }
        if (Time.realtimeSinceStartup >= expires) { Hide(); return; }
        float t = interpolationSeconds <= 0 ? 1 : Mathf.Clamp01((Time.realtimeSinceStartup - blendStart) / interpolationSeconds);
        Present(remoteLeft, 0, t);
        Present(remoteRight, 1, t);
    }

    private void ApplyPalette(VRCPlayerApi owner)
    {
        if (paletteOwner == owner.playerId) return;
        paletteOwner = owner.playerId;
        // Deterministic per visitor in this instance. Contrasting companion
        // hues distinguish hands; first pair preserves cyan/pink. The golden-
        // ratio step varies the whole pair for consecutively joining visitors.
        float hue = Mathf.Repeat(.5f + (owner.playerId - 1) * .618034f, 1);
        Color left = Color.HSVToRGB(hue, 1, 1);
        Color right = Color.HSVToRGB(Mathf.Repeat(hue + .42f, 1), .75f, 1);
        if (remoteLeftView != null) remoteLeftView.tint = left;
        if (remoteRightView != null) remoteRightView.tint = right;
        if (!owner.isLocal) return;
        if (localLeftView != null) localLeftView.tint = left;
        if (localRightView != null) localRightView.tint = right;
    }

    private int LocalMask()
    {
        if (station == null || !station.acquired || station.personalRig == null || !station.personalRig.activeInHierarchy) return 0;
        return (Usable(station.left) ? 1 : 0) | (Usable(station.right) ? 2 : 0);
    }
    private bool Usable(BirdCursorState cursor)
    {
        return cursor != null && cursor.enabled && cursor.gameObject.activeInHierarchy && cursor.poseValid &&
            Finite(cursor.position) && Finite(cursor.handRoot);
    }
    private bool Finite(Vector3 point)
    {
        // A generous numerical envelope, not Bird's interaction range cap.
        // Prevent malformed snapshots from overflowing presentation arithmetic.
        return Mathf.Abs(point.x) < 1e12f && Mathf.Abs(point.y) < 1e12f && Mathf.Abs(point.z) < 1e12f;
    }

    public override void OnPreSerialization()
    {
        if (!Networking.IsOwner(gameObject)) return;
        visibleMask = LocalMask();
        leftRoot = leftPoint = rightRoot = rightPoint = Vector3.zero;
        if ((visibleMask & 1) != 0)
        {
            leftRoot = station.left.handRoot; leftPoint = station.left.position;
            leftRevision = station.left.historyRevision;
        }
        if ((visibleMask & 2) != 0)
        {
            rightRoot = station.right.handRoot; rightPoint = station.right.position;
            rightRevision = station.right.historyRevision;
        }
        sampleTime = Networking.GetServerTimeInSeconds();
        sequence++;
    }
    public override void OnPostSerialization(SerializationResult result)
    {
        forcePublish = !result.success;
        if (result.success) sentMask = visibleMask;
    }
    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject)) forcePublish = true;
    }
    public override void OnDeserialization()
    {
        var owner = Networking.GetOwner(gameObject);
        if (!Utilities.IsValid(owner) || owner.isLocal) return;
        // Ignore duplicates/out-of-order packets, including packets arriving
        // after a newer put-away. Repeated stale data cannot renew visibility.
        if (sequence <= acceptedSequence) return;
        acceptedSequence = sequence;
        double age = Networking.GetServerTimeInSeconds() - sampleTime;
        if (double.IsNaN(age) || double.IsInfinity(age) || age < -.5 || age >= staleSeconds || (visibleMask & ~3) != 0)
        {
            Hide(); return;
        }
        bool gap = Time.realtimeSinceStartup >= expires;
        Accept(remoteLeft, 0, leftRoot, leftPoint, leftRevision, (visibleMask & 1) != 0, gap);
        Accept(remoteRight, 1, rightRoot, rightPoint, rightRevision, (visibleMask & 2) != 0, gap);
        blendStart = Time.realtimeSinceStartup;
        expires = blendStart + staleSeconds - (float)Mathf.Max(0, (float)age);
    }
    private void Accept(BirdCursorState cursor, int side, Vector3 root, Vector3 point, int revision, bool visible, bool gap)
    {
        int bit = 1 << side;
        if (cursor == null) return;
        if (!visible || !Finite(root) || !Finite(point))
        {
            ClearPoint(cursor); acceptedMask &= ~bit; return;
        }
        bool reset = gap || (acceptedMask & bit) == 0 || revisions[side] != revision || (root - toRoots[side]).sqrMagnitude > 4;
        if (reset) ClearPoint(cursor);
        fromRoots[side] = reset ? root : cursor.handRoot;
        fromPoints[side] = reset ? point : cursor.position;
        toRoots[side] = root; toPoints[side] = point; revisions[side] = revision;
        acceptedMask |= bit;
        // First snapshot/reentry appears immediately, without sweeping from zero.
        if (reset) Present(cursor, side, 1);
    }
    private void Present(BirdCursorState cursor, int side, float t)
    {
        if (cursor == null || (acceptedMask & (1 << side)) == 0) return;
        cursor.handRoot = Vector3.Lerp(fromRoots[side], toRoots[side], t);
        cursor.position = Vector3.Lerp(fromPoints[side], toPoints[side], t);
        cursor.rawPosition = toPoints[side];
        cursor.poseValid = true;
        // This embodiment is purely visual, never a remote interaction source.
        cursor.clicksAllowed = cursor.selected = cursor.down = cursor.up = false;
    }
    private void ClearPoint(BirdCursorState cursor)
    {
        if (cursor == null) return;
        if (cursor.poseValid) cursor.historyRevision++;
        cursor.poseValid = cursor.tracking = cursor.selected = cursor.down = cursor.up = cursor.clicksAllowed = false;
    }
    private void Hide()
    {
        ClearPoint(remoteLeft); ClearPoint(remoteRight); acceptedMask = 0;
    }
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (player == Networking.GetOwner(gameObject)) Hide();
    }
    private void OnDisable() { Hide(); }
}
