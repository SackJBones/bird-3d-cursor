using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdObjectRegion : UdonSharpBehaviour
{
    public Bounds localBounds=new Bounds(new Vector3(0,1,0),new Vector3(4,2,3));
    [HideInInspector] public Bounds pivotBounds;
    public bool TryPivotBounds(Transform item,BoxCollider volume)
    {
        if(!enabled || !gameObject.activeInHierarchy || item==null || volume==null || !volume.enabled ||
            !volume.gameObject.activeInHierarchy || !volume.transform.IsChildOf(item) || !FiniteVector(localBounds.center) ||
            !FiniteVector(localBounds.size) || localBounds.size.x<=0 || localBounds.size.y<=0 || localBounds.size.z<=0 ||
            !FiniteVector(volume.size) || volume.size.x<=0 || volume.size.y<=0 || volume.size.z<=0 ||
            Mathf.Abs(volume.transform.lossyScale.x*volume.transform.lossyScale.y*volume.transform.lossyScale.z)<1e-12f ||
            Mathf.Abs(transform.lossyScale.x*transform.lossyScale.y*transform.lossyScale.z)<1e-12f) return false;
        Vector3 pivot=transform.InverseTransformPoint(item.position);
        Vector3 min=Vector3.one*float.PositiveInfinity,max=-min;
        for(int i=0;i<8;i++)
        {
            Vector3 sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
            Vector3 corner=volume.transform.TransformPoint(volume.center+Vector3.Scale(volume.size*.5f,sign));
            Vector3 offset=transform.InverseTransformPoint(corner)-pivot;
            if(!FiniteVector(offset)) return false;
            min=Vector3.Min(min,offset); max=Vector3.Max(max,offset);
        }
        Vector3 low=localBounds.min-min,high=localBounds.max-max;
        if(low.x>high.x || low.y>high.y || low.z>high.z) return false;
        // Assign the struct explicitly; exposed instance mutations can lose the value in Udon.
        pivotBounds=new Bounds((low+high)*.5f,high-low);
        return FiniteVector(pivotBounds.center) && FiniteVector(pivotBounds.size);
    }
    public Vector3 Clamp(Bounds bounds,Vector3 point)
    {
        Vector3 min=bounds.min,max=bounds.max;
        return new Vector3(Mathf.Clamp(point.x,min.x,max.x),Mathf.Clamp(point.y,min.y,max.y),Mathf.Clamp(point.z,min.z,max.z));
    }
    public bool FiniteVector(Vector3 v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z); }
    public bool Finite(float v) { return !float.IsNaN(v)&&!float.IsInfinity(v); }
}
