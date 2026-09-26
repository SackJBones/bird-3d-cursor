using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(100)]
public class BirdUiRouter : UdonSharpBehaviour
{
    public BirdUiPointer[] pointers=new BirdUiPointer[0];
    public BirdUiElement[] elements=new BirdUiElement[0];
    public bool automatic=true;
    private int[] revisions=new int[0];
    private BirdUiElement[] candidates=new BirdUiElement[0];
    private bool stepping, ready;

    private void Start() { Initialize(); }
    public void Initialize()
    {
        if(stepping) return;
        revisions=new int[pointers.Length]; candidates=new BirdUiElement[pointers.Length];
        for(int i=0;i<pointers.Length;i++) if(pointers[i]!=null) revisions[i]=pointers[i].revision;
        foreach(BirdUiElement element in elements) if(element!=null) element.ResetPointers(pointers.Length);
        ready=true;
    }
    private void LateUpdate() { if(automatic) Process(); }
    public void Process()
    {
        if(stepping || !enabled || !gameObject.activeInHierarchy) return;
        if(!ready || revisions.Length!=pointers.Length) Initialize();
        stepping=true;
        foreach(BirdUiElement element in elements)
        {
            if(element==null) continue;
            BirdUiPanel panel=element.panel!=null ? element.panel.Root() : null;
            if(panel!=null && panel.state!=0 && panel.closeWhenFocusLost && !OwnerTracked(panel.owner)) panel.Close();
            element.BeginFrame();
        }
        for(int i=0;i<pointers.Length;i++)
        {
            BirdUiPointer pointer=pointers[i]; candidates[i]=null;
            if(pointer==null) continue;
            bool fresh=revisions[i]!=pointer.revision; revisions[i]=pointer.revision;
            float nearest=float.PositiveInfinity;
            foreach(BirdUiElement element in elements)
            {
                if(element==null || !element.Evaluate(pointer,i,fresh)) continue;
                BirdUiElement best=candidates[i];
                if(best==null || element.priority>best.priority || (element.priority==best.priority && element.hitDistance<nearest))
                { candidates[i]=element; nearest=element.hitDistance; }
            }
        }
        for(int i=0;i<pointers.Length;i++)
            if(enabled && gameObject.activeInHierarchy && candidates[i]!=null) candidates[i].Invoke(pointers[i]);
        foreach(BirdUiElement element in elements) if(element!=null) element.FinishFrame();
        stepping=false;
    }
    private bool OwnerTracked(string owner)
    {
        foreach(BirdUiPointer pointer in pointers) if(pointer!=null && pointer.IsTracked() && pointer.userId==owner) return true;
        return false;
    }
    private void OnDisable()
    {
        ready=false;
        foreach(BirdUiElement element in elements)
        {
            if(element==null) continue;
            if(element.panel!=null) element.panel.CloseAll();
            element.ResetPointers(pointers.Length); element.BeginFrame(); element.FinishFrame();
        }
    }
}
