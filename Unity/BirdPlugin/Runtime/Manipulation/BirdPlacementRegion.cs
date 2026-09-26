using UnityEngine;

namespace Bird3DCursor.Manipulation
{
    /// <summary>Experience-owned work volume. It constrains objects, never the logical Bird point.</summary>
    [AddComponentMenu("Bird/Manipulation/Placement Region")]
    public sealed class BirdPlacementRegion : MonoBehaviour
    {
        [SerializeField] Bounds localBounds = new Bounds(new Vector3(0,1,0),new Vector3(4,2,3));
        public Bounds LocalBounds { get { return localBounds; } set { localBounds=value; } }

        public bool TryPivotBounds(Transform item, BoxCollider volume, out Bounds result)
        {
            result=new Bounds();
            if (!isActiveAndEnabled || item==null || volume==null || !volume.enabled ||
                !volume.gameObject.activeInHierarchy || !volume.transform.IsChildOf(item) ||
                !Finite(localBounds.center) || !Finite(localBounds.size) ||
                localBounds.size.x<=0 || localBounds.size.y<=0 || localBounds.size.z<=0 ||
                Mathf.Abs(transform.lossyScale.x*transform.lossyScale.y*transform.lossyScale.z)<1e-12f) return false;
            Vector3 pivot=transform.InverseTransformPoint(item.position);
            Vector3 min=Vector3.one*float.PositiveInfinity,max=-min;
            for(int i=0;i<8;i++)
            {
                Vector3 sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                Vector3 corner=volume.transform.TransformPoint(volume.center+Vector3.Scale(volume.size*.5f,sign));
                Vector3 offset=transform.InverseTransformPoint(corner)-pivot;
                if(!Finite(offset)) return false;
                min=Vector3.Min(min,offset); max=Vector3.Max(max,offset);
            }
            Vector3 low=localBounds.min-min,high=localBounds.max-max;
            if(low.x>high.x || low.y>high.y || low.z>high.z) return false;
            result.SetMinMax(low,high); return Finite(result.center) && Finite(result.size);
        }

        public static Vector3 Clamp(Bounds bounds,Vector3 point)
        {
            Vector3 min=bounds.min,max=bounds.max;
            return new Vector3(Mathf.Clamp(point.x,min.x,max.x),Mathf.Clamp(point.y,min.y,max.y),Mathf.Clamp(point.z,min.z,max.z));
        }
        public static bool Finite(Vector3 v) { return Finite(v.x)&&Finite(v.y)&&Finite(v.z); }
        public static bool Finite(float v) { return !float.IsNaN(v)&&!float.IsInfinity(v); }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix=transform.localToWorldMatrix; Gizmos.color=new Color(.2f,.8f,1,.5f);
            Gizmos.DrawWireCube(localBounds.center,localBounds.size);
        }
    }
}
