using System.Collections.Generic;
using UnityEngine;
using Bird3DCursor.UI;
using Bird3DCursor.Manipulation;

namespace Bird3DCursor.Samples
{
    /// <summary>Optional pose docking beside the translation-only Hanoi sample. All geometry is diagnostic.</summary>
    [DefaultExecutionOrder(140)]
    public sealed class BirdPosePreview : MonoBehaviour
    {
        public bool desktopInput=true,createCamera=true;
        public bool createEnvironment=true;
        public Vector3 tabletopPosition=new Vector3(0,.78f,-1.1f),buildingPosition=new Vector3(0,-12,420);
        public BirdPointerInput externalLeft,externalRight;
        public BirdGrabInteractor Interactor { get; private set; }
        public BirdHeldPoseControls Controls { get; private set; }
        public BirdTwoHandPose TwoHandPose { get; private set; }
        public BirdPointerInput Pointer { get; private set; }
        public BirdPointerInput OtherPointer { get; private set; }
        public BirdGrabTarget[] Items { get; private set; }
        public Camera View { get; private set; }
        public Transform Generated { get { return generated; } }
        Transform generated,marker,statusBackdrop;
        TextMesh status;
        Material surface;
        readonly List<Material> materials=new List<Material>();
        BirdGrabFeedback[] feedback;
        float reach=2;
        bool ready;

        void Start() { Initialize(); }
        public void Initialize()
        {
            if(ready) return;
            surface=Resources.Load<Material>("BirdHanoiSurface");
            if(surface==null) throw new System.InvalidOperationException("Import the complete Hanoi Preview Resources.");
            ready=true; generated=Child("Generated pose docking",transform);
            Pointer=externalLeft!=null?externalLeft:Child("Synthetic left",generated).gameObject.AddComponent<BirdPointerInput>();
            OtherPointer=externalRight!=null?externalRight:Child("Synthetic right",generated).gameObject.AddComponent<BirdPointerInput>();
            Interactor=generated.gameObject.AddComponent<BirdGrabInteractor>();
            Controls=generated.gameObject.AddComponent<BirdHeldPoseControls>(); Controls.interactor=Interactor;
            TwoHandPose=generated.gameObject.AddComponent<BirdTwoHandPose>(); TwoHandPose.interactor=Interactor; TwoHandPose.first=Pointer; TwoHandPose.second=OtherPointer;
            Items=new[]{Station("TABLETOP",tabletopPosition,.3f),Station("BUILDING",buildingPosition,75)};
            Interactor.Configure(new[]{Pointer,OtherPointer},Items);
            foreach(var item in Items)
            {
                var cue=item.gameObject.AddComponent<BirdGrabFeedback>(); cue.interactor=Interactor; cue.target=item;
                cue.visual=item.transform.Find("Body").GetComponent<Renderer>();
            }
            feedback=generated.GetComponentsInChildren<BirdGrabFeedback>();
            if(createCamera)
            {
                View=Child("Preview camera",generated).gameObject.AddComponent<Camera>();
                View.transform.localPosition=new Vector3(0,1.65f,-3.2f); View.transform.localRotation=Quaternion.Euler(5,0,0);
                View.nearClipPlane=.08f; View.farClipPlane=2200; View.fieldOfView=52;
                View.clearFlags=CameraClearFlags.SolidColor; View.backgroundColor=new Color(.12f,.22f,.3f);
            }
            Box("Table",generated,tabletopPosition+Vector3.down*.08f,new Vector3(1.7f,.14f,1),new Color(.24f,.3f,.33f));
            Box("Far foundation",generated,buildingPosition+Vector3.down*6,new Vector3(410,12,240),new Color(.2f,.28f,.3f));
            if(createEnvironment)
            {
                Box("Terrace",generated,new Vector3(0,-.08f,0),new Vector3(12,.16f,10),new Color(.1f,.16f,.2f));
                Box("Valley",generated,new Vector3(0,-40,700),new Vector3(1800,12,1700),new Color(.15f,.24f,.22f));
                for(int i=0;i<7;i++) Box("Scale landmark",generated,new Vector3((i-3)*150,-12,780+(i%2)*130),new Vector3(35,70+(i%3)*25,40),new Color(.24f,.34f,.4f));
                Label("BIRD / POSE DOCKING",generated,new Vector3(0,4.4f,5.5f),.031f,Color.white);
                Label("Match the outline. Release when the piece turns green.",generated,new Vector3(0,4,5.5f),.018f,new Color(.75f,.9f,1));
            }
            status=Label("",generated,tabletopPosition+new Vector3(-1.2f,.57f,.3f),.0065f,Color.white); status.anchor=TextAnchor.UpperLeft; status.alignment=TextAlignment.Left;
            if(externalLeft!=null) statusBackdrop=Box("Instructions backing",generated,Vector3.zero,Vector3.one,new Color(.02f,.035f,.05f));
            marker=Box("Desktop logical point",generated,Vector3.zero,Vector3.one*.025f,Color.cyan); marker.gameObject.SetActive(false);
        }
        BirdGrabTarget Station(string name,Vector3 position,float size)
        {
            Transform frame=Child(name,generated); frame.localPosition=position; frame.localScale=Vector3.one*size;
            var region=frame.gameObject.AddComponent<BirdPlacementRegion>(); region.LocalBounds=new Bounds(new Vector3(0,1.6f,0),new Vector3(5,3.2f,3));
            var slots=new BirdSnapTarget[2];
            for(int i=0;i<2;i++)
            {
                var root=Child("Dock "+i,frame); root.localPosition=new Vector3(i==0?-1.35f:1.3f,0,0); root.localRotation=Quaternion.Euler(0,i*90,0);
                var slot=root.gameObject.AddComponent<BirdSnapTarget>(); slots[i]=slot;
                slot.matchRotation=slot.matchScale=true; slot.scaleFactor=i==0?1:1.25f; slot.captureRadius=.2f; slot.influenceRadius=.55f; slot.approachLength=1.4f;
                Box("Pad",frame,root.localPosition+Vector3.down*.03f,new Vector3(1.3f,.06f,1.3f),new Color(.3f,.39f,.44f));
                Wire(root,slot.scaleFactor);
                Label(i==0?"0 deg / 1x":"90 deg / 1.25x",frame,root.localPosition+new Vector3(0,-.15f,-.85f),.019f,Color.white);
            }
            var item=Child("Tower section",frame); item.localPosition=slots[0].transform.localPosition;
            var volume=item.gameObject.AddComponent<BoxCollider>(); volume.center=new Vector3(0,.46f,0); volume.size=new Vector3(.95f,.92f,.7f);
            var target=item.gameObject.AddComponent<BirdGrabTarget>(); target.Configure(volume,region,slots); target.ConfigurePose(true,true,.5f,2);
            Box("Body",item,new Vector3(0,.4f,0),new Vector3(.8f,.8f,.55f),new Color(.16f,.35f,.35f));
            Box("Roof",item,new Vector3(0,.86f,0),new Vector3(.95f,.12f,.7f),new Color(.84f,.5f,.2f));
            for(int row=0;row<6;row++) for(int col=0;col<4;col++)
                Box("Window",item,new Vector3((col-1.5f)*.16f,.08f+row*.12f,-.282f),new Vector3(.08f,.06f,.008f),new Color(.06f,.16f,.2f));
            return target;
        }
        void Wire(Transform slot,float factor)
        {
            Vector3 center=new Vector3(0,.46f,0)*factor,half=new Vector3(.95f,.92f,.7f)*(.5f*factor);
            var material=Material(new Color(.25f,.88f,.72f));
            for(int i=0;i<8;i++) for(int bit=0;bit<3;bit++)
            {
                int other=i^(1<<bit); if(other<i) continue;
                var line=Child("Required pose outline",slot).gameObject.AddComponent<LineRenderer>(); line.useWorldSpace=false; line.positionCount=2;
                line.SetPosition(0,center+Vector3.Scale(half,Sign(i))); line.SetPosition(1,center+Vector3.Scale(half,Sign(other)));
                line.widthMultiplier=.012f*Mathf.Abs(slot.lossyScale.x); line.sharedMaterial=material;
            }
        }
        static Vector3 Sign(int i) { return new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1); }
        static Transform Child(string name,Transform parent) { var t=new GameObject(name).transform; t.SetParent(parent,false); return t; }
        Transform Box(string name,Transform parent,Vector3 position,Vector3 size,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localScale=size;
            var collider=go.GetComponent<Collider>(); collider.enabled=false; Destroy(collider); go.GetComponent<Renderer>().sharedMaterial=Material(color); return go.transform;
        }
        Material Material(Color color)
        {
            foreach(var m in materials) if(m.color==color) return m;
            var result=new Material(surface){color=color,enableInstancing=true}; materials.Add(result); return result;
        }
        static TextMesh Label(string text,Transform parent,Vector3 position,float size,Color color)
        {
            var t=Child(text,parent); t.localPosition=position; var result=t.gameObject.AddComponent<TextMesh>();
            result.text=text; result.fontSize=64; result.characterSize=size; result.anchor=TextAnchor.MiddleCenter; result.alignment=TextAlignment.Center; result.color=color; return result;
        }
        public void TurnLeft() { Controls.RotateAroundWorkspaceUp(-15); }
        public void TurnRight() { Controls.RotateAroundWorkspaceUp(15); }
        public void Grow() { Controls.ResizeBy(1.25f); }
        public void Shrink() { Controls.ResizeBy(.8f); }
        void Update()
        {
            if(!ready || !desktopInput || View==null || externalLeft!=null) return;
            reach=Mathf.Clamp(reach*Mathf.Exp(Input.mouseScrollDelta.y*.14f),.15f,1500);
            Ray ray=View.ScreenPointToRay(Input.mousePosition); Vector3 point=ray.origin+ray.direction*reach;
            Pointer.Submit(ray.origin,point,true,Input.GetMouseButton(0));
            if(Input.GetKeyDown(KeyCode.Q)) TurnLeft(); if(Input.GetKeyDown(KeyCode.E)) TurnRight();
            if(Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) Grow();
            if(Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) Shrink();
            if(Input.GetKeyDown(KeyCode.R)) Controls.ResetPose();
            if(Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)) Interactor.Cancel();
            marker.gameObject.SetActive(true); marker.position=point; marker.localScale=Vector3.one*(.025f*Mathf.Max(1,reach/15));
        }
        void LateUpdate() { if(ready) UpdateFeedback(); }
        public void UpdateFeedback()
        {
            foreach(var cue in feedback) cue.Refresh();
            string action=Interactor.ActiveTarget==null?"POINT / HOLD TO PICK UP":Interactor.IsReturning?"RETURNING":Interactor.ReadyToPlace?"RELEASE TO PLACE":Interactor.PoseLimited?"POSE LIMIT / ADJUST OR RESET":"ALIGN WITH OUTLINE";
            status.text=action+(externalLeft!=null?
                "\n\nHold with one Bird to move.\nPoint the other Bird through it and click.\nWhile both hold: turn / spread your hands.\nRelease the second hand to freeze pose.\nMatch the outline, then release to place.":
                "\n\nDesktop input\nMouse aim / wheel reach\nHold left mouse to move\nQ / E rotate 15 deg\n- / + change size\nR restore held pose\nRight / Esc cancel");
            if(statusBackdrop!=null)
            {
                Bounds text=status.GetComponent<Renderer>().localBounds;
                statusBackdrop.localPosition=status.transform.localPosition+text.center+Vector3.forward*.02f;
                statusBackdrop.localScale=new Vector3(text.size.x+.05f,text.size.y+.05f,.01f);
            }
        }
        void OnDisable() { if(Interactor!=null) Interactor.CancelImmediately(); if(generated!=null) generated.gameObject.SetActive(false); }
        void OnEnable() { if(generated!=null) generated.gameObject.SetActive(true); }
        void OnDestroy() { if(generated!=null) Destroy(generated.gameObject); foreach(var m in materials) if(m!=null) Destroy(m); }
    }
}
