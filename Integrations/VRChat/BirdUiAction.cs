using UdonSharp;
using UnityEngine;
using VRC.Udon;

public enum BirdUiActionKind { OpenPanel, ClosePanel, CloseRoot, SetColor, ResetTransform }

// Local scene actions are separate from contact, layout and scroll ownership.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdUiAction : UdonSharpBehaviour
{
    public BirdUiElement element;
    public BirdUiActionKind action;
    public BirdUiPanel panel;
    public Renderer colorTarget;
    public Color color=Color.white;
    public Transform resetTarget;
    public UdonBehaviour callback;
    public string callbackEvent;
    [HideInInspector] public int invocationCount;
    private MaterialPropertyBlock block;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private void Start()
    {
        if(resetTarget!=null) { initialPosition=resetTarget.localPosition; initialRotation=resetTarget.localRotation; initialScale=resetTarget.localScale; }
    }
    public void Activate()
    {
        if(element==null || element.lastPointer==null) return;
        invocationCount++;
        if(action==BirdUiActionKind.OpenPanel && panel!=null) panel.Open(element.lastPointer);
        else if(action==BirdUiActionKind.ClosePanel && panel!=null) panel.Close();
        else if(action==BirdUiActionKind.CloseRoot && panel!=null) panel.CloseAll();
        else if(action==BirdUiActionKind.SetColor && colorTarget!=null)
        {
            if(block==null) block=new MaterialPropertyBlock();
            colorTarget.GetPropertyBlock(block); block.SetColor("_Color",color); colorTarget.SetPropertyBlock(block);
        }
        else if(action==BirdUiActionKind.ResetTransform && resetTarget!=null)
        {
            resetTarget.localPosition=initialPosition; resetTarget.localRotation=initialRotation; resetTarget.localScale=initialScale;
            Physics.SyncTransforms();
        }
        if(callback!=null && !string.IsNullOrEmpty(callbackEvent)) callback.SendCustomEvent(callbackEvent);
    }
}
