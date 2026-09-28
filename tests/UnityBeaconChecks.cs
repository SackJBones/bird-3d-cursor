#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UdonSharpEditor;
using VRC.SDKBase;
using VRC.Udon;

// Drives the actual compiled programs. Explicit pointer fixtures are not real gestures.
[DefaultExecutionOrder(32000)]
public class UnityBeaconChecks : MonoBehaviour
{
    const string Active = "Bird.Beacon.Checks";
    const string Folder = "../Validation/CoastalWorld/Beacons01";
    BirdTeleportRouter router;
    BirdPersonalStation station;
    BirdTeleportBeacon beacon;
    IEnumerator sequence;
    int assertions, frames;
    float deadline;

    public static void Run()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene("Assets/BirdWorld/Scenes/BirdCoastalWorld.unity");
            var networks = FindObjectsOfType<BirdTeleportRouter>(true);
            if (networks.Length != 1) throw new Exception("Exactly one saved travel network required");
            var network = networks[0];
            if (network == null || network.allowTeleport || network.beacons.Length != 5 || network.station == null) throw new Exception("Saved preview network contract");
            if (network.pointers.Length != 2 || network.GetComponentsInChildren<BirdAvatarUiInput>().Any(a => a.input == null || a.pointer == null || a.pointer.cursor != null)) throw new Exception("Saved after-IK input bindings");
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            if (UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError()) throw new Exception("Udon compilation failed");
            SessionState.SetBool(Active,true); EditorApplication.isPlaying = true;
        }
        catch (Exception e) { Result("coastal-beacons-check", false, e.ToString()); }
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if (SessionState.GetBool(Active,false)) new GameObject("Beacon compiled checks").AddComponent<UnityBeaconChecks>(); }
    void Start() { deadline = Time.unscaledTime + 120; }
    void LateUpdate()
    {
        if (!SessionState.GetBool(Active,false)) return;
        try
        {
            if (Time.unscaledTime > deadline) throw new Exception("Beacon checks timed out");
            if (!Utilities.IsValid(Networking.LocalPlayer) || Time.timeSinceLevelLoad < 3) return;
            if (sequence == null)
            {
                router = FindObjectOfType<BirdTeleportRouter>(); station = router.station;
                foreach (var adapter in router.GetComponentsInChildren<BirdAvatarUiInput>()) VM(adapter).enabled = false;
                Set(router,"automatic",false); VM(station).SendCustomEvent("TakeBird"); VM(router).SendCustomEvent("ResetContact");
                sequence = Scenarios();
            }
            frames++;
            if (!sequence.MoveNext()) Finish(true,"Compiled Udon "+assertions+" assertions / "+frames+" frames: finite disk targeting, occlusion, landing footprint/headroom, lifecycle, both-hand arbitration, gated travel, local teleport and renders. Explicit pointer fixtures; not finger-click, physical comfort or multiplayer acceptance.");
        }
        catch (Exception e) { Finish(false,e.ToString()); }
    }
    IEnumerator Scenarios()
    {
        yield return null;
        CaptureWorld("01-arrival",new Vector3(0,1.65f,-10),new Vector3(0,2,8));
        CaptureWorld("02-water-to-upper",new Vector3(23.5f,-.35f,49),new Vector3(17,16,25));
        CaptureWorld("03-lookout-to-water",new Vector3(15.1f,22.65f,34),new Vector3(23.5f,1.2f,54));
        CaptureWorld("06-lookout-inboard",new Vector3(10,22.65f,34),new Vector3(23.5f,1.2f,54));
        Vector3 start = Networking.LocalPlayer.GetPosition();
        string authoredFailures="";
        foreach (var target in router.beacons)
        {
            beacon = target;
            ThroughFrom(beacon.transform.position-beacon.transform.forward*.5f,false);
            if(State()!=1) authoredFailures+=beacon.name+" state="+State()+" hit="+Get<float>(beacon,"hitDistance")+" acquired="+Get<bool>(station,"acquired")+" eye="+Networking.LocalPlayer.GetAvatarEyeHeightAsMeters()+"; ";
            Hover(true); Require(Trips()==0 && Vector3.Distance(start,Networking.LocalPlayer.GetPosition())<.1f,"Preview cannot teleport: "+beacon.name);
            Feed(0,beacon.transform.TransformPoint(new Vector3(0,0,-3)),beacon.transform.TransformPoint(new Vector3(0,0,-.01f)),false); Process();
            Require(State()==0,"Finite Bird endpoint must reach the beacon plane");
            yield return null;
        }
        Require(authoredFailures=="","All authored landings supported and clear: "+authoredFailures);
        // The useful level-change sightlines are measured against actual world colliders.
        beacon = router.beacons[2];
        ThroughFrom(new Vector3(23.5f,-.8f,49),false); Require(State()==1,"Water promenade palm can address upper terrace");
        beacon = router.beacons[3];
        ThroughFrom(new Vector3(23.5f,-.8f,49),false); Require(State()==1,"Water promenade palm can address high lookout");
        beacon = router.beacons[4];
        ThroughFrom(new Vector3(15.1f,22.65f,34),false); Require(State()==1,"Raised hand near lookout edge can address water garden");

        beacon = router.beacons[1];
        // An isolated physical fixture tests dynamic obstacles without changing saved assets.
        beacon.transform.position = new Vector3(2000,110,2005); beacon.transform.rotation = Quaternion.identity;
        beacon.landing.position = new Vector3(2000,100,2000);
        var floor = Cube("Support fixture",new Vector3(2000,99.9f,2000),new Vector3(20,.2f,20));
        Physics.SyncTransforms();
        Set(router,"allowTeleport",true); VM(router).SendCustomEvent("ResetContact");
        Hover(true); Require(Trips()==0,"Permission change while held cannot travel");
        Hover(false); CaptureRing("04-hover-ring",true);
        var wall = Cube("Occlusion fixture",beacon.transform.position-Vector3.forward*1.5f,new Vector3(3,3,.1f)); Physics.SyncTransforms();
        Hover(false); Require(State()==0,"Solid geometry occludes targets");
        wall.GetComponent<Collider>().isTrigger=true; Physics.SyncTransforms(); Hover(false); Require(State()==1,"Triggers do not occlude targets");
        wall.GetComponent<Collider>().isTrigger=false; wall.transform.position=beacon.transform.position-Vector3.forward*3; Physics.SyncTransforms();
        Hover(false); Require(State()==0,"Origin inside solid geometry cannot point through it");
        DestroyImmediate(wall); Physics.SyncTransforms();
        Hover(false); Process(); Require(State()==0,"No new sample clears hover and arming");
        Hover(true); Require(Trips()==0,"Press after stale sample cannot reuse arming");
        Hover(false); VM(router.pointers[0]).SendCustomEvent("Cancel"); Hover(true); Require(Trips()==0,"Tracking loss consumes contact");
        Hover(false); Set(router.pointers[0],"userId","Replacement"); Hover(true); Require(Trips()==0,"Identity replacement cannot replay click");
        Set(router.pointers[0],"sampleTracked",false); VM(router.pointers[0]).SendCustomEvent("Submit"); Process(); Require(State()==0,"Invalid input clears highlight");
        Feed(0,new Vector3(float.NaN,0,0),Vector3.zero,false); Process(); Require(State()==0,"Nonfinite samples are rejected");

        // Rigid, upside-down and mirrored/nonuniform target transforms retain the same disk.
        for (int i=0;i<12;i++)
        {
            beacon.transform.rotation=Quaternion.AngleAxis(i*31,new Vector3(.3f,.7f,.2f).normalized);
            beacon.transform.localScale=new Vector3(i%2==0?-1.4f:1.4f,.8f,1.3f);
            beacon.landing.position=new Vector3(2000,100,2000); Physics.SyncTransforms();
            Hover(false); Require(State()==1,"Transformed finite disk remains selectable "+i);
            Feed(0,beacon.transform.TransformPoint(new Vector3(.83f,0,-3)),beacon.transform.TransformPoint(new Vector3(.83f,0,3)),false); Process();
            Require(State()==0,"Outside transformed disk is not a hit "+i);
        }
        beacon.transform.rotation=Quaternion.identity; beacon.transform.localScale=Vector3.one; beacon.landing.position=new Vector3(2000,100,2000);
        Physics.SyncTransforms();
        beacon.landing.position += Vector3.right*9.9f; Hover(false); Require(State()==2,"A supported center near a ledge is insufficient for the footprint");
        beacon.landing.position = new Vector3(2000,100,2000);
        floor.SetActive(false); Physics.SyncTransforms(); Hover(false); Require(State()==2,"Missing floor blocks landing");
        floor.SetActive(true); floor.GetComponent<Collider>().isTrigger=true; Physics.SyncTransforms(); Hover(false); Require(State()==2,"Trigger floor cannot support a landing");
        floor.GetComponent<Collider>().isTrigger=false;
        var roof = Cube("Headroom fixture",beacon.landing.position+Vector3.up*2.5f,new Vector3(3,.2f,3)); Physics.SyncTransforms();
        Set(router,"minimumClearanceHeight",1.75f); Hover(false); Require(State()==1,"Standing clearance under high ceiling");
        Set(router,"minimumClearanceHeight",3f); Hover(false); Require(State()==2,"Taller requested headroom cannot enter ceiling");
        Hover(true); Require(Trips()==0,"Blocked landing cannot teleport");
        Set(router,"minimumClearanceHeight",1.75f); DestroyImmediate(roof); Physics.SyncTransforms();
        beacon.landing.position += Vector3.up*.2f; Hover(false); Require(State()==2,"Badly authored floor elevation is rejected");
        beacon.landing.position = new Vector3(2000,100,2000); Physics.SyncTransforms();
        var side = Cube("Side wall fixture",beacon.landing.position+new Vector3(.25f,1,0),new Vector3(.1f,2,2)); Physics.SyncTransforms();
        Hover(false); Require(State()==2,"Standing capsule cannot overlap a side wall");
        DestroyImmediate(side); Physics.SyncTransforms();
        Set(router,"minimumClearanceHeight",9f); Hover(false); Require(State()==2,"Unsupported giant clearance is rejected instead of clamped");
        Set(router,"minimumClearanceHeight",1.75f);
        Hover(false); VM(station).SendCustomEvent("PutAway"); Hover(true); Require(Trips()==0 && State()==0,"Putting Bird away cancels travel");
        VM(station).SendCustomEvent("TakeBird"); VM(router).SendCustomEvent("ResetContact"); Hover(true); Require(Trips()==0,"Reacquisition with held press cannot travel");
        Feed(0,beacon.transform.position-Vector3.forward*3,beacon.transform.position+Vector3.forward*3,false);
        Feed(1,beacon.transform.position-Vector3.forward*3,beacon.transform.position+Vector3.forward*3,false); Process();
        Quaternion facing=Networking.LocalPlayer.GetRotation();
        Feed(0,beacon.transform.position-Vector3.forward*3,beacon.transform.position+Vector3.forward*3,true);
        Feed(1,beacon.transform.position-Vector3.forward*3,beacon.transform.position+Vector3.forward*3,true); Process();
        yield return null;
        Require(Trips()==1,"Both hands produce at most one trip");
        Require(Vector3.Distance(Networking.LocalPlayer.GetPosition(),beacon.landing.position)<.2f,"Normal local TeleportTo reaches clear destination");
        Require(Quaternion.Angle(Networking.LocalPlayer.GetRotation(),facing)<1,"Travel preserves local facing");
        Require(!Get<bool>(router.pointers[0],"tracked") && !Get<bool>(router.pointers[1],"tracked"),"Travel cancels both pointer histories");
        Hover(false); Hover(true); Require(Trips()==1,"Cooldown rejects immediate repeat");
        float until=Time.realtimeSinceStartup+1;while(Time.realtimeSinceStartup<until) yield return null;
        Hover(true); Require(Trips()==1,"A held click does not replay when cooldown expires");
        Feed(0,beacon.transform.position-Vector3.forward*3,beacon.transform.position+Vector3.forward*3,false);
        Set(router.pointers[0],"uiConsumed",true); Process(); Hover(true); Require(Trips()==1,"Earlier UI consumer blocks fresh travel intent");
        Hover(false); Hover(true); yield return null; Require(Trips()==2,"Fresh release then press can travel again");
        VM(beacon).enabled=false; Hover(false); Require(State()==0,"Disabled beacon is unavailable"); VM(beacon).enabled=true;
        VM(router.pointers[0]).SendCustomEvent("Cancel"); VM(router.pointers[1]).SendCustomEvent("Cancel"); Process(); CaptureRing("05-idle-ring",false);
    }
    void ThroughFrom(Vector3 origin,bool pressed) { Feed(0,origin,beacon.transform.position+(beacon.transform.position-origin).normalized*.5f,pressed);Process(); }
    void Hover(bool pressed) { Feed(0,beacon.transform.TransformPoint(new Vector3(0,0,-3)),beacon.transform.TransformPoint(new Vector3(0,0,3)),pressed);Process(); }
    void Feed(int slot,Vector3 origin,Vector3 point,bool pressed)
    { var p=router.pointers[slot];Set(p,"sampleOrigin",origin);Set(p,"samplePosition",point);Set(p,"sampleTracked",true);Set(p,"samplePressed",pressed);VM(p).SendCustomEvent("Submit"); }
    void Process() { VM(router).SendCustomEvent("Process"); }
    int State() { return Get<int>(beacon,"visualState"); }
    int Trips() { return Get<int>(router,"travelRequests"); }
    static GameObject Cube(string name,Vector3 p,Vector3 size)
    { var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.position=p;go.transform.localScale=size;return go; }
    void CaptureRing(string name,bool highlighted)
    {
        int layer=beacon.ring.gameObject.layer;beacon.ring.gameObject.layer=31;
        try { int pixels=Render(name,beacon.transform.position-beacon.transform.forward*3,beacon.transform.position,1<<31);Require(highlighted?pixels>500:pixels==0,"Actual ring highlight pixels "+name+"="+pixels); }
        finally {beacon.ring.gameObject.layer=layer;}
    }
    void CaptureWorld(string name,Vector3 eye,Vector3 target) { Render(name,eye,target,-1); }
    int Render(string name,Vector3 eye,Vector3 target,int mask)
    {
        var camera=new GameObject("Beacon validation camera").AddComponent<Camera>();camera.enabled=false;camera.transform.position=eye;camera.transform.LookAt(target);
        camera.fieldOfView=70;camera.nearClipPlane=.03f;camera.farClipPlane=10000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=mask==-1?new Color(.48f,.68f,.78f):Color.black;camera.cullingMask=mask;
        var texture=new RenderTexture(1200,750,24){antiAliasing=4};camera.targetTexture=texture;var image=new Texture2D(1200,750,TextureFormat.RGB24,false);
        try {camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,1200,750),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());return image.GetPixels32().Count(c=>c.r>90&&c.r<220&&c.g>240&&c.b>240);}
        finally {RenderTexture.active=null;camera.targetTexture=null;texture.Release();DestroyImmediate(texture);DestroyImmediate(image);DestroyImmediate(camera.gameObject);}
    }
    static UdonBehaviour VM(UdonSharp.UdonSharpBehaviour p) { return UdonSharpEditorUtility.GetBackingUdonBehaviour(p); }
    static void Set(UdonSharp.UdonSharpBehaviour p,string field,object value) { VM(p).SetProgramVariable(field,value); }
    static T Get<T>(UdonSharp.UdonSharpBehaviour p,string field) { return (T)VM(p).GetProgramVariable(field); }
    void Require(bool condition,string message) { assertions++;if(!condition)throw new Exception(message); }
    void Finish(bool pass,string message) { SessionState.SetBool(Active,false);File.WriteAllText(Folder+"/check-"+EditorUserBuildSettings.activeBuildTarget+".txt",(pass?"PASS: ":"FAIL: ")+message);Result("coastal-beacons-check",pass,message); }
    static void Result(string stem,bool pass,string message) {File.WriteAllText(stem+"-result.txt",(pass?"PASS: ":"FAIL: ")+message);EditorApplication.Exit(pass?0:1);}
}
#endif
