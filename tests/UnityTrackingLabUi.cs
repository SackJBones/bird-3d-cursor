#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;

// Explicit one-time authoring. Validation/build reopen the saved scene unchanged.
public static class UnityTrackingLabUi
{
    public const string StationName="Optional Bird reach station";
    public static void AddUi()
    {
        try
        {
            foreach(string name in new[]{"BirdAvatarUiInput","BirdLabScrollControl"})
            {
                string path="Assets/BirdWorld/Programs/"+name+".asset";
                var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/"+name+".cs");
                if(source==null) throw new Exception("Missing source "+name);
                var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); }
                else if(program.sourceCsScript!=source) throw new Exception("Unexpected program source "+name);
            }
            UnityTrackingLab.Compile();
            var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>()) if(go.scene.IsValid() && go.name==StationName) throw new Exception("Refusing to overwrite an authored reach station.");
            var root=new GameObject(StationName);
            var pointers=new BirdUiPointer[2];
            foreach(var input in UnityEngine.Object.FindObjectsOfType<BirdAvatarHandInput>())
            {
                int side=input.rightHand?1:0;
                var go=new GameObject((side==0?"Left":"Right")+" avatar UI bridge"); go.transform.SetParent(root.transform);
                var bridge=go.AddUdonSharpComponent<BirdAvatarUiInput>(); bridge.input=input;
                bridge.pointer=go.AddUdonSharpComponent<BirdUiPointer>(); pointers[side]=bridge.pointer;
                UdonSharpEditorUtility.CopyProxyToUdon(bridge.pointer); UdonSharpEditorUtility.CopyProxyToUdon(bridge);
            }
            if(pointers[0]==null || pointers[1]==null) throw new Exception("Both avatar inputs required.");
            var center=new Vector3(3.4f,1.65f,6.1f);
            var sphereGo=new GameObject("Lab reach sphere"); sphereGo.transform.SetParent(root.transform); sphereGo.transform.position=center;
            var sphere=sphereGo.AddComponent<SphereCollider>(); sphere.radius=.72f; sphere.isTrigger=true;
            var content=new GameObject("Lab reach colors").transform; content.SetParent(root.transform); content.position=center; content.rotation=Quaternion.Euler(15,20,0);
            var scroll=sphereGo.AddUdonSharpComponent<BirdUiSphericalScroll>(); scroll.sphere=sphere; scroll.rotationTarget=content; scroll.pointers=pointers; scroll.postLateUpdate=true;
            var elements=new BirdUiElement[12];
            for(int i=0;i<12;i++)
            {
                var color=Color.HSVToRGB(i/12f,.82f,1);
                var orb=Shape("Lab reach color "+i,center+content.rotation*Direction(i)*.36f,Vector3.one*.12f,
                    AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/Materials/UIColor"+i+".mat"),content,PrimitiveType.Sphere,true);
                var element=orb.AddUdonSharpComponent<BirdUiElement>(); elements[i]=element;
                element.target=orb.GetComponent<Collider>(); element.target.isTrigger=true; element.feedback=orb.GetComponent<Renderer>();
                element.normalColor=color; element.highlightColor=Color.Lerp(color,Color.white,.6f); element.pressedColor=Color.white;
                UdonSharpEditorUtility.CopyProxyToUdon(element);
            }
            var wire=AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/Materials/UiWire.mat");
            for(int plane=0;plane<3;plane++)
            {
                var line=new GameObject("Lab reach sphere guide").AddComponent<LineRenderer>(); line.transform.SetParent(sphereGo.transform,false);
                line.sharedMaterial=wire; line.useWorldSpace=false; line.loop=true; line.positionCount=96; line.startWidth=line.endWidth=.004f;
                for(int i=0;i<96;i++) { float a=i*Mathf.PI*2/96,x=Mathf.Cos(a)*sphere.radius,y=Mathf.Sin(a)*sphere.radius;line.SetPosition(i,plane==0?new Vector3(x,y,0):plane==1?new Vector3(x,0,y):new Vector3(0,x,y)); }
            }
            // Edges between adjacent face centers are just a visual scaffold.
            for(int i=0;i<12;i++) for(int j=i+1;j<12;j++) if(Vector3.Distance(Direction(i),Direction(j))<1.052f)
            {
                var line=new GameObject("Lab color guide edge").AddComponent<LineRenderer>(); line.transform.SetParent(content,false);
                line.sharedMaterial=wire; line.useWorldSpace=false; line.positionCount=2; line.startWidth=line.endWidth=.002f;
                line.SetPosition(0,Direction(i)*.36f);line.SetPosition(1,Direction(j)*.36f);
            }
            var router=new GameObject("Lab reach router").AddUdonSharpComponent<BirdUiRouter>(); router.transform.SetParent(root.transform);
            router.pointers=pointers; router.elements=elements; router.postLateUpdate=true;
            UdonSharpEditorUtility.CopyProxyToUdon(router); UdonSharpEditorUtility.CopyProxyToUdon(scroll);
            Board("Lab reach heading",new Vector3(3.4f,2.85f,6.0f),3.7f,.28f,40,"BIRD / REACH AND SPIN",root.transform);
            var status=Board("Lab reach status",new Vector3(3.4f,2.47f,6.0f),3.7f,.42f,28,"SET an open hand at the console first",root.transform);
            var reset=Shape("Lab reach reset",new Vector3(3.4f,.68f,5.9f),new Vector3(1.5f,.25f,.12f),Ink(),root.transform,PrimitiveType.Cube,true);
            Label("Lab reach reset label",reset.transform.position-Vector3.forward*.075f,1.45f,.23f,38,"RESET SPIN",root.transform);
            var control=reset.AddUdonSharpComponent<BirdLabScrollControl>(); control.scroll=scroll; control.left=pointers[0]; control.right=pointers[1]; control.status=status;
            UdonSharpEditorUtility.CopyProxyToUdon(control); var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(control); vm.interactText="Reset reach station rotation"; vm.proximity=12;
            var toggle=Shape("Lab reach toggle",new Vector3(2.25f,.9f,-.6f),new Vector3(1.35f,.30f,.12f),Ink(),null,PrimitiveType.Cube,true);
            var switcher=toggle.AddUdonSharpComponent<BirdLabToggle>();switcher.target=root;switcher.title="Reach station";switcher.initiallyEnabled=false;
            switcher.label=Label("Lab reach toggle label",new Vector3(2.25f,.9f,-.675f),1.3f,.28f,32,"Reach station / OFF",null);
            UdonSharpEditorUtility.CopyProxyToUdon(switcher);vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(switcher);vm.interactText="Toggle optional Bird reach station";vm.proximity=5;
            root.SetActive(false);
            ApplyLayout();
            foreach(var label in UnityEngine.Object.FindObjectsOfType<Text>(true)) if(label.transform.parent.name=="Welcome") label.text="BIRD / TRACKING LAB 06\nSphere geometry + optional reach interaction";
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();UnityTrackingLab.Finish("lab-ui-author",true,"Authored optional after-IK reach/highlight/spin station; native toggle defaults off, clicks remain disabled.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-ui-author",false,e.ToString()); }
    }
    public static void RefineUi()
    {
        try
        {
            UnityTrackingLab.Compile();var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);ApplyLayout();
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();UnityTrackingLab.Finish("lab-ui-layout",true,"Reach control clears existing diagnostic boards; station labels clear the wire sphere.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-ui-layout",false,e.ToString()); }
    }
    static void ApplyLayout()
    {
        var station=Find(StationName).transform;
        station.position=Vector3.zero;station.rotation=Quaternion.identity;
        Find("Lab reach toggle").transform.position=new Vector3(2.15f,.25f,-.6f);
        Find("Lab reach toggle").transform.localScale=new Vector3(1.05f,.30f,.12f);
        var toggle=Find("Lab reach toggle label");((RectTransform)toggle.transform).anchoredPosition3D=new Vector3(2.15f,.25f,-.675f);
        var label=toggle.GetComponentInChildren<Text>();label.rectTransform.sizeDelta=new Vector2(500,140);label.fontSize=30;
        var control=Find("Lab reach toggle").GetComponent<BirdLabToggle>();control.title="Reach station (right)";label.text="Reach station (right) / OFF";
        UdonSharpEditorUtility.CopyProxyToUdon(control);
        PlaceBoard("Lab reach heading",new Vector3(3.4f,3.02f,6),3,.28f,54);
        PlaceBoard("Lab reach status",new Vector3(3.4f,2.62f,6),3,.42f,36);
        // Face the spawn from the clear right-hand bay. The front diagnostic
        // boards otherwise hide most of the selector from the initial position.
        var rotation=Quaternion.LookRotation(new Vector3(4,0,1));
        station.position=new Vector3(4,1.65f,-2)-rotation*new Vector3(3.4f,1.65f,6.1f);
        station.rotation=rotation;
    }
    static void PlaceBoard(string name,Vector3 position,float width,float height,int size)
    {
        var canvas=Find(name);var rect=(RectTransform)canvas.transform;rect.anchoredPosition3D=rect.parent.InverseTransformPoint(position);
        var label=canvas.GetComponentInChildren<Text>();label.rectTransform.sizeDelta=new Vector2(width/.002f,height/.002f);label.fontSize=size;
        var backing=Find(name+" backing");backing.transform.position=position+Vector3.forward*.025f;backing.transform.localScale=new Vector3(width+.08f,height+.08f,.02f);
    }
    static GameObject Find(string name)
    {
        foreach(var go in Resources.FindObjectsOfTypeAll<GameObject>()) if(go.scene.IsValid() && go.name==name) return go;
        throw new Exception("Missing lab object "+name);
    }
    static Material Ink() { return AssetDatabase.LoadAssetAtPath<Material>("Assets/BirdWorld/TrackingLab/Ink.mat"); }
    static Vector3 Direction(int i)
    {
        float phi=(1+Mathf.Sqrt(5))*.5f,a=i%4<2?-1:1,b=i%2==0?-phi:phi;
        return (i<4?new Vector3(0,b,a):i<8?new Vector3(a,0,b):new Vector3(b,a,0)).normalized;
    }
    static GameObject Shape(string name,Vector3 p,Vector3 scale,Material material,Transform parent,PrimitiveType type,bool collider=false)
    {
        if(material==null) throw new Exception("Missing authored material for "+name);
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,true);go.transform.position=p;go.transform.localScale=scale;
        go.GetComponent<Renderer>().sharedMaterial=material;if(!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static Text Label(string name,Vector3 position,float width,float height,int fontSize,string text,Transform parent)
    {
        var canvas=new GameObject(name,typeof(RectTransform)).AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.transform.SetParent(parent,false);
        var rect=(RectTransform)canvas.transform;rect.anchoredPosition3D=parent==null?position:parent.InverseTransformPoint(position);rect.localScale=Vector3.one*.002f;
        var label=new GameObject("Text",typeof(RectTransform)).AddComponent<Text>();label.transform.SetParent(rect,false);label.rectTransform.sizeDelta=new Vector2(width/.002f,height/.002f);
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=fontSize;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;label.text=text;return label;
    }
    static Text Board(string name,Vector3 position,float width,float height,int fontSize,string text,Transform parent)
    {
        Shape(name+" backing",position+Vector3.forward*.025f,new Vector3(width+.08f,height+.08f,.02f),Ink(),parent,PrimitiveType.Cube);
        return Label(name,position,width,height,fontSize,text,parent);
    }
}
#endif
