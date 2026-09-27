using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// Laboratory comparison only. All modes share input, geometry and presentation.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdLabFilterControl : UdonSharpBehaviour
{
    public BirdCursorState[] cursors;
    public Text label;
    public bool filtered = true;
    public BirdRangeAdaptiveFilter[] adaptiveFilters=new BirdRangeAdaptiveFilter[0];
    public bool adaptive;

    private void Start() { Apply(); }
    public override void Interact()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        if(!filtered) { filtered=true; adaptive=false; }
        else if(!adaptive && HasAdaptive()) adaptive=true;
        else { filtered=false; adaptive=false; }
        Apply();
    }
    public void SetRaw()
    { if (enabled && gameObject.activeInHierarchy) { filtered = false; adaptive=false; Apply(); } }
    public void SetFiltered()
    { if (enabled && gameObject.activeInHierarchy) { filtered = true; adaptive=false; Apply(); } }
    public void SetAdaptive()
    { if(enabled && gameObject.activeInHierarchy && HasAdaptive()) { filtered=true; adaptive=true; Apply(); } }
    private bool HasAdaptive()
    {
        if(cursors==null || adaptiveFilters==null || adaptiveFilters.Length!=cursors.Length || cursors.Length==0) return false;
        for(int i=0;i<cursors.Length;i++) if(cursors[i]==null || adaptiveFilters[i]==null) return false;
        return true;
    }
    private void Apply()
    {
        if(adaptive && !HasAdaptive()) { adaptive=false; filtered=false; }
        if (cursors != null) for(int i=0;i<cursors.Length;i++)
        {
            var cursor=cursors[i]; if(cursor==null) continue;
            var policy=filtered && adaptive?adaptiveFilters[i]:null;
            if(cursor.smoothing!=filtered || cursor.adaptiveFilter!=policy)
            {
                cursor.Cancel(); cursor.smoothing=filtered; cursor.adaptiveFilter=policy;
                if(policy!=null) policy.Cancel();
            }
        }
        if (label != null) label.text = adaptive?"Point / ADAPTIVE\nPress for RAW":filtered?
            (HasAdaptive()?"Point / FILTERED\nPress for ADAPTIVE":"Point / FILTERED\nPress for RAW"):"Point / RAW\nPress for FILTERED";
    }
}
