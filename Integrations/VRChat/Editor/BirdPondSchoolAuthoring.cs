#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UdonSharp;
using UdonSharpEditor;

public static class BirdPondSchoolAuthoring
{
    public const string Folder="Assets/BirdWorld/PondSchool";
    public const string PrefabPath=Folder+"/Pond shoals.prefab";
    [MenuItem("Bird/Coastal world/Add pond shoals once")]
    public static async void FromMenu(){await Add();}
    public static async Task Add()
    {
        if(File.Exists(PrefabPath))throw new InvalidOperationException("Shoals already authored; edit the saved prefab.");
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        if(UnityEngine.Object.FindObjectOfType<BirdPondSchool>(true)!=null)throw new Exception("Pond school already exists.");
        var profile=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(BirdCoastalPondAuthoring.ProfilePath);
        if(profile==null)throw new Exception("Author curved pond first.");
        var station=UnityEngine.Object.FindObjectOfType<BirdPersonalStation>(true);
        var pond=GameObject.Find("04 Lower water and hidden lounge/Curved pond promenade").transform;
        string programPath="Assets/BirdWorld/Programs/BirdPondSchool.asset";
        var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programPath);
        if(program==null){program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();program.sourceCsScript=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdPondSchool.cs");AssetDatabase.CreateAsset(program,programPath);}
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        var deadline=DateTime.UtcNow.AddSeconds(45);
        while(program.ScriptVersion<UdonSharpProgramVersion.CurrentVersion&&DateTime.UtcNow<deadline)await Task.Delay(100);
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new Exception("Udon compile failed.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var fishMesh=FishMesh();AssetDatabase.CreateAsset(fishMesh,Folder+"/Small koi.asset");
        Color[] colors={new Color(1,.74f,.3f),new Color(.84f,.92f,.88f),new Color(.35f,.64f,.7f)};
        var materials=new Material[3];
        for(int i=0;i<3;i++){materials[i]=new Material(Shader.Find("Bird/Pond Fish")){color=colors[i],enableInstancing=true};AssetDatabase.CreateAsset(materials[i],Folder+"/Fish pigment "+i+".mat");}
        var water=new Material(Shader.Find("Bird/Shallow Pond Water")){color=new Color(.1f,.43f,.5f,.32f)};AssetDatabase.CreateAsset(water,Folder+"/Shallow water.mat");
        var bottom=new Material(Shader.Find("Unlit/Color")){color=new Color(.095f,.235f,.25f)};AssetDatabase.CreateAsset(bottom,Folder+"/Basin bed.mat");
        var root=new GameObject("Pond shoals");
        try
        {
            var school=root.AddUdonSharpComponent<BirdPondSchool>();school.fish=new Transform[24];
            BirdCoastalPondAuthoring.Sample(profile,out var line,out _);
            school.waterSurface=profile.floorHeight-profile.waterDepth;school.waterHalfWidth=profile.waterHalfWidth;
            school.centerline=line.Select(p=>new Vector3(p.x,school.waterSurface,p.y)).ToArray();
            for(int i=0;i<24;i++)
            {
                var go=new GameObject("Shoal "+(i/6+1)+" / fish "+(i%6+1),typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform,false);
                school.fish[i]=go.transform;go.transform.localScale=Vector3.one*(.85f+(i%4)*.08f);
                int k=Mathf.Clamp((int)((i/6+.5f)*line.Count/4f)+(i%6)-3,0,line.Count-2);
                go.transform.localPosition=school.centerline[k]+new Vector3((i%3-1)*.32f,-.16f,0);
                go.transform.localRotation=Quaternion.LookRotation(school.centerline[k+1]-school.centerline[k]);
                go.GetComponent<MeshFilter>().sharedMesh=fishMesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=materials[i%3];r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
            var floor=new GameObject("Submerged basin bed",typeof(MeshFilter),typeof(MeshRenderer));floor.transform.SetParent(root.transform,false);floor.transform.localPosition=Vector3.down*school.waterDepth;
            floor.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(BirdCoastalPondAuthoring.Folder+"/Curved water.asset");
            floor.GetComponent<MeshRenderer>().sharedMaterial=bottom;floor.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            UdonSharpEditorUtility.CopyProxyToUdon(school);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,pond);
            var saved=instance.GetComponent<BirdPondSchool>();saved.station=station;
            UdonSharpEditorUtility.CopyProxyToUdon(saved);PrefabUtility.RecordPrefabInstancePropertyModifications(saved);PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(saved));
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        var renderer=pond.Find("Curved water").GetComponent<MeshRenderer>();renderer.sharedMaterial=water;renderer.shadowCastingMode=ShadowCastingMode.Off;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        for(int i=0;i<16;i++){var old=pond.parent.Find("Fish study "+i);old.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(old.gameObject);}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static Mesh FishMesh()
    {
        var v=new List<Vector3>();var t=new List<int>();var colors=new List<Color>();
        Action<Vector3,Vector3,Vector3,Color> tri=(a,b,c,color)=>{int k=v.Count;v.Add(a);v.Add(b);v.Add(c);t.Add(k);t.Add(k+1);t.Add(k+2);colors.Add(color);colors.Add(color);colors.Add(color);};
        // Closed fusiform body: nose +Z, narrow tail -Z. Deliberately few facets.
        float[] z={-.25f,-.12f,.08f,.24f,.34f},width={.025f,.075f,.12f,.085f,.012f},height={.025f,.055f,.08f,.055f,.012f};
        Func<int,int,Vector3> ring=(k,j)=>new Vector3(Mathf.Cos(j*Mathf.PI/4)*width[k],Mathf.Sin(j*Mathf.PI/4)*height[k],z[k]);
        for(int k=0;k<4;k++)for(int j=0;j<8;j++){var a=ring(k,j);var b=ring(k,(j+1)%8);var c=ring(k+1,(j+1)%8);var d=ring(k+1,j);var color=(k==2&&j%3==0)?new Color(.4f,.46f,.45f):Color.white;tri(a,b,c,color);tri(a,c,d,color);}
        for(int j=0;j<8;j++){tri(new Vector3(0,0,z[0]),ring(0,(j+1)%8),ring(0,j),Color.white);tri(new Vector3(0,0,z[4]),ring(4,j),ring(4,(j+1)%8),Color.white);}
        Vector3[] fins={new Vector3(0,0,-.23f),new Vector3(-.14f,0,-.42f),new Vector3(.14f,0,-.42f),new Vector3(-.07f,0,.03f),new Vector3(-.2f,-.018f,-.09f),new Vector3(-.075f,0,-.1f),new Vector3(.07f,0,.03f),new Vector3(.075f,0,-.1f),new Vector3(.2f,-.018f,-.09f)};
        for(int i=0;i<fins.Length;i+=3){tri(fins[i],fins[i+1],fins[i+2],Color.white);tri(fins[i],fins[i+2],fins[i+1],Color.white);}
        for(int side=-1;side<=1;side+=2)tri(new Vector3(side*.058f,.045f,.255f),new Vector3(side*.078f,.035f,.215f),new Vector3(side*.078f,.055f,.215f),new Color(.045f,.055f,.05f));
        var m=new Mesh{name="Small koi"};m.SetVertices(v);m.SetTriangles(t,0);m.SetColors(colors);m.RecalculateNormals();m.RecalculateBounds();m.bounds=new Bounds(Vector3.zero,new Vector3(.55f,.3f,1));return m;
    }
}
#endif
