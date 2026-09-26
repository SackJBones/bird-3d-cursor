using System.Collections.Generic;
using UnityEngine;
using Bird3DCursor.UI;
using Bird3DCursor.Manipulation;

namespace Bird3DCursor.Samples
{
    /// <summary>Two scales, one manipulation implementation. Optional desktop input is synthetic.</summary>
    [DefaultExecutionOrder(140)]
    public sealed class BirdHanoiPreview : MonoBehaviour
    {
        public bool desktopInput=true;
        public bool createCamera=true;
        public BirdPointerInput externalLeft,externalRight;
        public BirdGrabInteractor Interactor { get; private set; }
        public BirdPointerInput Pointer { get; private set; }
        public BirdPointerInput OtherPointer { get; private set; }
        public BirdHanoiBoard Tabletop { get; private set; }
        public BirdHanoiBoard Buildings { get; private set; }
        public Camera View { get; private set; }
        public Transform Generated { get { return generated; } }
        Transform generated,marker,ghost;
        LineRenderer approach;
        TextMesh status;
        Material surface;
        BirdGrabFeedback[] feedbackComponents;
        readonly List<Material> materials=new List<Material>();
        float range=4;
        bool ready;
        void Start() { Initialize(); }
        public void Initialize()
        {
            if(ready) return;
            surface=Resources.Load<Material>("BirdHanoiSurface");
            if(surface==null) throw new System.InvalidOperationException("Import the complete Hanoi Preview sample including Resources.");
            ready=true;
            generated=new GameObject("Generated Hanoi example").transform; generated.SetParent(transform,false);
            Pointer=externalLeft!=null?externalLeft:Child("Synthetic left input",generated).gameObject.AddComponent<BirdPointerInput>();
            OtherPointer=externalRight!=null?externalRight:Child("Synthetic right input",generated).gameObject.AddComponent<BirdPointerInput>();
            Interactor=generated.gameObject.AddComponent<BirdGrabInteractor>();
            Tabletop=Board("TABLETOP",new Vector3(0,.78f,-1.15f),.2f);
            Buildings=Board("BUILDINGS",new Vector3(0,-12,420),75);
            var items=new List<BirdGrabTarget>(); items.AddRange(Tabletop.Pieces); items.AddRange(Buildings.Pieces);
            Interactor.Configure(new[]{Pointer,OtherPointer},items.ToArray());
            foreach(var item in items)
            {
                var feedback=item.gameObject.AddComponent<BirdGrabFeedback>();
                feedback.interactor=Interactor; feedback.target=item; feedback.visual=item.transform.Find("Building volume").GetComponent<Renderer>();
            }
            feedbackComponents=generated.GetComponentsInChildren<BirdGrabFeedback>();
            if(createCamera)
            {
                View=Child("Preview camera",generated).gameObject.AddComponent<Camera>();
                View.transform.localPosition=new Vector3(0,1.65f,-3.2f);
                View.transform.localRotation=Quaternion.LookRotation(new Vector3(0,-.07f,1));
                View.nearClipPlane=.08f; View.farClipPlane=2200; View.fieldOfView=52;
                View.clearFlags=CameraClearFlags.SolidColor; View.backgroundColor=new Color(.13f,.23f,.31f);
            }
            Primitive("Terrace",generated,new Vector3(0,-.06f,1),new Vector3(11,.12f,10),new Color(.1f,.14f,.17f));
            Primitive("Table",generated,new Vector3(0,.725f,-1.15f),new Vector3(1.05f,.1f,.68f),new Color(.22f,.28f,.3f));
            for(int i=0;i<4;i++) Primitive("Table leg",generated,new Vector3((i&1)==0?-.43f:.43f,.345f,-1.15f+((i&2)==0?-.23f:.23f)),new Vector3(.055f,.69f,.055f),new Color(.16f,.21f,.23f));
            Primitive("Distant foundation",generated,new Vector3(0,-21.7f,420),new Vector3(420,12,235),new Color(.22f,.29f,.3f));
            Primitive("Valley",generated,new Vector3(0,-28,650),new Vector3(1500,8,1800),new Color(.17f,.25f,.23f));
            for(int i=0;i<9;i++)
            {
                float x=(i-4)*140;
                Primitive("Scale landmark",generated,new Vector3(x,-5+(i%3)*8,690+(i%2)*140),new Vector3(26,40+(i%3)*16,28),new Color(.21f,.32f,.38f));
            }
            Label("BIRD / TOWERS OF HANOI",generated,new Vector3(0,4.3f,5.5f),.033f,Color.white);
            Label("Point through a top piece. Hold to move. Release in the glowing dock.",generated,new Vector3(0,3.95f,5.5f),.018f,new Color(.8f,.9f,.95f));
            status=Label("",generated,new Vector3(-1.05f,.8f,-.8f),.0058f,Color.white);
            status.anchor=TextAnchor.MiddleLeft; status.alignment=TextAlignment.Left;
            marker=Primitive("Logical input marker",generated,Vector3.zero,Vector3.one*.032f,Color.cyan,PrimitiveType.Sphere);
            ghost=Primitive("Allowed final pose",generated,Vector3.zero,Vector3.one,Color.green);
            ghost.gameObject.SetActive(false); marker.gameObject.SetActive(false);
            approach=Child("Approach guide",generated).gameObject.AddComponent<LineRenderer>();
            approach.sharedMaterial=MakeMaterial(new Color(.25f,1,.75f)); approach.positionCount=2; approach.enabled=false;
            UpdateFeedback();
        }
        Transform Child(string name,Transform parent)
        {
            var t=new GameObject(name).transform; t.SetParent(parent,false); return t;
        }
        BirdHanoiBoard Board(string name,Vector3 position,float size)
        {
            var root=Child(name,generated); root.localPosition=position; root.localScale=Vector3.one*size;
            var region=root.gameObject.AddComponent<BirdPlacementRegion>();
            region.LocalBounds=new Bounds(new Vector3(0,1.7f,0),new Vector3(4.5f,3.4f,2.6f));
            var board=root.gameObject.AddComponent<BirdHanoiBoard>();
            var slots=new BirdSnapTarget[3]; var pegs=new Transform[3]; var pieces=new BirdGrabTarget[3];
            float[] heights={.34f,.42f,.5f}; float[] widths={.62f,.87f,1.12f};
            Color[] colors={new Color(1,.63f,.2f),new Color(.17f,.8f,.85f),new Color(.6f,.43f,.94f)};
            for(int i=0;i<3;i++)
            {
                pegs[i]=Child("Dock "+(i+1),root); pegs[i].localPosition=new Vector3((i-1)*1.4f,0,0);
                slots[i]=Child("Placement "+(i+1),root).gameObject.AddComponent<BirdSnapTarget>();
                slots[i].approachLength=1.35f; slots[i].influenceRadius=.48f; slots[i].captureRadius=.23f;
                Primitive("Dock pad",root,pegs[i].localPosition-new Vector3(0,.025f,0),new Vector3(1.27f,.05f,1.15f),new Color(.38f,.48f,.52f));
                Label((i+1).ToString(),root,pegs[i].localPosition+new Vector3(0,.05f,-.66f),.023f,Color.white);
            }
            for(int i=0;i<3;i++)
            {
                var item=Child("Tower section "+(i+1),root); var box=item.gameObject.AddComponent<BoxCollider>();
                Vector3 bodySize=new Vector3(widths[i],heights[i],widths[i]*.83f);
                box.size=bodySize+new Vector3(0,0,.007f); box.center=new Vector3(0,0,-.0035f);
                pieces[i]=item.gameObject.AddComponent<BirdGrabTarget>(); pieces[i].Configure(box,region,slots,board);
                Primitive("Building volume",item,Vector3.zero,bodySize,colors[i]);
                // Windows are shallow boxes separated from the facade, not coplanar decals.
                int floors=8+i*2;
                for(int row=0;row<floors;row++) for(int col=0;col<6;col++)
                    Primitive("Window",item,new Vector3((col-2.5f)*widths[i]/7,(row-(floors-1)*.5f)*heights[i]/(floors+1),-bodySize.z*.5f-.005f),new Vector3(widths[i]/13,heights[i]/(floors+1)*.5f,.004f),new Color(.06f,.16f,.21f));
            }
            board.Configure(pieces,heights,pegs,slots);
            Label(name,root,new Vector3(0,-.17f,-.8f),.02f,new Color(.65f,.8f,.9f));
            return board;
        }
        Transform Primitive(string name,Transform parent,Vector3 position,Vector3 dimensions,Color color,PrimitiveType type=PrimitiveType.Cube)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=position; go.transform.localScale=dimensions;
            var c=go.GetComponent<Collider>(); c.enabled=false; Destroy(c);
            go.GetComponent<Renderer>().sharedMaterial=MakeMaterial(color); return go.transform;
        }
        Material MakeMaterial(Color color)
        {
            foreach(var existing in materials) if(existing.color==color) return existing;
            var m=new Material(surface) {color=color,enableInstancing=true}; materials.Add(m); return m;
        }
        TextMesh Label(string text,Transform parent,Vector3 position,float size,Color color)
        {
            var t=Child(text,parent); t.localPosition=position;
            var label=t.gameObject.AddComponent<TextMesh>(); label.text=text; label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center;
            label.fontSize=64; label.characterSize=size; label.color=color; return label;
        }
        void Update()
        {
            if(!ready || !desktopInput || View==null || externalLeft!=null) return;
            range=Mathf.Clamp(range*Mathf.Exp(Input.mouseScrollDelta.y*.14f),.15f,1500);
            var ray=View.ScreenPointToRay(Input.mousePosition);
            Vector3 point=ray.origin+ray.direction*range;
            Pointer.Submit(ray.origin,point,true,Input.GetMouseButton(0));
            if(Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) Interactor.Cancel();
            if(Input.GetKeyDown(KeyCode.R)) ResetPuzzles();
            marker.gameObject.SetActive(true); marker.position=point;
            marker.localScale=Vector3.one*(.032f*Mathf.Max(1,range/15));
        }
        void LateUpdate() { if(ready) UpdateFeedback(); }
        public void ResetPuzzles() { Interactor.CancelImmediately(); Tabletop.ResetPuzzle(); Buildings.ResetPuzzle(); }
        public void UpdateFeedback()
        {
            foreach(var feedback in feedbackComponents) feedback.Refresh();
            var item=Interactor.ActiveTarget; var slot=Interactor.Candidate;
            bool guided=item!=null && slot!=null && !Interactor.IsReturning;
            ghost.gameObject.SetActive(guided); approach.enabled=guided;
            if(guided)
            {
                ghost.position=slot.transform.position+item.Volume.transform.TransformVector(item.Volume.center); ghost.rotation=item.transform.rotation;
                Vector3 worldSize=Vector3.Scale(item.Volume.size,item.transform.lossyScale)*1.06f, parentScale=generated.lossyScale;
                ghost.localScale=new Vector3(worldSize.x/parentScale.x,worldSize.y/parentScale.y,worldSize.z/parentScale.z);
                // Only a thin footprint, so the target does not hide the actual piece.
                var size=ghost.localScale; size.y=Mathf.Max(.004f,size.y*.025f); ghost.localScale=size;
                ghost.position-=item.transform.up*(item.Volume.size.y*item.transform.lossyScale.y*.49f);
                ghost.GetComponent<Renderer>().sharedMaterial.color=Interactor.ReadyToPlace?new Color(.45f,1,.35f):new Color(.2f,.75f,.7f);
                Vector3 axis=item.Region.transform.TransformVector(Vector3.up*slot.approachLength);
                approach.SetPosition(0,slot.transform.position); approach.SetPosition(1,slot.transform.position+axis);
                approach.widthMultiplier=.012f*Mathf.Abs(item.Region.transform.lossyScale.x);
            }
            string action=item==null?"POINT / HOLD TO PICK UP":Interactor.IsReturning?"RETURNING":Interactor.ReadyToPlace?"RELEASE TO PLACE":guided?"FOLLOW THE GUIDE":"DRAGGING";
            status.text=action+"\nTable "+Tabletop.Moves+" | Buildings "+Buildings.Moves+
                (Tabletop.Solved||Buildings.Solved?"\nPUZZLE SOLVED":"")+"\n\nDesktop input\nMouse aim / wheel reach\nHold left mouse to move\nRight / Esc cancel\nR reset";
        }
        void OnDisable()
        {
            if(Interactor!=null) Interactor.CancelImmediately();
            if(generated!=null) generated.gameObject.SetActive(false);
        }
        void OnEnable() { if(generated!=null) generated.gameObject.SetActive(true); }
        void OnDestroy()
        {
            if(generated!=null) Destroy(generated.gameObject);
            foreach(var material in materials) if(material!=null) Destroy(material);
        }
    }
}
