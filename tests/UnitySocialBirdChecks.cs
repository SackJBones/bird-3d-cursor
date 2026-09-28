#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.SDK3.ClientSim;
using VRC.SDK3.ClientSim.EncodeDecoders;
using VRC.SDK3.Data;
using VRC.Udon;
using VRC.Udon.Common;

// Real ClientSim PlayerObjects and compiled Udon, synthetic snapshot delivery.
// This does not emulate a second VRChat client or establish real network timing.
[DefaultExecutionOrder(32000)]
public class UnitySocialBirdChecks : MonoBehaviour
{
    const string Active="Bird.Social.Checks", Folder="../Validation/CoastalWorld/SocialBird";
    IEnumerator sequence;
    BirdPersonalStation station;
    BirdSocialPresentation local, remote, late;
    VRCPlayerApi remotePlayer;
    int assertions,frames,requests;
    float deadline;
    public static void Run()
    {
        File.WriteAllText("coastal-social-check-result.txt","PENDING");
        try
        {
            EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdCoastalWorld.unity");
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            if(UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compile failure");
            SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
        }
        catch(Exception e){File.WriteAllText("coastal-social-check-result.txt","FAIL: "+e);EditorApplication.Exit(1);}
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin(){if(SessionState.GetBool(Active,false))new GameObject("Social Bird checks").AddComponent<UnitySocialBirdChecks>();}
    void Start(){deadline=Time.realtimeSinceStartup+100;}
    void LateUpdate()
    {
        if(!SessionState.GetBool(Active,false))return;
        try
        {
            if(Time.realtimeSinceStartup>deadline)throw new Exception("Social checks timed out");
            if(!Utilities.IsValid(Networking.LocalPlayer)||Time.timeSinceLevelLoad<3)return;
            if(sequence==null)
            {
                station=FindObjectOfType<BirdPersonalStation>();local=For(Networking.LocalPlayer);
                Require(local!=null,"ClientSim created the local player's template instance");
                UdonBehaviour.RequestSerializationHook+=Requested;
                sequence=Scenarios();
            }
            frames++;
            if(!sequence.MoveNext())Finish(true,"Compiled Udon "+assertions+" assertions / "+frames+" frames. Actual ClientSim per-player cloning, initial ownership, bindings, palettes and departure; synthetic snapshot codec/dispatch covers acquisition, both hands, interpolation, revisions, teleport, malformed/stale/reordered data, late join, recovery and put-away. Request cadence bounded; raw fields 72 bytes. ClientSim permits SetOwner on PlayerObjects unlike the documented client contract; immutability needs real-client acceptance. Not a multi-client network, wire-byte or headset performance measurement.");
        }
        catch(Exception e){Finish(false,e.ToString());}
    }
    IEnumerator Scenarios()
    {
        var template=station.GetComponentsInChildren<BirdSocialPresentation>(true).Single(x=>!x.gameObject.activeInHierarchy);
        Require(!template.gameObject.activeSelf,"SDK disables source template");
        Require(local.station==station,"Spawned copy keeps scene station binding");
        Require(local.remoteLeft!=template.remoteLeft&&local.remoteLeft.transform.IsChildOf(local.transform),"Spawned point references bind inside its own copy");
        Require(Networking.GetOwner(local.gameObject).isLocal,"Local instance ownership is correct");
        Require(local.remoteLeft.fitter==null&&local.remoteLeft.centerFilter==null,"Observer states have no solver");
        Require(VM(local).SyncMethod==Networking.SyncType.Manual,"One manual snapshot behavior");
        Require(VM(local).SyncMetadataTable.GetAllSyncMetadata().Count()==9,"Nine bounded scalar/vector fields, no unbounded arrays");
        ClientSimMain.SpawnRemotePlayer("Bird observer fixture");
        for(int i=0;i<5;i++)yield return null;
        remotePlayer=Players().Single(p=>p.displayName=="Bird observer fixture");remote=For(remotePlayer);
        Require(remote!=null&&Networking.GetOwner(remote.gameObject)==remotePlayer,"Remote player gets own object");
        // SDK 3.10.5 ClientSim permits SetOwner here despite the documented
        // PlayerObject client contract. Do not equate it with client enforcement.
        Require(!Valid(remote.remoteLeft)&&!Valid(local.remoteLeft),"No initial phantom/duplicate birds");
        Require(Get<Color>(VM(local.localLeftView),"tint")==Get<Color>(VM(local.remoteLeftView),"tint"),"Local and observer palette agree for the same player");
        Require(Get<Color>(VM(local.remoteLeftView),"tint")!=Get<Color>(VM(remote.remoteLeftView),"tint"),"Different players get different palette hues");
        Color.RGBToHSV(Get<Color>(VM(local.localLeftView),"tint"),out float lh,out _,out _);
        Color.RGBToHSV(Get<Color>(VM(local.localRightView),"tint"),out float rh,out _,out _);
        Require(Mathf.Abs(Mathf.DeltaAngle(lh*360,rh*360))>120,"Left/right retain contrasting companion colors");
        VM(station).SendCustomEvent("TakeBird");
        foreach(var input in station.personalRig.GetComponentsInChildren<BirdAvatarHandInput>())VM(input).enabled=false;
        Feed(station.left,new Vector3(-.2f,1.4f,0),new Vector3(-1,1.5f,3),10);
        Feed(station.right,new Vector3(.2f,1.4f,0),new Vector3(1,1.5f,10000),20);
        Transfer(remote); yield return null;
        Require(Valid(remote.remoteLeft)&&Valid(remote.remoteRight),"Both hands replicated");
        Require(!Valid(local.remoteLeft)&&!Valid(local.remoteRight),"Owner retains only the original local render");
        Require(Position(remote.remoteLeft)==Position(station.left)&&Position(remote.remoteRight)==Position(station.right),"First snapshot exact, including 10 km point");
        Require(remote.remoteLeftView.core.enabled&&remote.remoteRightView.core.enabled,"Remote embodiments render");
        Require(!Get<bool>(VM(remote.remoteLeft),"clicksAllowed"),"Observer state cannot click");
        // Codec roundtrip uses the SDK's field metadata and actual scalar types.
        var codec=new ClientSimUdonEncodeDecode();VM(local).OnPreSerialization();
        var payload=codec.Encode(VM(local));
        Require(VRCJson.TrySerializeToJson(payload,JsonExportType.Minify,out DataToken json),"SDK snapshot JSON encoding");
        Require(VRCJson.TryDeserializeFromJson(json.String,out DataToken decoded),"SDK snapshot JSON decoding");
        codec.Decode(VM(remote),decoded.DataDictionary);yield return null;
        Require(Get<Vector3>(VM(remote),"rightPoint")==Position(station.right),"SDK codec preserves far coordinates");
        // Receiver interpolation is finite-duration and never feeds local state.
        Vector3 before=Position(remote.remoteLeft),goal=before+Vector3.right*4;
        Feed(station.left,Get<Vector3>(VM(station.left),"handRoot"),goal,10);Transfer(remote);
        float start=Time.realtimeSinceStartup;
        yield return null;
        Require(Position(station.left)==goal,"Network interpolation cannot modify local input");
        while(Time.realtimeSinceStartup-start<.15f)yield return null;
        Require((Position(remote.remoteLeft)-goal).sqrMagnitude<1e-8f,"Observer reaches target in a bounded interval");
        Require(remote.remoteLeftView.trailRenderer.enabled,"Observer has a trail");
        int revision=Get<int>(VM(remote.remoteLeft),"historyRevision");
        Feed(station.left,new Vector3(4,1.4f,0),goal+Vector3.right*4,11);Transfer(remote);yield return null;
        Require(Get<int>(VM(remote.remoteLeft),"historyRevision")>revision,"Teleport/revision clears history");
        Require(Position(remote.remoteLeft)==Position(station.left),"Teleport starts at new point without sweeping");
        Require(Get<int>(VM(remote.remoteLeftView),"trailCount")<=2,"No long trail across reset");
        var old=Snapshot();
        VM(station.left).SetProgramVariable("poseValid",false);Transfer(remote);yield return null;
        Require(!Valid(remote.remoteLeft)&&Valid(remote.remoteRight),"One hand loss does not hide the other");
        Deliver(remote,old);yield return null;
        Require(!Valid(remote.remoteLeft),"Older packet cannot resurrect a hidden hand");
        Feed(station.left,new Vector3(-.2f,1.4f,0),goal,12);Transfer(remote);yield return null;
        Require(Valid(remote.remoteLeft),"Tracking recovery receives a fresh point");
        var bad=Snapshot();bad[WireKey(VM(local),"leftPoint")]=new Vector3(float.NaN,0,1);Deliver(remote,bad);yield return null;
        Require(!Valid(remote.remoteLeft)&&Valid(remote.remoteRight),"Malformed hand fails closed independently");
        bad=Snapshot();bad[WireKey(VM(local),"sampleTime")]=Networking.GetServerTimeInSeconds()-10;Deliver(remote,bad);yield return null;
        Require(!Valid(remote.remoteLeft)&&!Valid(remote.remoteRight),"Stale snapshot fails closed");
        Transfer(remote);yield return null;
        ClientSimMain.SpawnRemotePlayer("Bird late fixture");for(int i=0;i<5;i++)yield return null;
        late=For(Players().Single(p=>p.displayName=="Bird late fixture"));
        Transfer(late);yield return null;
        Require(Valid(late.remoteLeft)&&Valid(late.remoteRight),"Late instance initializes from a fresh complete snapshot");
        float until=Time.realtimeSinceStartup+1.65f;while(Time.realtimeSinceStartup<until)yield return null;
        Require(!Valid(remote.remoteLeft)&&!remote.remoteLeftView.trailRenderer.enabled,"Missing updates expire point and trail");
        Transfer(remote);yield return null;Require(Valid(remote.remoteLeft),"Fresh packets recover after timeout");
        // Real RequestSerialization cadence; acknowledgement is explicit fixture
        // because ClientSim persistence is not a multi-client network transport.
        requests=0;start=Time.realtimeSinceStartup;
        while(Time.realtimeSinceStartup-start<1)
        {
            VM(local).OnPreSerialization();VM(local).OnPostSerialization(new SerializationResult(true,72));
            yield return null;
        }
        Require(requests>0&&requests<=Mathf.CeilToInt((Time.realtimeSinceStartup-start)/local.sendInterval)+1,"Active request cadence at most configured rate: "+requests);
        VM(station).SendCustomEvent("PutAway");Transfer(remote);Transfer(late);yield return null;
        Require(!Valid(remote.remoteLeft)&&!Valid(late.remoteRight),"Put-away clears both observers");
        VM(local).OnPreSerialization();VM(local).OnPostSerialization(new SerializationResult(true,72));requests=0;
        until=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<until)yield return null;
        Require(requests==0,"Idle acknowledged station has no continuous traffic");
        VM(local).OnPostSerialization(new SerializationResult(false,0));
        until=Time.realtimeSinceStartup+.15f;while(Time.realtimeSinceStartup<until)yield return null;
        Require(requests>0,"Failed serialization schedules retry");
        var departed=remote.gameObject;ClientSimMain.RemovePlayer(remotePlayer);yield return null;yield return null;
        Require(departed==null,"SDK removes departed player's object and renderers");
        Require(local!=null&&late!=null,"Other players' streams survive departure");
    }
    Dictionary<string,object> Snapshot()
    {
        VM(local).OnPreSerialization();return VM(local).SyncMetadataTable.GetAllSyncMetadata().ToDictionary(m=>m.Name,m=>VM(local).GetProgramVariable(m.Name));
    }
    void Transfer(BirdSocialPresentation target){Deliver(target,Snapshot());}
    static void Deliver(BirdSocialPresentation target,Dictionary<string,object> data)
    {
        // The object-valued editor setter changes Udon heap type metadata to
        // System.Object. Preserve the wire types when injecting test packets.
        foreach(var item in data)
        {
            if(item.Value is Vector3 vector) VM(target).SetProgramVariable<Vector3>(item.Key,vector);
            else if(item.Value is int number) VM(target).SetProgramVariable<int>(item.Key,number);
            else if(item.Value is double time) VM(target).SetProgramVariable<double>(item.Key,time);
            else throw new Exception("Unexpected wire type: "+item.Key);
        }
        VM(target).RunEvent("_onDeserialization");
    }
    static void Feed(BirdCursorState point,Vector3 root,Vector3 position,int revision)
    {
        var vm=VM(point);vm.SetProgramVariable("handRoot",root);vm.SetProgramVariable("position",position);vm.SetProgramVariable("poseValid",true);vm.SetProgramVariable("historyRevision",revision);
    }
    static BirdSocialPresentation For(VRCPlayerApi player){return Networking.GetPlayerObjects(player).Select(g=>g.GetComponent<BirdSocialPresentation>()).SingleOrDefault(x=>x!=null);}
    static VRCPlayerApi[] Players(){var players=new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];VRCPlayerApi.GetPlayers(players);return players;}
    static bool Valid(BirdCursorState point){return Get<bool>(VM(point),"poseValid");}
    static Vector3 Position(BirdCursorState point){return Get<Vector3>(VM(point),"position");}
    static UdonBehaviour VM(UdonSharp.UdonSharpBehaviour proxy){return UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);}
    // Resolve wire fields from the actual compiled sync metadata.
    static string WireKey(UdonBehaviour vm,string field){return vm.SyncMetadataTable.GetAllSyncMetadata().Single(m=>m.Name==field||m.Name.EndsWith("_"+field)).Name;}
    static T Get<T>(UdonBehaviour vm,string field)
    {
        if(vm==null)throw new Exception("Missing Udon VM for "+field);
        var wire=vm.SyncMetadataTable.GetAllSyncMetadata().FirstOrDefault(m=>m.Name==field||m.Name.EndsWith("_"+field));
        string symbol=wire!=null?wire.Name:field;
        object value=vm.GetProgramVariable(symbol);
        if(value==null)throw new Exception("Missing value for "+symbol+" ("+field+"); symbols: "+string.Join(", ",vm.SyncMetadataTable.GetAllSyncMetadata().Select(m=>m.Name+"="+vm.GetProgramVariable(m.Name))));
        return (T)value;
    }
    void Requested(UdonBehaviour vm){if(local!=null&&vm==VM(local))requests++;}
    void Require(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
    void Finish(bool success,string message)
    {
        UdonBehaviour.RequestSerializationHook-=Requested;SessionState.SetBool(Active,false);Directory.CreateDirectory(Folder);
        string result=(success?"PASS: ":"FAIL: ")+message;File.WriteAllText("coastal-social-check-result.txt",result);File.WriteAllText(Folder+"/check-"+EditorUserBuildSettings.activeBuildTarget+".txt",result);EditorApplication.Exit(success?0:1);
    }
}
#endif
