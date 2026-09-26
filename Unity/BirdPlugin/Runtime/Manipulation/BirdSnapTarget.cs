using UnityEngine;

namespace Bird3DCursor.Manipulation
{
    /// <summary>A final pivot pose with an approach segment. Distances use region-local units.</summary>
    [AddComponentMenu("Bird/Manipulation/Snap Target")]
    public sealed class BirdSnapTarget : MonoBehaviour
    {
        [Min(0)] public float approachLength=1;
        [Min(.001f)] public float influenceRadius=.35f;
        [Min(.001f)] public float captureRadius=.16f;
        [Range(0,1)] public float attraction=.85f;
        [Min(1)] public float exitMultiplier=1.4f;
        public Vector3 localApproachDirection=Vector3.up;

        public bool Evaluate(BirdPlacementRegion region,Vector3 raw,out Vector3 end,out Vector3 guided,out float distance)
        {
            end=guided=Vector3.zero; distance=float.PositiveInfinity;
            if(!isActiveAndEnabled || region==null || !BirdPlacementRegion.Finite(raw) ||
                !BirdPlacementRegion.Finite(approachLength) || approachLength<0 ||
                !BirdPlacementRegion.Finite(influenceRadius) || influenceRadius<=0 ||
                !BirdPlacementRegion.Finite(captureRadius) || captureRadius<=0 ||
                !BirdPlacementRegion.Finite(attraction) || !BirdPlacementRegion.Finite(exitMultiplier) ||
                exitMultiplier<1) return false;
            end=region.transform.InverseTransformPoint(transform.position);
            Vector3 axis=region.transform.InverseTransformVector(transform.TransformVector(localApproachDirection)).normalized;
            if(!BirdPlacementRegion.Finite(end) || !BirdPlacementRegion.Finite(axis) || axis.sqrMagnitude<.5f) return false;
            Vector3 onPath=end+axis*Mathf.Clamp(Vector3.Dot(raw-end,axis),0,approachLength);
            distance=Vector3.Distance(raw,onPath);
            if(!BirdPlacementRegion.Finite(distance)) return false;
            float proximity=1-Mathf.Clamp01(distance/influenceRadius);
            float weight=proximity*proximity*(3-2*proximity)*Mathf.Clamp01(attraction);
            guided=Vector3.Lerp(raw,onPath,weight); return true;
        }
        void OnDrawGizmosSelected()
        {
            var region=GetComponentInParent<BirdPlacementRegion>();
            if(region==null) return;
            Vector3 end,guided; float distance;
            if(!Evaluate(region,region.transform.InverseTransformPoint(transform.position),out end,out guided,out distance)) return;
            Vector3 axis=region.transform.InverseTransformVector(transform.TransformVector(localApproachDirection)).normalized;
            Gizmos.matrix=region.transform.localToWorldMatrix; Gizmos.color=new Color(.2f,.9f,.7f,.6f);
            Gizmos.DrawLine(end,end+axis*approachLength); Gizmos.DrawWireSphere(end,captureRadius);
            Gizmos.color=new Color(.2f,.7f,.9f,.3f); Gizmos.DrawWireSphere(end+axis*approachLength,influenceRadius);
        }
    }
}
