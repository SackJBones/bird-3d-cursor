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
    static void EnsurePrograms()
    {
        foreach(string name in new[]{"BirdAvatarHandInput","BirdLabHandControl","BirdLabPointView","BirdLabPointTarget","BirdLabFilterControl","BirdLabGeometryView","BirdRangeAdaptiveFilter","BirdSphereSpaceFilter","BirdLabRootControl"})
        {
            string path="Assets/BirdWorld/Programs/"+name+".asset";
            var source=AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/BirdGenerated/Runtime/"+name+".cs");
            if(source==null) throw new Exception("Missing source "+name);
            var program=AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path);
            if(program==null) { program=ScriptableObject.CreateInstance<UdonSharpProgramAsset>(); program.sourceCsScript=source; AssetDatabase.CreateAsset(program,path); }
            else if(program.sourceCsScript!=source) throw new Exception("Program source mismatch "+name);
        }
        UnityTrackingLab.Compile();
    }
    public static void AddBird()
    {
        try
        {
            EnsurePrograms();
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
                input.cursor.smoothing=false; input.cursor.clicksAllowed=false; input.cursor.useHandLimits=true;
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
            AddPointDiagnostics();
            AddFilterControl();
            AddGeometryView();
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); UnityTrackingLab.Finish("lab-bird-author",true,"Authored explicit avatar calibration, separate long-range point presentation and logical point-through target.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-bird-author",false,e.ToString()); }
    }
    public static void RefineBird()
    {
        try
        {
            EnsurePrograms();
            var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            FaceCalibrationConsole();
            AddPointDiagnostics();
            AddFilterControl();
            AddGeometryView();
            foreach(var control in UnityEngine.Object.FindObjectsOfType<BirdLabHandControl>()) UdonSharpEditorUtility.GetBackingUdonBehaviour(control).proximity=5;
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); UnityTrackingLab.Finish("lab-bird-layout",true,"Calibration console faces the spawn and target clears instructions.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-bird-layout",false,e.ToString()); }
    }
    public static void AddAdaptive()
    {
        try
        {
            EnsurePrograms();
            var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            var control=UnityEngine.Object.FindObjectOfType<BirdLabFilterControl>();
            if(control==null || control.cursors==null || control.cursors.Length!=2) throw new Exception("Restore the authored two-hand lab first");
            if(UnityEngine.Object.FindObjectsOfType<BirdRangeAdaptiveFilter>(true).Length!=0) throw new Exception("Refusing to overwrite an authored filter experiment");
            control.adaptiveFilters=new BirdRangeAdaptiveFilter[2]; control.filtered=control.adaptive=false;
            for(int side=0;side<2;side++)
            {
                var go=new GameObject((side==0?"Left":"Right")+" optional adaptive filter");
                go.transform.SetParent(control.cursors[side].transform);
                var policy=go.AddUdonSharpComponent<BirdRangeAdaptiveFilter>(); control.adaptiveFilters[side]=policy;
                control.cursors[side].smoothing=false; control.cursors[side].adaptiveFilter=null;
                UdonSharpEditorUtility.CopyProxyToUdon(policy); UdonSharpEditorUtility.CopyProxyToUdon(control.cursors[side]);
            }
            control.label.text="Point / RAW\nPress for FILTERED"; UdonSharpEditorUtility.CopyProxyToUdon(control);
            UdonSharpEditorUtility.GetBackingUdonBehaviour(control).interactText="Compare raw / original filter / adaptive filter";
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
                if(text.transform.parent.name=="Welcome") text.text="BIRD / TRACKING LAB 07\nSphere geometry + optional filter comparison";
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            UnityTrackingLab.Finish("lab-filter-author",true,"Added independent per-hand adaptive policies, opt-in through the existing native control; RAW default retained.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-filter-author",false,e.ToString()); }
    }
    public static void AddSphereFilter()
    {
        try
        {
            EnsurePrograms();
            var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            var control=UnityEngine.Object.FindObjectOfType<BirdLabFilterControl>();
            if(control==null || control.cursors==null || control.cursors.Length!=2) throw new Exception("Restore the authored two-hand lab first");
            if(UnityEngine.Object.FindObjectsOfType<BirdSphereSpaceFilter>(true).Length!=0) throw new Exception("Refusing to overwrite an authored sphere filter");
            control.sphereFilters=new BirdSphereSpaceFilter[2]; control.filtered=control.adaptive=control.sphere=false;
            for(int side=0;side<2;side++)
            {
                var go=new GameObject((side==0?"Left":"Right")+" optional sphere filter");
                go.transform.SetParent(control.cursors[side].transform);
                var policy=go.AddUdonSharpComponent<BirdSphereSpaceFilter>(); control.sphereFilters[side]=policy;
                control.cursors[side].smoothing=false; control.cursors[side].adaptiveFilter=null; control.cursors[side].sphereFilter=null;
                UdonSharpEditorUtility.CopyProxyToUdon(policy); UdonSharpEditorUtility.CopyProxyToUdon(control.cursors[side]);
            }
            control.label.text="Point / RAW\nPress for FILTERED"; UdonSharpEditorUtility.CopyProxyToUdon(control);
            UdonSharpEditorUtility.GetBackingUdonBehaviour(control).interactText="Compare raw / original / adaptive / sphere filtering";
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
                if(text.transform.parent.name=="Welcome") text.text="BIRD / TRACKING LAB 08\nSphere geometry + optional filter comparison";
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            UnityTrackingLab.Finish("lab-sphere-filter-author",true,"Added per-hand sphere-vector policies; original range law and RAW default retained.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-sphere-filter-author",false,e.ToString()); }
    }
    public static void RefinePalmDirection()
    {
        try
        {
            EnsurePrograms();
            var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            var geometries=UnityEngine.Object.FindObjectsOfType<BirdLabGeometryView>(true);
            if(geometries.Length!=2) throw new Exception("Restore the authored two-hand lab first");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"XRayTips.mat");
            if(material==null) throw new Exception("Restore the existing white X-ray diagnostic material");
            foreach(var geometry in geometries)
            {
                // The user's later predictability requirement supersedes the
                // earlier experimental knuckleward tilt for this avatar lab.
                geometry.input.cursor.flatDirectionDegrees=0;
                UdonSharpEditorUtility.CopyProxyToUdon(geometry.input.cursor);
                if(geometry.rootMarker==null)
                    geometry.rootMarker=DiagnosticLine((geometry.input.rightHand?"Right":"Left")+" Bird ray origin",geometry.transform,material,5,false,.0015f);
                UdonSharpEditorUtility.CopyProxyToUdon(geometry);
            }
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
            {
                if(text.transform.parent.name=="Welcome") text.text="BIRD / TRACKING LAB 09\nPalm direction + visible ray origin";
                if(text.transform.parent.name=="Directions") text.text="SET LEFT / RIGHT: hold that hand straight; press with the other. Starts RAW.\nWhite cross = ray origin. Gold = actual fitted sphere, center and ray.\nGreen = palm normal. Cyan / pink = resulting Bird. Toggle Geometry below.\nFlat-hand limit follows the palm. White fingertip dots are estimates.";
            }
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            UnityTrackingLab.Finish("lab-palm-direction-author",true,"Palm-normal far direction, unchanged reference root and visible white root crosses.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-palm-direction-author",false,e.ToString()); }
    }
    public static void AddRootControl()
    {
        try
        {
            EnsurePrograms();var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            if(UnityEngine.Object.FindObjectOfType<BirdLabRootControl>(true)!=null) throw new Exception("Refusing to overwrite authored root comparison");
            var inputs=UnityEngine.Object.FindObjectsOfType<BirdAvatarHandInput>(true);
            if(inputs.Length!=2) throw new Exception("Restore the authored two-hand lab first");
            var position=new Vector3(0,.53f,-.6f);
            // Use the clear console center, between the existing point/geometry rows.
            var button=Primitive("Bird origin control",PrimitiveType.Cube,position,new Vector3(.85f,.24f,.12f),AssetDatabase.LoadAssetAtPath<Material>(Folder+"Ink.mat"),null,true);
            var control=button.AddUdonSharpComponent<BirdLabRootControl>();control.inputs=inputs;control.centered=true;
            control.label=Label("Bird origin label",position-Vector3.forward*.075f,.8f,.22f,30,"Origin / PALM\nPress for CLASSIC");
            foreach(var input in inputs)
            { input.littleFingerRootShare=.5f; UdonSharpEditorUtility.CopyProxyToUdon(input); }
            UdonSharpEditorUtility.CopyProxyToUdon(control);
            var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(control);vm.interactText="Compare palm / classic Bird ray origin";vm.proximity=5;
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
            {
                if(text.transform.parent.name=="Welcome") text.text="BIRD / TRACKING LAB 10\nPalm direction + adjustable ray origin";
                if(text.transform.parent.name=="Directions") text.text="SET LEFT / RIGHT: hold that hand straight; press with the other. Starts RAW.\nOrigin / PALM includes pinky knuckle. CLASSIC uses index + thumb only.\nWhite cross = origin. Gold = sphere / fit ray. Green = palm normal.\nCyan / pink = resulting Bird. Use Point / SPHERE to compare smoothing.";
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();UnityTrackingLab.Finish("lab-root-author",true,"Palm origin 30% index / 30% pinky / 40% thumb; native classic comparison preserves calibration.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-root-author",false,e.ToString()); }
    }
    public static void RefineSphereDirection()
    {
        try
        {
            EnsurePrograms();var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            var inputs=UnityEngine.Object.FindObjectsOfType<BirdAvatarHandInput>(true);
            if(inputs.Length!=2) throw new Exception("Restore two authored hands first");
            foreach(var input in inputs)
            {
                input.littleFingerRootShare=.5f;
                input.cursor.useSphereDirection=true;input.cursor.flatDirectionDegrees=45;input.cursor.insideOutFullBlend=.25f;
                UdonSharpEditorUtility.CopyProxyToUdon(input);UdonSharpEditorUtility.CopyProxyToUdon(input.cursor);
            }
            var filter=UnityEngine.Object.FindObjectOfType<BirdLabFilterControl>(true);
            var origin=UnityEngine.Object.FindObjectOfType<BirdLabRootControl>(true);
            if(filter==null || origin==null) throw new Exception("Restore the lab policy components first");
            filter.filtered=filter.adaptive=true;filter.sphere=false;origin.centered=true;
            foreach(var input in inputs)
            {
                int side=input.rightHand?1:0;
                input.cursor.smoothing=true;input.cursor.adaptiveFilter=filter.adaptiveFilters[side];input.cursor.sphereFilter=null;
                UdonSharpEditorUtility.CopyProxyToUdon(input.cursor);
            }
            if(filter.label!=null) UnityEngine.Object.DestroyImmediate(filter.label.transform.parent.gameObject);
            if(origin.label!=null) UnityEngine.Object.DestroyImmediate(origin.label.transform.parent.gameObject);
            filter.label=null;origin.label=null;
            foreach(var go in new[]{filter.gameObject,origin.gameObject})
            {
                foreach(var collider in go.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                foreach(var renderer in go.GetComponents<Renderer>()) UnityEngine.Object.DestroyImmediate(renderer);
                foreach(var mesh in go.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(mesh);
            }
            filter.gameObject.name="Bird point settings";origin.gameObject.name="Bird origin settings";
            UdonSharpEditorUtility.CopyProxyToUdon(filter);UdonSharpEditorUtility.CopyProxyToUdon(origin);
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
            {
                if(text.transform.parent.name=="Welcome") text.text="BIRD / TRACKING LAB 12\nSphere-directed aim + palm origin";
                if(text.transform.parent.name=="Directions") text.text="SET LEFT / RIGHT: hold that hand straight; press with the other.\nAdaptive smoothing and palm origin are on by default.\nWhite cross = origin. Gold = sphere / fit ray. Green = palm normal.\nBird follows the sphere center; inside-out fits blend to a safe direction.";
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            UnityTrackingLab.Finish("lab-center-direction-author",true,"Sphere-center aim independent of range; behind-palm correction only; ADAPTIVE/PALM defaults; both comparison buttons removed.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-center-direction-author",false,e.ToString()); }
    }
    public static void AddAutomaticSetup()
    {
        try
        {
            EnsurePrograms();var scene=EditorSceneManager.OpenScene(UnityTrackingLab.ScenePath);
            var inputs=UnityEngine.Object.FindObjectsOfType<BirdAvatarHandInput>(true);
            if(inputs.Length!=2) throw new Exception("Restore two authored hands first");
            foreach(var input in inputs)
            {
                input.automaticSetup=true;input.estimatedDistalBendRatio=.7f;
                UdonSharpEditorUtility.CopyProxyToUdon(input);
                string hand=input.rightHand?"RIGHT":"LEFT";
                GameObject.Find("SET "+hand+" label").GetComponentInChildren<Text>().text="REFINE "+hand+"\nOptional";
                GameObject.Find("RESET "+hand+" label").GetComponentInChildren<Text>().text="AUTO "+hand;
                UdonSharpEditorUtility.GetBackingUdonBehaviour(GameObject.Find("SET "+hand).GetComponent<BirdLabHandControl>()).interactText="Optional straight-hand fingertip correction";
                UdonSharpEditorUtility.GetBackingUdonBehaviour(GameObject.Find("RESET "+hand).GetComponent<BirdLabHandControl>()).interactText="Return to automatic fingertip estimates";
            }
            foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
            {
                if(text.transform.parent.name=="Welcome") text.text="BIRD / TRACKING LAB 13\nAutomatic fingertips + sphere-directed aim";
                if(text.transform.parent.name=="Directions") text.text="Bird starts automatically. Adaptive smoothing and palm origin are on.\nWhite cross = origin. Gold = sphere / fit ray. Green = palm normal.\nWhite fingertip dots are estimates; finger axes learn as you move.\nOptional REFINE: straighten one hand. AUTO returns to automatic estimates.";
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            UnityTrackingLab.Finish("lab-auto-setup-author",true,"Automatic fingertip startup; per-finger passive axis learning; optional REFINE/AUTO controls; accepted geometry/filter defaults retained.");
        }
        catch(Exception e) { UnityTrackingLab.Finish("lab-auto-setup-author",false,e.ToString()); }
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
    static void AddPointDiagnostics()
    {
        var xray=Shader.Find("Bird/Lab X-ray joints"); if(xray==null) throw new Exception("Restore lab X-ray shader");
        var probe=UnityEngine.Object.FindObjectOfType<BirdHandDataProbe>();
        var diagnostic=new Material[3];
        for(int i=0;i<3;i++)
        {
            string name=i==0?"LeftBones":i==1?"RightBones":"XRayTips";
            diagnostic[i]=AssetDatabase.LoadAssetAtPath<Material>(Folder+name+".mat");
            if(diagnostic[i]==null) diagnostic[i]=Material(name,xray,i==0?Color.cyan:i==1?new Color(1,.25f,.6f):Color.white);
        }
        for(int i=0;i<probe.markers.Length;i++) probe.markers[i].GetComponent<Renderer>().sharedMaterial=diagnostic[i<16?0:1];
        foreach(var input in UnityEngine.Object.FindObjectsOfType<BirdAvatarHandInput>()) input.status.fontSize=32;
        foreach(var view in UnityEngine.Object.FindObjectsOfType<BirdLabPointView>())
        {
            string name=view.input.rightHand?"Right":"Left";
            string path=Folder+name+"Direction.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null) mat=Material(name+"Direction",Shader.Find("Unlit/Color"),view.tint);
            if(view.directionGuide==null)
            {
                view.directionGuide=new GameObject(name+" Bird direction guide").AddComponent<LineRenderer>();
                view.directionGuide.transform.SetParent(view.transform); view.directionGuide.positionCount=2;
                view.directionGuide.useWorldSpace=true; view.directionGuide.startWidth=view.directionGuide.endWidth=.003f;
                view.directionGuide.enabled=false;
            }
            view.directionGuide.sharedMaterial=mat; UdonSharpEditorUtility.CopyProxyToUdon(view);
            foreach(var marker in view.tipMarkers) marker.GetComponent<Renderer>().sharedMaterial=diagnostic[2];
        }
        // Same point shader at a known ordinary distance, independent of live hand data.
        // The adjacent ordinary material distinguishes shader failure from missing geometry.
        var parent=GameObject.Find("Bird integration / avatar approximation").transform;
        if(GameObject.Find("Bird material reference")==null)
        {
            Primitive("Bird material reference",PrimitiveType.Sphere,new Vector3(-.12f,1.48f,.85f),Vector3.one*.08f,
                AssetDatabase.LoadAssetAtPath<Material>(Folder+"BirdPoint.mat"),parent);
            Primitive("Ordinary material reference",PrimitiveType.Sphere,new Vector3(.12f,1.48f,.85f),Vector3.one*.08f,
                AssetDatabase.LoadAssetAtPath<Material>(Folder+"EstimatedTips.mat"),parent);
            Board("Material reference hint",new Vector3(0,1.65f,.85f),1.8f,.2f,26,"Two white dots: cursor material / ordinary material");
        }
        foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
        {
            if(text.transform.parent.name=="Directions") text.text="SET learns fingertip directions: hold that hand straight; use the other to press.\nOrange cubes = hand origins. Small colored dots = joints, visible through skin.\nBird = larger cyan / pink point and short guide. Curl fingers to bring it near.\nWhite fingertip dots are estimates. Clicking is disabled.";
        }
    }
    static void AddFilterControl()
    {
        var control=UnityEngine.Object.FindObjectOfType<BirdLabFilterControl>();
        if(control==null)
        {
            var position=new Vector3(-.65f,.9f,-.6f);
            var button=Primitive("Bird filter control",PrimitiveType.Cube,position,new Vector3(1.15f,.30f,.12f),
                AssetDatabase.LoadAssetAtPath<Material>(Folder+"Ink.mat"),null,true);
            control=button.AddUdonSharpComponent<BirdLabFilterControl>();
            control.label=Label("Bird filter label",position-Vector3.forward*.075f,1.1f,.28f,36,"Point / FILTERED\nPress for RAW");
            control.cursors=new BirdCursorState[2];
            foreach(var input in UnityEngine.Object.FindObjectsOfType<BirdAvatarHandInput>()) control.cursors[input.rightHand?1:0]=input.cursor;
            UdonSharpEditorUtility.CopyProxyToUdon(control);
            var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(control); vm.interactText="Compare raw / filtered Bird"; vm.proximity=5;
        }
        // A lower front row leaves the existing bench controls and diagnostic
        // boards readable from spawn, rather than masking them with close panels.
        // Establish input fidelity without the known extreme-range filter lag.
        // The existing native button keeps the legacy filter available for comparison.
        control.filtered=control.adaptive=control.sphere=false;
        control.label.text="Point / RAW\nPress for FILTERED";
        foreach(var cursor in control.cursors) { cursor.smoothing=false; cursor.adaptiveFilter=null; cursor.sphereFilter=null; UdonSharpEditorUtility.CopyProxyToUdon(cursor); }
        UdonSharpEditorUtility.CopyProxyToUdon(control);
        control.transform.position=new Vector3(-.65f,.9f,-.6f);
        PlaceLabel(control.label.transform.parent.gameObject,new Vector3(-.65f,.9f,-.675f));
        GameObject.Find("Bird material reference").transform.position=new Vector3(-.8f,.55f,-.6f);
        GameObject.Find("Ordinary material reference").transform.position=new Vector3(-.5f,.55f,-.6f);
        PlaceLabel(GameObject.Find("Material reference hint"),new Vector3(-.65f,.35f,-.6f));
        GameObject.Find("Material reference hint backing").transform.position=new Vector3(-.65f,.35f,-.575f);
        GameObject.Find("Bird point-through target").transform.position=new Vector3(1,1.15f,-.6f);
        var hint=GameObject.Find("Bird target hint"); PlaceLabel(hint,new Vector3(1,.35f,-.6f));
        var label=hint.GetComponentInChildren<Text>(); label.rectTransform.sizeDelta=new Vector2(550,125); label.fontSize=26;
        label.text="Point through: green\nNo click needed";
        var backing=GameObject.Find("Bird target hint backing"); backing.transform.position=new Vector3(1,.35f,-.575f); backing.transform.localScale=new Vector3(1.18f,.33f,.02f);
        foreach(var text in UnityEngine.Object.FindObjectsOfType<Text>(true))
        {
            if(text.transform.parent.name=="Welcome") text.text=control.sphereFilters!=null && control.sphereFilters.Length==2?
                "BIRD / TRACKING LAB 08\nSphere geometry + optional filter comparison":control.adaptiveFilters!=null && control.adaptiveFilters.Length==2?
                "BIRD / TRACKING LAB 07\nSphere geometry + optional filter comparison":UnityEngine.Object.FindObjectsOfType<BirdAvatarUiInput>(true).Length>0?
                "BIRD / TRACKING LAB 06\nSphere geometry + optional reach interaction":"BIRD / TRACKING LAB 05\nInspect the fitted sphere and resulting Bird";
            if(text.transform.parent.name=="Directions") text.text="SET LEFT / RIGHT: hold that hand straight; press with the other. Starts RAW.\nGold = fitted sphere, center and fit ray. Green = palm normal.\nCyan / pink ray and diamond = resulting Bird. Geometry is X-ray; toggle below.\nWhite tips are estimates. Singular fits hide the gold sphere, not Bird.";
        }
    }
    static void AddGeometryView()
    {
        if(GameObject.Find("Bird geometry diagnostics")!=null) return;
        var root=new GameObject("Bird geometry diagnostics");
        var shader=Shader.Find("Bird/Lab X-ray joints");
        var gold=Material("FitWire",shader,new Color(1,.72f,.15f,.65f));
        var center=Material("FitCenter",shader,new Color(1,.72f,.15f,1));
        var normal=Material("PalmNormal",shader,new Color(.25f,1,.3f,.9f));
        foreach(var view in UnityEngine.Object.FindObjectsOfType<BirdLabPointView>())
        {
            string hand=view.input.rightHand?"Right":"Left";
            var go=new GameObject(hand+" Bird geometry"); go.transform.SetParent(root.transform);
            var geometry=go.AddUdonSharpComponent<BirdLabGeometryView>(); geometry.input=view.input; geometry.pointView=view;
            var tint=Material(hand+"Geometry",shader,new Color(view.tint.r,view.tint.g,view.tint.b,.8f));
            geometry.sphereRings=new LineRenderer[3];
            for(int i=0;i<3;i++) geometry.sphereRings[i]=DiagnosticLine(hand+" fit ring "+i,go.transform,gold,64,true,.0009f);
            geometry.fitCenter=Primitive(hand+" fitted center",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.009f,center,go.transform).GetComponent<Renderer>();
            geometry.fitCenter.enabled=false;
            geometry.fitRay=DiagnosticLine(hand+" fit ray",go.transform,gold,2,false,.0012f);
            geometry.birdRay=DiagnosticLine(hand+" resulting Bird ray",go.transform,tint,2,false,.0012f);
            geometry.birdMarker=DiagnosticLine(hand+" resulting Bird diamond",go.transform,tint,4,true,.0015f);
            geometry.palmNormal=DiagnosticLine(hand+" palm normal",go.transform,normal,2,false,.002f);
            UdonSharpEditorUtility.CopyProxyToUdon(geometry);
        }
        var button=Primitive("Bird geometry toggle",PrimitiveType.Cube,new Vector3(.65f,.9f,-.6f),new Vector3(1.15f,.30f,.12f),AssetDatabase.LoadAssetAtPath<Material>(Folder+"Ink.mat"),null,true);
        var control=button.AddUdonSharpComponent<BirdLabToggle>(); control.target=root; control.initiallyEnabled=true; control.title="Geometry";
        control.label=Label("Bird geometry label",new Vector3(.65f,.9f,-.675f),1.1f,.28f,36,"Geometry / ON");
        UdonSharpEditorUtility.CopyProxyToUdon(control);
        var vm=UdonSharpEditorUtility.GetBackingUdonBehaviour(control); vm.interactText="Toggle fitted Bird geometry"; vm.proximity=5;
    }
    static LineRenderer DiagnosticLine(string name,Transform parent,Material material,int count,bool loop,float width)
    {
        var line=new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent);
        line.sharedMaterial=material; line.positionCount=count; line.loop=loop; line.useWorldSpace=true;
        line.startWidth=line.endWidth=width; line.enabled=false; return line;
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
        var canvas=new GameObject(name,typeof(RectTransform)).AddComponent<Canvas>(); canvas.renderMode=RenderMode.WorldSpace; PlaceLabel(canvas.gameObject,position); canvas.transform.localScale=Vector3.one*.002f;
        var label=new GameObject("Text",typeof(RectTransform)).AddComponent<Text>(); label.transform.SetParent(canvas.transform,false); label.rectTransform.sizeDelta=new Vector2(width/.002f,height/.002f);
        label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize=fontSize; label.alignment=TextAnchor.MiddleCenter; label.raycastTarget=false; label.text=value; return label;
    }
    static void PlaceLabel(GameObject go,Vector3 world)
    {
        var rect=go.GetComponent<RectTransform>();
        // RectTransform serializes anchored XY; assigning Transform.position alone
        // can leave the old anchor coordinates when the scene is saved/reloaded.
        rect.anchoredPosition3D=rect.parent==null?world:rect.parent.InverseTransformPoint(world);
        EditorUtility.SetDirty(rect);
    }
    static Text Board(string name,Vector3 position,float width,float height,int fontSize,string value)
    {
        Primitive(name+" backing",PrimitiveType.Cube,position+Vector3.forward*.025f,new Vector3(width+.08f,height+.08f,.02f),AssetDatabase.LoadAssetAtPath<Material>(Folder+"Ink.mat"),null);
        return Label(name,position,width,height,fontSize,value);
    }
}
#endif
