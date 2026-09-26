#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Samples;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public class UnityVisualStateChecks : MonoBehaviour
{
    const string Active="Bird.Visual.State.Checks";
    static int checks;
    int wait;
    sealed class Fixture:IDisposable
    {
        public GameObject root=new GameObject("Visual fixture");
        public BirdMenuElement element;
        public BirdPointerInput pointer;
        public BirdMenuInteractor router;
        public BirdMenuVisual visual;
        public Transform art;
        public Renderer renderer;
        public Material material;
        public BoxCollider hit;
        public Vector3 home=new Vector3(.1f,.2f,.3f),size=new Vector3(-.3f,.4f,.5f);
        public Quaternion rotation=Quaternion.Euler(10,20,30);
        public Fixture()
        {
            hit=root.AddComponent<BoxCollider>(); element=root.AddComponent<BirdMenuElement>(); element.Configure(hit,null,BirdMenuElement.Activation.SelectThrough);
            pointer=new GameObject("Input").AddComponent<BirdPointerInput>(); pointer.transform.SetParent(root.transform,false);
            router=root.AddComponent<BirdMenuInteractor>(); router.Configure(new[]{pointer},new[]{element});
            art=new GameObject("Artwork").transform; art.SetParent(root.transform,false); art.localPosition=home; art.localRotation=rotation; art.localScale=size;
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube); DestroyImmediate(body.GetComponent<Collider>()); body.transform.SetParent(art,false); renderer=body.GetComponent<Renderer>();
            material=new Material(Shader.Find("Unlit/Color")) { color=Color.magenta }; renderer.sharedMaterial=material;
            var block=new MaterialPropertyBlock(); block.SetColor("_Color",Color.red); block.SetFloat("_Sentinel",.42f); renderer.SetPropertyBlock(block);
            visual=root.AddComponent<BirdMenuVisual>(); visual.Configure(element,art,renderer); visual.SetTransition(.2f);
            visual.SetAppearance(BirdMenuVisualState.Highlighted,new BirdMenuAppearance { positionOffset=new Vector3(.2f,.1f,-.1f),rotationOffset=new Vector3(0,30,0),scaleMultiplier=new Vector3(1.2f,.8f,1.1f),color=Color.cyan });
            visual.SetAppearance(BirdMenuVisualState.Activated,new BirdMenuAppearance { positionOffset=new Vector3(-.2f,0,.1f),rotationOffset=new Vector3(20,0,0),scaleMultiplier=Vector3.one*.9f,color=Color.yellow });
            Physics.SyncTransforms(); router.Process(); visual.Apply(0);
        }
        public void Feed(bool pressed=false,bool outside=false)
        { pointer.Submit(root.transform.TransformPoint(new Vector3(0,0,-3)),root.transform.TransformPoint(outside?new Vector3(3,0,1):Vector3.forward),true,pressed); router.Process(); }
        public Color ReadColor() { var block=new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); return block.GetColor("_Color"); }
        public void Dispose() { DestroyImmediate(root); DestroyImmediate(material); }
    }
    public static void Run()
    {
        File.WriteAllText("visual-result.txt","PENDING"); var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Visual state menu preview").AddComponent<BirdVisualStatesPreview>(); EditorSceneManager.SaveScene(scene,"Assets/VisualStates.unity");
        SessionState.SetBool(Active,true); EditorApplication.isPlaying=true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if(SessionState.GetBool(Active,false)) new GameObject("Visual checks").AddComponent<UnityVisualStateChecks>(); }
    void Update()
    {
        if(wait++<2) return; enabled=false;
        try
        {
            Laws(); Lifecycle(); Rates(); Prefab(); Preview();
            File.WriteAllText("visual-result.txt","PASS: "+checks+" actual Unity visual-state assertions, five rendered states and prefab/style/event round trip; no headset or Udon claim.");
            SessionState.SetBool(Active,false); EditorApplication.Exit(0);
        }
        catch(Exception e) { File.WriteAllText("visual-result.txt","FAIL: "+e); Debug.LogException(e); SessionState.SetBool(Active,false); EditorApplication.Exit(1); }
    }
    static void Laws()
    {
        using(var f=new Fixture())
        {
            Bounds hit=f.hit.bounds; Color shared=f.material.color; int actions=0; f.element.Activated.AddListener(p=>actions++);
            Assert(f.visual.DisplayedState==BirdMenuVisualState.Enabled,"First binding follows current state");
            f.Feed(); f.visual.Apply(.1f); Near(f.art.localPosition,f.home+new Vector3(.1f,.05f,-.05f),1e-6f,"Half-time smoothstep pose");
            Color mid=f.ReadColor(); Assert(mid.g>.45f && mid.b>.5f,"Tint interpolates"); Vector3 before=f.art.localPosition; Quaternion beforeRotation=f.art.localRotation;
            f.Feed(true); f.visual.Apply(0); Near(f.art.localPosition,before,1e-7f,"Interrupted pose starts at current value"); Assert(Quaternion.Angle(f.art.localRotation,beforeRotation)<.001f,"Interrupted orientation continuous");
            f.visual.Apply(.2f); Near(f.art.localPosition,f.home+new Vector3(-.2f,0,.1f),1e-6f,"Pressed target pose");
            Near(f.art.localScale,f.size*.9f,1e-6f,"Preserves mirrored/nonuniform baseline"); Assert(f.ReadColor()==Color.yellow && actions==1,"Tint and independent action both work");
            Physics.SyncTransforms(); Assert(f.hit.bounds==hit && f.material.color==shared,"Art animation does not mutate collider or shared material");
            f.router.Process(); f.visual.Apply(1); Assert(actions==1,"Rendering never repeats actions");
            f.pointer.Cancel(); f.router.Process(); f.visual.Apply(.2f); Assert(f.visual.DisplayedState==BirdMenuVisualState.Enabled,"Loss clears pressed visual");
            f.Feed(true); f.visual.Apply(.2f); Assert(actions==1 && f.visual.DisplayedState==BirdMenuVisualState.Activated,"Pressed presentation is a level, not action acknowledgement");
            f.element.enabled=false; f.visual.Apply(.1f); Assert(f.art.gameObject.activeSelf,"Outgoing invisible state can finish its transition"); f.visual.Apply(.1f);
            Assert(!f.art.gameObject.activeSelf && f.root.activeSelf && f.hit.enabled,"Inactive hides artwork only");
            f.element.enabled=true; f.pointer.Cancel(); f.router.Process(); f.visual.Apply(0); Assert(f.art.gameObject.activeSelf,"Visible state restores artwork immediately");
            f.visual.enabled=false; Near(f.art.localPosition,f.home,1e-7f,"Disable restores position"); Near(f.art.localScale,f.size,1e-7f,"Disable restores scale");
            var block=new MaterialPropertyBlock(); f.renderer.GetPropertyBlock(block); Assert(block.GetColor("_Color")==Color.red && Mathf.Abs(block.GetFloat("_Sentinel")-.42f)<1e-7f,"Disable restores complete original property block");
            f.visual.Apply(1); Assert(block.GetColor("_Color")==f.ReadColor(),"Disabled explicit Apply cannot rebind");
        }
        using(var f=new Fixture())
        {
            var panel=new GameObject("Parent").AddComponent<BirdMenuPanel>(); panel.transform.SetParent(f.root.transform); var parentContent=new GameObject("Parent content"); parentContent.transform.SetParent(f.root.transform); panel.Configure(parentContent);
            var content=(new GameObject("Child content")); content.transform.SetParent(f.root.transform);
            var child=new GameObject("Child").AddComponent<BirdMenuPanel>(); child.transform.SetParent(f.root.transform); child.Configure(content,panel);
            f.element.Configure(f.hit,panel,BirdMenuElement.Activation.SelectThrough); f.Feed(); panel.Open(f.pointer); f.router.Process(); f.visual.Apply(.2f);
            child.Open(f.pointer); f.router.Process(); f.visual.Apply(.2f); Assert(f.visual.DisplayedState==BirdMenuVisualState.Background,"Nested focus drives background");
            child.Close(); f.router.Process(); f.visual.Apply(.2f); Assert(f.visual.DisplayedState==BirdMenuVisualState.Highlighted,"Back restores hover");
            panel.Close(); f.router.Process(); f.visual.Apply(.2f); Assert(!f.art.gameObject.activeSelf,"Closed panel drives inactive");
        }
    }
    static void Lifecycle()
    {
        using(var f=new Fixture())
        {
            f.Feed(); f.visual.Apply(.2f); Vector3 current=f.art.localPosition; f.visual.Apply(float.NaN); f.visual.Apply(-1); Near(current,f.art.localPosition,0,"Bad elapsed time leaves finite pose unchanged");
            var copy=f.visual.GetAppearance(BirdMenuVisualState.Highlighted); copy.positionOffset=Vector3.one*100; f.visual.Apply(1); Near(current,f.art.localPosition,0,"Returned appearance is a copy");
            bool bad=false; try { f.visual.SetAppearance(BirdMenuVisualState.Enabled,new BirdMenuAppearance { scaleMultiplier=new Vector3(-1,1,1) }); } catch(ArgumentException) { bad=true; } Assert(bad,"Negative multiplier rejected");
            bad=false; try { f.visual.SetTransition(float.PositiveInfinity); } catch(ArgumentException) { bad=true; } Assert(bad,"Nonfinite duration rejected");
            var added=f.art.gameObject.AddComponent<BoxCollider>(); f.visual.Apply(0); Assert(f.visual.ConfigurationError!=null,"New collider in animated branch detected"); Near(f.art.localPosition,f.home,0,"Unsafe branch restores rest pose"); DestroyImmediate(added);
            f.visual.Apply(.2f); Assert(f.visual.ConfigurationError==null,"Removed invalid shape permits rebinding");
            var outside=new GameObject("Outside tint").AddComponent<MeshRenderer>(); outside.transform.SetParent(f.root.transform); f.visual.Configure(f.element,f.art,outside); f.visual.Apply(1); Assert(f.visual.ConfigurationError!=null,"Outside tint target rejected");
            f.visual.Configure(f.element,f.root.transform,f.renderer); f.visual.Apply(1); Assert(f.visual.ConfigurationError!=null,"Controller/source ancestor cannot animate or hide itself");
            f.visual.Configure(f.element,f.art,f.renderer); f.art.gameObject.SetActive(false); f.visual.Apply(0); Assert(f.art.gameObject.activeSelf,"Initially hidden artwork can show"); f.visual.enabled=false; Assert(!f.art.gameObject.activeSelf,"Restores initially hidden state");
        }
        using(var f=new Fixture())
        {
            var serial=new SerializedObject(f.visual); serial.FindProperty("highlighted").FindPropertyRelative("positionOffset").vector3Value=new Vector3(float.NaN,0,0); serial.ApplyModifiedPropertiesWithoutUndo();
            f.Feed(); f.visual.Apply(.2f); Near(f.art.localPosition,f.home,0,"Invalid serialized pose does not reach Transform"); Assert(f.ReadColor()==Color.red,"Invalid pose restores renderer block");
        }
        using(var f=new Fixture())
        {
            f.visual.SetAppearance(BirdMenuVisualState.Enabled,new BirdMenuAppearance { color=new Color(-float.MaxValue,0,0,1) }); f.visual.Apply(1);
            f.visual.SetAppearance(BirdMenuVisualState.Highlighted,new BirdMenuAppearance { color=new Color(float.MaxValue,0,0,1) }); f.Feed(); f.visual.Apply(.1f);
            Color tint=f.ReadColor();
            Assert(tint==Color.red || tint==new Color(0,0,0,1),"Extreme tint transition either interpolates safely or restores its original block");
        }
    }
    static void Rates()
    {
        var values=new List<Vector3>(); var csv=new List<string>{"hz,x,y,z"};
        foreach(int hz in new[]{30,72,120}) using(var f=new Fixture())
        {
            f.visual.SetTransition(.4f); f.visual.Apply(1); f.Feed(); f.visual.Apply(0);
            for(int i=0;i<hz/10;i++) f.visual.Apply(1f/hz);
            f.visual.Apply(Mathf.Max(0,.1f-(hz/10)/(float)hz));
            values.Add(f.art.localPosition); csv.Add(hz+","+f.art.localPosition.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+f.art.localPosition.y.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+","+f.art.localPosition.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture));
            float t=.1f/.4f; t=t*t*(3-2*t); Near(f.art.localPosition,f.home+new Vector3(.2f,.1f,-.1f)*t,1e-6f,"Analytic elapsed-time curve at "+hz);
            f.visual.Apply(2); Assert(!f.visual.IsTransitioning,"Long pause finishes bounded visual transition");
        }
        Directory.CreateDirectory("VisualCaptures"); File.WriteAllLines("VisualCaptures/visual-rates.csv",csv);
        foreach(Quaternion turn in new[]{Quaternion.identity,Quaternion.Euler(0,0,180),Quaternion.Euler(31,70,15)}) using(var f=new Fixture())
        {
            f.root.transform.rotation=turn; f.root.transform.localScale=new Vector3(-2,.5f,1.3f); f.Feed(); f.visual.Apply(.2f);
            Near(f.art.position,f.root.transform.TransformPoint(f.home+new Vector3(.2f,.1f,-.1f)),1e-6f,"Pose uses local parent coordinates");
        }
    }
    static void Prefab()
    {
        using(var f=new Fixture())
        {
            f.visual.Restore();
            f.renderer.sharedMaterial=Resources.Load<Material>("BirdMenuPreviewSurface");
            UnityEventTools.AddFloatPersistentListener(f.element.Activated,f.visual.SetTransition,.05f);
            PrefabUtility.SaveAsPrefabAsset(f.root,"Assets/VisualStyle.prefab"); var loaded=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VisualStyle.prefab"));
            var visual=loaded.GetComponent<BirdMenuVisual>(); var input=loaded.GetComponentInChildren<BirdPointerInput>(); var router=loaded.GetComponent<BirdMenuInteractor>(); var hit=loaded.GetComponent<BoxCollider>();
            Assert(visual.VisualRoot.IsChildOf(loaded.transform) && visual.VisualRoot!=f.art,"Prefab reference remaps to its own artwork");
            Near(visual.GetAppearance(BirdMenuVisualState.Highlighted).positionOffset,new Vector3(.2f,.1f,-.1f),0,"Prefab pose fields round trip");
            input.Submit(Vector3.back*3,Vector3.forward,true,false); Physics.SyncTransforms(); router.Process(); input.Submit(Vector3.back*3,Vector3.forward,true,true); router.Process();
            var serial=new SerializedObject(visual); Assert(Mathf.Abs(serial.FindProperty("transitionSeconds").floatValue-.05f)<1e-6f,"Persistent event reaches remapped visual component"); DestroyImmediate(loaded);
        }
    }
    static void Preview()
    {
        var preview=FindObjectOfType<BirdMenuPreview>(); preview.desktopInput=false; var source=preview.Pointer;
        Feed(preview,new Vector3(0,.35f,-.2f)); Feed(preview,new Vector3(0,1.05f,-.2f)); Feed(preview,new Vector3(3,3,0)); Settle(preview); Capture(preview,"enabled");
        Feed(preview,preview.ColorButton.transform.position); Settle(preview); Capture(preview,"highlighted");
        Feed(preview,preview.ColorButton.transform.position,true); Settle(preview); Capture(preview,"background");
        Feed(preview,preview.CyanButton.transform.position); Feed(preview,preview.CyanButton.transform.position,true); Settle(preview); Capture(preview,"activated");
        source.Cancel(); preview.Interactor.Process(); Settle(preview); Capture(preview,"inactive");
        Assert(preview.Menu.State==BirdMenuPanel.PanelState.Closed && preview.SelectionCount==1,"Presentation sample preserves actions and focus lifecycle");
    }
    static void Feed(BirdMenuPreview p,Vector3 point,bool pressed=false) { p.Pointer.Submit(p.View.transform.position,point,true,pressed); p.Interactor.Process(); }
    static void Settle(BirdMenuPreview p) { foreach(var visual in p.GetComponentsInChildren<BirdMenuVisual>(true)) visual.Apply(1); }
    static void Capture(BirdMenuPreview p,string name)
    {
        var rt=new RenderTexture(1280,900,24){antiAliasing=4}; var pixels=new Texture2D(1280,900,TextureFormat.RGB24,false); p.View.targetTexture=rt; p.View.Render(); RenderTexture.active=rt;
        pixels.ReadPixels(new Rect(0,0,1280,900),0,0); pixels.Apply(); File.WriteAllBytes("VisualCaptures/"+name+".png",pixels.EncodeToPNG()); RenderTexture.active=null; p.View.targetTexture=null; rt.Release(); Destroy(rt); Destroy(pixels);
    }
    static void Assert(bool ok,string message) { checks++; if(!ok) throw new Exception("Assertion "+checks+": "+message); }
    static void Near(Vector3 a,Vector3 b,float tolerance,string message) { Assert(Vector3.Distance(a,b)<=tolerance,message+" "+a+" vs "+b); }
}
#endif
