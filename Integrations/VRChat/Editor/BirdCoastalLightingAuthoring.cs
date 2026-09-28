#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Ordinary saved LightingSettings, lights, UV2 and light probes. No runtime code.
public static class BirdCoastalLightingAuthoring
{
    public const string Folder="Assets/BirdWorld/CoastalLighting";
    public const string SettingsPath=Folder+"/Coastal daylight.lighting";
    [MenuItem("Bird/Coastal world/Prepare baked daylight once")]
    public static void Prepare()
    {
        if(File.Exists(SettingsPath))throw new InvalidOperationException("Lighting already prepared. Edit saved lights/settings, then rebake.");
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var settings=new LightingSettings
        {
            name="Coastal daylight",autoGenerate=false,bakedGI=true,realtimeGI=false,
            lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,
            lightmapResolution=3,lightmapMaxSize=1024,lightmapPadding=4,
            directionalityMode=LightmapsMode.NonDirectional,
            directSampleCount=64,indirectSampleCount=128,environmentSampleCount=64,
            minBounces=2,maxBounces=4,lightProbeSampleCountMultiplier=2,
            ao=true,aoMaxDistance=.5f,aoExponentIndirect=.65f,aoExponentDirect=0,
            filteringMode=LightingSettings.FilterMode.Auto
        };
        AssetDatabase.CreateAsset(settings,SettingsPath);Lightmapping.lightingSettings=settings;
        var meshSet=new HashSet<Mesh>();int mapped=0;
        foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>(true))
        {
            var root=r.transform.root.name;
            bool region=root.StartsWith("01 ")||root.StartsWith("02 ")||root.StartsWith("03 ")||root.StartsWith("04 ")||root.StartsWith("05 ");
            if(!region)continue;
            var flags=GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            bool lit=r.enabled&&r.gameObject.activeInHierarchy&&r.sharedMaterials.All(m=>m!=null&&m.shader.name=="Standard");
            GameObjectUtility.SetStaticEditorFlags(r.gameObject,lit?flags|StaticEditorFlags.ContributeGI:flags&~StaticEditorFlags.ContributeGI);
            if(!lit)continue;
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;
            if(mesh==null)throw new Exception("Missing mesh: "+r.name);
            string path=AssetDatabase.GetAssetPath(mesh);
            if(path.StartsWith("Assets/",StringComparison.Ordinal)&&mesh.uv2.Length!=mesh.vertexCount&&meshSet.Add(mesh))
            {
                GenerateUv(mesh,1);
            }
            r.receiveGI=ReceiveGI.Lightmaps;r.lightProbeUsage=LightProbeUsage.BlendProbes;
            r.scaleInLightmap=root.StartsWith("05 ")?.035f:r.name.Contains("edge")||r.name.Contains("parapet")?.35f:1;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            PrefabUtility.RecordPrefabInstancePropertyModifications(r.gameObject);mapped++;
        }
        foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())
        {
            light.lightmapBakeType=LightmapBakeType.Baked;
            if(light.type==LightType.Directional)light.shadows=LightShadows.Soft;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        var rig=new GameObject("Baked coastal daylight");rig.transform.SetParent(GameObject.Find("06 Experience anchors").transform,false);
        Area(rig.transform,"Cavern ceiling bounce",new Vector3(0,7,-8),Vector3.down,new Vector2(16,12),2.5f);
        Area(rig.transform,"Circular opening sky fill",new Vector3(0,6,3.5f),Vector3.back,new Vector2(10,8),2);
        Area(rig.transform,"Terrace soffit bounce",new Vector3(3,2,16),Vector3.up,new Vector2(14,16),1.1f);
        var probes=new GameObject("Visitor light probes").AddComponent<LightProbeGroup>();probes.transform.SetParent(rig.transform,false);
        var points=new List<Vector3>();Physics.SyncTransforms();
        for(int x=-12;x<=12;x+=6)for(int z=-15;z<=3;z+=6)foreach(float y in new[]{.7f,1.7f,3.5f})Probe(points,new Vector3(x,y,z));
        foreach(var room in new[]{new Vector3(-21,0,1),new Vector3(21,0,1),new Vector3(-19,6,20),new Vector3(-21,-2,38)})
            foreach(float x in new[]{-3f,0,3f})foreach(float z in new[]{-2f,2f})foreach(float y in new[]{.7f,1.7f,3.3f})Probe(points,room+new Vector3(x,y,z));
        foreach(float floor in new[]{1f,13f,21f,-2f})for(int x=-10;x<=14;x+=8)for(int z=8;z<=48;z+=10)
        {
            var foot=new Vector3(x,floor,z);
            if(Physics.Raycast(foot+Vector3.up*.2f,Vector3.down,out var hit,.4f,(1<<0)|(1<<2)|(1<<11),QueryTriggerInteraction.Ignore)&&hit.normal.y>.9f)
                foreach(float y in new[]{.7f,1.7f,3.3f})Probe(points,foot+Vector3.up*y);
        }
        probes.probePositions=points.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("coastal-lighting-prepare-result.txt","PASS: Prepared ordinary baked daylight: "+mapped+" receivers, "+meshSet.Count+" UV2 meshes, "+points.Count+" probes. No geometry or Bird changes. Bake required.");
    }
    [MenuItem("Bird/Coastal world/Refine first daylight bake once")]
    public static void Refine()
    {
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var settings=Lightmapping.lightingSettings;
        if(settings==null||settings.lightmapResolution!=3)throw new Exception("This migration applies only to the initial 3 texel daylight setup.");
        settings.lightmapResolution=6;settings.lightmapPadding=8;settings.ao=false;
        settings.directSampleCount=128;settings.indirectSampleCount=256;EditorUtility.SetDirty(settings);
        var meshes=new HashSet<Mesh>();
        foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
        {
            var filter=r.GetComponent<MeshFilter>();var mesh=filter==null?null:filter.sharedMesh;
            if(mesh!=null&&AssetDatabase.GetAssetPath(mesh).StartsWith(BirdCoastalWorldAuthoring.Folder+"/Meshes/",StringComparison.Ordinal)&&mesh.uv2.Length==mesh.vertexCount&&meshes.Add(mesh))GenerateUv(mesh,4);
        }
        foreach(var light in UnityEngine.Object.FindObjectsOfType<Light>())
        {
            if(light.name=="Cavern ceiling bounce")light.intensity=1.3f;
            if(light.name=="Circular opening sky fill")light.intensity=1.2f;
            if(light.name=="Terrace soffit bounce")light.intensity=.7f;
            if(light.type==LightType.Point)light.intensity=1.6f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("coastal-lighting-refine-result.txt","PASS: Refined ordinary saved lights and UV chart spacing; 6 texels/m, no added AO, exact geometry retained. Rebake required.");
    }
    static void GenerateUv(Mesh mesh,float paddingMultiplier)
    {
        // Authored meshes have separate triangle corners. Generate UVs only;
        // GenerateSecondaryUVSet also welds positions, changing collider seams.
        var before=Surface(mesh);var bounds=mesh.bounds;var indices=mesh.triangles;
        if(indices.Length!=mesh.vertexCount||indices.Distinct().Count()!=mesh.vertexCount)throw new Exception("Mesh needs authored split UV2 before baking: "+mesh.name);
        UnwrapParam.SetDefaults(out var parameters);parameters.packMargin*=paddingMultiplier;
        var corners=Unwrapping.GeneratePerTriangleUV(mesh,parameters);
        if(corners==null||corners.Length!=indices.Length)throw new Exception("Could not unwrap "+mesh.name);
        var uv=new Vector2[mesh.vertexCount];for(int i=0;i<indices.Length;i++)uv[indices[i]]=corners[i];mesh.uv2=uv;
        if(!Surface(mesh).SequenceEqual(before)||mesh.bounds!=bounds)throw new Exception("UV unwrap altered surface geometry: "+mesh.name);
        EditorUtility.SetDirty(mesh);
    }
    [MenuItem("Bird/Coastal world/Set curved shell bake offset once")]
    public static void SmoothJoins()
    {
        const string path=Folder+"/Curved shell rays.giparams";
        if(File.Exists(path))throw new Exception("Curved shell parameters already exist; edit their Inspector.");
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var source=new SerializedObject(Lightmapping.lightingSettings).FindProperty("m_LightmapParameters").objectReferenceValue as LightmapParameters;
        if(source==null)throw new Exception("Missing default lightmap parameters.");
        var parameters=UnityEngine.Object.Instantiate(source);parameters.name="Curved shell rays";parameters.pushoff=.02f;AssetDatabase.CreateAsset(parameters,path);
        int count=0;
        foreach(var r in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
        {
            var filter=r.GetComponent<MeshFilter>();if(filter==null||filter.sharedMesh==null)continue;
            string name=filter.sharedMesh.name;
            if(name!="Branching support"&&name!="Arrival vault"&&name!="Circular threshold"&&name!="R05 Wing throat")continue;
            var serialized=new SerializedObject(r);serialized.FindProperty("m_LightmapParameters").objectReferenceValue=parameters;serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);count++;
        }
        if(count<3)throw new Exception("Expected curved shells were not found.");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("coastal-lighting-joins-result.txt","PASS: Set 2 cm bake ray offset on "+count+" curved shells only; floor and stair settings unchanged. Rebake required.");
    }
    static Vector3[] Surface(Mesh mesh){var v=mesh.vertices;return mesh.triangles.Select(i=>v[i]).ToArray();}
    static void Probe(List<Vector3> points,Vector3 p){if(!Physics.CheckSphere(p,.12f,(1<<0)|(1<<2)|(1<<11),QueryTriggerInteraction.Ignore))points.Add(p);}
    static void Area(Transform root,string name,Vector3 p,Vector3 direction,Vector2 size,float intensity)
    {
        var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(root,false);light.transform.position=p;
        light.transform.rotation=Quaternion.LookRotation(direction,Mathf.Abs(direction.y)>.9f?Vector3.forward:Vector3.up);
        light.type=LightType.Area;light.areaSize=size;light.range=50;light.color=Color.white;light.intensity=intensity;light.lightmapBakeType=LightmapBakeType.Baked;light.shadows=LightShadows.Soft;
    }
    [MenuItem("Bird/Coastal world/Bake current saved lighting")]
    public static async void FromMenu(){await Bake();}
    public static async Task Bake()
    {
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        if(Lightmapping.lightingSettings==null||AssetDatabase.GetAssetPath(Lightmapping.lightingSettings)!=SettingsPath)throw new Exception("Prepare lighting first.");
        if(!Lightmapping.BakeAsync())throw new Exception("Lightmapper did not start.");
        var deadline=DateTime.UtcNow.AddMinutes(8);
        while(Lightmapping.isRunning&&DateTime.UtcNow<deadline)await Task.Delay(200);
        if(Lightmapping.isRunning){Lightmapping.Cancel();throw new Exception("Bounded lighting bake timed out.");}
        if(Lightmapping.lightingDataAsset==null||LightmapSettings.lightmaps.Length==0)throw new Exception("Bake produced no lighting data.");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("coastal-lighting-bake-result.txt","PASS: Baked "+LightmapSettings.lightmaps.Length+" non-directional lightmaps and "+LightmapSettings.lightProbes.count+" light probes into ordinary scene assets.");
    }
}
#endif
