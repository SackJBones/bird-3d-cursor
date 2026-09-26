using UdonSharp;
using UnityEngine;

// Logical samples only. Geometry and visible cursor size are upstream/downstream choices.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(50)]
public class BirdUiPointer : UdonSharpBehaviour
{
    public string userId = "LocalUser";
    [Tooltip("Optional accepted cursor state. Leave empty for an explicit sample producer.")]
    public BirdCursorState cursor;
    [HideInInspector] public Vector3 sampleOrigin, samplePosition;
    [HideInInspector] public bool sampleTracked, samplePressed;
    [HideInInspector] public Vector3 origin, position, previousPosition;
    [HideInInspector] public bool tracked, pressed, pressedThisSample, hasHistory;
    [HideInInspector] public bool uiConsumed;
    [HideInInspector] public int revision;
    private string previousUser = "LocalUser";

    private void LateUpdate() { if (cursor != null) SampleCursor(); }
    public void SampleCursor()
    {
        if (cursor == null) { Cancel(); return; }
        sampleOrigin=cursor.handRoot; samplePosition=cursor.position;
        sampleTracked=cursor.enabled && cursor.gameObject.activeInHierarchy && cursor.tracking && cursor.poseValid;
        samplePressed=cursor.selected;
        Submit();
    }
    public bool IsTracked() { return enabled && gameObject.activeInHierarchy && tracked; }
    public void Submit()
    {
        if (userId != previousUser) { Cancel(); previousUser=userId; }
        if (!enabled || !gameObject.activeInHierarchy || !sampleTracked || !FiniteVector(sampleOrigin) ||
            !FiniteVector(samplePosition) || !Finite((samplePosition-sampleOrigin).sqrMagnitude)) { Cancel(); return; }
        hasHistory=IsTracked(); previousPosition=hasHistory ? position : samplePosition;
        pressedThisSample=hasHistory && !pressed && samplePressed;
        origin=sampleOrigin; position=samplePosition; pressed=samplePressed; tracked=true; uiConsumed=false;
        Advance();
    }
    public void Cancel() { tracked=pressed=pressedThisSample=hasHistory=uiConsumed=false; Advance(); }
    private void OnDisable() { Cancel(); }
    private void Advance() { revision=revision==int.MaxValue ? 0 : revision+1; }
    private bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    private bool FiniteVector(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
}
