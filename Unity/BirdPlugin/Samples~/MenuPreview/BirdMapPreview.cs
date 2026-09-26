using System.Collections.Generic;
using Bird3DCursor.UI;
using UnityEngine;

namespace Bird3DCursor.Samples
{
    /// <summary>Separate map content, fixed gesture volume and conventional menu actions.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class BirdMapPreview : MonoBehaviour
    {
        public bool desktopInput=true;
        public BirdPointerInput Pointer { get; private set; }
        public BirdMenuInteractor Interactor { get; private set; }
        public BirdMenuPanel Menu { get; private set; }
        public BirdMenuElement OpenButton { get; private set; }
        public BirdMenuElement RotateButton { get; private set; }
        public BirdMenuElement ZoomButton { get; private set; }
        public BirdMenuElement ResetButton { get; private set; }
        public BirdMenuElement CloseButton { get; private set; }
        public BirdRangeScale Zoom { get; private set; }
        public BirdSphericalScroll Scroll { get; private set; }
        public SphereCollider Volume { get; private set; }
        public Transform Rotation { get; private set; }
        public Transform Map { get; private set; }
        public Camera View { get; private set; }
        public bool ZoomMode { get; private set; }
        Transform generated,marker;
        TextMesh status;
        Material template;
        readonly List<Material> materials=new List<Material>();
        readonly Quaternion initialRotation=Quaternion.Euler(-30,-20,0);
        float range=4.6f;

        void Start() { if(generated==null) Initialize(); }
        public void Initialize(BirdPointerInput[] inputs=null)
        {
            if(generated!=null) return;
            template=Resources.Load<Material>("BirdMenuPreviewSurface");
            if(template==null) throw new System.InvalidOperationException("Import the complete Menu Preview Resources folder.");
            generated=new GameObject("Generated map example").transform; generated.SetParent(transform,false);
            if(inputs==null)
            {
                Pointer=new GameObject("Map desktop pointer").AddComponent<BirdPointerInput>(); Pointer.transform.SetParent(generated,false); inputs=new[]{Pointer};
                View=new GameObject("Map preview camera").AddComponent<Camera>(); View.transform.SetParent(generated,false);
                View.transform.localPosition=new Vector3(0,1.45f,-3.6f); View.fieldOfView=50; View.nearClipPlane=.05f; View.farClipPlane=100;
                View.clearFlags=CameraClearFlags.SolidColor; View.backgroundColor=new Color(.025f,.045f,.075f);
                marker=Shape("Logical point",generated,PrimitiveType.Sphere,Vector3.zero,Vector3.one*.032f,Color.white); marker.gameObject.SetActive(false);
            }
            else desktopInput=false;
            Menu=new GameObject("Map menu").AddComponent<BirdMenuPanel>(); Menu.transform.SetParent(generated,false);
            var content=new GameObject("Content").transform; content.SetParent(Menu.transform,false); Menu.Configure(content.gameObject);
            OpenButton=Button("OPEN MAP",generated,new Vector3(0,.15f,-.1f),null,BirdMenuElement.Activation.EnterThrough);
            OpenButton.Activated.AddListener(Menu.Open);
            var sphereObject=new GameObject("Fixed map gesture sphere"); sphereObject.transform.SetParent(content,false); sphereObject.transform.localPosition=new Vector3(0,1.55f,0);
            Volume=sphereObject.AddComponent<SphereCollider>(); Volume.radius=.95f; Volume.isTrigger=true;
            Rotation=new GameObject("Map rotation pivot").transform; Rotation.SetParent(content,false); Rotation.localPosition=sphereObject.transform.localPosition; Rotation.localRotation=initialRotation;
            Map=new GameObject("Map scale pivot").transform; Map.SetParent(Rotation,false);
            BuildMap();
            Scroll=sphereObject.AddComponent<BirdSphericalScroll>(); Scroll.Configure(Volume,Rotation,inputs,Menu);
            Zoom=sphereObject.AddComponent<BirdRangeScale>(); Zoom.Configure(Volume,Map,inputs,Menu); Zoom.SetLimits(.3f,2.3f);
            DrawSphere(sphereObject.transform);
            RotateButton=Button("ROTATE",content,new Vector3(-1.23f,.48f,-.35f),Menu);
            ZoomButton=Button("ZOOM",content,new Vector3(-.41f,.48f,-.35f),Menu);
            ResetButton=Button("RESET",content,new Vector3(.41f,.48f,-.35f),Menu);
            CloseButton=Button("CLOSE",content,new Vector3(1.23f,.48f,-.35f),Menu);
            RotateButton.Activated.AddListener(SelectRotate); ZoomButton.Activated.AddListener(SelectZoom);
            ResetButton.Activated.AddListener(ResetView); CloseButton.Activated.AddListener(Close);
            Menu.Opened.AddListener(OpenMode); Menu.Closed.AddListener(StopModes);
            Interactor=generated.gameObject.AddComponent<BirdMenuInteractor>(); Interactor.Configure(inputs,new[]{OpenButton,RotateButton,ZoomButton,ResetButton,CloseButton});
            Label("BIRD / MAP",generated,new Vector3(0,2.85f,0),.04f,new Color(.8f,.94f,1));
            status=Label("Reach through OPEN MAP",generated,new Vector3(0,2.58f,0),.018f,new Color(.55f,.75f,.85f));
            Physics.SyncTransforms();
        }
        void BuildMap()
        {
            Shape("Coastal map",Map,PrimitiveType.Cube,Vector3.zero,new Vector3(.72f,.035f,.72f),new Color(.1f,.36f,.48f));
            var land=new Color(.2f,.55f,.43f); var road=new Color(.55f,.63f,.65f);
            for(int x=0;x<6;x++) for(int z=0;z<6;z++)
            {
                if(x==0 && z>1 || x==1 && z>3) continue;
                float h=.035f+((x*3+z*7)%5)*.018f;
                Vector3 p=new Vector3((x-2.5f)*.105f,.0175f+h*.5f,(z-2.5f)*.105f);
                Shape("Map block",Map,PrimitiveType.Cube,p,new Vector3(.085f,h,.085f),Color.Lerp(land,new Color(.12f,.25f,.3f),(x+z)/12f));
                Shape("Separated roof",Map,PrimitiveType.Cube,p+Vector3.up*(h*.5f+.002f),new Vector3(.085f,.004f,.085f),(x+z)%3==0?new Color(.94f,.58f,.24f):new Color(.52f,.78f,.61f));
            }
            for(int i=0;i<5;i++)
            {
                Shape("Street",Map,PrimitiveType.Cube,new Vector3((i-2)*.105f,.0185f,0),new Vector3(.013f,.002f,.65f),road);
                for(int block=0;block<6;block++)
                    Shape("Street segment",Map,PrimitiveType.Cube,new Vector3((block-2.5f)*.105f,.0185f,(i-2)*.105f),new Vector3(.092f,.002f,.013f),road);
            }
            Shape("North marker",Map,PrimitiveType.Cube,new Vector3(0,.023f,.335f),new Vector3(.025f,.01f,.025f),Color.white);
        }
        void OpenMode() { SelectRotate(null); }
        void StopModes() { Zoom.StopScaling(); Scroll.Cancel(); }
        public void SelectRotate(BirdPointerInput pointer) { Zoom.StopScaling(); Scroll.enabled=true; ZoomMode=false; }
        public void SelectZoom(BirdPointerInput pointer) { Scroll.enabled=false; Zoom.StartScaling(); ZoomMode=true; }
        public void ResetView(BirdPointerInput pointer) { Zoom.ResetScale(); Scroll.Cancel(); Rotation.localRotation=initialRotation; SelectRotate(pointer); Physics.SyncTransforms(); }
        public void Close(BirdPointerInput pointer) { Menu.CloseAll(); }
        void Update()
        {
            if(!desktopInput || View==null || Pointer==null) return;
            range=Mathf.Clamp(range*Mathf.Exp(Input.mouseScrollDelta.y*.08f),.1f,100);
            Ray ray=View.ScreenPointToRay(Input.mousePosition); Vector3 point=ray.origin+ray.direction*range;
            Pointer.Submit(ray.origin,point,true,Input.GetMouseButton(0)); marker.gameObject.SetActive(true); marker.position=point;
        }
        void LateUpdate() { RefreshVisuals(); }
        public void RefreshVisuals()
        {
            if(status==null) return;
            status.text=Menu.State==BirdMenuPanel.PanelState.Closed?"Reach through OPEN MAP":ZoomMode?
                "ZOOM  /  point through, vary reach; withdraw to release\n"+Zoom.Factor.ToString("0.00")+"x":
                "ROTATE  /  pierce the BACK, flick; withdraw to coast";
        }
        void OnGUI() { if(desktopInput) GUI.Label(new Rect(20,20,1100,30),"Mouse: aim  |  Wheel: Bird reach  |  Click: choose mode or reset  |  Zoom uses reach, without holding click"); }
        BirdMenuElement Button(string text,Transform parent,Vector3 position,BirdMenuPanel panel,BirdMenuElement.Activation activation=BirdMenuElement.Activation.SelectThrough)
        {
            var go=new GameObject(text); go.transform.SetParent(parent,false); go.transform.localPosition=position;
            var volume=go.AddComponent<BoxCollider>(); volume.size=new Vector3(.72f,.23f,.1f); volume.isTrigger=true;
            var element=go.AddComponent<BirdMenuElement>(); element.Configure(volume,panel,activation);
            var visual=Shape("Button visual",go.transform,PrimitiveType.Cube,Vector3.zero,volume.size,new Color(.13f,.2f,.27f));
            go.AddComponent<BirdMenuFeedback>().Configure(element,visual.GetComponent<Renderer>());
            Label(text,go.transform,new Vector3(0,0,-.068f),.027f,Color.white); return element;
        }
        Material Material(Color color) { var value=new Material(template) { color=color }; materials.Add(value); return value; }
        Transform Shape(string name,Transform parent,PrimitiveType kind,Vector3 position,Vector3 scale,Color color)
        {
            var go=GameObject.CreatePrimitive(kind); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>(); collider.enabled=false; Destroy(collider); go.GetComponent<Renderer>().sharedMaterial=Material(color); return go.transform;
        }
        TextMesh Label(string text,Transform parent,Vector3 position,float size,Color color)
        {
            var go=new GameObject(text); go.transform.SetParent(parent,false); go.transform.localPosition=position;
            var label=go.AddComponent<TextMesh>(); label.text=text; label.anchor=TextAnchor.MiddleCenter; label.fontSize=64; label.characterSize=size; label.color=color; return label;
        }
        void DrawSphere(Transform parent)
        {
            var material=Material(new Color(.12f,.23f,.3f));
            for(int plane=0;plane<3;plane++)
            {
                var line=new GameObject("Fixed gesture sphere guide").AddComponent<LineRenderer>(); line.transform.SetParent(parent,false); line.useWorldSpace=false; line.loop=true;
                line.positionCount=96; line.startWidth=line.endWidth=.003f; line.sharedMaterial=material;
                for(int i=0;i<96;i++) { float angle=i*Mathf.PI*2/96,x=Mathf.Cos(angle)*.95f,y=Mathf.Sin(angle)*.95f; line.SetPosition(i,plane==0?new Vector3(x,y,0):plane==1?new Vector3(x,0,y):new Vector3(0,x,y)); }
            }
        }
        void OnDisable() { if(generated!=null) generated.gameObject.SetActive(false); }
        void OnEnable() { if(generated!=null) generated.gameObject.SetActive(true); }
        void OnDestroy()
        {
            if(generated!=null) { generated.gameObject.SetActive(false); Destroy(generated.gameObject); }
            foreach(var material in materials) if(material!=null) Destroy(material);
        }
    }
}
