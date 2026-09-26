using UdonSharp;
using UnityEngine;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdUiPanel : UdonSharpBehaviour
{
    [Tooltip("Separate child holding visuals and controls; never this controller or its ancestor.")]
    public GameObject content;
    public BirdUiPanel parent;
    public bool closeWhenFocusLost = true;
    public UdonBehaviour eventTarget;
    public string openedEvent, closedEvent;
    [HideInInspector] public int state; // 0 closed, 1 open, 2 background.
    [HideInInspector] public string owner;
    [HideInInspector] public BirdUiPanel child;
    [HideInInspector] public bool closing, changingChild;

    private void Start() { ShowContent(state != 0); }
    private void OnDisable() { Close(); }
    public bool Accepts(BirdUiPointer pointer)
    {
        return enabled && gameObject.activeInHierarchy && state!=0 && pointer!=null && pointer.IsTracked() && owner==pointer.userId;
    }
    public BirdUiPanel Root()
    {
        BirdUiPanel slow=this, fast=this;
        do
        {
            slow=slow!=null ? slow.parent : null;
            fast=fast!=null && fast.parent!=null ? fast.parent.parent : null;
            if (slow!=null && slow==fast) return null;
        } while (fast!=null);
        BirdUiPanel root=this;
        while(root.parent!=null) root=root.parent;
        return root;
    }
    public void Open(BirdUiPointer pointer)
    {
        if (closing || !enabled || !gameObject.activeInHierarchy || state!=0 || pointer==null || !pointer.IsTracked() || Root()==null) return;
        if (parent!=null)
        {
            if (parent.changingChild || !parent.Accepts(pointer)) return;
            BirdUiPanel owningParent=parent;
            owningParent.changingChild=true;
            if (owningParent.child!=null && owningParent.child!=this) owningParent.child.Close();
            if (owningParent==null) return;
            owningParent.changingChild=false;
            if (!enabled || !gameObject.activeInHierarchy || parent==null || parent!=owningParent || !parent.Accepts(pointer)) return;
            parent.child=this; parent.state=2;
        }
        owner=pointer.userId; state=1; ShowContent(true);
        if (eventTarget!=null && !string.IsNullOrEmpty(openedEvent)) eventTarget.SendCustomEvent(openedEvent);
    }
    [RecursiveMethod]
    public void Close()
    {
        if (closing || state==0) return;
        closing=true; state=0; owner=null;
        if (child!=null) child.Close();
        child=null; ShowContent(false);
        if (parent!=null && parent.child==this)
        {
            parent.child=null;
            if (parent.state==2) parent.state=1;
        }
        if (eventTarget!=null && !string.IsNullOrEmpty(closedEvent)) eventTarget.SendCustomEvent(closedEvent);
        closing=false;
    }
    public void CloseAll() { BirdUiPanel root=Root(); if(root!=null) root.Close(); else Close(); }
    private void ShowContent(bool visible)
    {
        if (content!=null && content!=gameObject && !transform.IsChildOf(content.transform)) content.SetActive(visible);
    }
}
