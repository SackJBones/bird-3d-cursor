using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdObjectSnapTarget : UdonSharpBehaviour
{
    [Min(0)] public float approachLength=1;
    [Min(.001f)] public float influenceRadius=.35f,captureRadius=.16f;
    [Range(0,1)] public float attraction=.85f;
    [Min(1)] public float exitMultiplier=1.4f;
    public Vector3 localApproachDirection=Vector3.up;
    [HideInInspector] public Vector3 end,guided;
    [HideInInspector] public float distance;
    public bool Evaluate(BirdObjectRegion region,Vector3 raw)
    {
        if(!enabled || !gameObject.activeInHierarchy || region==null || !region.FiniteVector(raw) ||
            !region.Finite(approachLength) || approachLength<0 || !region.Finite(influenceRadius) || influenceRadius<=0 ||
            !region.Finite(captureRadius) || captureRadius<=0 || !region.Finite(attraction) || !region.Finite(exitMultiplier) || exitMultiplier<1) return false;
        end=region.transform.InverseTransformPoint(transform.position);
        Vector3 axis=region.transform.InverseTransformVector(transform.TransformVector(localApproachDirection)).normalized;
        if(!region.FiniteVector(end) || !region.FiniteVector(axis) || axis.sqrMagnitude<.5f) return false;
        Vector3 onPath=end+axis*Mathf.Clamp(Vector3.Dot(raw-end,axis),0,approachLength);
        distance=Vector3.Distance(raw,onPath);
        if(!region.Finite(distance)) return false;
        float proximity=1-Mathf.Clamp01(distance/influenceRadius);
        float weight=proximity*proximity*(3-2*proximity)*Mathf.Clamp01(attraction);
        guided=Vector3.Lerp(raw,onPath,weight); return true;
    }
}
