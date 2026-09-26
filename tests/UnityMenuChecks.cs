#if UNITY_EDITOR
using System;
using System.IO;
using Bird3DCursor.UI;
using Bird3DCursor.Samples;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class UnityMenuChecks : MonoBehaviour
{
    const string Active = "Bird.Menu.Checks";
    int checks, captures;
    BirdMenuPreview demo;
    RenderTexture texture;
    Texture2D pixels;
    public static void Run()
    {
        File.WriteAllText("menu-result.txt","PENDING");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Bird Spherical Selector Preview").AddComponent<BirdSphericalSelectorPreview>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/SphericalSelector.unity");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var preview = new GameObject("Bird Menu Preview").AddComponent<BirdMenuPreview>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/MenuPreview.unity");
        preview.desktopInput = false;
        SessionState.SetBool(Active,true); EditorApplication.isPlaying = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Begin() { if (SessionState.GetBool(Active,false)) new GameObject("Menu checks").AddComponent<UnityMenuChecks>(); }

    void Start()
    {
        try
        {
            demo = FindObjectOfType<BirdMenuPreview>(); demo.Initialize();
            Directory.CreateDirectory("MenuCaptures");
            texture = new RenderTexture(1280,800,24) { antiAliasing = 4 };
            pixels = new Texture2D(1280,800,TextureFormat.RGB24,false);
            demo.View.targetTexture = texture;
            Require(!string.IsNullOrEmpty(typeof(BirdMenuPanel).Assembly.GetName().Name) && typeof(BirdMenuPanel).Assembly.GetName().Name=="Bird3D.UI","UI assembly boundary");
            Require(demo.Menu.State == BirdMenuPanel.PanelState.Closed,"Initially closed");
            Capture("closed");
            Sample(demo.Pointer,new Vector3(0,.35f,-.2f));
            Sample(demo.Pointer,new Vector3(0,1.05f,-.2f));
            Require(demo.Menu.State == BirdMenuPanel.PanelState.Open,"Upward sweep opens");
            Require(demo.Menu.Owner==demo.Pointer.UserId,"Opening claims user focus");
            Capture("open");
            Color idle = ControlPixel(demo.ColorButton);
            Aim(demo.Pointer,demo.ColorButton,false);
            Require(demo.ColorButton.State==BirdMenuVisualState.Highlighted,"Point-through highlights");
            Capture("highlight");
            Color hover = ControlPixel(demo.ColorButton);
            Require(hover.g>idle.g*1.5f && hover.b>idle.b*1.5f,"Rendered highlight must visibly change");
            Aim(demo.Pointer,demo.ColorButton,true);
            Require(demo.Colors.State==BirdMenuPanel.PanelState.Open && demo.Menu.State==BirdMenuPanel.PanelState.Background,"Select opens child and backgrounds parent");
            Require(demo.ColorButton.State==BirdMenuVisualState.Background,"Background visual state");
            Capture("submenu");
            demo.Interactor.Process();
            Aim(demo.Pointer,demo.CyanButton,true);
            Require(demo.SelectionCount==0,"Opening click/held press must not activate child");
            Aim(demo.Pointer,demo.CyanButton,false); Aim(demo.Pointer,demo.CyanButton,true);
            Require(demo.SelectionCount==1 && demo.ChosenColor.b>.9f,"Color UnityEvent executed");
            demo.Interactor.Process(); demo.Interactor.Process();
            Require(demo.SelectionCount==1,"Sample processed once");
            Require(demo.CyanButton.State==BirdMenuVisualState.Activated,"Held selection visual");
            Capture("selected");

            var otherHand = new GameObject("Other hand").AddComponent<BirdPointerInput>();
            var stranger = new GameObject("Other user").AddComponent<BirdPointerInput>(); stranger.SetUser("OtherUser");
            var all = demo.GetComponentsInChildren<BirdMenuElement>(true);
            demo.Interactor.Configure(new[]{demo.Pointer,otherHand,stranger},all);
            demo.Interactor.Process();
            Require(demo.SelectionCount==1,"Reconfiguring must not replay a press");
            Aim(otherHand,demo.AmberButton,false); Aim(otherHand,demo.AmberButton,true);
            Require(demo.SelectionCount==2 && demo.ChosenColor.r>.9f,"Same user's other hand shares focus");
            Aim(stranger,demo.CyanButton,false); Aim(stranger,demo.CyanButton,true);
            demo.Menu.Open(stranger);
            Require(demo.SelectionCount==2 && demo.Menu.Owner==demo.Pointer.UserId,"Other user cannot select or steal focus");
            demo.Pointer.Cancel(); Step();
            Require(demo.Menu.State!=BirdMenuPanel.PanelState.Closed,"One hand loss preserves other hand focus");
            otherHand.Cancel(); Step();
            Require(demo.Menu.State==BirdMenuPanel.PanelState.Closed && demo.Colors.State==BirdMenuPanel.PanelState.Closed,"Both hands lost closes branch");
            Require(demo.SelectionCount==2,"Loss must not activate an action"); Capture("loss");
            Sample(demo.Pointer,demo.ColorButton.transform.position+Vector3.forward,true);
            demo.Menu.Open(demo.Pointer); Step();
            Require(demo.Colors.State==BirdMenuPanel.PanelState.Closed,"Recovery-held press is not a new click");
            Aim(demo.Pointer,demo.ColorButton,false); Aim(demo.Pointer,demo.ColorButton,true);
            Aim(demo.Pointer,demo.BackButton,false); Aim(demo.Pointer,demo.BackButton,true);
            Require(demo.Colors.State==BirdMenuPanel.PanelState.Closed && demo.Menu.State==BirdMenuPanel.PanelState.Open,"Back restores parent");
            Capture("back");
            Aim(demo.Pointer,demo.CloseButton,false); Aim(demo.Pointer,demo.CloseButton,true);
            Require(demo.Menu.State==BirdMenuPanel.PanelState.Closed,"Close event closes root");
            Sample(demo.Pointer,new Vector3(0,1.05f,-.2f)); Sample(demo.Pointer,new Vector3(0,.35f,-.2f));
            Require(demo.Menu.State==BirdMenuPanel.PanelState.Closed,"Reverse gesture cannot open");
            Sample(demo.Pointer,new Vector3(0,1.05f,-.2f));
            Require(demo.Menu.State==BirdMenuPanel.PanelState.Open,"Open works after close");
            demo.Pointer.Submit(Vector3.zero,new Vector3(float.NaN,0,0),true,true); Step();
            Require(!demo.Pointer.IsTracked && demo.Menu.State==BirdMenuPanel.PanelState.Closed,"Invalid input cancels focus");

            demo.enabled=false;
            Require(!demo.View.gameObject.activeInHierarchy && !demo.Interactor.isActiveAndEnabled,"Disabling preview hides its generated camera and router");
            demo.enabled=true; Step();
            Require(demo.View.gameObject.activeInHierarchy && !demo.Pointer.IsTracked && demo.Menu.State==BirdMenuPanel.PanelState.Closed,"Preview enable recovers without stale input or focus");

            HitAndLifecycleChecks();
            InteractionBoundaryChecks();
            SerializationChecks();
            demo.enabled=false;
            string spherical=UnitySphericalScrollChecks.Run();
            string result="PASS: "+checks+" actual Unity menu assertions; "+captures+" camera captures; directional open, point-through highlight, selection, nested/back/close, two-hand user focus, loss/recovery, precise collider hits, distant reach, callback reentrancy and Inspector UnityEvent prefab roundtrip. GPU="+SystemInfo.graphicsDeviceType+". Synthetic input; not live-hand, Udon or headset validation.";
            result+=" "+spherical;
            File.WriteAllText("menu-result.txt",result); Debug.Log(result);
            SessionState.SetBool(Active,false); EditorApplication.Exit(0);
        }
        catch(Exception e)
        {
            File.WriteAllText("menu-result.txt","FAIL: "+e); Debug.LogException(e);
            SessionState.SetBool(Active,false); EditorApplication.Exit(1);
        }
    }

    void HitAndLifecycleChecks()
    {
        var go = new GameObject("Rotated point target"); go.transform.position=new Vector3(4,1,0); go.transform.rotation=Quaternion.Euler(0,0,45);
        var collider=go.AddComponent<BoxCollider>(); collider.size=new Vector3(1,.2f,.2f);
        var element=go.AddComponent<BirdMenuElement>(); element.Configure(collider,null,BirdMenuElement.Activation.SelectAtPoint);
        int actions=0; element.Activated.AddListener(p=>actions++);
        demo.Interactor.Configure(new[]{demo.Pointer},new[]{element});
        demo.Pointer.transform.position=new Vector3(99,99,99);
        Vector3 outside=new Vector3(4.38f,.62f,0), inside=new Vector3(4.2f,1.2f,0);
        Physics.SyncTransforms(); Require(collider.bounds.Contains(outside),"Rotated shape control is inside AABB");
        Sample(demo.Pointer,outside); Sample(demo.Pointer,outside,true);
        Require(actions==0,"AABB false positive rejected");
        Sample(demo.Pointer,inside); Sample(demo.Pointer,inside,true);
        Require(actions==1,"In-bounds selection uses logical point, not input component transform");

        go.transform.position=new Vector3(0,1,2000); go.transform.rotation=Quaternion.identity; collider.size=Vector3.one;
        element.Configure(collider,null,BirdMenuElement.Activation.SelectThrough);
        Sample(demo.Pointer,new Vector3(0,1,3000)); Sample(demo.Pointer,new Vector3(0,1,3000),true);
        Require(actions==2,"No 1000m interaction cap");
        element.enabled=false; Step(); Require(element.State==BirdMenuVisualState.Inactive,"Disabled clears feedback");
        element.enabled=true; Step(); Require(actions==2,"Enable must not repeat consumed press");
        demo.Interactor.enabled=false; demo.Interactor.enabled=true; Step();
        Require(actions==2,"Router enable must not repeat consumed press");
        Sample(demo.Pointer,new Vector3(0,1,3000));
        element.Activated.AddListener(p=>{ element.enabled=false; demo.Interactor.Process(); });
        Sample(demo.Pointer,new Vector3(0,1,3000),true);
        Require(actions==3 && element.State==BirdMenuVisualState.Inactive,"Reentrant Process/disable callback executes once");
        DestroyImmediate(go);

        var gateGo=new GameObject("Rotated directional gate"); gateGo.transform.position=new Vector3(6,1,0); gateGo.transform.rotation=Quaternion.Euler(0,0,90);
        var gateCollider=gateGo.AddComponent<BoxCollider>(); gateCollider.size=new Vector3(.6f,.1f,.2f);
        var gate=gateGo.AddComponent<BirdMenuElement>(); gate.Configure(gateCollider,null,BirdMenuElement.Activation.DirectionalPass);
        int passes=0; gate.Activated.AddListener(p=>passes++);
        demo.Interactor.Configure(new[]{demo.Pointer},new[]{gate});
        Sample(demo.Pointer,new Vector3(5.5f,1,0)); Sample(demo.Pointer,new Vector3(6.5f,1,0));
        Require(passes==0,"World-right fails rotated local-up gate");
        Sample(demo.Pointer,new Vector3(5.5f,1,0)); Require(passes==1,"Local direction follows rotation");
        demo.Pointer.Cancel(); Step(); Sample(demo.Pointer,new Vector3(6.5f,1,0));
        Require(passes==1,"Tracking recovery cannot invent a crossing");
        DestroyImmediate(gateGo);
    }

    void InteractionBoundaryChecks()
    {
        var root = new GameObject("Interaction boundary checks");
        var pointer = root.AddComponent<BirdPointerInput>();
        var target = new GameObject("Target"); target.transform.SetParent(root.transform,false); target.transform.position=Vector3.forward*3;
        var collider = target.AddComponent<BoxCollider>();
        var element = target.AddComponent<BirdMenuElement>();
        element.Configure(collider,null,BirdMenuElement.Activation.EnterThrough);
        int enters=0; element.Activated.AddListener(p=>enters++);
        demo.Interactor.Configure(new[]{pointer},new[]{element});
        Action<Vector3,bool> send=(point,pressed)=>{ pointer.Submit(Vector3.zero,point,true,pressed); Step(); };
        send(Vector3.forward,false);
        Require(element.State==BirdMenuVisualState.Enabled && enters==0,"Point before target does not highlight");
        send(Vector3.forward*4,false); send(Vector3.forward*5,false); Step();
        Require(enters==1,"Enter-through triggers once while passing through, without a click");
        send(Vector3.right*5,false); send(Vector3.forward*4,false);
        Require(enters==2,"Withdrawal and reentry starts a new enter-through action");
        pointer.Cancel(); Step(); send(Vector3.forward*4,true);
        Require(enters==2,"Recovery inside a target does not synthesize entry");
        pointer.enabled=false; send(Vector3.forward*4,true); pointer.enabled=true; Step();
        Require(!pointer.IsTracked && enters==2,"Disabled input cannot queue a tracked pose for reenabling");

        var meshCollider=target.AddComponent<MeshCollider>();
        element.Configure(meshCollider,null,BirdMenuElement.Activation.SelectThrough);
        send(Vector3.forward*4,false); send(Vector3.forward*4,true);
        Require(element.State==BirdMenuVisualState.Inactive && enters==2,"Unsupported hit volume fails closed");
        element.Configure(collider,null,BirdMenuElement.Activation.SelectThrough);
        element.Activated.RemoveAllListeners();

        var farther=new GameObject("Competing target"); farther.transform.SetParent(root.transform,false); farther.transform.position=Vector3.forward*5;
        var farCollider=farther.AddComponent<BoxCollider>();
        var farElement=farther.AddComponent<BirdMenuElement>(); farElement.Configure(farCollider,null,BirdMenuElement.Activation.SelectThrough);
        int nearActions=0,farActions=0;
        element.Activated.AddListener(p=>nearActions++); farElement.Activated.AddListener(p=>farActions++);
        demo.Interactor.Configure(new[]{pointer},new[]{farElement,element});
        send(Vector3.forward*7,false); send(Vector3.forward*7,true);
        Require(nearActions==1 && farActions==0,"Nearest eligible target wins regardless of array order");
        farElement.Configure(farCollider,null,BirdMenuElement.Activation.SelectThrough,actionPriority:1);
        send(Vector3.forward*7,false); send(Vector3.forward*7,true);
        Require(nearActions==1 && farActions==1,"Explicit priority can override distance");

        var panel=root.AddComponent<BirdMenuPanel>();
        var content=new GameObject("Content"); content.transform.SetParent(root.transform,false); panel.Configure(content);
        var child=farther.AddComponent<BirdMenuPanel>();
        var childContent=new GameObject("Child content"); childContent.transform.SetParent(farther.transform,false); child.Configure(childContent,panel);
        element.Configure(collider,panel,BirdMenuElement.Activation.SelectThrough,true);
        farElement.Configure(farCollider,child,BirdMenuElement.Activation.SelectThrough);
        panel.Open(pointer); child.Open(pointer);
        send(Vector3.forward*4,false); send(Vector3.forward*4,true);
        Require(panel.State==BirdMenuPanel.PanelState.Background && nearActions==2,"Explicit background control stays interactive");
        demo.Interactor.Configure(new[]{pointer},new[]{farElement});
        pointer.Cancel(); Step();
        Require(panel.State==BirdMenuPanel.PanelState.Closed && child.State==BirdMenuPanel.PanelState.Closed,"Loss closes root even if only child controls are registered");

        var other=new GameObject("Second input").AddComponent<BirdPointerInput>(); other.transform.SetParent(root.transform,false);
        target.transform.position=new Vector3(-1,0,3); farther.transform.position=new Vector3(1,0,3);
        element.Activated.RemoveAllListeners(); element.Activated.AddListener(p=>panel.Close());
        demo.Interactor.Configure(new[]{pointer,other},new[]{element,farElement});
        pointer.Submit(Vector3.zero,target.transform.position*2,true,false);
        other.Submit(Vector3.zero,farther.transform.position*2,true,false); Step();
        panel.Open(pointer); child.Open(pointer);
        pointer.Submit(Vector3.zero,target.transform.position*2,true,true);
        other.Submit(Vector3.zero,farther.transform.position*2,true,true); Step();
        Require(panel.State==BirdMenuPanel.PanelState.Closed && farActions==1,"Closing from one hand cancels the other hand's queued child action");

        panel.Open(pointer); child.Open(pointer); pointer.SetUser("Replacement"); other.Cancel(); Step();
        Require(panel.State==BirdMenuPanel.PanelState.Closed,"Changing user cancels previous ownership");

        send(Vector3.forward,false); panel.Open(pointer); child.Open(pointer);
        var siblingGo=new GameObject("Requested sibling"); siblingGo.transform.SetParent(root.transform,false);
        var sibling=siblingGo.AddComponent<BirdMenuPanel>(); sibling.Configure(null,panel);
        var reentrantGo=new GameObject("Reentrant sibling"); reentrantGo.transform.SetParent(root.transform,false);
        var reentrant=reentrantGo.AddComponent<BirdMenuPanel>(); reentrant.Configure(null,panel);
        child.Closed.AddListener(()=>reentrant.Open(pointer));
        sibling.Open(pointer);
        Require(sibling.State==BirdMenuPanel.PanelState.Open && child.State==BirdMenuPanel.PanelState.Closed && reentrant.State==BirdMenuPanel.PanelState.Closed,"Sibling replacement cannot leave an orphan branch from a close callback");
        panel.Close();

        var visual=GameObject.CreatePrimitive(PrimitiveType.Cube); visual.transform.SetParent(root.transform,false);
        var renderer=visual.GetComponent<Renderer>();
        var material=new Material(Shader.Find("Unlit/Color")); renderer.sharedMaterial=material;
        var original=new MaterialPropertyBlock(); original.SetColor("_Color",Color.magenta); original.SetFloat("_OtherOwner",7); renderer.SetPropertyBlock(original);
        var feedback=visual.AddComponent<BirdMenuFeedback>(); feedback.Configure(element,renderer); feedback.Apply(1);
        var observed=new MaterialPropertyBlock(); renderer.GetPropertyBlock(observed);
        Require(observed.GetColor("_Color")!=Color.magenta && observed.GetFloat("_OtherOwner")==7,"Feedback preserves unrelated property values");
        feedback.enabled=false; renderer.GetPropertyBlock(observed);
        Require(observed.GetColor("_Color")==Color.magenta && renderer.sharedMaterial==material,"Disabling feedback restores original block without material mutation");
        DestroyImmediate(root); DestroyImmediate(material);
    }

    void SerializationChecks()
    {
        var root=new GameObject("Serialized menu fixture");
        var panel=root.AddComponent<BirdMenuPanel>();
        var content=new GameObject("Content"); content.transform.SetParent(root.transform,false); panel.Configure(content);
        var button=new GameObject("Open"); button.transform.SetParent(root.transform,false);
        var collider=button.AddComponent<BoxCollider>();
        var element=button.AddComponent<BirdMenuElement>(); element.Configure(collider,null,BirdMenuElement.Activation.SelectThrough);
        UnityEventTools.AddPersistentListener(element.Activated,panel.Open);
        var asset=PrefabUtility.SaveAsPrefabAsset(root,"Assets/MenuEventRoundtrip.prefab");
        Require(asset!=null,"Prefab authored"); DestroyImmediate(root);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
        var copy=instance.GetComponentInChildren<BirdMenuElement>(); var copyPanel=instance.GetComponent<BirdMenuPanel>();
        Require(copy.Activated.GetPersistentEventCount()==1,"Inspector action persisted");
        demo.Interactor.Configure(new[]{demo.Pointer},new[]{copy});
        Sample(demo.Pointer,new Vector3(0,0,1)); Sample(demo.Pointer,new Vector3(0,0,1),true);
        Require(copyPanel.State==BirdMenuPanel.PanelState.Open,"Deserialized event opens deserialized panel");
        int closes=0; copyPanel.Closed.AddListener(()=>{ closes++; copyPanel.Close(); });
        copyPanel.Close(); Require(closes==1,"Reentrant close is idempotent");
        copyPanel.Opened.AddListener(copyPanel.Close);
        copyPanel.Open(demo.Pointer);
        Require(copyPanel.State==BirdMenuPanel.PanelState.Closed && closes==2,"Open listener can close without state corruption");
        DestroyImmediate(instance);
    }

    void Aim(BirdPointerInput pointer,BirdMenuElement target,bool pressed)
    {
        Vector3 delta=target.transform.position-demo.View.transform.position;
        Sample(pointer,demo.View.transform.position+delta.normalized*(delta.magnitude+1),pressed);
    }
    void Sample(BirdPointerInput pointer,Vector3 point,bool pressed=false) { pointer.Submit(demo.View.transform.position,point,true,pressed); Step(); }
    void Step() { Physics.SyncTransforms(); demo.Interactor.Process(); }
    void Require(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
    void Capture(string name)
    {
        foreach(var feedback in demo.GetComponentsInChildren<BirdMenuFeedback>()) feedback.Apply(1);
        demo.View.Render(); RenderTexture.active=texture;
        pixels.ReadPixels(new Rect(0,0,1280,800),0,0); pixels.Apply();
        File.WriteAllBytes("MenuCaptures/"+name+".png",pixels.EncodeToPNG()); captures++;
    }
    Color ControlPixel(BirdMenuElement element)
    {
        Vector3 v=demo.View.WorldToViewportPoint(element.transform.position+Vector3.up*.17f-Vector3.forward*.062f);
        return pixels.GetPixel(Mathf.RoundToInt(v.x*1280),Mathf.RoundToInt(v.y*800));
    }
}
#endif
