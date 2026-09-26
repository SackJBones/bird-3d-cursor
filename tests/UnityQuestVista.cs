using System.Collections.Generic;
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
        root.transform.position-=heading.normalized*2;
        root.transform.rotation=Quaternion.LookRotation(heading,Vector3.up);
        view.nearClipPlane=.05f; view.farClipPlane=5000;
        view.backgroundColor=new Color(.58f,.75f,.86f);
        RenderSettings.ambientMode=AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.58f,.65f,.7f);
        RenderSettings.fog=true; RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=view.backgroundColor; RenderSettings.fogDensity=.00055f;
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
        Box(root,"Terrace",new Vector3(0,-.14f,4.5f),new Vector3(10,.25f,2.2f),floor);
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
        for (int i=-4;i<=4;i++) Box(root,"Meter-spaced terrace seam",new Vector3(i,0,4.5f),new Vector3(.014f,.008f,2.1f),timber);
        for (int i=4;i<=5;i++) Box(root,"Meter-spaced terrace seam",new Vector3(0,0,i),new Vector3(9.8f,.008f,.014f),timber);

        Box(root,"0.75m table",new Vector3(-2,.72f,1),new Vector3(1.5f,.06f,.8f),timber);
        foreach (float x in new[] {-2.6f,-1.4f}) foreach (float z in new[] {.72f,1.28f})
            Box(root,"Table leg",new Vector3(x,.35f,z),new Vector3(.06f,.7f,.06f),timber);
        Box(root,"Chair seat",new Vector3(-2,.45f,-.05f),new Vector3(.48f,.06f,.48f),timber);
        Box(root,"Chair back",new Vector3(-2,.72f,-.27f),new Vector3(.48f,.55f,.05f),timber);
        foreach (float x in new[] {-2.18f,-1.82f}) foreach (float z in new[] {-.23f,.13f})
            Box(root,"Chair leg",new Vector3(x,.22f,z),new Vector3(.05f,.44f,.05f),timber);
        Box(root,"Book",new Vector3(-2.2f,.78f,1),new Vector3(.22f,.04f,.3f),plaster);

        // The eye stays at ordinary room height; the landscape falls away.
        // This scene placement is never an input to Bird's hand geometry.
        Box(root,"Clifftop foundation",new Vector3(0,-75,-4),new Vector3(18,149.7f,19),hill);
        Valley(root,grass,water,floor);
        Box(root,"Terrace handrail",new Vector3(0,.95f,5.5f),new Vector3(10,.07f,.07f),timber);
        foreach(float x in new[]{-4.8f,-2.4f,0,2.4f,4.8f})
            Box(root,"Terrace rail post",new Vector3(x,.48f,5.5f),new Vector3(.07f,.96f,.07f),timber);
        for (int i=0;i<14;i++)
        {
            float x=(i%2==0 ? -1 : 1)*(35+i*17), z=90+i*37, height=5+(i%4)*2;
            Box(root,"Tree trunk",new Vector3(x,-149+height*.35f,z),new Vector3(.3f,height*.7f,.3f),timber);
            Shape(root,"Tree canopy",PrimitiveType.Sphere,new Vector3(x,-149+height*.8f,z),new Vector3(height*.7f,height*.65f,height*.7f),leaf);
        }
        Tower(root,new Vector3(-75,-149,160),4,12,building,glass,"Village house");
        Tower(root,new Vector3(65,-149,330),10,25,building,glass,"Lakeside building");
        Tower(root,new Vector3(-120,-149,800),24,65,building,glass,"Far valley tower");
        // Overlapping near shoulders and distant peaks make parallax and scale
        // legible without labels. The broad valley stays open down the middle.
        Mountain(root,"Near left shoulder",new Vector3(-350,-149,350),400,260,460,hill);
        Mountain(root,"Near right shoulder",new Vector3(450,-149,550),420,370,520,hill);
        for (int i=0;i<9;i++)
            Mountain(root,"Distant mountain",new Vector3(-1300+i*320,-149,1350+(i%3)*210),
                520+(i%2)*160,300+(i%4)*85,650,hill);
        return root;
    }

    static void Mountain(GameObject root,string name,Vector3 foot,float width,float height,float depth,Material material)
    {
        // Simple faceted pyramids, with an off-center summit and broad feet.
        var go=new GameObject(name); go.transform.SetParent(root.transform,false); go.transform.localPosition=foot;
        var mesh=new Mesh { name=name };
        Vector3 a=new Vector3(-width*.5f,0,-depth*.5f), b=new Vector3(width*.5f,0,-depth*.5f);
        Vector3 c=new Vector3(width*.5f,0,depth*.5f), d=new Vector3(-width*.5f,0,depth*.5f);
        Vector3 top=new Vector3(-width*.12f,height,depth*.08f);
        mesh.vertices=new[]{a,top,b,b,top,c,c,top,d,d,top,a};
        mesh.triangles=new[]{0,1,2,3,4,5,6,7,8,9,10,11}; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
    }

    static void Tower(GameObject root,Vector3 p,float width,float height,Material wall,Material glass,string name)
    {
        var go=new GameObject(name); go.transform.SetParent(root.transform,false);
        go.transform.localPosition=p+Vector3.up*height*.5f;
        go.transform.localScale=new Vector3(width,height,width*.65f);
        var vertices=new List<Vector3>(); var walls=new List<int>(); var windows=new List<int>();
        // Five closed faces; the front is partitioned into wall/window cells.
        // No backing wall exists under a window, even at kilometer distances.
        Quad(vertices,walls,new Vector3(-.5f,-.5f,.5f),new Vector3(-.5f,.5f,.5f),new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,-.5f,-.5f));
        Quad(vertices,walls,new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(.5f,.5f,.5f),new Vector3(.5f,-.5f,.5f));
        Quad(vertices,walls,new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f),new Vector3(-.5f,-.5f,.5f));
        Quad(vertices,walls,new Vector3(-.5f,.5f,-.5f),new Vector3(-.5f,.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(.5f,.5f,-.5f));
        Quad(vertices,walls,new Vector3(-.5f,-.5f,.5f),new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,-.5f,.5f));
        var levels=new List<float> { 0 };
        for(int i=1;i<Mathf.FloorToInt(height/3);i++) { levels.Add(i*3-.6f); levels.Add(i*3+.6f); }
        levels.Add(height);
        float[] columns={-.5f,-.36f,.36f,.5f};
        for(int y=0;y<levels.Count-1;y++) for(int x=0;x<3;x++)
        {
            float bottom=levels[y]/height-.5f, top=levels[y+1]/height-.5f;
            Quad(vertices,x==1 && y%2==1 ? windows : walls,
                new Vector3(columns[x],bottom,-.5f),new Vector3(columns[x],top,-.5f),
                new Vector3(columns[x+1],top,-.5f),new Vector3(columns[x+1],bottom,-.5f));
        }
        SurfaceMesh(go,vertices,new[]{walls,windows},new[]{wall,glass});
    }
    static void Valley(GameObject root,Material grass,Material water,Material road)
    {
        var go=new GameObject("Valley floor"); go.transform.SetParent(root.transform,false);
        go.transform.localPosition=Vector3.down*149;
        var vertices=new List<Vector3>();
        var regions=new[]{new List<int>(),new List<int>(),new List<int>()};
        float[] xs={-1800,-247.5f,-242.5f,-215,215,1800};
        float[] zs={-500,-5,275,805,1045,2400};
        for(int z=0;z<zs.Length-1;z++) for(int x=0;x<xs.Length-1;x++)
        {
            float mx=(xs[x]+xs[x+1])*.5f, mz=(zs[z]+zs[z+1])*.5f;
            int region=mx>-215 && mx<215 && mz>275 && mz<805 ? 1 :
                mx>-247.5f && mx<-242.5f && mz>-5 && mz<1045 ? 2 : 0;
            Quad(vertices,regions[region],new Vector3(xs[x],0,zs[z]),new Vector3(xs[x],0,zs[z+1]),
                new Vector3(xs[x+1],0,zs[z+1]),new Vector3(xs[x+1],0,zs[z]));
        }
        SurfaceMesh(go,vertices,regions,new[]{grass,water,road});
    }
    static void Quad(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        int n=vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2);
        triangles.Add(n); triangles.Add(n+2); triangles.Add(n+3);
    }
    static void SurfaceMesh(GameObject go,List<Vector3> vertices,List<int>[] triangles,Material[] materials)
    {
        var mesh=new Mesh { name=go.name }; mesh.SetVertices(vertices); mesh.subMeshCount=triangles.Length;
        for(int i=0;i<triangles.Length;i++) mesh.SetTriangles(triangles[i],i);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterials=materials;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
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
}
