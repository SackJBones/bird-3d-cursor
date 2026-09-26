using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// An authored local viewing area. Fail closed outside it or if a protected workspace overlaps it.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdObjectPlayArea : UdonSharpBehaviour
{
    public BoxCollider playerArea;
    public BirdObjectRegion[] protectedRegions=new BirdObjectRegion[0];
    public bool automatic=true;
    [HideInInspector] public Vector3 samplePosition;
    [HideInInspector] public bool hasPlayer,allowed;
    private void Update()
    {
        if(!automatic) return;
        VRCPlayerApi player=Networking.LocalPlayer;
        hasPlayer=Utilities.IsValid(player);
        if(hasPlayer) samplePosition=player.GetPosition();
        Evaluate();
    }
    public void Evaluate()
    {
        allowed=false;
        if(!enabled || !gameObject.activeInHierarchy || !hasPlayer || playerArea==null || !playerArea.enabled || !playerArea.gameObject.activeInHierarchy ||
            float.IsNaN(samplePosition.sqrMagnitude) || float.IsInfinity(samplePosition.sqrMagnitude)) return;
        if(!FiniteVector(playerArea.center) || !FiniteVector(playerArea.size) || !FiniteVector(playerArea.transform.position) || !FiniteVector(playerArea.transform.lossyScale) ||
            playerArea.size.x<=0 || playerArea.size.y<=0 || playerArea.size.z<=0 ||
            Mathf.Abs(playerArea.transform.lossyScale.x*playerArea.transform.lossyScale.y*playerArea.transform.lossyScale.z)<1e-12f) return;
        if((playerArea.ClosestPoint(samplePosition)-samplePosition).sqrMagnitude>.000001f) return;
        foreach(BirdObjectRegion region in protectedRegions)
        {
            if(region==null || !region.enabled || !region.gameObject.activeInHierarchy || !region.FiniteVector(region.localBounds.center) || !region.FiniteVector(region.localBounds.size)) return;
            if(region.localBounds.size.x<=0 || region.localBounds.size.y<=0 || region.localBounds.size.z<=0 ||
                Mathf.Abs(region.transform.lossyScale.x*region.transform.lossyScale.y*region.transform.lossyScale.z)<1e-12f) return;
            Vector3 min=Vector3.one*float.PositiveInfinity,max=-min;
            for(int i=0;i<8;i++)
            {
                Vector3 sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                Vector3 p=region.transform.TransformPoint(region.localBounds.center+Vector3.Scale(region.localBounds.extents,sign));
                if(!region.FiniteVector(p)) return;
                min=Vector3.Min(min,p); max=Vector3.Max(max,p);
            }
            Bounds remote=new Bounds((min+max)*.5f,max-min);
            if(remote.Intersects(playerArea.bounds)) return;
        }
        allowed=true;
    }
    private void OnDisable() { allowed=false; }
    private bool FiniteVector(Vector3 v) { return !float.IsNaN(v.sqrMagnitude) && !float.IsInfinity(v.sqrMagnitude); }
}
