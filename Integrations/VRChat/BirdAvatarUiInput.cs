using UdonSharp;
using UnityEngine;

// Single producer for an explicit UI pointer. Avatar input owns the solver;
// this adapter only copies a fresh accepted logical sample after avatar IK.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(50)]
public class BirdAvatarUiInput : UdonSharpBehaviour
{
    public BirdAvatarHandInput input;
    [Tooltip("Dedicated pointer. Leave its optional cursor poller unassigned.")]
    public BirdUiPointer pointer;
    [HideInInspector] public int submittedFrame = -1;
    private BirdUiPointer boundPointer;
    private BirdAvatarHandInput boundInput;
    private BirdCursorState boundCursor;
    private int acceptedFrame=-1;
    private float acceptedTime;
    private int boundCalibration;
    private bool boundSmoothing;
    private BirdRangeAdaptiveFilter boundFilter;
    private BirdSphereSpaceFilter boundSphere;
    private int boundHistory;

    public override void PostLateUpdate()
    {
        if (!enabled || !gameObject.activeInHierarchy || submittedFrame == Time.frameCount) return;
        submittedFrame = Time.frameCount;
        BirdCursorState cursor = input == null ? null : input.cursor;
        int calibration=input==null?0:input.calibrationRevision;
        bool smoothing=cursor!=null && cursor.smoothing;
        var filter=cursor==null?null:cursor.adaptiveFilter;
        var sphere=cursor==null?null:cursor.sphereFilter;
        int history=cursor==null?0:cursor.historyRevision;
        if (boundPointer != pointer || boundInput != input || boundCursor != cursor || boundCalibration!=calibration || boundSmoothing!=smoothing || boundFilter!=filter || boundSphere!=sphere || boundHistory!=history)
        {
            if (boundPointer != null) boundPointer.Cancel();
            boundPointer = pointer; boundInput = input; boundCursor = cursor;
            boundCalibration=calibration; boundSmoothing=smoothing;
            boundFilter=filter; boundSphere=sphere; boundHistory=history;
            acceptedFrame=-1;
            if (pointer != null) pointer.Cancel();
        }
        if (pointer == null) return;
        // Never compete with the pointer's earlier automatic cursor poller.
        if (pointer.cursor != null || input == null || !input.enabled || !input.gameObject.activeInHierarchy ||
            !input.tipsReady || !input.dataReady || input.sampledFrame != Time.frameCount ||
            cursor == null || !cursor.enabled || !cursor.gameObject.activeInHierarchy || !cursor.tracking || !cursor.poseValid)
        { pointer.Cancel(); acceptedFrame=-1; return; }
        float now=Time.realtimeSinceStartup;
        if(acceptedFrame>=0 && (Time.frameCount!=acceptedFrame+1 || now<acceptedTime || now-acceptedTime>.25f)) pointer.Cancel();
        pointer.sampleOrigin = cursor.handRoot;
        pointer.samplePosition = cursor.position;
        pointer.sampleTracked = true;
        pointer.samplePressed = cursor.clicksAllowed && cursor.selected;
        pointer.Submit();
        acceptedFrame=Time.frameCount; acceptedTime=now;
    }

    private void OnDisable()
    {
        if (boundPointer != null) boundPointer.Cancel();
        boundPointer = null; boundInput = null; boundCursor = null;
        acceptedFrame=-1;
        // Preserve submittedFrame: disable/re-enable cannot emit twice in one frame.
    }
}
