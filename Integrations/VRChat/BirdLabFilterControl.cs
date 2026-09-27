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
    public BirdSphereSpaceFilter[] sphereFilters=new BirdSphereSpaceFilter[0];
    public bool sphere;

    private void Start() { Apply(); }
    public override void Interact()
    {
        if (!enabled || !gameObject.activeInHierarchy) return;
        if(!filtered) { filtered=true; adaptive=sphere=false; }
        else if(!adaptive && !sphere && HasAdaptive()) adaptive=true;
        else if(!sphere && HasSphere()) { adaptive=false; sphere=true; }
        else { filtered=false; adaptive=sphere=false; }
        Apply();
    }
    public void SetRaw()
    { if (enabled && gameObject.activeInHierarchy) { filtered = false; adaptive=sphere=false; Apply(); } }
    public void SetFiltered()
    { if (enabled && gameObject.activeInHierarchy) { filtered = true; adaptive=sphere=false; Apply(); } }
    public void SetAdaptive()
    { if(enabled && gameObject.activeInHierarchy && HasAdaptive()) { filtered=true; adaptive=true; sphere=false; Apply(); } }
    public void SetSphere()
    { if(enabled && gameObject.activeInHierarchy && HasSphere()) { filtered=true; sphere=true; adaptive=false; Apply(); } }
    private bool HasSphere()
    {
        if(cursors==null || sphereFilters==null || sphereFilters.Length!=cursors.Length || cursors.Length==0) return false;
        for(int i=0;i<cursors.Length;i++) if(cursors[i]==null || sphereFilters[i]==null) return false;
        return true;
    }
    private bool HasAdaptive()
    {
        if(cursors==null || adaptiveFilters==null || adaptiveFilters.Length!=cursors.Length || cursors.Length==0) return false;
        for(int i=0;i<cursors.Length;i++) if(cursors[i]==null || adaptiveFilters[i]==null) return false;
        return true;
    }
    private void Apply()
    {
        if(adaptive && !HasAdaptive()) { adaptive=false; filtered=false; }
        if(sphere && (!HasSphere() || adaptive)) { sphere=adaptive=false; filtered=false; }
        if (cursors != null) for(int i=0;i<cursors.Length;i++)
        {
            var cursor=cursors[i]; if(cursor==null) continue;
            var policy=filtered && adaptive?adaptiveFilters[i]:null;
            var spherePolicy=filtered && sphere?sphereFilters[i]:null;
            if(cursor.smoothing!=filtered || cursor.adaptiveFilter!=policy || cursor.sphereFilter!=spherePolicy)
            {
                cursor.Cancel(); cursor.smoothing=filtered; cursor.adaptiveFilter=policy;
                cursor.sphereFilter=spherePolicy;
                if(policy!=null) policy.Cancel();
                if(spherePolicy!=null) spherePolicy.Cancel();
            }
        }
        if (label != null) label.text = sphere?"Point / SPHERE\nPress for RAW":adaptive?
            (HasSphere()?"Point / ADAPTIVE\nPress for SPHERE":"Point / ADAPTIVE\nPress for RAW"):filtered?
            (HasAdaptive()?"Point / FILTERED\nPress for ADAPTIVE":HasSphere()?"Point / FILTERED\nPress for SPHERE":"Point / FILTERED\nPress for RAW"):"Point / RAW\nPress for FILTERED";
    }
}
