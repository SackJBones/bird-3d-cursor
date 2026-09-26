using System.Collections.Generic;
using UnityEngine;
using Bird3DCursor.UI;

namespace Bird3DCursor.Samples
{
    /// <summary>Desktop example of logical Bird input driving conventional menu components.</summary>
    public sealed class BirdMenuPreview : MonoBehaviour
    {
        public bool desktopInput = true;
        public BirdPointerInput Pointer { get; private set; }
        public BirdMenuInteractor Interactor { get; private set; }
        public BirdMenuPanel Menu { get; private set; }
        public BirdMenuPanel Colors { get; private set; }
        public BirdMenuElement Gate { get; private set; }
        public BirdMenuElement ColorButton { get; private set; }
        public BirdMenuElement CloseButton { get; private set; }
        public BirdMenuElement BackButton { get; private set; }
        public BirdMenuElement CyanButton { get; private set; }
        public BirdMenuElement AmberButton { get; private set; }
        public Camera View { get; private set; }
        public int SelectionCount { get; private set; }
        public Color ChosenColor { get; private set; }
        Transform marker, result, generated;
        Material markerMaterial, resultMaterial, surfaceMaterial;
        readonly List<Material> ownedMaterials = new List<Material>();
        float range = 6, gesture = -1;
        bool ready;

        void Start() { Initialize(); }
        public void Initialize()
        {
            if (ready) return;
            surfaceMaterial = Resources.Load<Material>("BirdMenuPreviewSurface");
            if (surfaceMaterial == null) throw new System.InvalidOperationException("Import the complete Menu Preview sample, including its Resources material.");
            ready = true;
            generated = new GameObject("Generated menu example").transform;
            generated.SetParent(transform,false);
            View = new GameObject("Menu preview camera").AddComponent<Camera>();
            View.transform.SetParent(generated,false);
            View.transform.position = new Vector3(0,1.45f,-4);
            View.nearClipPlane = .05f; View.farClipPlane = 100;
            View.fieldOfView = 48; View.clearFlags = CameraClearFlags.SolidColor;
            View.backgroundColor = new Color(.025f,.045f,.075f);
            Pointer = new GameObject("Synthetic Bird input").AddComponent<BirdPointerInput>();
            Pointer.transform.SetParent(generated,false);
            Interactor = generated.gameObject.AddComponent<BirdMenuInteractor>();
            Menu = Panel("Main menu",null);
            Colors = Panel("Color submenu",Menu);
            Gate = Control("Sweep upward to open",new Vector3(0,.55f,-.2f),new Vector3(1.5f,.15f,.2f),null,BirdMenuElement.Activation.DirectionalPass);
            Gate.Activated.AddListener(Menu.Open);
            ColorButton = Control("COLORS",new Vector3(-.72f,1.83f,0),new Vector3(1.2f,.4f,.12f),Menu);
            ColorButton.Activated.AddListener(Colors.Open);
            CloseButton = Control("CLOSE",new Vector3(.72f,1.83f,0),new Vector3(1.2f,.4f,.12f),Menu);
            CloseButton.Activated.AddListener(CloseMenu);
            CyanButton = Control("CYAN",new Vector3(-.62f,1.3f,-.35f),new Vector3(1.05f,.38f,.12f),Colors);
            AmberButton = Control("AMBER",new Vector3(.62f,1.3f,-.35f),new Vector3(1.05f,.38f,.12f),Colors);
            CyanButton.Activated.AddListener(ChooseCyan); AmberButton.Activated.AddListener(ChooseAmber);
            BackButton = Control("BACK",new Vector3(0,.88f,-.35f),new Vector3(.95f,.28f,.12f),Colors);
            BackButton.Activated.AddListener(Back);
            Interactor.Configure(new[]{Pointer},GetComponentsInChildren<BirdMenuElement>(true));
            Label("BIRD / MENU",new Vector3(0,2.4f,0),.048f,new Color(.75f,.9f,1));
            Label("Reach through to highlight. Click to choose.",new Vector3(0,2.12f,0),.021f,new Color(.5f,.65f,.75f));
            result = Primitive("Chosen color",PrimitiveType.Sphere,new Vector3(1.95f,.75f,.1f),Vector3.one*.34f,new Color(.7f,.75f,.8f));
            resultMaterial = result.GetComponent<Renderer>().sharedMaterial;
            Label("YOUR COLOR",new Vector3(1.95f,.43f,.1f),.024f,new Color(.6f,.75f,.85f));
            Primitive("Floor",PrimitiveType.Cube,new Vector3(0,.1f,1),new Vector3(8,.1f,7),new Color(.055f,.08f,.11f));
            marker = Primitive("Logical Bird marker",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.032f,Color.cyan);
            markerMaterial = marker.GetComponent<Renderer>().sharedMaterial;
            marker.gameObject.SetActive(false);
        }

        BirdMenuPanel Panel(string name,BirdMenuPanel parent)
        {
            var go = new GameObject(name); go.transform.SetParent(generated,false);
            var panel = go.AddComponent<BirdMenuPanel>();
            var content = new GameObject("Content"); content.transform.SetParent(go.transform,false);
            panel.Configure(content,parent);
            return panel;
        }

        BirdMenuElement Control(string label,Vector3 position,Vector3 size,BirdMenuPanel panel,
            BirdMenuElement.Activation mode = BirdMenuElement.Activation.SelectThrough)
        {
            var go = new GameObject(label);
            go.transform.SetParent(panel != null ? panel.transform.Find("Content") : generated,false);
            go.transform.position = position;
            var collider = go.AddComponent<BoxCollider>(); collider.size = size;
            var element = go.AddComponent<BirdMenuElement>(); element.Configure(collider,panel,mode);
            var visual = Primitive(label+" visual",PrimitiveType.Cube,position,size,new Color(.13f,.2f,.27f));
            visual.SetParent(go.transform,true);
            var feedback = go.AddComponent<BirdMenuFeedback>(); feedback.Configure(element,visual.GetComponent<Renderer>());
            var text = Label(label,position-Vector3.forward*(size.z*.5f+.018f),mode == BirdMenuElement.Activation.DirectionalPass ? .022f : .035f,Color.white);
            text.transform.SetParent(go.transform,true);
            return element;
        }

        Transform Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale,Color color)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(generated,false);
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            go.transform.position = position; go.transform.localScale = scale;
            var material = new Material(surfaceMaterial) { color = color };
            ownedMaterials.Add(material);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        TextMesh Label(string text,Vector3 position,float size,Color color)
        {
            var go = new GameObject(text); go.transform.SetParent(generated,false); go.transform.position = position;
            var label = go.AddComponent<TextMesh>(); label.text = text; label.anchor = TextAnchor.MiddleCenter;
            label.fontSize = 64; label.characterSize = size; label.color = color;
            return label;
        }

        public void ChooseCyan(BirdPointerInput pointer) { Choose(new Color(.05f,.85f,1)); }
        public void ChooseAmber(BirdPointerInput pointer) { Choose(new Color(1,.55f,.12f)); }
        void Choose(Color color) { ChosenColor = color; SelectionCount++; resultMaterial.color = color; }
        public void Back(BirdPointerInput pointer) { Colors.Close(); }
        public void CloseMenu(BirdPointerInput pointer) { Menu.Close(); }

        void Update()
        {
            if (!desktopInput || !ready) return;
            if (Input.GetKeyDown(KeyCode.G)) gesture = 0;
            if (Input.GetKeyDown(KeyCode.Escape)) Menu.CloseAll();
            range = Mathf.Clamp(range+Input.mouseScrollDelta.y*.3f,.2f,20);
            Ray ray = View.ScreenPointToRay(Input.mousePosition);
            Vector3 point = View.transform.position+ray.direction*range;
            if (gesture >= 0)
            {
                point = new Vector3(0,Mathf.Lerp(.35f,1.05f,gesture/.35f),-.2f);
                gesture += Time.unscaledDeltaTime;
                if (gesture > .35f) gesture = -1;
            }
            Pointer.Submit(View.transform.position,point,true,Input.GetMouseButton(0));
            marker.gameObject.SetActive(true); marker.position = point;
            markerMaterial.color = Pointer.IsPressed ? Color.white : Color.cyan;
        }

        void OnGUI()
        {
            if (!desktopInput) return;
            GUI.Label(new Rect(20,20,900,28),"G: sweep upward to open   |   Mouse: aim   |   Wheel: reach   |   Left click: choose   |   Esc: close");
        }

        void OnDestroy()
        {
            if (generated != null) { generated.gameObject.SetActive(false); Destroy(generated.gameObject); }
            foreach (var material in ownedMaterials) if (material != null) Destroy(material);
            ownedMaterials.Clear();
        }

        void OnDisable() { if (generated != null) generated.gameObject.SetActive(false); }
        void OnEnable() { if (generated != null) generated.gameObject.SetActive(true); }
    }
}
