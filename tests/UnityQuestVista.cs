using UnityEngine;
using UnityEngine.Rendering;

// A meter-scale test setting owned by the demo, independent of Bird geometry.
public static class UnityQuestVista
{
    public static GameObject Create(Camera view)
    {
        var root = new GameObject("Bird scale-reference house and vista");
        // Environment placement may use the view; geometric hand laws do not.
        // A seated/device-origin rig gets a consistent illustrative 1.65m eye.
        root.transform.position = view.transform.position-Vector3.up*1.65f;
        Vector3 heading=Vector3.ProjectOnPlane(view.transform.forward,Vector3.up);
        if (heading.sqrMagnitude<.01f) heading=Vector3.forward;
        root.transform.rotation=Quaternion.LookRotation(heading,Vector3.up);
        view.backgroundColor=new Color(.58f,.75f,.86f);
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.58f,.65f,.7f);
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=view.backgroundColor; RenderSettings.fogDensity=.0014f;
        var sun=new GameObject("Soft daylight").AddComponent<Light>();
        sun.transform.SetParent(root.transform,false);
        sun.type=LightType.Directional; sun.color=new Color(1,.91f,.77f); sun.intensity=1.1f;
        sun.shadows=LightShadows.None; sun.transform.localRotation=Quaternion.Euler(45,-35,0);

        Material plaster=Surface("Warm plaster",new Color(.8f,.77f,.68f));
        Material timber=Surface("Timber",new Color(.22f,.15f,.105f));
        Material floor=Surface("Stone",new Color(.56f,.54f,.47f));
        Material grass=Surface("Garden",new Color(.38f,.48f,.32f));
        Material water=Surface("Lake",new Color(.23f,.45f,.53f));
        Material leaf=Surface("Tree canopy",new Color(.22f,.36f,.25f));
        Material hill=Surface("Far hills",new Color(.35f,.45f,.48f));
        Material building=Surface("Landmark stone",new Color(.71f,.69f,.58f));
        Material glass=Surface("Windows",new Color(.22f,.35f,.4f));

        Box(root,"House floor",new Vector3(0,-.1f,0),new Vector3(8,.2f,7),floor);
        Box(root,"Terrace",new Vector3(0,-.14f,6),new Vector3(10,.25f,5),floor);
        Box(root,"Back wall",new Vector3(0,1.65f,-3.5f),new Vector3(8,3.3f,.2f),plaster);
        Box(root,"Left wall",new Vector3(-4,1.65f,0),new Vector3(.2f,3.3f,7),plaster);
        Box(root,"Right wall lower",new Vector3(4,.5f,0),new Vector3(.2f,1,7),plaster);
        Box(root,"Right wall upper",new Vector3(4,3.05f,0),new Vector3(.2f,.5f,7),plaster);
        for (int i=0;i<3;i++) Box(root,"Window mullion",new Vector3(4,1.9f,-3+i*3),new Vector3(.22f,1.8f,.15f),timber);
        foreach (float x in new[] {-3.8f,3.8f})
        {
            Box(root,"Front timber post",new Vector3(x,1.8f,3.4f),new Vector3(.18f,3.6f,.18f),timber);
            var roof=Box(root,"Pitched roof",new Vector3(x*.5f,3.9f,0),new Vector3(4.5f,.15f,7.8f),timber);
            roof.transform.localRotation=Quaternion.Euler(0,0,x<0 ? 20 : -20);
        }
        Box(root,"Front lintel",new Vector3(0,3.55f,3.4f),new Vector3(8,.2f,.2f),timber);
        Box(root,"Roof ridge",new Vector3(0,4.6f,0),new Vector3(.18f,.18f,7.8f),timber);
        for (int i=-4;i<=4;i++) Box(root,"Meter-spaced terrace seam",new Vector3(i,0,6),new Vector3(.014f,.008f,4.8f),timber);
        for (int i=4;i<=8;i++) Box(root,"Meter-spaced terrace seam",new Vector3(0,0,i),new Vector3(9.8f,.008f,.014f),timber);

        Box(root,"0.75m table",new Vector3(-2,.72f,1),new Vector3(1.5f,.06f,.8f),timber);
        foreach (float x in new[] {-2.6f,-1.4f}) foreach (float z in new[] {.72f,1.28f})
            Box(root,"Table leg",new Vector3(x,.35f,z),new Vector3(.06f,.7f,.06f),timber);
        Box(root,"Chair seat",new Vector3(-2,.45f,-.05f),new Vector3(.48f,.06f,.48f),timber);
        Box(root,"Chair back",new Vector3(-2,.72f,-.27f),new Vector3(.48f,.55f,.05f),timber);
        foreach (float x in new[] {-2.18f,-1.82f}) foreach (float z in new[] {-.23f,.13f})
            Box(root,"Chair leg",new Vector3(x,.22f,z),new Vector3(.05f,.44f,.05f),timber);
        Box(root,"Book",new Vector3(-2.2f,.78f,1),new Vector3(.22f,.04f,.3f),plaster);

        Box(root,"Garden",new Vector3(0,-.3f,15),new Vector3(100,.3f,70),grass);
        Box(root,"Lake",new Vector3(0,-1,125),new Vector3(350,.2f,170),water);
        Box(root,"Across-lake shore",new Vector3(0,-.4f,275),new Vector3(650,.8f,150),grass);
        Box(root,"Garden path",new Vector3(0,-.13f,18),new Vector3(2,.04f,21),floor);
        for (int i=0;i<6;i++)
        {
            float x=(i%2==0 ? -1 : 1)*(8+i*2), z=13+i*4, height=4+i*.4f;
            Box(root,"Tree trunk",new Vector3(x,height*.35f,z),new Vector3(.25f,height*.7f,.25f),timber);
            Shape(root,"Tree canopy",PrimitiveType.Sphere,new Vector3(x,height*.8f,z),new Vector3(height*.7f,height*.65f,height*.7f),leaf);
        }
        Tower(root,new Vector3(-10,0,30),4,12,building,glass,"30 m / 12 m tall");
        Tower(root,new Vector3(25,0,100),10,25,building,glass,"100 m / 25 m tall");
        Tower(root,new Vector3(-10,0,300),24,65,building,glass,"300 m / 65 m tall");
        Label(root,"10 m",new Vector3(0,.65f,10),.08f);
        Box(root,"Ten meter marker",new Vector3(0,.25f,10),new Vector3(.08f,.5f,.08f),timber);
        for (int i=0;i<7;i++)
            Shape(root,"Distant ridge",PrimitiveType.Sphere,new Vector3(-420+i*135,-35,550+(i%2)*100),
                new Vector3(250,140+(i%3)*60,230),hill);
        return root;
    }

    static void Tower(GameObject root,Vector3 p,float width,float height,Material wall,Material glass,string name)
    {
        Box(root,name.Replace("/","-"),p+Vector3.up*height*.5f,new Vector3(width,height,width*.65f),wall);
        for (int i=1;i<Mathf.FloorToInt(height/3);i++)
            Box(root,"Landmark window band",p+new Vector3(0,i*3,-width*.325f-.03f),new Vector3(width*.72f,1.2f,.06f),glass);
        Label(root,name,p+new Vector3(0,height+height*.07f,-width*.4f),height*.025f);
    }
    static Material Surface(string name,Color color)
    {
        var m=new Material(Shader.Find("Standard")) { name=name, color=color };
        m.SetFloat("_Glossiness",0); return m;
    }
    static GameObject Box(GameObject root,string name,Vector3 position,Vector3 scale,Material material)
    { return Shape(root,name,PrimitiveType.Cube,position,scale,material); }
    static GameObject Shape(GameObject root,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(root.transform,false);
        go.transform.localPosition=position; go.transform.localScale=scale;
        var renderer=go.GetComponent<Renderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        return go;
    }
    static void Label(GameObject root,string text,Vector3 position,float size)
    {
        var label=new GameObject(text+" label").AddComponent<TextMesh>(); label.transform.SetParent(root.transform,false);
        label.transform.localPosition=position; label.text=text; label.anchor=TextAnchor.MiddleCenter;
        label.fontSize=64; label.characterSize=size; label.color=new Color(.13f,.2f,.23f);
    }
}
