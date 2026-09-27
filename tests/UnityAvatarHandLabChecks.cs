#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.Udon;

// Exercises saved components through their compiled Udon and real frame dispatch.
// SDK return-value fixtures substitute articulated hands, never call C# proxies.
[DefaultExecutionOrder(32000)]
public partial class UnityAvatarHandLabChecks : MonoBehaviour
{
    const string Active="Bird.AvatarHandLab.Checks";
    static Func<VRCPlayerApi,HumanBodyBones,Vector3> originalPosition;
    static Func<VRCPlayerApi,HumanBodyBones,Quaternion> originalRotation;
    static VRCPlayerApi eventRemote;
    static float savedHeight;
    static readonly Dictionary<HumanBodyBones,Vector3> positions=new Dictionary<HumanBodyBones,Vector3>();
    static readonly Dictionary<HumanBodyBones,Quaternion> rotations=new Dictionary<HumanBodyBones,Quaternion>();
    static readonly string[] suffix={"Hand","ThumbProximal","ThumbIntermediate","ThumbDistal","IndexProximal","IndexIntermediate","IndexDistal","MiddleProximal","MiddleIntermediate","MiddleDistal","RingProximal","RingIntermediate","RingDistal","LittleProximal","LittleIntermediate","LittleDistal"};
    static readonly Vector3[,] expectedTips=new Vector3[2,5];
    static Vector3 expectedPalmNormal;
    readonly UdonBehaviour[] inputs=new UdonBehaviour[2], cursors=new UdonBehaviour[2], views=new UdonBehaviour[2];
    readonly UdonBehaviour[] geometry=new UdonBehaviour[2], fitters=new UdonBehaviour[2];
    readonly Vector3[] previousRaw=new Vector3[2];
    UdonBehaviour target;
    IEnumerator sequence;
    float deadline;
    int assertions, frames;
    bool started;
    string realBaseline="";
    readonly List<string> measurements=new List<string>{"phase,side,bend_deg,scale,range_m,finger_length_m,flat_weight,fist_weight"};
    readonly List<string> temporal=new List<string>{"phase,frame,side,raw_m,shown_m,calibrated,data_ready"};
    public static void Run()
    {
        File.WriteAllText("lab-hand-check-result.txt","PENDING");
        EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdTrackingLab.unity");
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failure");
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Avatar hand lab checks").AddComponent<UnityAvatarHandLabChecks>(); }
    void Start() { deadline=Time.unscaledTime+300; }
    void LateUpdate()
    {
        if(!SessionState.GetBool(Active,false)) return;
        try
        {
            if(Time.unscaledTime>deadline) throw new Exception("Avatar hand lab timeout");
            if(!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad<3) return;
            if(!started)
            {
                started=true;
                var authored=FindObjectsOfType<BirdAvatarHandInput>(); Require(authored.Length==2,"Two authored inputs");
                foreach(var source in authored)
                {
                    int side=source.rightHand?1:0; inputs[side]=VM(source); cursors[side]=VM(source.cursor);
                    fitters[side]=VM(source.cursor.fitter);
                    foreach(var view in FindObjectsOfType<BirdLabPointView>()) if(view.input==source) views[side]=VM(view);
                    foreach(var view in FindObjectsOfType<BirdLabGeometryView>()) if(view.input==source) geometry[side]=VM(view);
                }
                target=VM(FindObjectOfType<BirdLabPointTarget>()); sequence=Scenarios();
            }
            frames++;
            if(!sequence.MoveNext()) Finish(true,"Compiled Udon: "+assertions+" assertions over "+frames+" normal frames. "+realBaseline+" Articulated SDK fixtures, not physical hand tracking; inferred endpoints/click fidelity remain separate.");
        }
        catch(Exception e) { Finish(false,e.ToString()); }
    }
    IEnumerator Scenarios()
    {
        CheckDiagnosticRendering();
        Require(!Get<bool>(VM(FindObjectOfType<BirdLabFilterControl>()),"filtered"),"Saved lab starts in RAW for input diagnosis");
        for(int side=0;side<2;side++)
        {
            Require(!Get<bool>(cursors[side],"smoothing"),"Authored cursor starts without filtered history");
            Require(Get<int>(inputs[side],"available")==16,"ClientSim supplies all 16 avatar origins");
            var sampled=Get<Vector3[]>(inputs[side],"bonePositions");
            for(int i=0;i<16;i++) Near(sampled[i],Networking.LocalPlayer.GetBonePosition(Bone(side,i)),.00001f,"Real SDK source binding");
            Require(!Get<bool>(inputs[side],"calibrated") && !Get<bool>(cursors[side],"poseValid"),"No automatic calibration");
            Require(geometry[side]!=null && !Get<LineRenderer>(geometry[side],"birdRay").enabled,"Uncalibrated geometry stays hidden");
            inputs[side].SendCustomEvent("CalibrateOpenHand");
            realBaseline+=(side==0?"Left":"Right")+" default-avatar open-pose calibration="+Get<bool>(inputs[side],"calibrated")+"; ";
            cursors[side].SetProgramVariable("smoothing",false);
        }
        yield return null;
        var realRanges=new float[2]; var realLengths=new float[2];
        for(int side=0;side<2;side++)
        {
            Require(Get<bool>(inputs[side],"calibrated") && Get<bool>(cursors[side],"poseValid"),"Default ClientSim avatar drives Bird");
            realRanges[side]=Range(side); realLengths[side]=Get<float>(inputs[side],"fingerLength");
            realBaseline+=(side==0?"Left":"Right")+" normalized range="+realRanges[side].ToString("G6")+"m, estimated finger length="+realLengths[side].ToString("G6")+"m; ";
        }
        savedHeight=Networking.LocalPlayer.GetAvatarEyeHeightAsMeters();
        foreach(float scale in new[]{.5f,1.5f,1f})
        {
            Networking.LocalPlayer.SetAvatarEyeHeightByMeters(savedHeight*scale); yield return null; yield return null;
            for(int side=0;side<2;side++)
            {
                Require(Get<bool>(inputs[side],"calibrated") && Get<bool>(cursors[side],"poseValid"),"Real SDK avatar scaling retains input");
                Require(Mathf.Abs(Range(side)-realRanges[side])<Mathf.Max(.002f,realRanges[side]*.01f),"Real SDK normalized range under avatar scaling");
                Require(Mathf.Abs(Get<float>(inputs[side],"fingerLength")-realLengths[side]*scale)<.0002f,"Real SDK phalanx lengths scale");
            }
        }
        savedHeight=0;
        for(int side=0;side<2;side++) inputs[side].SendCustomEvent("ResetCalibration");
        originalPosition=VRCPlayerApi._GetBonePosition; originalRotation=VRCPlayerApi._GetBoneRotation;
        Require(originalPosition!=null && originalRotation!=null,"SDK position and rotation providers available");
        VRCPlayerApi._GetBonePosition=Position; VRCPlayerApi._GetBoneRotation=Rotation;
        SetHands(0,1,Quaternion.identity); yield return null;
        CalibrateControls(); yield return null;
        CheckHands("open",0,1);
        for(int side=0;side<2;side++) Require(Range(side)>1e9f,"Unclamped far endpoint");
        CheckActualPointRendering("far");
        foreach(float bend in new[]{-5f,0,5,15,30,45,60,90,120,150,180,210,230})
        {
            SetHands(bend,1,Quaternion.identity); yield return null; CheckHands("articulation",bend,1);
            if(bend==90) CheckActualPointRendering("near");
            if(bend>=210) for(int side=0;side<2;side++) Near(Get<Vector3>(cursors[side],"rawPosition"),Get<Vector3>(cursors[side],"handRoot"),.00001f,"Fist stays at palm");
        }
        foreach(float bend in new[]{0f,30,60,90,150,230})
        {
            SetHands(bend,1,Quaternion.identity); yield return null;
            for(int side=0;side<2;side++) previousRaw[side]=Get<Vector3>(cursors[side],"rawPosition")-Get<Vector3>(cursors[side],"handRoot");
            foreach(float scale in new[]{.5f,1.5f,3f})
            {
                var rig=Quaternion.Euler(37,121,180);
                SetHands(bend,scale,rig); yield return null; CheckHands("scale_transform",bend,scale);
                for(int side=0;side<2;side++) Near(Get<Vector3>(cursors[side],"rawPosition")-Get<Vector3>(cursors[side],"handRoot"),rig*previousRaw[side],Mathf.Max(.001f,previousRaw[side].magnitude*.002f),"Scale-normalized rigid/upside-down range");
            }
        }
        // Consecutive changing SDK poses catch accidental sample throttling and pre-IK order.
        for(int frame=0;frame<30;frame++)
        {
            SetHands(55+frame,1,Quaternion.Euler(frame,frame*2,0)); yield return null; CheckHands("cadence",55+frame,1);
        }
        SetHands(75,1,Quaternion.identity); yield return null;
        var geometryToggle=VM(GameObject.Find("Bird geometry toggle").GetComponent<BirdLabToggle>());
        var beforeGeometryToggle=Get<Vector3>(cursors[0],"rawPosition");
        Require(geometryToggle.RunEvent("_interact"),"Native geometry toggle off"); yield return null;
        Require(!Get<LineRenderer>(geometry[0],"birdRay").gameObject.activeInHierarchy,"Geometry toggle hides its ray");
        Near(Get<Vector3>(cursors[0],"rawPosition"),beforeGeometryToggle,.00001f,"Geometry cannot change solver output");
        Require(Get<Renderer>(views[0],"core").enabled,"Geometry is independent of ordinary point view");
        Require(geometryToggle.RunEvent("_interact"),"Native geometry toggle on");
        // The SDK drains PostLateUpdate registrations after dispatching that
        // frame, so reactivation joins the following dispatch, not the queued one.
        yield return null; yield return null;
        Require(Get<LineRenderer>(geometry[0],"birdRay").enabled,"Geometry resumes after SDK post-IK re-registration");
        var origin=Get<Vector3>(cursors[0],"handRoot"); var point=Get<Vector3>(cursors[0],"position");
        target.transform.position=Vector3.Lerp(origin,point,.5f); yield return null;
        Require(Get<bool>(target,"highlighted"),"Logical point-through target highlights");
        target.transform.position=origin+(point-origin)*2+Vector3.right*.7f; yield return null;
        Require(!Get<bool>(target,"highlighted"),"Off-axis/beyond-end target does not highlight");
        for(int side=0;side<2;side++) cursors[side].SetProgramVariable("smoothing",true);
        SetHands(0,1,Quaternion.identity); yield return null;
        SetHands(230,1,Quaternion.identity); yield return null;
        for(int side=0;side<2;side++) Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"handRoot"),.00001f,"Immediate fist return clears distant filter history");
        for(int side=0;side<2;side++) cursors[side].SetProgramVariable("smoothing",false);
        // The authored native comparison control changes only smoothing. Reproduce
        // the far-to-near history case without changing the production recurrence.
        var filter=VM(FindObjectOfType<BirdLabFilterControl>());
        filter.SendCustomEvent("SetFiltered");
        SetHands(0,1,Quaternion.identity); yield return null; CalibrateControls(); yield return null;
        for(int frame=0;frame<12;frame++) yield return null;
        SetHands(90,1,Quaternion.identity); yield return null;
        for(int side=0;side<2;side++)
        {
            Require(Range(side)<4,"Comparison pose places raw Bird in the working volume");
            previousRaw[side]=Get<Vector3>(cursors[side],"rawPosition");
            RecordTemporal("legacy_return",side);
        }
        filter.SendCustomEvent("SetRaw");
        for(int side=0;side<2;side++) Require(Get<bool>(inputs[side],"calibrated") && !Get<bool>(cursors[side],"poseValid"),"Mode change clears history, keeps calibration");
        yield return null;
        for(int side=0;side<2;side++)
        {
            Require(!Get<bool>(cursors[side],"smoothing"),"RAW bypasses filtering on both hands");
            Near(Get<Vector3>(cursors[side],"rawPosition"),previousRaw[side],.00001f,"Mode switch leaves geometry unchanged");
            Near(Get<Vector3>(cursors[side],"position"),previousRaw[side],.00001f,"RAW displays incoming point next frame");
            RecordTemporal("raw_comparison",side);
        }
        filter.enabled=false; filter.SendCustomEvent("SetFiltered"); yield return null;
        Require(!Get<bool>(filter,"filtered"),"Disabled comparison control cannot change mode"); filter.enabled=true;
        Require(filter.RunEvent("_interact"),"Native FILTERED comparison event"); yield return null;
        for(int side=0;side<2;side++) Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"rawPosition"),.00001f,"Filtered mode seeds from fresh point");
        filter.SendCustomEvent("SetFiltered");
        for(int side=0;side<2;side++) Require(Get<bool>(cursors[side],"poseValid"),"Setting the same mode does not reset it");
        // Simulate the unscaled timestamp discontinuity after application suspension,
        // without an intervening invalid-bone frame or another calibration.
        SetHands(0,1,Quaternion.identity); yield return null; CalibrateControls(); yield return null;
        SetHands(90,1,Quaternion.identity);
        inputs[0].SetProgramVariable("lastSampleTime",Time.realtimeSinceStartup-1);
        yield return null;
        Require(Get<bool>(inputs[0],"calibrated"),"Sample gap retains this avatar's calibration");
        Near(Get<Vector3>(cursors[0],"position"),Get<Vector3>(cursors[0],"rawPosition"),.00001f,"Sample gap reseeds distant history");
        RecordTemporal("sample_gap_recovery",0);
        // Every required origin and invalid distal orientation independently pause
        // only one hand. Bone-local calibration survives sample loss; cursor state does not.
        for(int fault=0;fault<36;fault++)
        {
            SetHands(0,1,Quaternion.identity); yield return null; CalibrateControls(); yield return null;
            int side=fault<32?fault/16:fault%2;
            if(fault<32) positions[Bone(side,fault%16)]=Vector3.zero;
            else if(fault<34) positions[Bone(side,8)]=new Vector3(float.NaN,1,1);
            else rotations[Bone(side,9)]=new Quaternion(0,0,0,0);
            yield return null;
            Require(!Get<bool>(inputs[side],"dataReady") && Get<bool>(inputs[side],"calibrated") && !Get<bool>(cursors[side],"poseValid"),"Invalid bone pauses source/solver and retains calibration "+fault);
            Require(!Get<Renderer>(views[side],"core").enabled,"Loss hides point");
            Require(!Get<LineRenderer>(views[side],"directionGuide").enabled,"Loss hides guide");
            Require(!Get<LineRenderer>(geometry[side],"birdRay").enabled && !Get<Renderer>(geometry[side],"fitCenter").enabled,"Loss hides geometry and fitted center");
            foreach(var marker in Get<Transform[]>(views[side],"tipMarkers")) Require(!marker.gameObject.activeSelf,"Loss hides estimated tip");
            Require(Get<bool>(inputs[1-side],"calibrated") && Get<bool>(cursors[1-side],"poseValid"),"Other hand survives fault");
            SetHands(90,1,Quaternion.identity); yield return null;
            Require(Get<bool>(inputs[side],"calibrated") && Get<bool>(cursors[side],"poseValid"),"Same-avatar data recovery resumes without SET");
            Near(Get<Vector3>(cursors[side],"position"),Get<Vector3>(cursors[side],"rawPosition"),.00001f,"Recovery cannot reuse old far filter history");
            CheckHands("fault_recovery",90,1);
        }
        positions[Bone(0,4)]=Vector3.zero;
        inputs[0].SetProgramVariable("nextText",0f);
        for(int frame=0;frame<30;frame++)
        {
            yield return null;
            Require(Get<bool>(inputs[0],"calibrated") && !Get<bool>(cursors[0],"poseValid"),"Longer data gap remains paused with calibration retained");
        }
        Require(Get<UnityEngine.UI.Text>(inputs[0],"status").text.Contains("Paused / SET retained"),"Panel explains paused calibrated state");
        inputs[0].SendCustomEvent("CalibrateOpenHand"); Require(!Get<bool>(inputs[0],"calibrated"),"Explicit SET cannot calibrate invalid skeleton");
        SetHands(0,1,Quaternion.identity); yield return null;
        Require(!Get<bool>(inputs[0],"calibrated"),"Failed explicit calibration needs a successful SET");
        SetHands(90,1,Quaternion.identity); yield return null;
        CalibrateControls(false); yield return null;
        for(int side=0;side<2;side++) Require(!Get<bool>(inputs[side],"calibrated"),"Curled pose refuses open-hand calibration");
        SetHands(0,1,Quaternion.identity); yield return null; CalibrateControls(); yield return null;
        inputs[0].enabled=false; yield return null;
        Require(!Get<bool>(cursors[0],"poseValid") && !Get<Renderer>(views[0],"core").enabled,"Disabled input clears downstream display");
        inputs[0].enabled=true; yield return null;
        Require(!Get<bool>(inputs[0],"calibrated"),"Re-enable requires calibration");
        CalibrateControls(); yield return null;
        inputs[0].SetProgramVariable("rightHand",true); yield return null;
        Require(!Get<bool>(inputs[0],"calibrated"),"Hand-role change invalidates calibration"); inputs[0].SetProgramVariable("rightHand",false);
        SetHands(0,1,Quaternion.identity); yield return null; CalibrateControls(); yield return null;
        inputs[0].SetProgramVariable("tipLengthRatio",float.PositiveInfinity); yield return null;
        Require(!Get<bool>(inputs[0],"calibrated") && !Get<bool>(inputs[0],"dataReady"),"Invalid authoring settings cancel"); inputs[0].SetProgramVariable("tipLengthRatio",.8f);
        SetHands(0,1,Quaternion.identity); yield return null; CalibrateControls(); yield return null;
        int playersBefore=VRCPlayerApi.GetPlayerCount(); ClientSimMain.SpawnRemotePlayer("Avatar hand lab event fixture");
        var players=new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()]; VRCPlayerApi.GetPlayers(players);
        foreach(var player in players) if(Utilities.IsValid(player) && !player.isLocal && player.displayName=="Avatar hand lab event fixture") eventRemote=player;
        Require(eventRemote!=null,"Remote event fixture created"); yield return null;
        var sender=new ClientSimUdonManagerEventSender(UdonManager.Instance);
        sender.RunEvent("_onAvatarChanged",("player",eventRemote));
        for(int side=0;side<2;side++) Require(Get<bool>(inputs[side],"calibrated"),"Remote avatar change preserves local calibration");
        sender.RunEvent("_onAvatarChanged",("player",Networking.LocalPlayer));
        for(int side=0;side<2;side++) Require(!Get<bool>(inputs[side],"calibrated") && !Get<bool>(cursors[side],"poseValid"),"Local avatar event immediately cancels");
        yield return null;
        for(int side=0;side<2;side++) Require(!Get<Renderer>(views[side],"core").enabled,"Avatar event clears downstream view");
        ClientSimMain.RemovePlayer(eventRemote); eventRemote=null; Require(VRCPlayerApi.GetPlayerCount()==playersBefore,"Remote fixture removed");
        CalibrateControls(); yield return null;
        SetHands(75,1,Quaternion.Euler(0,180,0)); yield return null;
        Capture("bird-station",new Vector3(-1.3f,1.7f,-4.5f),new Vector3(-1.7f,1.5f,0));
        Capture("bird-calibration",new Vector3(-.5f,1.65f,-4.5f),new Vector3(-3.35f,1.55f,-1.2f));
        SetHands(90,1,Quaternion.identity); yield return null;
        Capture("bird-geometry",new Vector3(-.22f,1.5f,-1.62f),new Vector3(-.22f,1.43f,-2));
        for(int side=0;side<2;side++) VM(GameObject.Find(side==0?"RESET LEFT":"RESET RIGHT").GetComponent<BirdLabHandControl>()).RunEvent("_interact");
        yield return null;
        for(int side=0;side<2;side++) Require(!Get<bool>(cursors[side],"poseValid") && !Get<Renderer>(views[side],"core").enabled,"Native reset hides Bird");
        var adaptive=AdaptiveScenarios(); while(adaptive.MoveNext()) yield return null;
        var ui=UiScenarios(); while(ui.MoveNext()) yield return null;
        Restore();
    }
    void CalibrateControls(bool expect=true)
    {
        for(int side=0;side<2;side++)
        {
            Require(VM(GameObject.Find(side==0?"SET LEFT":"SET RIGHT").GetComponent<BirdLabHandControl>()).RunEvent("_interact"),"Native set control");
            if(expect) Require(Get<bool>(inputs[side],"calibrated"),"Open hand calibrates "+side+"; "+Get<string>(inputs[side],"calibrationMessage")+"; ready="+Get<bool>(inputs[side],"dataReady"));
        }
    }
    void CheckHands(string phase,float bend,float scale)
    {
        for(int side=0;side<2;side++)
        {
            Require(Get<int>(inputs[side],"sampledFrame")==Time.frameCount,"Current post-IK frame");
            Require(Get<bool>(inputs[side],"calibrated") && Get<bool>(cursors[side],"poseValid"),"Calibrated pose valid "+phase+" bend="+bend);
            Require(!Get<bool>(cursors[side],"clicksAllowed") && !Get<bool>(cursors[side],"selected"),"No inferred clicks");
            var tips=Get<Vector3[]>(inputs[side],"estimatedTips");
            for(int f=0;f<5;f++) Near(tips[f],expectedTips[side,f],.0001f,"Distal orientation and calibrated local axis "+f);
            var points=Get<Vector3[]>(cursors[side],"points"); Require(points.Length==16,"Canonical fit count");
            Near(Get<Vector3>(inputs[side],"normal"),expectedPalmNormal,.0001f,"Anatomical palm-facing normal; independent of flexion and wrist orientation");
            var fitVm=fitters[side];
            bool valid=Get<bool>(fitVm,"fitValid"); Vector3 center=Get<Vector3>(fitVm,"center"); float radius=Get<float>(fitVm,"radius");
            Require(Get<Renderer>(geometry[side],"fitCenter").enabled==valid,"Center visibility reports actual fit validity");
            foreach(var ring in Get<LineRenderer[]>(geometry[side],"sphereRings"))
            {
                Require(ring.enabled==valid,"No invented sphere for singular fit");
                if(valid) for(int j=0;j<ring.positionCount;j+=8) Require(Mathf.Abs(Vector3.Distance(ring.GetPosition(j),center)-radius)<.0001f,"Wire sphere uses actual fitted radius and center");
            }
            if(valid) Near(Get<Renderer>(geometry[side],"fitCenter").transform.position,center,.00001f,"Actual fitted center marker");
            Near(points[0],positions[Bone(side,2)],.00001f,"Thumb intermediate mapping"); Near(points[1],positions[Bone(side,3)],.00001f,"Thumb distal mapping");
            Near(points[2],expectedTips[side,0],.0001f,"Thumb endpoint mapping"); Near(points[3],positions[Bone(side,4)],.00001f,"Index proximal mapping");
            for(int f=2;f<5;f++) for(int j=0;j<4;j++) Near(points[4+(f-2)*4+j],j==3?expectedTips[side,f]:positions[Bone(side,1+f*3+j)],.0001f,"Fit finger mapping");
            Vector3 raw=Get<Vector3>(cursors[side],"rawPosition"); Require(!float.IsNaN(raw.sqrMagnitude) && !float.IsInfinity(raw.sqrMagnitude),"Finite logical range");
            var core=Get<Renderer>(views[side],"core"); Require(core.enabled,"Point view enabled same frame");
            var birdRay=Get<LineRenderer>(geometry[side],"birdRay");
            Near(birdRay.GetPosition(0),Get<Vector3>(cursors[side],"handRoot"),.00001f,"Bird ray starts at root");
            Near(birdRay.GetPosition(1),core.transform.position,.0001f,"Bird ray ends at actual displayed Bird");
            var guide=Get<LineRenderer>(views[side],"directionGuide");
            Vector3 root=Get<Vector3>(cursors[side],"handRoot"), filtered=Get<Vector3>(cursors[side],"position");
            Near(guide.GetPosition(0),root,.00001f,"Guide starts at logical root");
            Near(guide.GetPosition(1),root+(filtered-root).normalized*Mathf.Min(.4f,(filtered-root).magnitude),.00001f,"Guide is bounded and uses logical direction");
            var head=Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            Require(Vector3.Distance(core.transform.position,head)<500.1f,"Visual shell does not constrain logical range");
            if(Vector3.Distance(Get<Vector3>(cursors[side],"position"),head)<=4) Require(Mathf.Abs(core.transform.localScale.x-.032f)<.00001f,"Fixed near cursor size");
            for(int f=0;f<5;f++) Near(Get<Transform[]>(views[side],"tipMarkers")[f].position,expectedTips[side,f],.0001f,"Visible inferred endpoint");
            measurements.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4:R},{5:R},{6:R},{7:R}",phase,side,bend,scale,Range(side),Get<float>(inputs[side],"fingerLength"),Get<float>(cursors[side],"flatWeight"),Get<float>(cursors[side],"fistWeight")));
        }
    }
    static void SetHands(float bend,float scale,Quaternion rig)
    {
        positions.Clear(); rotations.Clear();
        expectedPalmNormal=rig*Vector3.back;
        for(int side=0;side<2;side++)
        {
            Vector3 offset=new Vector3(side==0?-.22f:.22f,1.4f,-2);
            // These fingers flex toward -Z: that is the independently specified
            // palm side. A right hand with this orientation has its thumb on +X.
            // The old fixture reversed anatomy AND the adapter normal together,
            // so synthetic curl tests masked the palm-backward bug.
            Func<Vector3,Vector3> mirror=p=>new Vector3(side==0?p.x:-p.x,p.y,p.z);
            Func<Vector3,Vector3> point=p=>offset+rig*mirror(p)*scale;
            positions[Bone(side,0)]=point(new Vector3(-.02f,-.07f,0)); rotations[Bone(side,0)]=rig;
            for(int f=0;f<5;f++)
            {
                Vector3 start=f==0?new Vector3(-.045f,-.035f,0):new Vector3(-.025f+(f-1)*.018f,f==2?.005f:f==4?-.01f:0,0);
                float a=f==0?.023f:f==1?.032f:f==2?.038f:f==3?.034f:.027f;
                float b=f==0?.023f:f==1?.02f:f==2?.025f:f==3?.023f:.018f;
                Vector3 da=f==0?new Vector3(-.65f,.76f,0).normalized:Direction(bend*.32f);
                Vector3 db=f==0?da:Direction(bend*.72f), dc=f==0?da:Direction(bend);
                Vector3 middle=start+da*a, distal=middle+db*b;
                int first=1+f*3;
                positions[Bone(side,first)]=point(start); positions[Bone(side,first+1)]=point(middle); positions[Bone(side,first+2)]=point(distal);
                // Independent bone-local axes ensure calibration is not assuming Unity +Y.
                Quaternion bind=Quaternion.Euler(17+f*13,-32+f*20,12+side*37);
                rotations[Bone(side,first)]=rig*Quaternion.FromToRotation(Vector3.up,mirror(da))*bind;
                rotations[Bone(side,first+1)]=rig*Quaternion.FromToRotation(Vector3.up,mirror(db))*bind;
                rotations[Bone(side,first+2)]=rig*Quaternion.FromToRotation(Vector3.up,mirror(dc))*bind;
                expectedTips[side,f]=point(distal+dc*b*.8f);
            }
        }
    }
    static Vector3 Direction(float degrees) { float a=degrees*Mathf.Deg2Rad; return new Vector3(0,Mathf.Cos(a),-Mathf.Sin(a)); }
    static HumanBodyBones Bone(int side,int i) { return (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),(side==0?"Left":"Right")+suffix[i]); }
    static Vector3 Position(VRCPlayerApi player,HumanBodyBones bone) { return player.isLocal && positions.ContainsKey(bone)?positions[bone]:originalPosition(player,bone); }
    static Quaternion Rotation(VRCPlayerApi player,HumanBodyBones bone) { return player.isLocal && rotations.ContainsKey(bone)?rotations[bone]:originalRotation(player,bone); }
    static void Restore()
    {
        if(originalPosition!=null) VRCPlayerApi._GetBonePosition=originalPosition; if(originalRotation!=null) VRCPlayerApi._GetBoneRotation=originalRotation; originalPosition=null; originalRotation=null;
        if(eventRemote!=null) { ClientSimMain.RemovePlayer(eventRemote); eventRemote=null; }
        if(savedHeight>0 && Utilities.IsValid(Networking.LocalPlayer)) Networking.LocalPlayer.SetAvatarEyeHeightByMeters(savedHeight); savedHeight=0;
    }
    float Range(int side) { return Vector3.Distance(Get<Vector3>(cursors[side],"rawPosition"),Get<Vector3>(cursors[side],"handRoot")); }
    static UdonBehaviour VM(UdonSharp.UdonSharpBehaviour p) { return UdonSharpEditorUtility.GetBackingUdonBehaviour(p); }
    static T Get<T>(UdonBehaviour vm,string name) { return (T)vm.GetProgramVariable(name); }
    void Require(bool value,string message) { assertions++; if(!value) throw new Exception(message); }
    void Near(Vector3 a,Vector3 b,float tolerance,string message) { Require(Vector3.Distance(a,b)<=tolerance,message+" error="+Vector3.Distance(a,b)+" tolerance="+tolerance); }
    static void Capture(string name,Vector3 position,Vector3 look)
    {
        // The injected skeleton deliberately differs from the still-rendered simulator
        // avatar. Hide only humanoid artwork for these fixture captures, then restore it.
        var avatarRenderers=new Dictionary<Renderer,bool>();
        foreach(var animator in FindObjectsOfType<Animator>()) if(animator.isHuman)
            foreach(var renderer in animator.GetComponentsInChildren<Renderer>(true)) if(!avatarRenderers.ContainsKey(renderer)) { avatarRenderers.Add(renderer,renderer.enabled); renderer.enabled=false; }
        Directory.CreateDirectory("../Validation/TrackingLab"); var camera=new GameObject("Avatar fixture capture").AddComponent<Camera>(); camera.transform.position=position; camera.transform.LookAt(look); camera.fieldOfView=75; camera.farClipPlane=1000;
        camera.cullingMask=~((1<<5)|(1<<9)|(1<<10)|(1<<19)); var target=new RenderTexture(1600,1000,24){antiAliasing=4}; camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply(); File.WriteAllBytes("../Validation/TrackingLab/"+name+".png",image.EncodeToPNG()); RenderTexture.active=null; camera.targetTexture=null; target.Release(); Destroy(target); Destroy(image); Destroy(camera.gameObject);
        foreach(var item in avatarRenderers) item.Key.enabled=item.Value;
    }
    void RecordTemporal(string phase,int side)
    {
        temporal.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3:R},{4:R},{5},{6}",
            phase,frames,side,Range(side),(Get<Vector3>(cursors[side],"position")-Get<Vector3>(cursors[side],"handRoot")).magnitude,
            Get<bool>(inputs[side],"calibrated"),Get<bool>(inputs[side],"dataReady")));
    }
    void CheckActualPointRendering(string phase)
    {
        // Inspect the actual compiled-Udon view, not a substitute sphere/material.
        // Isolate its layer while retaining its transforms, property blocks and halo.
        var head=Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        var camera=new GameObject("Actual Bird render check").AddComponent<Camera>();
        camera.transform.position=head.position; camera.fieldOfView=60;
        camera.nearClipPlane=.01f; camera.farClipPlane=1000; camera.cullingMask=1<<31;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        var render=new RenderTexture(1024,1024,24){antiAliasing=4}; camera.targetTexture=render;
        var pixels=new Texture2D(1024,1024,TextureFormat.RGB24,false);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=31;
        var black=new Material(Shader.Find("Unlit/Color")){color=Color.black}; wall.GetComponent<Renderer>().sharedMaterial=black;
        try
        {
            for(int side=0;side<2;side++)
            {
                var core=Get<Renderer>(views[side],"core"); var halo=Get<LineRenderer>(views[side],"halo");
                int coreLayer=core.gameObject.layer,haloLayer=halo.gameObject.layer;
                core.gameObject.layer=halo.gameObject.layer=31;
                try
                {
                    camera.transform.LookAt(core.transform.position);
                    float range=Vector3.Distance(head.position,Get<Vector3>(cursors[side],"position"));
                    wall.transform.position=head.position+camera.transform.forward*Mathf.Min(20,range*.5f);
                    wall.transform.rotation=camera.transform.rotation; wall.transform.localScale=new Vector3(30,30,.01f);
                    for(int occluded=0;occluded<2;occluded++)
                    {
                        wall.SetActive(occluded==1); camera.Render(); RenderTexture.active=render;
                        pixels.ReadPixels(new Rect(0,0,1024,1024),0,0); pixels.Apply(); RenderTexture.active=null;
                        int colored=0;
                        foreach(var color in pixels.GetPixels32()) if(side==0?color.g>60 && color.b>60 && color.r<30:color.r>60 && color.b>30 && color.g<color.r*.65f) colored++;
                        Require(occluded==0?colored>=5:colored==0,"Actual "+phase+" Bird "+side+" render occluded="+occluded+" colored pixels="+colored);
                        if(occluded==0) File.WriteAllBytes("../Validation/TrackingLab/bird-visible-"+phase+"-"+side+".png",pixels.EncodeToPNG());
                    }
                }
                finally { core.gameObject.layer=coreLayer; halo.gameObject.layer=haloLayer; }
            }
        }
        finally
        {
            RenderTexture.active=null; camera.targetTexture=null; render.Release();
            DestroyImmediate(camera.gameObject); DestroyImmediate(wall); DestroyImmediate(black); DestroyImmediate(pixels); DestroyImmediate(render);
        }
    }
    void CheckDiagnosticRendering()
    {
        var camera=new GameObject("Isolated diagnostic render check").AddComponent<Camera>();
        camera.transform.position=new Vector3(1000,1000,997); camera.orthographic=true; camera.orthographicSize=1;
        camera.nearClipPlane=.01f; camera.farClipPlane=10; camera.cullingMask=1<<31;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.layer=31; sphere.transform.position=new Vector3(1000,1000,1000); sphere.transform.localScale=Vector3.one*.5f;
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.layer=31; wall.transform.position=new Vector3(1000,1000,999); wall.transform.localScale=new Vector3(2,2,.1f);
        var black=new Material(Shader.Find("Unlit/Color")){color=Color.black}; wall.GetComponent<Renderer>().sharedMaterial=black;
        var target=new RenderTexture(128,128,24); camera.targetTexture=target;
        var pixels=new Texture2D(128,128,TextureFormat.RGB24,false);
        for(int mode=0;mode<3;mode++)
        {
            sphere.GetComponent<Renderer>().sharedMaterial=mode==0?AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/TrackingLab/EstimatedTips.mat"):
                mode==1?FindObjectOfType<BirdHandDataProbe>().markers[0].GetComponent<Renderer>().sharedMaterial:
                AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/TrackingLab/BirdPoint.mat");
            wall.SetActive(mode!=2); camera.Render(); RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,128,128),0,0); pixels.Apply(); RenderTexture.active=null;
            Color center=pixels.GetPixel(64,64);
            Require(mode==0?center.maxColorComponent<.05f:mode==1?center.r<.1f && center.g>.8f && center.b>.8f:center.r>.8f && center.g>.8f && center.b>.8f,
                "Rendered ordinary occlusion / X-ray bone / visible point shader mode="+mode+" color="+center);
        }
        camera.targetTexture=null; target.Release(); DestroyImmediate(camera.gameObject); DestroyImmediate(sphere); DestroyImmediate(wall);
        DestroyImmediate(target); DestroyImmediate(pixels); DestroyImmediate(black);
    }
    void Finish(bool success,string message)
    {
        Restore(); SessionState.SetBool(Active,false); Directory.CreateDirectory("../Validation/TrackingLab"); File.WriteAllLines("../Validation/TrackingLab/avatar-hand-input.csv",measurements);
        File.WriteAllLines("../Validation/TrackingLab/avatar-hand-temporal.csv",temporal);
        File.WriteAllText("lab-hand-check-result.txt",(success?"PASS: ":"FAIL: ")+message+"\nBaseline: "+realBaseline); EditorApplication.Exit(success?0:1);
    }
}
#endif
