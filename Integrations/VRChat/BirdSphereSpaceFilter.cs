using UdonSharp;
using UnityEngine;

// Optional caller-stepped experiment. Filters the existing sphere vector before
// range expansion; it neither estimates openness nor owns geometry/presentation.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BirdSphereSpaceFilter : UdonSharpBehaviour
{
    [Min(.01f)] public float minimumCutoff=4;
    [Min(0)] public float radialSpeedCoefficient=100;
    [Min(0)] public float angularSpeedCoefficient=4;
    [Min(.01f)] public float derivativeCutoff=5;
    [HideInInspector] public Vector3 sampleVector;
    // Hand-derived axes resolve the nonunique path at an exact half-turn.
    // They do not carry history or measure opening. No head/body/world-up input.
    [HideInInspector] public Vector3 sampleNormal, sampleForward;
    [HideInInspector] public float sampleDeltaTime=1f/72;
    [HideInInspector] public Vector3 vector;
    [HideInInspector] public float gain;
    [HideInInspector] public bool valid;
    [HideInInspector] public int historyRevision;
    private bool ready;
    private bool mapValid;
    private float radius, previousRadius, radialVelocity, angularSpeed;
    private Vector3 direction, previousDirection;
    private float boundMinimum, boundRadial, boundAngular, boundDerivative;

    public void Step()
    {
        valid=false;
        if(!enabled || !gameObject.activeInHierarchy || !Settings() || !FiniteVector(sampleVector) ||
            !FiniteVector(sampleNormal) || !FiniteVector(sampleForward) ||
            !Finite(sampleDeltaTime) || sampleDeltaTime<=0 || sampleDeltaTime>.25f)
        { Cancel(); return; }
        if(minimumCutoff!=boundMinimum || radialSpeedCoefficient!=boundRadial ||
            angularSpeedCoefficient!=boundAngular || derivativeCutoff!=boundDerivative)
        {
            Cancel(); boundMinimum=minimumCutoff; boundRadial=radialSpeedCoefficient;
            boundAngular=angularSpeedCoefficient; boundDerivative=derivativeCutoff;
        }
        float rawRadius=sampleVector.magnitude;
        if(!Finite(rawRadius)) { Cancel(); return; }
        // Explicit division avoids Vector3.normalized's small-vector cutoff.
        Vector3 rawDirection=rawRadius>0?sampleVector/rawRadius:Vector3.zero;
        if(!ready || rawRadius==0 || radius==0)
        {
            radius=previousRadius=rawRadius; direction=previousDirection=rawDirection;
            radialVelocity=angularSpeed=0; vector=sampleVector; gain=1; ready=valid=true; return;
        }
        float derivativeGain=Alpha(derivativeCutoff);
        radialVelocity+=( (rawRadius-previousRadius)/sampleDeltaTime-radialVelocity)*derivativeGain;
        mapValid=true;
        Vector3 rawMotion=SphereLog(previousDirection,rawDirection);
        if(!mapValid) { Cancel(); return; }
        float speed=rawMotion.magnitude/sampleDeltaTime;
        angularSpeed+=(speed-angularSpeed)*derivativeGain;
        // One gain for radial and tangential displacement gives the same local
        // first-order response in every direction, unlike independent cutoffs.
        // Motion is measured from incoming samples, never delayed Bird range.
        float cutoff=minimumCutoff+Mathf.Max(radialSpeedCoefficient*Mathf.Abs(radialVelocity),angularSpeedCoefficient*angularSpeed);
        if(!Finite(cutoff) || cutoff<=0) { Cancel(); return; }
        gain=Alpha(cutoff);
        // Intrinsic S2 update: exp_n(gain * log_n(measurement)). The state is a
        // unit vector, never Euler angles, azimuth/elevation or quaternion roll.
        Vector3 residual=SphereLog(direction,rawDirection);
        if(!mapValid) { Cancel(); return; }
        Vector3 nextDirection=SphereExp(direction,residual*gain);
        float nextLength=nextDirection.magnitude;
        float nextRadius=radius*(1-gain)+rawRadius*gain;
        if(!Finite(nextLength) || nextLength<=0 || !Finite(nextRadius)) { Cancel(); return; }
        nextDirection/=nextLength;
        Vector3 candidate=nextDirection*nextRadius;
        if(!FiniteVector(candidate)) { Cancel(); return; }
        radius=nextRadius; direction=nextDirection; vector=candidate;
        previousRadius=rawRadius; previousDirection=rawDirection; ready=valid=true;
    }
    public void Cancel()
    {
        historyRevision=historyRevision==int.MaxValue?0:historyRevision+1;
        ready=valid=false; radialVelocity=angularSpeed=0; gain=0;
    }
    private void OnDisable() { Cancel(); }
    private float Alpha(float cutoff) { return 1/(1+1/(2*Mathf.PI*cutoff*sampleDeltaTime)); }
    private Vector3 SphereLog(Vector3 origin,Vector3 destination)
    {
        Vector3 cross=Vector3.Cross(origin,destination);
        float cosine=Mathf.Clamp(Vector3.Dot(origin,destination),-1,1);
        // Double cross lies in T_origin S2 and is zero for identical inputs.
        Vector3 tangent=Vector3.Cross(cross,origin);
        float length=tangent.magnitude;
        if(length<.000001f)
        {
            if(cosine>=0) return tangent; // analytic log limit at coincidence
            // The antipodal log is multivalued. A hand-derived tangent chooses
            // a path covariantly: these axes rotate with the input under SE(3).
            tangent=Vector3.Cross(Vector3.Cross(origin,sampleNormal),origin);
            if(tangent.sqrMagnitude<.000000000001f)
                tangent=Vector3.Cross(Vector3.Cross(origin,sampleForward),origin);
            length=tangent.magnitude;
            if(!Finite(length) || length<.000001f) { mapValid=false; return Vector3.zero; }
        }
        // atan2 supplies an intrinsic tangent norm, not an orientation chart.
        float norm=Mathf.Atan2(cross.magnitude,cosine);
        return tangent*(norm/length);
    }
    private Vector3 SphereExp(Vector3 origin,Vector3 tangent)
    {
        float squared=tangent.sqrMagnitude;
        if(squared<.00000001f)
        {
            // Analytic cosine/sinc limits avoid a zero-norm division.
            float fourth=squared*squared;
            return origin*(1-squared*.5f+fourth/24)+tangent*(1-squared/6+fourth/120);
        }
        float norm=Mathf.Sqrt(squared);
        return origin*Mathf.Cos(norm)+tangent*(Mathf.Sin(norm)/norm);
    }
    private bool Settings()
    {
        return Finite(minimumCutoff) && minimumCutoff>=.01f && Finite(derivativeCutoff) && derivativeCutoff>=.01f &&
            Finite(radialSpeedCoefficient) && radialSpeedCoefficient>=0 && Finite(angularSpeedCoefficient) && angularSpeedCoefficient>=0;
    }
    private bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
    private bool FiniteVector(Vector3 v) { return Finite(v.x) && Finite(v.y) && Finite(v.z); }
}
