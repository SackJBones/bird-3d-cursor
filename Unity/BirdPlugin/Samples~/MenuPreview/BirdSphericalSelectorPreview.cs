using System.Collections.Generic;
using Bird3DCursor.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Bird3DCursor.Samples
{
    /// <summary>Example layout and color actions. Supply inputs to embed it in a live host without a camera.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class BirdSphericalSelectorPreview : MonoBehaviour
    {
        [System.Serializable] public sealed class ColorEvent : UnityEvent<Color> { }
        [SerializeField] ColorEvent colorSelected = new ColorEvent();
        public bool desktopInput = true;
        public BirdPointerInput Pointer { get; private set; }
        public BirdMenuInteractor Interactor { get; private set; }
        public BirdSphericalScroll Scroll { get; private set; }
        public SphereCollider Sphere { get; private set; }
        public Transform Content { get; private set; }
        public Camera View { get; private set; }
        public BirdMenuElement[] Choices { get; private set; }
        public Color ChosenColor { get; private set; }
        public int SelectionCount { get; private set; }
        public ColorEvent ColorSelected { get { return colorSelected; } }
        public int EdgeCount { get; private set; }
        readonly List<Material> ownedMaterials = new List<Material>();
        Material template, resultMaterial;
        Material[] choiceMaterials;
        Color[] colors;
        Transform generated, marker;
        TextMesh status;
        float range=3.6f;

        void Start() { if (generated == null) Initialize(); }

        public void Initialize(BirdPointerInput[] inputs = null)
        {
            if (generated != null) return;
            template=Resources.Load<Material>("BirdMenuPreviewSurface");
            if (template == null) throw new System.InvalidOperationException("Import the Menu Preview Resources material.");
            generated=new GameObject("Generated spherical example").transform; generated.SetParent(transform,false);
            if (inputs == null)
            {
                View=new GameObject("Spherical preview camera").AddComponent<Camera>(); View.transform.SetParent(generated,false);
                View.transform.localPosition=new Vector3(0,1.4f,-3.6f); View.fieldOfView=50;
                View.nearClipPlane=.05f; View.farClipPlane=100; View.clearFlags=CameraClearFlags.SolidColor;
                View.backgroundColor=new Color(.025f,.045f,.075f);
                Pointer=new GameObject("Synthetic Bird input").AddComponent<BirdPointerInput>(); Pointer.transform.SetParent(generated,false);
                inputs=new[]{Pointer};
            }
            else desktopInput=false;

            var volume=new GameObject("Back-surface interaction sphere"); volume.transform.SetParent(generated,false);
            volume.layer=LayerMask.NameToLayer("Ignore Raycast");
            volume.transform.localPosition=new Vector3(0,1.4f,0);
            Sphere=volume.AddComponent<SphereCollider>(); Sphere.radius=1.1f; Sphere.isTrigger=true;
            Content=new GameObject("Rotating choices").transform; Content.SetParent(generated,false); Content.localPosition=volume.transform.localPosition;
            Scroll=volume.AddComponent<BirdSphericalScroll>(); Scroll.Configure(Sphere,Content,inputs);
            Choices=new BirdMenuElement[12]; choiceMaterials=new Material[12]; colors=new Color[12];
            var items=new Transform[12];
            for(int i=0;i<12;i++)
            {
                int index=i;
                colors[i]=Color.HSVToRGB(i/12f,.82f,1);
                var orb=Primitive("Color "+(i+1),Content,Vector3.zero,.14f,colors[i]); items[i]=orb;
                choiceMaterials[i]=orb.GetComponent<Renderer>().sharedMaterial;
                var collider=orb.gameObject.AddComponent<SphereCollider>(); collider.radius=.5f; collider.isTrigger=true;
                Choices[i]=orb.gameObject.AddComponent<BirdMenuElement>();
                Choices[i].Configure(collider,null,BirdMenuElement.Activation.SelectThrough);
                Choices[i].Activated.AddListener(p=>Select(index));
            }
            Content.gameObject.AddComponent<BirdDodecahedronLayout>().Configure(items,.55f);
            BuildEdges(.55f);
            var cage=NewMaterial(new Color(.12f,.23f,.3f));
            for(int plane=0;plane<3;plane++)
            {
                var points=new Vector3[96];
                for(int i=0;i<96;i++)
                {
                    float a=i*Mathf.PI*2/96, x=Mathf.Cos(a)*1.1f,y=Mathf.Sin(a)*1.1f;
                    points[i]=plane==0 ? new Vector3(x,y,0) : plane==1 ? new Vector3(x,0,y) : new Vector3(0,x,y);
                }
                Line("Outer sphere guide",volume.transform,points,.003f,cage,true);
            }
            Interactor=generated.gameObject.AddComponent<BirdMenuInteractor>(); Interactor.Configure(inputs,Choices);
            Label("BIRD / SPHERICAL SCROLL",new Vector3(0,2.85f,0),.032f,new Color(.75f,.9f,1));
            status=Label("Move freely inside. Reach through the back to scroll.",new Vector3(0,2.6f,0),.017f,new Color(.5f,.7f,.8f));
            var result=Primitive("Chosen color",generated,new Vector3(1.55f,1.4f,0),.2f,new Color(.7f,.75f,.8f));
            resultMaterial=result.GetComponent<Renderer>().sharedMaterial;
            Label("YOUR COLOR",new Vector3(1.55f,1.12f,0),.02f,new Color(.6f,.75f,.85f));
            Label("Reach through a color, then click.",new Vector3(0,.08f,0),.022f,new Color(.6f,.75f,.85f));
            if (Pointer != null)
            {
                marker=Primitive("Logical Bird point",generated,Vector3.zero,.032f,Color.white);
                marker.gameObject.SetActive(false);
            }
            Physics.SyncTransforms();
        }

        void Select(int index)
        {
            ChosenColor=colors[index]; resultMaterial.color=ChosenColor; SelectionCount++;
            colorSelected.Invoke(ChosenColor);
        }

        void Update()
        {
            if (!desktopInput || View == null || Pointer == null) return;
            range=Mathf.Clamp(range+Input.mouseScrollDelta.y*.15f,.2f,12);
            var ray=View.ScreenPointToRay(Input.mousePosition);
            var point=View.transform.position+ray.direction*range;
            Pointer.Submit(View.transform.position,point,true,Input.GetMouseButton(0));
            marker.gameObject.SetActive(true); marker.position=point;
        }

        void LateUpdate() { RefreshVisuals(); }
        public void RefreshVisuals()
        {
            if (Choices == null) return;
            for(int i=0;i<Choices.Length;i++)
                choiceMaterials[i].color=Color.Lerp(colors[i],Color.white,Choices[i].State==BirdMenuVisualState.Activated ? .8f : Choices[i].State==BirdMenuVisualState.Highlighted ? .35f : 0);
            status.text=Scroll.ActivePointer != null ? "SCROLLING  /  withdraw inside to choose" : "Move freely inside. Reach through the back to scroll.";
        }

        void OnGUI()
        {
            if (desktopInput) GUI.Label(new Rect(20,20,1100,30),"Mouse: aim  |  Wheel: reach through the back to flick; retract inside to choose  |  Left click: choose a color");
        }

        Material NewMaterial(Color color)
        {
            var material=new Material(template) { color=color }; ownedMaterials.Add(material); return material;
        }
        Transform Primitive(string name,Transform parent,Vector3 position,float diameter,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name=name; go.transform.SetParent(parent,false);
            var collider=go.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            go.transform.localPosition=position; go.transform.localScale=Vector3.one*diameter;
            go.GetComponent<Renderer>().sharedMaterial=NewMaterial(color); return go.transform;
        }
        TextMesh Label(string text,Vector3 position,float size,Color color)
        {
            var go=new GameObject(text); go.transform.SetParent(generated,false); go.transform.localPosition=position;
            var label=go.AddComponent<TextMesh>(); label.text=text; label.anchor=TextAnchor.MiddleCenter;
            label.fontSize=64; label.characterSize=size; label.color=color;
            var backing=GameObject.CreatePrimitive(PrimitiveType.Quad); backing.name="Label contrast backing";
            var collider=backing.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            backing.transform.SetParent(go.transform,false);
            Bounds bounds=label.GetComponent<Renderer>().localBounds;
            backing.transform.localPosition=bounds.center+Vector3.forward*.02f;
            backing.transform.localScale=new Vector3(bounds.size.x+.08f,bounds.size.y+.05f,1);
            backing.GetComponent<Renderer>().sharedMaterial=NewMaterial(new Color(.015f,.035f,.055f));
            return label;
        }
        static void Line(string name,Transform parent,Vector3[] points,float width,Material material,bool loop=false)
        {
            var line=new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent,false);
            line.useWorldSpace=false; line.loop=loop; line.positionCount=points.Length; line.SetPositions(points);
            line.startWidth=line.endWidth=width; line.sharedMaterial=material;
        }
        void BuildEdges(float faceRadius)
        {
            // Intersect triples of face planes; keep convex interior vertices, then connect shortest pairs.
            var vertices=new List<Vector3>();
            for(int i=0;i<12;i++) for(int j=i+1;j<12;j++) for(int k=j+1;k<12;k++)
            {
                Vector3 a=BirdDodecahedronLayout.Direction(i),b=BirdDodecahedronLayout.Direction(j),c=BirdDodecahedronLayout.Direction(k);
                float determinant=Vector3.Dot(a,Vector3.Cross(b,c)); if(Mathf.Abs(determinant)<.0001f) continue;
                Vector3 v=(Vector3.Cross(b,c)+Vector3.Cross(c,a)+Vector3.Cross(a,b))*(faceRadius/determinant);
                bool inside=true;
                for(int n=0;n<12;n++) if(Vector3.Dot(BirdDodecahedronLayout.Direction(n),v)>faceRadius+.0001f) inside=false;
                foreach(var existing in vertices) if((v-existing).sqrMagnitude<.000001f) inside=false;
                if(inside) vertices.Add(v);
            }
            float edge=float.PositiveInfinity;
            for(int i=0;i<vertices.Count;i++) for(int j=i+1;j<vertices.Count;j++) edge=Mathf.Min(edge,Vector3.Distance(vertices[i],vertices[j]));
            var material=NewMaterial(new Color(.25f,.35f,.42f));
            for(int i=0;i<vertices.Count;i++) for(int j=i+1;j<vertices.Count;j++)
                if(Vector3.Distance(vertices[i],vertices[j])<edge*1.01f)
                { Line("Dodecahedron edge",Content,new[]{vertices[i],vertices[j]},.004f,material); EdgeCount++; }
        }
        void OnDisable() { if(generated != null) generated.gameObject.SetActive(false); }
        void OnEnable() { if(generated != null) generated.gameObject.SetActive(true); }
        void OnDestroy()
        {
            if(generated != null) { generated.gameObject.SetActive(false); Destroy(generated.gameObject); }
            foreach(var material in ownedMaterials) if(material != null) Destroy(material);
        }
    }
}
