#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UdonSharp;
using UdonSharpEditor;

// One-time authored lab extension. Reproduction/build uses the saved scene.
public static class UnityTrackingLabBird
{
    const string Folder="Assets/BirdWorld/TrackingLab/";
    public static void AddBird()
    {
        try
        {
            foreach(string name in new[]{"BirdAvatarHandInput","BirdLabHandControl","BirdLabPointView","BirdLabPointTarget"})
            {
                string path="Assets/BirdWorld/Programs/"+name+".asset";
                var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/"+name+".cs");
                if(source==null) throw new Exception("Missing source "+name);
                var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
                if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); }
                else if(program.sourceCsScript!=source) throw new Exception("Program source mismatch "+name);
            }
            UnityTrackingLab.Compile();
            var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            if(UnityEngine.Object.FindObjectOfType<BirdAvatarHandInput>(true)!=null) throw new Exception("Refusing to overwrite authored Bird integration");
            var root=GameObject.Find("Bird integration / future local input and presentation");
            if(root==null) root=new GameObject("Bird integration / avatar approximation");
            root.name="Bird integration / avatar approximation";
            var shader=Shader.Find("Bird/LogicalDepth"); if(shader==null) throw new Exception("Restore logical-depth shader");
            var point=Material("BirdPoint",shader,Color.white); point.renderQueue=2501; point.SetFloat("_ZWrite",1);
            var halo=Material("BirdHalo",shader,Color.white); halo.SetFloat("_UseVertexColor",1);
            var tips=Material("EstimatedTips",Shader.Find("Unlit/Color"),Color.white);
            var targetMaterial=Material("PointTarget",Shader.Find("Unlit/Color"),Color.gray);
            var inputs=new BirdAvatarHandInput[2];
            for(int side=0;side<2;side++)
            {
                string hand=side==0?"Left":"Right";
                var go=new GameObject(hand+" avatar Bird input"); go.transform.SetParent(root.transform);
                var input=go.AddUdonSharpComponent<BirdAvatarHandInput>(); inputs[side]=input; input.rightHand=side==1;
                input.cursor=new GameObject(hand+" Bird solver").AddUdonSharpComponent<BirdCursorState>(); input.cursor.transform.SetParent(root.transform);
                input.cursor.fitter=new GameObject(hand+" sphere fit").AddUdonSharpComponent<BirdSphereFit>(); input.cursor.fitter.transform.SetParent(root.transform);
                input.cursor.smoothing=true; input.cursor.clicksAllowed=false; input.cursor.useHandLimits=true;
                input.status=Board(hand+" Bird status",new Vector3(-3.35f,side==0?2.2f:1.45f,-1.2f),2.7f,.65f,40,hand+" / Open hand, then SET with the other hand");
                var view=new GameObject(hand+" optional Bird presentation").AddUdonSharpComponent<BirdLabPointView>(); view.transform.SetParent(root.transform); view.input=input;
                view.tint=side==0?Color.cyan:new Color(1,.25f,.6f);
                var core=Primitive(hand+" Bird cursor",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.032f,point,root.transform);
                view.core=core.GetComponent<Renderer>(); view.core.enabled=false;
                var line=new GameObject(hand+" far locator").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform); line.sharedMaterial=halo;
                line.useWorldSpace=true; line.loop=true; line.positionCount=32; line.enabled=false; view.halo=line;
                view.tipMarkers=new Transform[5];
                for(int i=0;i<5;i++)
                { var marker=Primitive(hand+" estimated tip "+i,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.007f,tips,root.transform); marker.SetActive(false); view.tipMarkers[i]=marker.transform; }
                UdonSharpEditorUtility.CopyProxyToUdon(input.cursor); UdonSharpEditorUtility.CopyProxyToUdon(input); UdonSharpEditorUtility.CopyProxyToUdon(view);
                for(int reset=0;reset<2;reset++)
                {
                    string name=(reset==0?"SET ":"RESET ")+hand.ToUpperInvariant();
                    var position=new Vector3(side==0?-3.95f:-2.75f,reset==0?.92f:.60f,-1.3f);
                    var button=Primitive(name,PrimitiveType.Cube,position,new Vector3(1.05f,.24f,.12f),AssetDatabase.LoadAssetAtPath<Material>(Folder+"Ink.mat"),null,true);
                    var control=button.AddUdonSharpComponent<BirdLabHandControl>(); control.input=input; control.reset=reset==1;
                    Label(name+" label",position-Vector3.forward*.075f,1,.21f,44,name);
                    UdonSharpEditorUtility.CopyProxyToUdon(control); var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(control); vm.interactText=name; vm.proximity=5;
                }
            }
            var targetGo=Primitive("Bird point-through target",PrimitiveType.Sphere,new Vector3(1.4f,1.35f,-.3f),Vector3.one*.24f,targetMaterial,root.transform);
            var target=targetGo.AddUdonSharpComponent<BirdLabPointTarget>(); target.artwork=targetGo.GetComponent<Renderer>(); target.left=inputs[0]; target.right=inputs[1];
            UdonSharpEditorUtility.CopyProxyToUdon(target);
            Board("Bird target hint",new Vector3(1.4f,1.1f,-.3f),1.7f,.25f,28,"Point through: green / no click needed");
            FaceCalibrationConsole();
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
            {
                if(text.transform.parent.name=="Directions") text.text="Open one hand. Use the other to SET LEFT or SET RIGHT.\nWhite dots are estimated fingertips; colored dots are avatar bones.\nBird uses this approximation. Clicking is not enabled yet.";
                if(text.transform.parent.name=="Welcome") text.text="BIRD / TRACKING LAB 03\nAvatar geometry, estimated tips, live Bird point";
            }
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); UnityTrackingLab.Finish("lab-bird-author",true,"Authored explicit avatar calibration, separate long-range point presentation and logical point-through target.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-bird-author",false,e.ToString()); }
    }
    public static void RefineBird()
    {
        try
        {
            var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            GameObject.Find("Bird point-through target").transform.position=new Vector3(1.4f,1.35f,-.3f);
            var hint=GameObject.Find("Bird target hint"); hint.transform.position=new Vector3(1.4f,1.1f,-.3f);
            var text=hint.GetComponentInChildren<Text>(); text.rectTransform.sizeDelta=new Vector2(850,125); text.fontSize=28;
            var backing=GameObject.Find("Bird target hint backing"); backing.transform.position=hint.transform.position+Vector3.forward*.025f; backing.transform.localScale=new Vector3(1.78f,.33f,.02f);
            FaceCalibrationConsole();
            foreach(var control in UnityEngine.Object.FindObjectsOfType<BirdLabHandControl>()) UdonSharpEditorUtility.GetBackingUdonBehaviour(control).proximity=5;
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); UnityTrackingLab.Finish("lab-bird-layout",true,"Calibration console faces the spawn and target clears instructions.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-bird-layout",false,e.ToString()); }
    }
    static void FaceCalibrationConsole()
    {
        if(GameObject.Find("Bird calibration console")!=null) return;
        var console=new GameObject("Bird calibration console"); console.transform.position=new Vector3(-3.35f,0,-1.2f);
        foreach(string hand in new[]{"Left","Right"})
        {
            foreach(string name in new[]{hand+" Bird status",hand+" Bird status backing","SET "+hand.ToUpperInvariant(),"SET "+hand.ToUpperInvariant()+" label","RESET "+hand.ToUpperInvariant(),"RESET "+hand.ToUpperInvariant()+" label"})
                GameObject.Find(name).transform.SetParent(console.transform,true);
        }
        console.transform.rotation=Quaternion.Euler(0,-50,0);
    }
    static Material Material(string name,Shader shader,Color color)
    { var m=new Material(shader){color=color}; AssetDatabase.CreateAsset(m,Folder+name+".mat"); return m; }
    static GameObject Primitive(string name,PrimitiveType type,Vector3 position,Vector3 size,Material material,Transform parent,bool collider=false)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent); go.transform.position=position; go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material; if(!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static Text Label(string name,Vector3 position,float width,float height,int fontSize,string value)
    {
        var canvas=new GameObject(name,typeof(RectTransform)).AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; canvas.transform.position=position; canvas.transform.localScale=Vector3.one*.002f;
        var label=new GameObject("Text",typeof(RectTransform)).AddComponent<Text>(); label.transform.SetParent(canvas.transform,false); label.rectTransform.sizeDelta=new Vector2(width/.002f,height/.002f);
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=fontSize; label.alignment=TextAnchor.MiddleCenter; label.raycastTarget=false; label.text=value; return label;
    }
    static Text Board(string name,Vector3 position,float width,float height,int fontSize,string value)
    {
        Primitive(name+" backing",PrimitiveType.Cube,position+Vector3.forward*.025f,new Vector3(width+.08f,height+.08f,.02f),AssetDatabase.LoadAssetAtPath<Material>(Folder+"Ink.mat"),null);
        return Label(name,position,width,height,fontSize,value);
    }
}
#endif
