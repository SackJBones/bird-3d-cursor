#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

// Extends the real post-IK avatar fixture; tests backing Udon, never C# proxies.
public partial class UnityAvatarHandLabChecks
{
    sealed class UiHandPose
    {
        public Dictionary<HumanBodyBones,Vector3> points=new Dictionary<HumanBodyBones,Vector3>(positions);
        public Dictionary<HumanBodyBones,Quaternion> orientations=new Dictionary<HumanBodyBones,Quaternion>(rotations);
        public Vector3[] roots=new Vector3[2], offsets=new Vector3[2];
    }
    UiHandPose SaveUiPose()
    {
        var pose=new UiHandPose();
        for(int side=0;side<2;side++) { pose.roots[side]=Get<Vector3>(cursors[side],"handRoot");pose.offsets[side]=Get<Vector3>(cursors[side],"position")-pose.roots[side]; }
        return pose;
    }
    static void PlaceUiPose(UiHandPose pose,int side,Vector3 root,Vector3 direction)
    {
        Quaternion rotation=Quaternion.FromToRotation(pose.offsets[side],direction);
        for(int i=0;i<16;i++)
        {
            var bone=Bone(side,i);positions[bone]=root+rotation*(pose.points[bone]-pose.roots[side]);
            rotations[bone]=rotation*pose.orientations[bone];
        }
    }
    static int NextUiRevision(int value) { return value==int.MaxValue?0:value+1; }
    IEnumerator UiScenarios()
    {
        VM(FindObjectOfType<BirdLabFilterControl>()).SendCustomEvent("SetRaw");
        var toggle=VM(GameObject.Find("Lab reach toggle").GetComponent<BirdLabToggle>());
        var station=Get<GameObject>(toggle,"target");
        Require(station!=null && !station.activeSelf,"Reach station is optional and saved off");
        CheckReachControlLayout();
        var bridges=new UdonBehaviour[2];var pointers=new UdonBehaviour[2];
        foreach(var proxy in station.GetComponentsInChildren<BirdAvatarUiInput>(true))
        {
            int side=proxy.input.rightHand?1:0;bridges[side]=VM(proxy);pointers[side]=VM(proxy.pointer);
            Require(Get<UdonBehaviour>(pointers[side],"cursor")==null,"Bridge has sole ownership of explicit pointer");
        }
        var scroll=VM(station.GetComponentInChildren<BirdUiSphericalScroll>(true));
        var router=VM(station.GetComponentInChildren<BirdUiRouter>(true));
        var reset=VM(station.GetComponentInChildren<BirdLabScrollControl>(true));
        var sphere=Get<SphereCollider>(scroll,"sphere");var content=Get<Transform>(scroll,"rotationTarget");
        var elements=station.GetComponentsInChildren<BirdUiElement>(true);
        Require(elements.Length==12,"Twelve authored highlight targets");
        Require(Get<bool>(router,"postLateUpdate") && Get<bool>(scroll,"postLateUpdate"),"Consumers configured for post-IK phase");
        Quaternion initialRotation=content.localRotation;
        SetHands(0,1,Quaternion.identity);yield return null;CalibrateControls();yield return null;
        Require(toggle.RunEvent("_interact"),"Native reach station toggle");yield return null;yield return null;
        Require(station.activeSelf,"Native toggle enables saved station");
        for(int side=0;side<2;side++)
        {
            Require(Vector3.Distance(Get<Vector3>(pointers[side],"position"),Get<Vector3>(pointers[side],"origin"))>1e9f,"UI retains enormous logical Bird range");
            Require(Vector3.Distance(Get<Renderer>(views[side],"core").transform.position,Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position)<500.1f,"Presentation shell stays independent of UI reach");
        }
        foreach(var canvas in station.GetComponentsInChildren<Canvas>())
        {
            var backing=GameObject.Find(canvas.name+" backing");if(backing==null) continue;
            Near(backing.transform.position,canvas.transform.position+canvas.transform.forward*.025f,.0001f,"Reloaded reach label matches backing");
        }
        var previous=new int[2];for(int side=0;side<2;side++) previous[side]=Get<int>(pointers[side],"revision");
        for(int frame=0;frame<40;frame++)
        {
            SetHands(55+frame,1,Quaternion.Euler(frame*.7f,frame*2,frame*.2f));yield return null;
            for(int side=0;side<2;side++)
            {
                Require(Get<int>(bridges[side],"submittedFrame")==Time.frameCount,"Bridge ran in this post-IK frame");
                Require(Get<bool>(pointers[side],"tracked"),"Fresh calibrated UI pointer");
                Near(Get<Vector3>(pointers[side],"position"),Get<Vector3>(cursors[side],"position"),.00001f,"UI uses same-frame logical Bird");
                Near(Get<Vector3>(pointers[side],"origin"),Get<Vector3>(cursors[side],"handRoot"),.00001f,"UI uses same-frame hand origin");
                Require(Get<int>(pointers[side],"revision")==NextUiRevision(previous[side]),"Exactly one pointer sample per normal frame");
                Require(!Get<bool>(pointers[side],"pressed") && !Get<bool>(pointers[side],"pressedThisSample"),"Avatar bridge does not invent clicks");
                previous[side]=Get<int>(pointers[side],"revision");
                bridges[side].RunEvent("_postLateUpdate");
                Require(Get<int>(pointers[side],"revision")==previous[side],"Repeated same-frame callback is idempotent");
                pointers[side].SetProgramVariable("position",Vector3.one*12345); // Must be replaced next post-IK frame.
            }
            Require(Get<int>(router,"automaticFrame")==Time.frameCount && Get<int>(scroll,"automaticFrame")==Time.frameCount,"Consumers run in same post-IK frame");
        }
        yield return null;
        for(int side=0;side<2;side++) { views[side].enabled=false;views[side].RunEvent("_postLateUpdate"); }
        var geometryToggle=VM(GameObject.Find("Bird geometry toggle").GetComponent<BirdLabToggle>());geometryToggle.RunEvent("_interact");
        for(int frame=0;frame<3;frame++)
        {
            SetHands(70+frame,1,Quaternion.identity);yield return null;
            for(int side=0;side<2;side++)
            {
                Require(Get<bool>(pointers[side],"tracked") && !Get<Renderer>(views[side],"core").enabled,"UI survives optional presentation disable: tracked="+Get<bool>(pointers[side],"tracked")+" core="+Get<Renderer>(views[side],"core").enabled);
                Near(Get<Vector3>(pointers[side],"position"),Get<Vector3>(cursors[side],"position"),.00001f,"Hidden rendering does not alter logical interaction");
            }
        }
        for(int side=0;side<2;side++) views[side].enabled=true;
        geometryToggle.RunEvent("_interact");yield return null;yield return null;
        // Calibrate/cache articulated SDK hand poses while interaction is off.
        // Replaying a rigid transform of these bones aims at the authored station;
        // the runtime still derives sphere, range and point from bones each frame.
        toggle.RunEvent("_interact");yield return null;
        for(int side=0;side<2;side++) Require(!Get<bool>(pointers[side],"tracked") && Get<bool>(inputs[side],"calibrated"),"Turning station off cancels UI without losing calibration");
        SetHands(90,1,Quaternion.identity);yield return null;var near=SaveUiPose();
        SetHands(60,1,Quaternion.identity);yield return null;var far=SaveUiPose();
        for(int side=0;side<2;side++) Require(far.offsets[side].magnitude>near.offsets[side].magnitude+sphere.radius,"Articulated opening crosses beyond station back: near="+near.offsets[side].magnitude+" far="+far.offsets[side].magnitude);
        toggle.RunEvent("_interact");yield return null;yield return null;
        bridges[1].enabled=false;
        Vector3 center=sphere.transform.TransformPoint(sphere.center),origin=center-Vector3.forward*near.offsets[0].magnitude;
        scroll.SendCustomEvent("Cancel");
        for(int frame=0;frame<15;frame++)
        {
            PlaceUiPose(far,0,origin,Quaternion.Euler(0,Mathf.Sin(frame*.3f)*10,0)*Vector3.forward);yield return null;
            Require(Get<UdonBehaviour>(scroll,"activePointer")==null && Get<Vector3>(scroll,"angularVelocity")==Vector3.zero,"Far wrist sweep cannot acquire untouched station");
        }
        // Highlight through a real bone-derived near point in this exact frame.
        Vector3 colorPoint=elements[0].transform.position;
        PlaceUiPose(near,0,colorPoint-Vector3.forward*near.offsets[0].magnitude,Vector3.forward);yield return null;
        Require(Get<int>(VM(elements[0]),"state")==2,"Bone-derived point highlights authored color in same frame");
        Require(Get<UdonBehaviour>(VM(elements[0]),"lastPointer")==null,"Highlight is not a click/action");
        Capture("reach-highlight",new Vector3(0,1.65f,-3),center+Vector3.up*.3f);
        yield return null;
        PlaceUiPose(near,0,origin,Vector3.forward);yield return null;
        Near(Get<Vector3>(pointers[0],"position"),center,.001f,"Articulated near pose reaches station interior");
        PlaceUiPose(far,0,origin,Vector3.forward);yield return null;
        Require(Get<UdonBehaviour>(scroll,"activePointer")==pointers[0],"Real post-IK bone opening acquires back surface");
        Quaternion before=content.rotation;
        for(int frame=0;frame<30;frame++)
        { PlaceUiPose(far,0,origin,Quaternion.Euler(0,(frame+1)*.4f,0)*Vector3.forward);yield return null; }
        Require(Quaternion.Angle(before,content.rotation)>1 && Get<Vector3>(scroll,"angularVelocity").magnitude>0,"Wrist motion drives reusable scroll through post-IK bridge");
        Quaternion beforeDisabledReset=content.rotation;reset.enabled=false;reset.RunEvent("_interact");
        Require(Quaternion.Angle(beforeDisabledReset,content.rotation)<.0001f,"Disabled native reset cannot alter rotation");reset.enabled=true;
        Quaternion once=content.rotation;
        scroll.RunEvent("_postLateUpdate");scroll.RunEvent("_lateUpdate");router.RunEvent("_postLateUpdate");
        Require(Quaternion.Angle(once,content.rotation)<.0001f,"Repeated automatic callbacks cannot integrate twice per frame");
        scroll.SetProgramVariable("postLateUpdate",false);scroll.RunEvent("_lateUpdate");
        Require(Quaternion.Angle(once,content.rotation)<.0001f,"Automatic phase change in one frame cannot double-step");scroll.SetProgramVariable("postLateUpdate",true);
        PlaceUiPose(near,0,origin,Vector3.forward);yield return null;
        Require(Get<UdonBehaviour>(scroll,"activePointer")==null && Get<Vector3>(scroll,"angularVelocity").magnitude>0,"Articulated withdrawal coasts");
        before=content.rotation;
        for(int i=0;i<8;i++) { PlaceUiPose(near,0,origin,Vector3.forward);yield return null; }
        Require(Quaternion.Angle(before,content.rotation)>.01f,"Unpressed inside point leaves visible coast");
        Capture("reach-coast",new Vector3(0,1.65f,-3),center+Vector3.up*.3f);
        Require(reset.RunEvent("_interact"),"Native reset works without Bird clicking");
        Require(Quaternion.Angle(content.localRotation,initialRotation)<.001f && Get<Vector3>(scroll,"angularVelocity")==Vector3.zero,"Native reset restores authored orientation and cancels motion");
        yield return null;
        // Loss/recovery and both hands retain source/calibration ownership.
        positions[Bone(0,0)]=Vector3.zero;yield return null;
        Require(!Get<bool>(pointers[0],"tracked") && Get<bool>(inputs[0],"calibrated"),"Missing SDK bone cancels UI, retains calibration");
        PlaceUiPose(near,0,origin,Vector3.forward);yield return null;
        Require(Get<bool>(pointers[0],"tracked") && !Get<bool>(pointers[0],"hasHistory"),"Recovery seeds pointer without prior interaction history");
        bridges[1].enabled=true;yield return null;yield return null;
        PlaceUiPose(near,0,origin,Vector3.forward);
        Vector3 rightOrigin=center-Vector3.forward*near.offsets[1].magnitude;
        PlaceUiPose(near,1,rightOrigin,Vector3.forward);yield return null;
        PlaceUiPose(far,1,rightOrigin,Vector3.forward);yield return null;
        Require(Get<UdonBehaviour>(scroll,"activePointer")==pointers[1],"Mirrored right avatar hand acquires through same pipeline");
        bridges[1].enabled=false;yield return null;
        Require(Get<UdonBehaviour>(scroll,"activePointer")==null && Get<Vector3>(scroll,"angularVelocity")==Vector3.zero,"Disabling active source bridge cancels drive/inertia");
        Require(Get<bool>(inputs[1],"calibrated"),"UI bridge disable does not reset avatar input");
        bridges[1].enabled=true;yield return null;yield return null;
        Require(Get<UdonBehaviour>(scroll,"activePointer")==null,"Re-enabled outside pointer requires new interior contact");

        // Contract faults and rebinding operate on the VM heap, with real frame dispatch.
        bridges[0].SetProgramVariable("acceptedTime",Time.realtimeSinceStartup-1);yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Long source gap reseeds UI history");
        bridges[0].SetProgramVariable("acceptedTime",Time.realtimeSinceStartup+10);yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Clock rollback reseeds UI history");
        bridges[0].SetProgramVariable("acceptedFrame",Time.frameCount-5);yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Skipped callback frames invalidate pointer history");
        pointers[0].SetProgramVariable("cursor",cursors[0]);yield return null;
        Require(!Get<bool>(pointers[0],"tracked"),"Conflicting early cursor poller is rejected");pointers[0].SetProgramVariable("cursor",null);yield return null;
        Require(Get<bool>(pointers[0],"tracked") && !Get<bool>(pointers[0],"hasHistory"),"Exclusive-producer configuration recovers cleanly");
        bridges[0].SetProgramVariable("input",inputs[1]);yield return null;
        Near(Get<Vector3>(pointers[0],"position"),Get<Vector3>(cursors[1],"position"),.0001f,"Rebinding uses the new source point");
        Require(!Get<bool>(pointers[0],"hasHistory"),"Rebinding source resets history");bridges[0].SetProgramVariable("input",inputs[0]);yield return null;
        bridges[0].SetProgramVariable("pointer",null);yield return null;
        Require(!Get<bool>(pointers[0],"tracked"),"Unbinding releases the old pointer");bridges[0].SetProgramVariable("pointer",pointers[0]);yield return null;
        Require(Get<bool>(pointers[0],"tracked") && !Get<bool>(pointers[0],"hasHistory"),"Rebinding pointer seeds new history");
        var filter=VM(FindObjectOfType<BirdLabFilterControl>());filter.RunEvent("_interact");yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Raw/filtered comparison switch rebases interaction history");
        Near(Get<Vector3>(pointers[0],"position"),Get<Vector3>(cursors[0],"position"),.0001f,"Bridge follows accepted filtered output");
        filter.RunEvent("_interact");yield return null;
        Require(Get<bool>(filter,"adaptive") && !Get<bool>(pointers[0],"hasHistory"),"Legacy to adaptive rebases even though smoothing stays true");
        var policy=(UdonBehaviour)cursors[0].GetProgramVariable("adaptiveFilter");
        policy.SetProgramVariable("farResponseSeconds",.06f);yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Adaptive settings reseed interaction history");
        policy.SetProgramVariable("farResponseSeconds",.05f);yield return null;
        filter.SendCustomEvent("SetRaw");filter.SendCustomEvent("SetAdaptive");yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Mode round-trip within a frame cannot retain contact history");
        filter.RunEvent("_interact");yield return null;
        Require(Get<bool>(filter,"sphere") && !Get<bool>(pointers[0],"hasHistory"),"Adaptive to sphere rebases cleanly");
        var spherePolicy=(UdonBehaviour)cursors[0].GetProgramVariable("sphereFilter");
        float oldCutoff=Get<float>(spherePolicy,"minimumCutoff");
        spherePolicy.SetProgramVariable("minimumCutoff",oldCutoff+1);yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Sphere settings rebase UI contact");
        spherePolicy.SetProgramVariable("minimumCutoff",oldCutoff);yield return null;yield return null;
        filter.SendCustomEvent("SetRaw");filter.SendCustomEvent("SetSphere");yield return null;
        Require(!Get<bool>(pointers[0],"hasHistory"),"Sphere round trip within one observed frame rebases contact");
        filter.RunEvent("_interact");yield return null;
        Require(!Get<bool>(filter,"filtered") && !Get<bool>(pointers[0],"hasHistory"),"Sphere to RAW rebases cleanly");
        SetHands(0,1,Quaternion.identity);yield return null;CalibrateControls();yield return null;
        for(int side=0;side<2;side++) Require(!Get<bool>(pointers[side],"hasHistory"),"Explicit SET rebases UI even when no invalid sample is observed");
        // A stale source cannot be reused, even if a renderer remains visible.
        inputs[0].SetProgramVariable("sampledFrame",Time.frameCount-1);bridges[0].SetProgramVariable("submittedFrame",-1);bridges[0].RunEvent("_postLateUpdate");
        Require(!Get<bool>(pointers[0],"tracked"),"Stale source frame rejected explicitly");yield return null;
        Require(Get<bool>(pointers[0],"tracked") && !Get<bool>(pointers[0],"hasHistory"),"Fresh source recovers after stale-frame rejection");
        inputs[0].SendCustomEvent("ResetCalibration");yield return null;
        Require(!Get<bool>(pointers[0],"tracked") && Get<bool>(pointers[1],"tracked"),"Reset one hand preserves other UI input");
        toggle.RunEvent("_interact");yield return null;
        for(int side=0;side<2;side++) Require(!Get<bool>(pointers[side],"tracked"),"Station off leaves no UI pointer live");
        Require(Get<Vector3>(scroll,"angularVelocity")==Vector3.zero,"Station off clears scrolling");
        Capture("reach-off",new Vector3(0,1.65f,-3),new Vector3(0,1.6f,2));
    }
    void CheckReachControlLayout()
    {
        var label=GameObject.Find("Lab reach toggle label").GetComponentInChildren<UnityEngine.UI.Text>();
        Near(label.transform.position,GameObject.Find("Lab reach toggle").transform.position-Vector3.forward*.075f,.0001f,"Saved native reach label stays on its button");
        var camera=new GameObject("Reach layout audit camera").AddComponent<Camera>();camera.transform.position=new Vector3(0,1.65f,-3);camera.fieldOfView=75;camera.aspect=1.6f;
        try
        {
            Rect button=ProjectedText(camera,label.rectTransform);
            foreach(string name in new[]{"Tracking origins","Bone availability","Bird target hint"})
            {
                var board=GameObject.Find(name).GetComponentInChildren<UnityEngine.UI.Text>();
                Require(!button.Overlaps(ProjectedText(camera,board.rectTransform)),"Reach control clears existing board from spawn: "+name);
            }
        }
        finally { DestroyImmediate(camera.gameObject); }
    }
    static Rect ProjectedText(Camera camera,RectTransform rect)
    {
        var corners=new Vector3[4];rect.GetWorldCorners(corners);Vector2 min=Vector2.one*float.PositiveInfinity,max=Vector2.one*float.NegativeInfinity;
        foreach(var corner in corners) { Vector2 p=camera.WorldToViewportPoint(corner);min=Vector2.Min(min,p);max=Vector2.Max(max,p); }
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
}
#endif
