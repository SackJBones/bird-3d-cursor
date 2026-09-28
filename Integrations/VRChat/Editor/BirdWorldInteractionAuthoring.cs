#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;

// Scoped migration; existing geometry, transforms and collision matrix stay intact.
public static class BirdWorldInteractionAuthoring
{
    public const string Folder="Assets/BirdWorld/ClickPractice";
    public const string PrefabPath=Folder+"/Finger tap practice.prefab";
    [MenuItem("Bird/Coastal world/Add safe pointing and click practice once")]
    public static async void FromMenu() { await Add(); }
    public static async Task Add()
    {
        if(File.Exists(PrefabPath)) throw new InvalidOperationException("Practice already authored; edit saved assets.");
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var station=UnityEngine.Object.FindObjectOfType<BirdPersonalStation>(true);
        var travel=UnityEngine.Object.FindObjectOfType<BirdTeleportRouter>(true);
        if(station==null||travel==null||travel.allowTeleport) throw new Exception("Expected gated personal Bird world.");
        // The SDK's standard layer matrix is retained, not patched.
        foreach(int player in new[]{9,10})
        {
            if(Physics.GetIgnoreLayerCollision(2,player))throw new Exception("Walking fences must collide with players.");
            if(!Physics.GetIgnoreLayerCollision(17,player))throw new Exception("Pointing proxies must be walkthrough for players.");
        }
        int guards=0;
        foreach(var region in scene.GetRootGameObjects().Where(PrefabUtility.IsAnyPrefabInstanceRoot))
        {
            string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(region);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var fences=root.GetComponentsInChildren<Collider>(true).Where(c=>c.name.EndsWith(" safety boundary")).ToArray();
                foreach(var fence in fences)
                {
                    var renderer=fence.GetComponent<Renderer>();
                    var visual=fence.transform.parent.Find(fence.name.Replace(" safety boundary",""));
                    if(renderer==null||renderer.enabled||fence.isTrigger||visual==null||visual.GetComponent<MeshFilter>()==null)throw new Exception("Unexpected guard structure: "+fence.name);
                    if(fence.gameObject.layer!=0&&fence.gameObject.layer!=2)throw new Exception("Preserve custom fence layer: "+fence.name);
                    if(visual.gameObject.layer!=0&&visual.gameObject.layer!=17)throw new Exception("Preserve custom rail layer: "+visual.name);
                }
                foreach(var fence in fences)
                {
                    var visual=fence.transform.parent.Find(fence.name.Replace(" safety boundary",""));
                    fence.gameObject.layer=2; visual.gameObject.layer=17;
                    var proxy=visual.GetComponent<MeshCollider>();
                    if(proxy==null)proxy=visual.gameObject.AddComponent<MeshCollider>();
                    proxy.sharedMesh=visual.GetComponent<MeshFilter>().sharedMesh;proxy.convex=false;proxy.isTrigger=false;
                    guards++;
                }
                if(fences.Length>0) PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        if(guards<10)throw new Exception("Expected complete guard inventory");
        var network=PrefabUtility.LoadPrefabContents(BirdTeleportAuthoring.PrefabPath);
        try
        {
            foreach(var beacon in network.GetComponentsInChildren<BirdTeleportBeacon>())
            {
                beacon.occlusionLayers=(1<<0)|(1<<11)|(1<<17);
                beacon.solidLayers=(1<<0)|(1<<2)|(1<<11);
                UdonSharpEditorUtility.CopyProxyToUdon(beacon);
            }
            PrefabUtility.SaveAsPrefabAsset(network,BirdTeleportAuthoring.PrefabPath);
        }finally{PrefabUtility.UnloadPrefabContents(network);}

        string programPath="Assets/BirdWorld/Programs/BirdClickPractice.asset";
        var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(programPath);
        if(program==null)
        {
            program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            program.sourceCsScript=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/BirdClickPractice.cs");
            AssetDatabase.CreateAsset(program,programPath);
        }
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();var deadline=DateTime.UtcNow.AddSeconds(60);
        while(program.ScriptVersion<UdonSharpProgramVersion.CurrentVersion&&DateTime.UtcNow<deadline)await Task.Delay(100);
        if(program.ScriptVersion<UdonSharpProgramVersion.CurrentVersion)throw new Exception("SDK program initialization timed out");
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
        if(UdonSharpProgramAsset.AnyUdonSharpScriptHasError())throw new Exception("Udon compile failed");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var rootPanel=new GameObject("Finger tap practice");
        try
        {
            var probe=rootPanel.AddUdonSharpComponent<BirdClickPractice>();
            var canvas=new GameObject("Practice artwork",typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.WorldSpace;canvas.transform.SetParent(rootPanel.transform,false);canvas.transform.localScale=Vector3.one*.001f;
            canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(2000,1300);
            Graphic(canvas.transform,"Panel",Vector2.zero,new Vector2(2000,1300),new Color(.03f,.12f,.16f));
            Label(canvas.transform,"Title",new Vector2(0,505),new Vector2(1800,150),105,"Finger tap practice");
            probe.status=Label(canvas.transform,"Instructions",new Vector2(0,315),new Vector2(1800,200),62,"Get a Bird, then come closer\nPractice only: nothing will move");
            var sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if(sprite==null)throw new Exception("Missing built-in UI sprite");
            for(int i=0;i<2;i++)
            {
                float x=i==0?-480:480;
                probe.lights[i]=Graphic(canvas.transform,"Hand state "+i,new Vector2(x,100),new Vector2(110,110),Color.gray);
                probe.readouts[i]=Label(canvas.transform,"Hand reading "+i,new Vector2(x,-75),new Vector2(860,170),65,(i==0?"LEFT":"RIGHT")+"  SHOW HAND\n0.0 mm   /   0 taps");
                Graphic(canvas.transform,"Meter backing "+i,new Vector2(x,-255),new Vector2(750,70),new Color(.16f,.23f,.26f));
                var meter=Graphic(canvas.transform,"Depth meter "+i,new Vector2(x,-255),new Vector2(750,70),i==0?Color.cyan:Color.magenta);
                meter.sprite=sprite;meter.type=Image.Type.Filled;meter.fillMethod=Image.FillMethod.Horizontal;meter.fillOrigin=0;meter.fillAmount=0;probe.meters[i]=meter;
                Graphic(canvas.transform,"Press threshold "+i,new Vector2(x,-255),new Vector2(5,100),Color.white);
            }
            Label(canvas.transform,"Meter key",new Vector2(0,-445),new Vector2(1800,170),52,"White line = press depth\nTry a relaxed hand, a pointing finger and a closed fist");
            UdonSharpEditorUtility.CopyProxyToUdon(probe);
            var prefab=PrefabUtility.SaveAsPrefabAsset(rootPanel,PrefabPath);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,GameObject.Find("06 Experience anchors").transform);
            instance.transform.position=new Vector3(7,1.5f,-7);instance.transform.rotation=Quaternion.Euler(0,65,0);
            var saved=instance.GetComponent<BirdClickPractice>();saved.station=station;
            saved.inputs=station.personalRig.GetComponentsInChildren<BirdAvatarHandInput>(true).OrderBy(i=>i.rightHand).ToArray();
            saved.views=saved.inputs.Select(i=>station.personalRig.GetComponentsInChildren<BirdPointPresentation>(true).Single(v=>v.cursor==i.cursor)).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(saved);
            PrefabUtility.RecordPrefabInstancePropertyModifications(saved);
            PrefabUtility.RecordPrefabInstancePropertyModifications(UdonSharpEditorUtility.GetBackingUdonBehaviour(saved));
            UnityEngine.Object.DestroyImmediate(rootPanel);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Scene save failed");
            AssetDatabase.SaveAssets();
        }finally{if(rootPanel!=null)UnityEngine.Object.DestroyImmediate(rootPanel);}
    }
    static Image Graphic(Transform parent,string name,Vector2 p,Vector2 size,Color color)
    {
        var image=new GameObject(name,typeof(RectTransform)).AddComponent<Image>();image.transform.SetParent(parent,false);
        image.rectTransform.anchoredPosition=p;image.rectTransform.sizeDelta=size;image.color=color;image.raycastTarget=false;return image;
    }
    static Text Label(Transform parent,string name,Vector2 p,Vector2 size,int fontSize,string text)
    {
        var label=new GameObject(name,typeof(RectTransform)).AddComponent<Text>();label.transform.SetParent(parent,false);
        label.rectTransform.anchoredPosition=p;label.rectTransform.sizeDelta=size;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize=fontSize;label.text=text;label.alignment=TextAnchor.MiddleCenter;label.color=Color.white;label.raycastTarget=false;return label;
    }
}
#endif
