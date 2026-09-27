using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// Optional, local X-ray geometry inspection. Never feeds the fit or cursor.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[DefaultExecutionOrder(110)]
public class BirdLabGeometryView : UdonSharpBehaviour
{
    public BirdAvatarHandInput input;
    public BirdLabPointView pointView;
    public LineRenderer[] sphereRings;
    public Renderer fitCenter;
    public LineRenderer fitRay, birdRay, birdMarker, palmNormal, rootMarker;

    public override void PostLateUpdate()
    {
        if (!enabled || !gameObject.activeInHierarchy || input == null || !input.enabled || !input.gameObject.activeInHierarchy ||
            !input.tipsReady || input.sampledFrame != Time.frameCount || input.cursor == null || !input.cursor.poseValid)
        { Clear(); return; }
        var player=Networking.LocalPlayer;
        if (!Utilities.IsValid(player) || pointView == null || pointView.core == null) { Clear(); return; }
        var cursor=input.cursor; var fit=cursor.fitter;
        bool valid=fit != null && fit.fitValid;
        if (fitCenter != null)
        {
            fitCenter.enabled=valid;
            if(valid) fitCenter.transform.position=fit.center;
        }
        if(sphereRings != null) for(int ring=0;ring<sphereRings.Length;ring++)
        {
            var line=sphereRings[ring]; if(line==null) continue;
            line.enabled=valid;
            if(valid) for(int i=0;i<line.positionCount;i++)
            {
                float angle=i*Mathf.PI*2/line.positionCount;
                float a=Mathf.Cos(angle)*fit.radius,b=Mathf.Sin(angle)*fit.radius;
                line.SetPosition(i,fit.center+(ring==0?new Vector3(a,b,0):ring==1?new Vector3(a,0,b):new Vector3(0,a,b)));
            }
        }
        if(fitRay!=null)
        {
            fitRay.enabled=valid;
            if(valid) { fitRay.SetPosition(0,cursor.handRoot); fitRay.SetPosition(1,fit.center); }
        }
        if(palmNormal!=null)
        {
            palmNormal.enabled=true; palmNormal.SetPosition(0,cursor.handRoot);
            palmNormal.SetPosition(1,cursor.handRoot+input.normal*.08f);
        }
        if(rootMarker!=null)
        {
            Vector3 across=(input.bonePositions[13]-input.bonePositions[4]).normalized*.007f;
            Vector3 along=Vector3.Cross(input.normal,across).normalized*.007f;
            rootMarker.enabled=true;
            rootMarker.SetPosition(0,cursor.handRoot-across);
            rootMarker.SetPosition(1,cursor.handRoot+across);
            rootMarker.SetPosition(2,cursor.handRoot);
            rootMarker.SetPosition(3,cursor.handRoot-along);
            rootMarker.SetPosition(4,cursor.handRoot+along);
        }
        // Same render-shell endpoint as the displayed Bird. At ordinary distances
        // this is the actual point; astronomical ranges preserve its viewing direction.
        Vector3 endpoint=pointView.core.transform.position;
        if(birdRay!=null)
        {
            birdRay.enabled=true; birdRay.SetPosition(0,cursor.handRoot); birdRay.SetPosition(1,endpoint);
        }
        if(birdMarker!=null)
        {
            var head=player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            float rendered=Vector3.Distance(endpoint,head.position);
            float radius=Mathf.Max(.02f,rendered*.003f);
            birdMarker.enabled=true; birdMarker.startWidth=birdMarker.endWidth=Mathf.Max(.0015f,rendered*.0005f);
            for(int i=0;i<4;i++)
            {
                float angle=i*Mathf.PI*.5f;
                birdMarker.SetPosition(i,endpoint+head.rotation*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0));
            }
        }
    }
    public void Clear()
    {
        if(fitCenter!=null) fitCenter.enabled=false;
        if(sphereRings!=null) foreach(var line in sphereRings) if(line!=null) line.enabled=false;
        if(fitRay!=null) fitRay.enabled=false;
        if(birdRay!=null) birdRay.enabled=false;
        if(birdMarker!=null) birdMarker.enabled=false;
        if(palmNormal!=null) palmNormal.enabled=false;
        if(rootMarker!=null) rootMarker.enabled=false;
    }
    private void OnDisable() { Clear(); }
}
