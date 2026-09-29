#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.Udon;

[DefaultExecutionOrder(32000)]
public class UnityPondFishChecks:MonoBehaviour
{
    const string Active="Bird.Pond.Fish",Folder="../Validation/CoastalWorld/Fish01";
    BirdPondSchool school;UdonBehaviour vm;BirdPersonalStation station;
    IEnumerator sequence;int assertions,frames;float deadline;string output;
    readonly List<string> metrics=new List<string>();
    public static void Run()
    {
        try{EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdCoastalWorld.unity");UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new Exception("Udon compile failed");SessionState.SetBool(Active,true);EditorApplication.isPlaying=true;}
        catch(Exception e){File.WriteAllText("coastal-fish-check-result.txt","FAIL: "+e);EditorApplication.Exit(1);}
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Begin(){if(SessionState.GetBool(Active,false))new GameObject("Pond fish checks").AddComponent<UnityPondFishChecks>();}
    void Start(){deadline=Time.realtimeSinceStartup+150;}
    void LateUpdate()
    {
        if(!SessionState.GetBool(Active,false)||Time.timeSinceLevelLoad<4)return;
        try
        {
            if(Time.realtimeSinceStartup>deadline)throw new Exception("Fish check timeout");
            if(sequence==null){school=FindObjectOfType<BirdPondSchool>();Require(school!=null,"Saved school exists");vm=VM(school);station=school.station;output=Folder+"/"+EditorUserBuildSettings.activeBuildTarget;Directory.CreateDirectory(output);sequence=Scenarios();}
            frames++;if(!sequence.MoveNext())Finish(true,"Compiled Udon "+assertions+" assertions / "+frames+" frames; local/remote logical water stimuli, expiry/put-away, four shoals, containment, bounded motion, lifecycle, normal-frame interpolation and rendered views. Synthetic remote packets; not real multiplayer or physical Bird input.");
        }catch(Exception e){Finish(false,e.ToString());}
    }
    IEnumerator Scenarios()
    {
        Require(Get<bool>(vm,"ready"),"Compiled school initialized");Require(vm.SyncMethod==Networking.SyncType.None,"Fish add no network stream");Require(school.fish.Length==24,"Bounded fish count");
        var water=GameObject.Find("04 Lower water and hidden lounge/Curved pond promenade/Curved water");
        Require(water.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="Bird/Shallow Pond Water","Authored translucent water");
        Require(!school.GetComponentsInChildren<Collider>().Any(),"Fish/bed add no walking collision");
        vm.SetProgramVariable("lastStep",Time.realtimeSinceStartup+1000f); // Drive the actual Udon VM at a fixed simulated timestep.
        VM(station).SendCustomEvent("TakeBird");
        foreach(var input in station.personalRig.GetComponentsInChildren<BirdAvatarHandInput>())VM(input).enabled=false;
        foreach(var view in station.personalRig.GetComponentsInChildren<BirdPointPresentation>())VM(view).enabled=false;
        vm.SendCustomEvent("DiscoverSources");
        Vector3 a=new Vector3(16.4f,-2.78f,58.2f),b=new Vector3(-7,-2.78f,54);
        Feed(station.left,a,true);Feed(station.right,b,true);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==2,"Two independent submerged local Birds");
        Feed(station.right,b+Vector3.up,true);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==1,"Bird above water ignored");
        Feed(station.right,new Vector3(5,-2.78f,51),true);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==1,"Dry inland terrace inside AABB ignored");
        Feed(station.right,new Vector3(19,-4,54),true);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==1,"Below basin ignored");
        Feed(station.right,new Vector3(float.NaN,0,0),true);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==1,"Nonfinite input ignored");
        Feed(station.right,new Vector3(19,-2.78f,10000),true);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==1,"Distant logical cursor ignored even with nearby view transform");
        Feed(station.left,a,false);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==0,"Tracking loss releases attraction");
        Capture("01-idle-east",new Vector3(23.4f,-.35f,51),new Vector3(18,-2.8f,55));
        Feed(station.left,a,true);Feed(station.right,b,true);
        var before=Get<Vector3[]>(vm,"positions").ToArray();float initial=before.Take(6).Average(p=>(p-a).magnitude);
        var timer=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<220;i++)
        {
            var old=Get<Vector3[]>(vm,"positions").ToArray();vm.SendCustomEvent("Step");CheckPositions(old);
            if(i%20==0)yield return null;
        }
        timer.Stop();float approached=Get<Vector3[]>(vm,"positions").Take(6).Average(p=>(p-a).magnitude);
        Require(approached<initial*.7f,"Eastern shoal approaches submerged Bird: "+initial+" -> "+approached);
        var following=Get<UnityEngine.Object[]>(vm,"followed")[0];
        for(int i=0;i<20;i++)
        {
            Feed(station.left,a+Vector3.right*(i%2==0?.015f:-.015f),true);Feed(station.right,a+Vector3.right*(i%2==0?-.015f:.015f),true);vm.SendCustomEvent("Step");
            Require(Get<UnityEngine.Object[]>(vm,"followed")[0]==following,"Neighboring target jitter keeps group choice stable");
        }
        Feed(station.left,a,true);Feed(station.right,b,true);
        metrics.Add("220 compiled steps including assertions/yields ms="+timer.Elapsed.TotalMilliseconds.ToString("F2"));metrics.Add("Eastern mean distance m="+initial+" -> "+approached);
        vm.SetProgramVariable("lastStep",Time.realtimeSinceStartup);for(int i=0;i<12;i++)yield return null;
        Capture("02-two-birds-east",new Vector3(23.4f,-.35f,51),a);
        Capture("03-two-birds-west",new Vector3(-11.5f,-.35f,52),b);
        Capture("04-water-oblique",new Vector3(23.1f,-.7f,54),new Vector3(18.5f,-2.77f,55));
        Vector3 closeEye=new Vector3(20.4f,-.35f,57);
        Require(Physics.Raycast(closeEye,Vector3.down,out var closeFloor,2,(1<<0)|(1<<2)|(1<<11),QueryTriggerInteraction.Ignore)&&closeFloor.normal.y>.98f,"Close fish view has supporting floor");
        Capture("06-gathering-close",closeEye,a);
        vm.SetProgramVariable("lastStep",Time.realtimeSinceStartup+1000f);
        // Actual SDK-created remote PlayerObject, synthetic accepted snapshot.
        ClientSimMain.SpawnRemotePlayer("Pond observer fixture");for(int i=0;i<6;i++)yield return null;
        var players=new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];VRCPlayerApi.GetPlayers(players);var remotePlayer=players.Single(p=>p.displayName=="Pond observer fixture");
        var social=Networking.GetPlayerObjects(remotePlayer).Select(o=>o.GetComponent<BirdSocialPresentation>()).Single(p=>p!=null);
        var remote=VM(social);vm.SendCustomEvent("DiscoverSources");VM(station).SendCustomEvent("PutAway");
        Packet(remote,1,a,Networking.GetServerTimeInSeconds());yield return null;vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==1,"Remote accepted logical endpoint attracts while local Bird is away");
        VM(social.remoteLeft).SetProgramVariable("position",a);VM(social.remoteLeft).SetProgramVariable("rawPosition",a+Vector3.up*10);vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==0,"Observer interpolation cannot manufacture water contact");
        Packet(remote,2,b,Networking.GetServerTimeInSeconds());yield return null;vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==1,"Fresh remote snapshot recovers");
        Packet(remote,3,a,Networking.GetServerTimeInSeconds()-10);yield return null;vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==0,"Stale remote packet releases school");
        Packet(remote,4,a,Networking.GetServerTimeInSeconds());yield return null;
        float until=Time.realtimeSinceStartup+1.7f;while(Time.realtimeSinceStartup<until)yield return null;
        vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==0,"Remote silence expires before attraction");
        // Several real ClientSim player-object clones, still synthetic transport.
        for(int i=0;i<3;i++)ClientSimMain.SpawnRemotePlayer("Pond load fixture "+i);
        for(int i=0;i<6;i++)yield return null;
        players=new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];VRCPlayerApi.GetPlayers(players);
        var visitors=players.Where(p=>p.displayName=="Pond observer fixture"||p.displayName.StartsWith("Pond load fixture ")).ToArray();
        Require(visitors.Length==4,"Four remote visitor fixture instances");
        foreach(var visitor in visitors)
        {
            var bridge=Networking.GetPlayerObjects(visitor).Select(o=>o.GetComponent<BirdSocialPresentation>()).Single(p=>p!=null);var u=VM(bridge);
            SetWire(u,"sequence",5);SetWire(u,"visibleMask",3);SetWire(u,"leftPoint",a);SetWire(u,"rightPoint",b);SetWire(u,"leftRoot",a+Vector3.up);SetWire(u,"rightRoot",b+Vector3.up);SetWire(u,"sampleTime",Networking.GetServerTimeInSeconds());u.RunEvent("_onDeserialization");
        }
        yield return null;vm.SendCustomEvent("DiscoverSources");vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==8,"Eight submerged hands from four remote visitors");
        var workload=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<100;i++)vm.SendCustomEvent("Step");
        workload.Stop();metrics.Add("100 compiled 8-stimulus steps, synchronous editor CPU ms="+workload.Elapsed.TotalMilliseconds.ToString("F2"));
        Require(Get<int>(vm,"stimulusCount")==8,"All visitor stimuli remain admitted during bounded workload");
        foreach(var visitor in visitors)if(visitor!=remotePlayer)ClientSimMain.RemovePlayer(visitor);
        ClientSimMain.RemovePlayer(remotePlayer);yield return null;vm.SendCustomEvent("DiscoverSources");vm.SendCustomEvent("Step");Require(Get<int>(vm,"stimulusCount")==0,"Departure clears source binding");
        for(int i=0;i<100;i++){var old=Get<Vector3[]>(vm,"positions").ToArray();vm.SendCustomEvent("Step");CheckPositions(old);if(i%20==0)yield return null;}
        vm.SetProgramVariable("lastStep",Time.realtimeSinceStartup);
        // Publish the manually advanced fixture state before measuring ordinary
        // frame-to-frame motion; that fixture jump is not a runtime timestep.
        yield return null;yield return null;
        var first=school.fish.Select(f=>f.position).ToArray();
        for(int i=0;i<90;i++)
        {
            if(i%15==0)Capture("motion-"+i.ToString("D3"),new Vector3(23.4f,-.35f,51),new Vector3(18,-2.8f,55));
            var old=school.fish.Select(f=>f.localPosition).ToArray();yield return null;
            for(int j=0;j<24;j++)Require((school.fish[j].localPosition-old[j]).magnitude<.16f,"Bounded interpolated normal-frame movement");
        }
        Require(school.fish.Where((f,i)=>(f.position-first[i]).sqrMagnitude>.001f).Count()>16,"Autonomous normal-frame fish movement");
        school.gameObject.SetActive(false);yield return null;school.gameObject.SetActive(true);for(int i=0;i<3;i++)yield return null;
        Require(Get<bool>(vm,"ready"),"Disable/re-enable initializes safely");
        File.WriteAllLines(output+"/metrics.txt",metrics);
        Capture("05-return-to-patrol",new Vector3(23.4f,-.35f,51),new Vector3(18,-2.8f,55));
    }
    void CheckPositions(Vector3[] old)
    {
        var points=Get<Vector3[]>(vm,"positions");
        for(int i=0;i<24;i++)
        {
            Require(!float.IsNaN(points[i].sqrMagnitude)&&!float.IsInfinity(points[i].sqrMagnitude),"Finite fish");
            Require((points[i]-old[i]).magnitude<=school.swimSpeed*1.35f*school.stepSeconds+.001f,"Bounded per-step displacement");
            float best=float.MaxValue;
            for(int k=0;k<school.centerline.Length-1;k++){Vector3 d=school.centerline[k+1]-school.centerline[k],v=points[i]-school.centerline[k];d.y=v.y=0;Vector3 q=school.centerline[k]+d*Mathf.Clamp01(Vector3.Dot(v,d)/d.sqrMagnitude);q.y=points[i].y;best=Mathf.Min(best,(points[i]-q).magnitude);}
            Require(best<=school.waterHalfWidth-.549f,"Fish retain full-body margin inside curved shore and round caps");
        }
    }
    static void Feed(BirdCursorState point,Vector3 p,bool valid){var u=VM(point);u.SetProgramVariable("position",p);u.SetProgramVariable("rawPosition",p);u.SetProgramVariable("poseValid",valid);}
    static void Packet(UdonBehaviour u,int seq,Vector3 point,double time)
    {
        SetWire(u,"sequence",seq);SetWire(u,"visibleMask",1);SetWire(u,"leftPoint",point);SetWire(u,"leftRoot",point+Vector3.up);SetWire(u,"sampleTime",time);u.RunEvent("_onDeserialization");
    }
    static void SetWire<T>(UdonBehaviour u,string name,T value){var key=u.SyncMetadataTable.GetAllSyncMetadata().Single(m=>m.Name==name||m.Name.EndsWith("_"+name)).Name;u.SetProgramVariable<T>(key,value);}
    static UdonBehaviour VM(UdonSharpBehaviour p)=>UdonSharpEditorUtility.GetBackingUdonBehaviour(p);
    static T Get<T>(UdonBehaviour u,string name)=>(T)u.GetProgramVariable(name);
    void Require(bool value,string message){assertions++;if(!value)throw new Exception(message);}
    void Capture(string name,Vector3 eye,Vector3 target)
    {
        var cam=new GameObject("Fish evidence camera").AddComponent<Camera>();cam.enabled=false;cam.transform.position=eye;cam.transform.LookAt(target);cam.fieldOfView=78;cam.nearClipPlane=.03f;cam.farClipPlane=10000;cam.clearFlags=CameraClearFlags.Skybox;
        var rt=new RenderTexture(1200,750,24){antiAliasing=4};var tex=new Texture2D(1200,750,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,750),0,0);tex.Apply();File.WriteAllBytes(output+"/"+name+".png",tex.EncodeToPNG());}
        finally{RenderTexture.active=null;cam.targetTexture=null;rt.Release();DestroyImmediate(tex);DestroyImmediate(rt);DestroyImmediate(cam.gameObject);}
    }
    void Finish(bool ok,string message){SessionState.SetBool(Active,false);File.WriteAllText("coastal-fish-check-result.txt",(ok?"PASS: ":"FAIL: ")+message);if(output!=null)File.WriteAllLines(output+"/metrics.txt",metrics);EditorApplication.Exit(ok?0:1);}
}
#endif
