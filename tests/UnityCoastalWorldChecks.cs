#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using VRC;
using VRC.Core;
using VRC.Editor;
using VRC.SDK3.Components;
using VRC.SDK3.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;
using VRC.SDKBase.Editor.Validation;

public partial class UnityCoastalWorldChecks : IProcessSceneWithReport
{
    const string Folder="../Validation/CoastalWorld";
    static bool audit;
    static int processed;
    static string inventory;
    public int callbackOrder=>int.MaxValue;
    public static void Author(){try{BirdCoastalWorldAuthoring.Create();Finish("coastal-author",true,"Created separate authored coastal world, six editable region prefabs and saved profile meshes.");}catch(Exception e){Finish("coastal-author",false,e.ToString());}}
    public static void ReviseR05(){try{BirdCoastalWorldAuthoring.ApplyR05();Finish("coastal-r05",true,"Applied scoped R05 prefab revision.");}catch(Exception e){Finish("coastal-r05",false,e.ToString());}}
    public static void RefineR05(){try{BirdCoastalWorldAuthoring.RefineR05();Finish("coastal-r05-refine",true,"Applied independent-review doorway and sightline refinement.");}catch(Exception e){Finish("coastal-r05-refine",false,e.ToString());}}
    public static void AddBird(){try{BirdPersonalStationAuthoring.AddToCoastalWorld();Finish("coastal-bird-author",true,"Added independent local Bird station prefab; architecture preserved.");}catch(Exception e){Finish("coastal-bird-author",false,e.ToString());}}
    public static async void AddSocial(){try{await BirdSocialPresentationAuthoring.AddToCoastalWorld();Finish("coastal-social-author",true,"Added optional per-player Bird presentation to the existing prefab.");}catch(Exception e){Finish("coastal-social-author",false,e.ToString());}}
    public static async void AddBeacons(){try{await BirdTeleportAuthoring.Add();Finish("coastal-beacons-author",true,"Saved five editable vertical beacons and local targeting; teleport permission remains off.");}catch(Exception e){Finish("coastal-beacons-author",false,e.ToString());}}
    public static void Check()
    {
        try
        {
            Directory.CreateDirectory(Folder);var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);Physics.SyncTransforms();
            Require(UnityEngine.Object.FindObjectsOfType<VRCSceneDescriptor>().Length==1,"One world descriptor");
            Require(scene.GetRootGameObjects().Count(PrefabUtility.IsAnyPrefabInstanceRoot)==6,"Six separately editable regions");
            var descriptor=UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            Require(descriptor.ReferenceCamera.GetComponent<Camera>().farClipPlane>=10000,"Long rendering range");
            Vector3 spawn=descriptor.spawns[0].position;
            Require(Physics.Raycast(spawn+Vector3.up,Vector3.down,out var hit,2),"Spawn has supporting floor");
            Require(hit.normal.y>.9f,"Spawn floor is flat");
            Require(!Physics.CheckCapsule(spawn+Vector3.up*.35f,spawn+Vector3.up*1.55f,.25f),"Spawn standing capsule is clear");
            CheckWingPassages();
            CheckR06Repairs();
            var sources=new List<NavMeshBuildSource>();
            foreach(var c in UnityEngine.Object.FindObjectsOfType<Collider>())
            {
                if(!c.enabled || c.isTrigger)continue;
                if(c is BoxCollider box)sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=box.transform.localToWorldMatrix*Matrix4x4.Translate(box.center),size=box.size,area=0});
                else if(c is MeshCollider mesh)sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Mesh,transform=mesh.transform.localToWorldMatrix,sourceObject=mesh.sharedMesh,area=0});
                else throw new Exception("Unaccounted collision shape: "+c.GetType());
            }
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.25f;settings.agentHeight=1.75f;settings.agentClimb=.24f;settings.agentSlope=40;
            settings.overrideVoxelSize=true;settings.voxelSize=.08f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(new Vector3(0,12,18),new Vector3(80,70,90)),Vector3.zero,Quaternion.identity);
            Require(data!=null,"Navigation geometry baked");var nav=NavMesh.AddNavMeshData(data);
            var rows=new List<string>{"destination,status,path_corners,length_m"};var paths=new List<Vector3[]>();var walkRoutes=new List<UnityCoastalWalkChecks.Route>();int failed=0;
            try
            {
                Require(NavMesh.SamplePosition(spawn,out var start,1,NavMesh.AllAreas),"Spawn is navigable");
                foreach(string name in new[]{"Arrival wing lantern","Arrival wing gallery","Cliff lounge","Hidden tide lounge","Conversation pit","Water overlook","High lookout"})
                {
                    Vector3 destination=GameObject.Find("06 Experience anchors/"+name).transform.position;
                    bool sampled=NavMesh.SamplePosition(destination,out var end,.65f,NavMesh.AllAreas);var path=new NavMeshPath();
                    bool valid=sampled && NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
                    float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);
                    rows.Add(name+","+(valid?"complete":sampled?path.status.ToString():"no destination")+","+path.corners.Length+","+length.ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
                    File.WriteAllLines(Folder+"/route-"+name+".txt",path.corners.Select(p=>p.ToString("F3")));
                    if(valid){paths.Add(path.corners);walkRoutes.Add(new UnityCoastalWalkChecks.Route{name=name,points=path.corners});}else failed++;
                }
                File.WriteAllLines(Folder+"/routes.csv",rows);
                File.WriteAllText(Folder+"/walk-routes.json",JsonUtility.ToJson(new UnityCoastalWalkChecks.Routes{routes=walkRoutes.ToArray()},true));
            }
            finally{nav.Remove();UnityEngine.Object.DestroyImmediate(data);}
            var filters=UnityEngine.Object.FindObjectsOfType<MeshFilter>();
            var dynamicTrails=new HashSet<MeshFilter>(UnityEngine.Object.FindObjectsOfType<BirdPointPresentation>(true).Select(v=>v.trailMesh));
            Require(filters.All(f=>f.sharedMesh!=null||dynamicTrails.Contains(f)),"Only known runtime Bird trails may omit an authored mesh");
            long tris=filters.Where(f=>f.sharedMesh!=null).Sum(f=>(long)f.sharedMesh.triangles.Length/3);
            int materials=UnityEngine.Object.FindObjectsOfType<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct().Count();
            File.WriteAllText(Folder+"/geometry-budget.txt","Scene instances: "+filters.Length+" mesh filters ("+filters.Count(f=>f.sharedMesh==null)+" known runtime Bird trail slots); "+tris+" authored triangles; "+materials+" shared materials; "+sources.Count+" colliders. Inactive personal rigs and runtime-generated trails are not a measured device workload.\nNavigation capsule: radius .25 m, height 1.75 m, climb .24 m; voxel .08 m. Failed routes="+failed+".\n");
            Capture("01-arrival",new Vector3(0,1.65f,-13),new Vector3(0,4,4),90);
            Capture("02-threshold",new Vector3(0,2.65f,4.5f),new Vector3(3,6,17),82);
            Capture("03-main-terrace",new Vector3(14,2.65f,29),new Vector3(-8,7,16),82);
            Capture("04-lantern-room",new Vector3(-17.8f,1.65f,1),new Vector3(-23,1.2f,1),85);
            Capture("05-discovery",new Vector3(-10,-.35f,43),new Vector3(-19,.5f,38),78);
            Capture("06-water-overlook",new Vector3(15,-.35f,47),new Vector3(130,15,220),78);
            Capture("07-exterior",new Vector3(65,22,78),new Vector3(-3,10,15),70);
            Capture("08-upper-lookout",new Vector3(3,22.65f,36),new Vector3(130,5,220),80);
            Capture("09-gallery",new Vector3(17.8f,1.65f,1),new Vector3(23,1.5f,1),85);
            CapturePlan(paths);
            Capture("11-cliff-room-looking-out",new Vector3(-21,7.65f,20),new Vector3(-11,7.5f,20),85);
            Capture("12-hidden-room-looking-out",new Vector3(-21,-.35f,38),new Vector3(-11,.5f,38),85);
            Capture("13-left-wing-looking-out",new Vector3(-23,1.65f,1),new Vector3(-13,2,1),90);
            Capture("14-right-wing-looking-out",new Vector3(23,1.65f,1),new Vector3(13,2,1),90);
            Capture("15-arrival-rear-wall",new Vector3(0,1.65f,-8),new Vector3(0,5,-18),90);
            Capture("16-tide-room-seated-coast",new Vector3(-22.082f,-.9f,37.773f),new Vector3(-9.404f,-.9f,64.962f),85);
            Capture("17-tide-room-approach",new Vector3(-14.3f,-.35f,44.2f),new Vector3(-20.5f,-.4f,38.8f),85);
            Capture("18-left-passage-lateral",new Vector3(-17.8f,1.65f,2),new Vector3(-13,2,1),90);
            Capture("19-right-passage-lateral",new Vector3(17.8f,1.65f,0),new Vector3(13,2,1),90);
            Capture("20-upper-floor",new Vector3(2,14.65f,23),new Vector3(-15,15,22),85);
            if(UnityEngine.Object.FindObjectOfType<BirdPersonalStation>(true)!=null)
                Capture("21-personal-bird-pedestal",new Vector3(0,1.6f,-3.7f),new Vector3(0,1.2f,-1.4f),65);
            CaptureR06Repairs();
            Require(failed==0,"Navigation routes incomplete; inspect routes.csv (captures retained)");
            CheckEditableProfile();
            int captures=20+(UnityEngine.Object.FindObjectOfType<BirdPersonalStation>(true)!=null?1:0)+(GameObject.Find("03 Supported coastal terraces/Closed conversation pit steps")!=null?8:0);
            Finish("coastal-check",true,"Saved authored scene: safe spawn, six prefab regions, all seven destination routes complete for standing capsule; scoped mesh edit preservation and "+captures+" captures. "+tris+" instance triangles / "+materials+" materials. Not physical headset, multiplayer or measured device performance.");
        }
        catch(Exception e){Finish("coastal-check",false,e.ToString());}
    }
    static void CheckWingPassages()
    {
        // Check both traversal directions and lateral room-to-throat movement,
        // rather than judging a circular opening only from its center image.
        int samples=0;
        foreach(int s in new[]{-1,1})for(int x=0;x<=12;x++)for(int z=0;z<=12;z++)
        {
            var foot=new Vector3(s*(14.5f+x*.25f),.03f,-.5f+z*.25f);
            Require(!Physics.CheckCapsule(foot+Vector3.up*.25f,foot+Vector3.up*1.5f,.25f),"Wing passage standing clearance at "+foot);
            Require(Physics.Raycast(foot+Vector3.up*.1f,Vector3.down,out var floor,.3f)&&floor.normal.y>.9f,"Continuous wing passage floor at "+foot);
            samples++;
        }
        File.WriteAllText(Folder+"/passage-clearance.txt","PASS: "+samples+" standing capsule samples across both wing passages (3 m longitudinal by 3 m lateral, .25 m spacing), radius .25 m / height 1.75 m, with continuous flat supporting floor. This is sampled geometric clearance, not a varied-avatar comfort test.");
    }
    static void CheckEditableProfile()
    {
        string path=BirdCoastalWorldAuthoring.Folder+"/WorldProfile.asset";
        var profile=AssetDatabase.LoadAssetAtPath<BirdCoastalWorldProfile>(path);string saved=EditorJsonUtility.ToJson(profile);
        string meshPath=BirdCoastalWorldAuthoring.Folder+"/Meshes/Circular threshold.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);string guid=AssetDatabase.AssetPathToGUID(meshPath);
        var original=mesh.vertices;var poses=UnityEngine.Object.FindObjectsOfType<Transform>().ToDictionary(t=>t,t=>t.localToWorldMatrix);
        var sentinel=new GameObject("Authored child preservation check");sentinel.transform.SetParent(GameObject.Find("01 Arrival cavern").transform,false);sentinel.transform.localPosition=new Vector3(1,2,3);
        try
        {
            profile.thresholdRadius+=.1f;BirdCoastalWorldAuthoring.UpdateProfileMeshes();
            Require(AssetDatabase.AssetPathToGUID(meshPath)==guid&&AssetDatabase.LoadAssetAtPath<Mesh>(meshPath)==mesh,"Profile update preserves mesh identity and GUID");
            Require(!mesh.vertices.SequenceEqual(original),"Profile changes actual mesh geometry");
            Require(poses.All(p=>p.Key!=null&&p.Key.localToWorldMatrix==p.Value)&&sentinel.transform.localPosition==new Vector3(1,2,3),"Profile update preserves transforms and authored additions");
            var valid=mesh.vertices;profile.thresholdRadius=float.NaN;bool rejected=false;
            try{BirdCoastalWorldAuthoring.UpdateProfileMeshes();}catch(InvalidOperationException){rejected=true;}
            Require(rejected&&mesh.vertices.SequenceEqual(valid),"Invalid dimensions fail before mutating geometry");
        }
        finally{EditorJsonUtility.FromJsonOverwrite(saved,profile);BirdCoastalWorldAuthoring.UpdateProfileMeshes();UnityEngine.Object.DestroyImmediate(sentinel);}
        Require(mesh.vertices.SequenceEqual(original),"Restored profile exactly restores mesh vertices");
        File.WriteAllText(Folder+"/editability.txt","PASS: scoped profile updates change geometry, preserve mesh GUID/reference identity, all scene transforms and authored additions, reject invalid dimensions before mutation, and restore original vertices exactly. Open-scene mesh collider cooking refreshed. Changes are not claimed to reflow circulation automatically.");
    }
    static void Capture(string name,Vector3 eye,Vector3 look,float fov)
    {
        var go=new GameObject("Validation camera");var cam=go.AddComponent<Camera>();cam.transform.position=eye;cam.transform.LookAt(look);cam.fieldOfView=fov;
        cam.nearClipPlane=.03f;cam.farClipPlane=10000;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.48f,.68f,.78f);
        Render(cam,Folder+"/"+name+".png");UnityEngine.Object.DestroyImmediate(go);
    }
    static void CapturePlan(List<Vector3[]> paths)
    {
        var go=new GameObject("Plan camera");var cam=go.AddComponent<Camera>();cam.transform.position=new Vector3(0,90,20);cam.transform.rotation=Quaternion.Euler(90,0,0);cam.orthographic=true;cam.orthographicSize=45;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.14f,.18f,.21f);
        // Remove roof/upper slabs only for the explicitly labeled circulation plan.
        var hidden=UnityEngine.Object.FindObjectsOfType<Renderer>().Where(r=>r.name.Contains("vault") || r.name.Contains("overhang") || r.name=="High lookout" || r.name=="Arrival ridge shoulder").ToArray();foreach(var r in hidden)r.enabled=false;
        var lines=new List<GameObject>();foreach(var path in paths){var line=new GameObject("Measured navigation route");var lr=line.AddComponent<LineRenderer>();lr.sharedMaterial=new Material(Shader.Find("Unlit/Color")){color=Color.cyan};lr.positionCount=path.Length;lr.SetPositions(path.Select(p=>p+Vector3.up*.15f).ToArray());lr.startWidth=lr.endWidth=.12f;lines.Add(line);}
        Render(cam,Folder+"/10-circulation-plan-roofs-hidden.png");foreach(var r in hidden)r.enabled=true;foreach(var line in lines){UnityEngine.Object.DestroyImmediate(line.GetComponent<Renderer>().sharedMaterial);UnityEngine.Object.DestroyImmediate(line);}UnityEngine.Object.DestroyImmediate(go);
    }
    static void Render(Camera cam,string path)
    {
        var target=new RenderTexture(1600,1000,24){antiAliasing=4};cam.targetTexture=target;cam.Render();RenderTexture.active=target;
        var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(image);
    }
    public static async void Build()
    {
        VRCSdkControlPanel panel=null;bool had=EditorPrefs.HasKey("VRC.SDKBase_StripAllShaders"),old=EditorPrefs.GetBool("VRC.SDKBase_StripAllShaders");
        try
        {
            Directory.CreateDirectory(Folder);var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            Require(!UdonSharp.UdonSharpProgramAsset.AnyUdonSharpScriptHasError(),"Udon compilation");
            audit=true;processed=0;var descriptor=UnityEngine.Object.FindObjectOfType<VRCSceneDescriptor>();
            panel=ScriptableObject.CreateInstance<VRCSdkControlPanel>();var builder=new VRCSdkControlPanelWorldBuilder();builder.RegisterBuilder(panel);
            Require(builder.IsValidBuilder(out string reason),reason);string errors="";builder.OnSdkBuildError+=(_,e)=>errors+=e+"\n";
            string source=await builder.Build();Require(string.IsNullOrEmpty(errors)&&File.Exists(source),"Normal SDK export failed: "+errors);Require(processed>0,"Processed scene audit did not execute");
            bool mobile=EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android;string platform=mobile?"Android":"Windows",dest=Folder+"/BirdCoastalWorld_"+platform+".vrcw";File.Copy(source,dest,true);
            Require(!ValidationEditorHelpers.CheckIfAssetBundleFileTooLarge(ContentType.World,dest,out int size,mobile),"Compressed upload-size gate");
            Require(!ValidationEditorHelpers.CheckIfUncompressedAssetBundleFileTooLarge(ContentType.World,out int unpacked,mobile),"Uncompressed upload-size gate");
            var bundle=AssetBundle.LoadFromFile(dest);Require(bundle!=null,"Bundle catalog opens");var catalog=bundle.GetAllScenePaths();bundle.Unload(true);Require(catalog.Length==1&&catalog[0].Equals(BirdCoastalWorldAuthoring.ScenePath,StringComparison.OrdinalIgnoreCase),"Scene catalog identity");
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(dest))).Replace("-","");
            string result="Normal SDK "+platform+" build and upload-size gates; "+new FileInfo(dest).Length+" bytes; SHA256="+hash+"; "+inventory+"; catalog verified. No launch or upload.";
            File.WriteAllText(Folder+"/build-"+platform+".txt",result);Finish("coastal-build",true,result,false);
        }
        catch(Exception e){Finish("coastal-build",false,e.ToString(),false);}
        finally{audit=false;if(had)EditorPrefs.SetBool("VRC.SDKBase_StripAllShaders",old);else EditorPrefs.DeleteKey("VRC.SDKBase_StripAllShaders");if(panel!=null)UnityEngine.Object.DestroyImmediate(panel);EditorApplication.Exit(File.ReadAllText("coastal-build-result.txt").StartsWith("PASS:")?0:1);}
    }
    public void OnProcessScene(Scene scene,BuildReport report)
    {
        if(!audit || scene.path!=BirdCoastalWorldAuthoring.ScenePath)return;
        var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Component>(true)).ToArray();
        Require(all.All(c=>c!=null),"No missing components after SDK processing");
        Require(!all.OfType<MonoBehaviour>().Any(c=>c.GetType().Assembly.GetName().Name.StartsWith("Assembly-CSharp")),"No project MonoBehaviours in exported world");
        var programs=all.OfType<VRC.Udon.UdonBehaviour>().ToArray();
        int beaconCount=all.OfType<Transform>().Count(t=>t.name.StartsWith("Beacon / "));
        Require(beaconCount==0||beaconCount==5,"Travel network has exactly five authored destinations when present");
        int expected=beaconCount==0?18:28;
        Require(programs.Length==expected,"Personal/social pipelines and any complete travel network survive SDK processing");
        var templates=all.OfType<VRCPlayerObject>().ToArray();
        Require(templates.Length==1,"One per-player presentation template");
        Require(!all.OfType<VRCEnablePersistence>().Any(),"Transient cursor streams are not persisted");
        Require(programs.Count(p=>p.SyncMethod==VRC.SDKBase.Networking.SyncType.Manual)==1,"One manual snapshot stream per player");
        foreach(var vm in programs)
        {
            var program=new SerializedObject(vm).FindProperty("serializedProgramAsset");
            Require(program!=null&&program.objectReferenceValue!=null,"Exported Udon bytecode reference exists");
            Require(vm.SyncMethod==VRC.SDKBase.Networking.SyncType.None ||
                (vm.SyncMethod==VRC.SDKBase.Networking.SyncType.Manual && vm.gameObject==templates[0].gameObject),"Only the player-object bridge is networked");
        }
        Require(all.OfType<VRCSceneDescriptor>().Count()==1&&all.OfType<PipelineManager>().Count()==1,"Descriptor and pipeline retained");processed++;
        inventory=all.OfType<Transform>().Count()+" GameObjects, "+all.Length+" components, "+(expected-1)+" unsynced Udon programs + one manual per-player stream, no persistence or missing/project scripts";
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Finish(string stem,bool pass,string message,bool exit=true)
    {
        File.WriteAllText(stem+"-result.txt",(pass?"PASS: ":"FAIL: ")+message);
        if(exit)EditorApplication.Exit(pass?0:1);
    }
}
#endif
