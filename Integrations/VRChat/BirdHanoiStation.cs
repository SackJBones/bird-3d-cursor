using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(140)]
public class BirdHanoiStation : UdonSharpBehaviour
{
    public BirdObjectGrip grip;
    public BirdHanoiBoard tabletop,buildings;
    public Transform footprint;
    public LineRenderer approach;
    public Text label;
    public BirdObjectFeedback[] feedbacks=new BirdObjectFeedback[0];
    [HideInInspector] public int grabbedCount,placedCount,cancelledCount;
    public void RecordGrab() { grabbedCount++; }
    public void RecordPlace() { placedCount++; }
    public void RecordCancel() { cancelledCount++; }
    public void CancelGrip() { if(grip!=null) grip.Cancel(); }
    public void ResetPuzzles()
    {
        if(grip!=null) grip.CancelImmediately();
        if(tabletop!=null) tabletop.ResetPuzzle(); if(buildings!=null) buildings.ResetPuzzle();
    }
    private void LateUpdate() { Refresh(); }
    public void Refresh()
    {
        if(grip==null || tabletop==null || buildings==null) return;
        foreach(BirdObjectFeedback feedback in feedbacks) if(feedback!=null) feedback.Refresh();
        BirdObjectTarget item=grip.ActiveTarget; BirdObjectSnapTarget slot=grip.Candidate;
        bool guided=item!=null && slot!=null && !grip.IsReturning;
        if(footprint!=null) footprint.gameObject.SetActive(guided);
        if(approach!=null) approach.enabled=guided;
        if(guided)
        {
            Vector3 size=Vector3.Scale(item.volume.size,item.transform.lossyScale);
            if(footprint!=null)
            {
                footprint.position=slot.transform.position+item.volume.transform.TransformVector(item.volume.center)-item.transform.up*(size.y*.49f);
                footprint.rotation=item.transform.rotation; footprint.localScale=new Vector3(size.x*1.06f,Mathf.Max(.004f,size.y*.025f),size.z*1.06f);
            }
            if(approach!=null)
            {
                approach.SetPosition(0,slot.transform.position);
                Vector3 axis=item.region.transform.InverseTransformVector(slot.transform.TransformVector(slot.localApproachDirection)).normalized;
                approach.SetPosition(1,slot.transform.position+item.region.transform.TransformVector(axis*slot.approachLength));
                approach.widthMultiplier=.012f*Mathf.Abs(item.region.transform.lossyScale.x);
                approach.startColor=approach.endColor=grip.ReadyToPlace?Color.green:Color.cyan;
            }
        }
        string action=item==null?"POINT / HOLD TO PICK UP":grip.IsReturning?"RETURNING":grip.ReadyToPlace?"RELEASE TO PLACE":guided?"FOLLOW THE GUIDE":"DRAGGING";
        if(label!=null) label.text=action+"\nTable "+tabletop.moves+" | Buildings "+buildings.moves+
            (tabletop.solved||buildings.solved?"\nPUZZLE SOLVED":"")+"\n\nLocal desktop demo\nLook / wheel reach\nHold mouse to move\nX cancels";
    }
}
