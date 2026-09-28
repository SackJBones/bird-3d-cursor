#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.Core;
using VRC.SDK3.Components;

// Explicit creation never overwrites a scene. Mesh updates preserve prefab/scene
// transforms, added artwork and overrides. Each spatial region is an ordinary prefab.
public static class BirdCoastalWorldAuthoring
{
    public const string ScenePath="Assets/BirdWorld/Scenes/BirdCoastalWorld.unity";
    public const string Folder="Assets/BirdWorld/CoastalWorld";
    static BirdCoastalWorldProfile profile;
    static Transform region;
    static Material white,stone,wood,cushion,sea,leaf,ink,glow,rock;
    static readonly List<Vector3> vertices=new List<Vector3>();
    static readonly List<int> triangles=new List<int>();
    static int meshNumber;

    [MenuItem("Bird/Coastal world/Create authored world")]
    public static void Create()
    {
        if(File.Exists(ScenePath))throw new InvalidOperationException("World already exists. Edit its prefab instances, or update profile meshes; creation never replaces authored work.");
        Prepare();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var descriptor=new GameObject("VRCWorld").AddComponent<VRCSceneDescriptor>();
        descriptor.gameObject.AddComponent<PipelineManager>();
        var spawn=new GameObject("Arrival spawn").transform;spawn.position=new Vector3(0,.05f,-13);
        descriptor.spawns=new[]{spawn};descriptor.RespawnHeightY=-45;
        var reference=new GameObject("World camera settings").AddComponent<Camera>();
        reference.enabled=false;reference.nearClipPlane=.03f;reference.farClipPlane=10000;
        reference.clearFlags=CameraClearFlags.SolidColor;reference.backgroundColor=new Color(.48f,.68f,.78f);
        descriptor.ReferenceCamera=reference.gameObject;
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;
        sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.color=Color.white;sun.intensity=.85f;sun.shadows=LightShadows.Hard;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.45f,.45f);
        RenderSettings.skybox=null;RenderSettings.fog=false;
        Arrival();SocialWings();Terraces();LowerWater();Landscape();Anchors();
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
    }
    static void Prepare()
    {
        foreach(string path in new[]{Folder,Folder+"/Meshes",Folder+"/Materials",Folder+"/Prefabs"})Directory.CreateDirectory(path);
        AssetDatabase.Refresh();
        profile=AssetDatabase.LoadAssetAtPath<BirdCoastalWorldProfile>(Folder+"/WorldProfile.asset");
        if(profile==null){profile=ScriptableObject.CreateInstance<BirdCoastalWorldProfile>();AssetDatabase.CreateAsset(profile,Folder+"/WorldProfile.asset");}
        white=Mat("Plaster",profile.plaster);stone=Mat("Warm stone",profile.stone);wood=Mat("Warm floor",profile.wood);
        cushion=Mat("Muted sage cushions",new Color(.40f,.52f,.49f));sea=Mat("Water",profile.sea);
        leaf=Mat("Coastal green",profile.foliage);ink=Mat("Dark bronze",new Color(.17f,.21f,.20f));glow=Mat("Warm lantern",new Color(1,.72f,.28f),true);rock=Mat("Coastal rock",new Color(.38f,.46f,.48f));
        UpdateProfileMeshes();
    }
    [MenuItem("Bird/Coastal world/Update profile meshes only")]
    public static void UpdateProfileMeshes()
    {
        profile=AssetDatabase.LoadAssetAtPath<BirdCoastalWorldProfile>(Folder+"/WorldProfile.asset");
        if(profile==null)throw new InvalidOperationException("Create/select the world profile first.");
        ValidateProfile();
        SaveMesh("Arrival vault",Vault(profile.arrivalHalfWidth,profile.vaultHeight,profile.vaultSpring,profile.arrivalLength,48));
        SaveMesh("Circular threshold",Portal(profile.arrivalHalfWidth,profile.thresholdWallHeight,profile.thresholdRadius,profile.thresholdCenterHeight,profile.silhouetteSegments,profile.vaultSpring,profile.vaultHeight+.25f));
        SaveMesh("Branching support",Column(profile.supportProfile,profile.silhouetteSegments));
        SaveMesh("Upper inhabited slab",SlabWithVoid(profile.upperDeckRadii.x,profile.upperDeckRadii.y,3.2f,profile.silhouetteSegments,new Rect(-10,-13,4,15),-15));
        SaveMesh("Lookout slab",SlabWithVoid(profile.lookoutRadii.x,profile.lookoutRadii.y,2.2f,profile.silhouetteSegments,new Rect(-12,-.8f,16,3.6f)));
        // Refresh cooking in an already-open scene; persisted prefab references
        // continue to point at the same mesh assets/GUIDs when loaded later.
        foreach(var collider in UnityEngine.Object.FindObjectsOfType<MeshCollider>())
        {
            var mesh=collider.sharedMesh;
            if(mesh!=null && AssetDatabase.GetAssetPath(mesh).StartsWith(Folder+"/Meshes/",StringComparison.Ordinal))
            {collider.sharedMesh=null;collider.sharedMesh=mesh;}
        }
        AssetDatabase.SaveAssets();
    }
    static void ValidateProfile()
    {
        if(profile.silhouetteSegments<24 || profile.silhouetteSegments>128)throw new InvalidOperationException("Silhouette segments must be 24–128.");
        foreach(float value in new[]{profile.arrivalHalfWidth,profile.arrivalLength,profile.vaultSpring,profile.vaultHeight,profile.thresholdRadius,profile.thresholdCenterHeight,profile.thresholdWallHeight,profile.upperDeckRadii.x,profile.upperDeckRadii.y,profile.lookoutRadii.x,profile.lookoutRadii.y})
            if(float.IsNaN(value)||float.IsInfinity(value)||value<=0)throw new InvalidOperationException("Profile dimensions must be finite and positive.");
        if(profile.arrivalHalfWidth<=profile.thresholdRadius || profile.thresholdWallHeight<profile.thresholdCenterHeight+profile.thresholdRadius || profile.supportProfile==null || profile.supportProfile.Length<2)
            throw new InvalidOperationException("The circular threshold must fit within its wall, and the support needs at least two rings.");
        for(int i=0;i<profile.supportProfile.Length;i++)
        {
            var ring=profile.supportProfile[i];
            if(float.IsNaN(ring.x)||float.IsInfinity(ring.x)||float.IsNaN(ring.y)||float.IsInfinity(ring.y)||float.IsNaN(ring.z)||float.IsInfinity(ring.z)||ring.z<=0 || (i>0&&ring.y<=profile.supportProfile[i-1].y))
                throw new InvalidOperationException("Support rings need finite offsets/heights, positive radii and strictly increasing heights.");
        }
    }
    static void Begin(string name){region=new GameObject(name).transform;meshNumber=0;}
    static void End()
    {
        string path=Folder+"/Prefabs/"+region.name+".prefab";
        if(File.Exists(path))throw new InvalidOperationException("Refusing to replace an authored region prefab: "+path);
        PrefabUtility.SaveAsPrefabAssetAndConnect(region.gameObject,path,InteractionMode.AutomatedAction);
    }
    static void Arrival()
    {
        Begin("01 Arrival cavern");
        Box("Plain standing floor",new Vector3(0,-.25f,-7),new Vector3(30,.5f,22),stone);
        Box("Plain rear wall",new Vector3(0,6,-18),new Vector3(30,13,.6f),white);
        MeshObject("Smooth cavern vault",Load("Arrival vault"),new Vector3(0,0,-7),white,true);
        MeshObject("Circular architectural cutout",Load("Circular threshold"),new Vector3(0,0,4),white,true);
        for(int s=-1;s<=1;s+=2)
        {
            var door=MeshObject("Side wing opening "+s,Portal(3,4.3f,2.6f,1.65f,48),new Vector3(s*15,0,1),white,true);
            door.transform.rotation=Quaternion.Euler(0,90,0);
            Box("Rear side infill "+s,new Vector3(s*15,2,-10),new Vector3(.4f,4,16),white);
            Box("Wing connector floor "+s,new Vector3(s*16.5f,-.2f,1),new Vector3(3,.4f,6),stone);
            Box("Wing connector ceiling "+s,new Vector3(s*16.5f,4.4f,1),new Vector3(3,.4f,6),white);
        }
        Stairs("Threshold steps",new Vector3(0,0,1.5f),new Vector3(0,1,4),10,5);
        Box("Onboarding plinth",new Vector3(0,.5f,-1.4f),new Vector3(1.6f,1,1.2f),white);
        var form=MeshObject("Bird gesture study",Ribbon(.6f,.18f,42),new Vector3(0,1.6f,-1.4f),white,false);form.transform.rotation=Quaternion.Euler(20,0,25);
        Text("Bird identity",new Vector3(0,1.13f,-2.02f),"B I R D",.22f,ink);
        End();
    }
    static void SocialWings()
    {
        Begin("02 Arrival social wings");
        Room("Lantern room",new Vector3(-21,0,1),90,false);
        Room("Fold gallery",new Vector3(21,0,1),-90,true);
        End();
    }
    static void Terraces()
    {
        Begin("03 Supported coastal terraces");
        MeshObject("Main terrace around conversation pit",HorizontalHole(-15,17,4,34,new Vector2(-2,26),5.2f,1,96),Vector3.zero,stone,true);
        MeshObject("Main branching support",Load("Branching support"),new Vector3(3,1,14),white,true);
        MeshObject("Upper inhabited overhang",Load("Upper inhabited slab"),new Vector3(0,13,20),white,true);
        var support=MeshObject("Lookout branching support",Load("Branching support"),new Vector3(8,1,36),white,true);support.transform.localScale=new Vector3(.35f,20f/12,.35f);
        MeshObject("High lookout",Load("Lookout slab"),new Vector3(4,21,31),white,true);
        Stairs("Cliff stair lower",new Vector3(-12,1,6),new Vector3(-12,6,19),3.4f,28);
        Box("Cliff landing",new Vector3(-11.1f,5.8f,20.3f),new Vector3(7,.4f,3),stone);
        Stairs("Cliff stair upper",new Vector3(-8,6,20),new Vector3(-8,13,7),3.4f,39);
        Stairs("Lookout stair",new Vector3(-8,13,32),new Vector3(8,21,32),3,45);
        Room("Cliff lantern lounge",new Vector3(-19,6,20),90,false);
        // A substantial occupied cliff-side volume contains the lounge. The
        // broad crowned facade and rear shell join the room to the landscape.
        var shoulder=MeshObject("Inhabited cliff shoulder",Portal(14,18,4.2f,9,64,13,5),new Vector3(-15.5f,0,20),white,true);shoulder.transform.rotation=Quaternion.Euler(0,90,0);
        var roof=MeshObject("Cliff shoulder vault",Vault(14,5,13,12,48),new Vector3(-21.1f,0,20),white,true);roof.transform.rotation=Quaternion.Euler(0,90,0);
        Box("Cliff shoulder base",new Vector3(-21.1f,2.7f,20),new Vector3(12,5.4f,28),white);
        Box("Cliff shoulder rear",new Vector3(-27.1f,6.5f,20),new Vector3(.5f,13,28),white);
        Box("Lounge threshold",new Vector3(-15,5.8f,20),new Vector3(5,.4f,4),stone);
        GuardEllipse("Upper edge",new Vector3(0,13,20),19,19,64,white,-15);
        GuardEllipse("Lookout edge",new Vector3(4,21,31),12,10,48,white);
        GuardLine("Coast parapet",new Vector3(17,1,5),new Vector3(17,1,34),white);
        GuardLine("Front parapet center",new Vector3(-9,1,34),new Vector3(7,1,34),white);
        GuardLine("Front parapet right",new Vector3(13,1,34),new Vector3(17,1,34),white);
        GuardLine("Upper stair void left",new Vector3(-10,13,7),new Vector3(-10,13,22),white);
        GuardLine("Upper stair void right",new Vector3(-6,13,7),new Vector3(-6,13,22),white);
        GuardLine("Upper stair void end",new Vector3(-10,13,22),new Vector3(-6,13,22),white);
        GuardLine("Lookout stair void side A",new Vector3(-8,21,30.2f),new Vector3(8,21,30.2f),white);
        GuardLine("Lookout stair void side B",new Vector3(-8,21,33.8f),new Vector3(8,21,33.8f),white);
        Pit();End();
    }
    static void Pit()
    {
        Vector3 c=new Vector3(-2,0,26);
        for(int i=0;i<4;i++)MeshObject("Pit shallow step "+i,Ring(5.2f-i*.25f,4.95f-i*.25f,0,360,64,.2f),c+Vector3.up*(.8f-i*.2f),white,true);
        MeshObject("Conversation floor",Slab(4.2f,4.2f,.3f,64),c,wood,true);
        MeshObject("Conversation bench",Ring(3.75f,2.95f,15,255,48,.5f),c+Vector3.up*.5f,white,true);
        MeshObject("Conversation cushion",Ring(3.70f,3.0f,15,255,48,.12f),c+Vector3.up*.62f,cushion,false);
        MeshObject("Conversation back",Ring(3.85f,3.62f,15,255,48,.75f),c+Vector3.up*.95f,white,true);
        Box("Low conversation table",c+new Vector3(0,.3f,0),new Vector3(1.5f,.6f,1.0f),white);
        Sphere("Conversation lamp",c+new Vector3(0,.73f,0),.28f,glow);
    }
    static void LowerWater()
    {
        Begin("04 Lower water and hidden lounge");
        Stairs("Water stair",new Vector3(10,1,34),new Vector3(10,-2,42),3.4f,17);
        Stairs("Discovery stair",new Vector3(-12,1,34),new Vector3(-12,-2,42),3.4f,17);
        Box("Lower promenade",new Vector3(2.5f,-2.25f,43),new Vector3(41,.5f,4),stone);
        Box("Lower overlook",new Vector3(9,-2.25f,50),new Vector3(18,.5f,14),stone);
        Box("Outer water walk",new Vector3(23.5f,-2.25f,49),new Vector3(3,.5f,16),stone);
        Box("Pool front walk",new Vector3(19,-2.25f,56),new Vector3(12,.5f,2),stone);
        Box("Fish water",new Vector3(20,-2.7f,49),new Vector3(4,.25f,12),sea);
        GuardLine("Pool inner edge",new Vector3(18,-2,43),new Vector3(18,-2,55),white);
        GuardLine("Pool outer edge",new Vector3(22,-2,43),new Vector3(22,-2,55),white);
        GuardLine("Sea edge",new Vector3(25,-2,41),new Vector3(25,-2,57),white);
        GuardLine("Lower front edge",new Vector3(0,-2,57),new Vector3(25,-2,57),white);
        for(int i=0;i<16;i++)
        {
            var fish=MeshObject("Fish study "+i,Fish(),new Vector3(19+(i%3)*.9f,-2.47f,44+(i/3)*1.9f),i%3==0?glow:ink,false);
            fish.transform.rotation=Quaternion.Euler(0,(i*71)%360,0);
        }
        Room("Hidden tide room",new Vector3(-19,-2,38),90,true);
        Box("Discovery landing",new Vector3(-16,-2.2f,41),new Vector3(7,.4f,8),stone);
        End();
    }
    static void Room(string name,Vector3 position,float yaw,bool gallery)
    {
        Transform old=region;region=new GameObject(name).transform;region.SetParent(old,false);
        Box("Warm floor",new Vector3(0,-.18f,0),new Vector3(9,.36f,8),wood);
        Box("Quiet back wall",new Vector3(0,3,-4),new Vector3(9,6,.3f),white);
        for(int s=-1;s<=1;s+=2)Box("Room side "+s,new Vector3(s*4.5f,1.25f,0),new Vector3(.3f,2.5f,8),white);
        MeshObject("Room vault",Vault(4.5f,3.5f,2.4f,8,28),Vector3.zero,white,true);
        MeshObject("Room threshold",Portal(4.5f,6,2.6f,1.7f,40),new Vector3(0,0,4),white,true);
        Box("Low table",new Vector3(0,.28f,.1f),new Vector3(1.5f,.56f,1.1f),white);
        Vector3 lamp=gallery?new Vector3(2.2f,2.7f,1.4f):new Vector3(0,2.7f,0);
        Sphere("Warm pendant",lamp,.35f,glow);
        var fill=new GameObject("Unshadowed vertex fill").AddComponent<Light>();fill.transform.SetParent(region,false);fill.transform.localPosition=lamp;
        fill.type=LightType.Point;fill.renderMode=LightRenderMode.ForceVertex;fill.range=7;fill.intensity=1.5f;fill.color=Color.white;fill.shadows=LightShadows.None;
        Box("Pendant cord",lamp+Vector3.up*1.3f,new Vector3(.035f,2.6f,.035f),ink,false);
        for(int s=-1;s<=1;s+=2)
        {
            Box("Sofa base "+s,new Vector3(s*2.4f,.25f,-.5f),new Vector3(1.1f,.5f,2.8f),white);
            Box("Sofa cushion "+s,new Vector3(s*2.4f,.56f,-.5f),new Vector3(1.05f,.13f,2.7f),cushion,false);
            Box("Sofa back "+s,new Vector3(s*2.9f,.9f,-.5f),new Vector3(.2f,.9f,2.8f),white);
        }
        if(gallery)
        {
            Box("Sculpture timber backdrop",new Vector3(0,2.1f,-3.82f),new Vector3(4,3.5f,.08f),wood,false);
            Box("Sculpture plinth",new Vector3(0,.6f,-2.6f),new Vector3(2,1.2f,1),white);
            var sculpture=MeshObject("Folded ribbon sculpture",Ribbon(1.1f,.32f,56),new Vector3(-.25f,2,-2.2f),white,false);sculpture.transform.rotation=Quaternion.Euler(32,28,16);
            Box("Small study plinth",new Vector3(3.1f,.48f,2.2f),new Vector3(.75f,.96f,.75f),white);
            var study=MeshObject("Second folded study",Ribbon(.32f,.1f,32),new Vector3(3.1f,1.45f,2.2f),white,false);study.transform.rotation=Quaternion.Euler(65,0,32);
        }
        else
        {
            Box("Warm back inset",new Vector3(0,2.1f,-3.82f),new Vector3(5,3,.08f),wood,false);
            for(int i=0;i<9;i++)Box("Timber rhythm "+i,new Vector3(-2+i*.5f,2.1f,-3.75f),new Vector3(.05f,3,.05f),stone,false);
        }
        Plant(new Vector3(-3.6f,0,2),.6f);
        region.localPosition=position;region.localRotation=Quaternion.Euler(0,yaw,0);region=old;
    }
    static void Landscape()
    {
        Begin("05 Coastal ridge and distant island");
        Box("Quiet sea",new Vector3(400,-31,150),new Vector3(1600,.4f,1800),sea,false);
        MeshObject("Long steep coastal ridge",Ridge(),Vector3.zero,rock,false);
        MeshObject("Bounded distant cliff island",Island(),new Vector3(260,-31,145),rock,false);
        MeshObject("Island top land",IslandTop(),new Vector3(260,-31,145),leaf,false);
        var shore=MeshObject("Distant shoreline headland",Island(),new Vector3(-15,-31,220),rock,false);shore.transform.localScale=new Vector3(.9f,1.3f,1.8f);
        var shoreTop=MeshObject("Headland top land",IslandTop(),new Vector3(-15,-31,220),leaf,false);shoreTop.transform.localScale=shore.transform.localScale;
        End();
    }
    static void Anchors()
    {
        Begin("06 Experience anchors");
        Anchor("Bird first flight",new Vector3(0,1.8f,-1.4f));
        Anchor("Tabletop Hanoi",new Vector3(10,1,10));Anchor("Building Hanoi",new Vector3(250,37,145));
        Anchor("Mandala clearing",new Vector3(4,13,23));Anchor("Color selector",new Vector3(-6,1,10));
        Anchor("Arrival wing lantern",new Vector3(-19.2f,0,1));Anchor("Arrival wing gallery",new Vector3(19.2f,0,1));
        Anchor("Cliff lounge",new Vector3(-17.2f,6,20));Anchor("Hidden tide lounge",new Vector3(-17.2f,-2,38));
        Anchor("Conversation pit",new Vector3(-2,0,27.8f));Anchor("Water overlook",new Vector3(23.5f,-2,49));
        Anchor("High lookout",new Vector3(4,21,36));End();
    }
    static void Anchor(string name,Vector3 p){var go=new GameObject(name);go.transform.SetParent(region,false);go.transform.localPosition=p;}
    static void Plant(Vector3 p,float size)
    {
        var pot=MeshObject("White planter",Slab(size*.62f,size*.62f,size,12),p+Vector3.up*size,white,true);
        ResetMesh();
        for(int i=0;i<7;i++)
        {
            float a=i*2.39996f;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),side=Vector3.Cross(Vector3.up,d)*size*.28f;
            Vector3 root=Vector3.up*size,mid=root+d*size*.38f+Vector3.up*size*.7f,tip=root+d*size*.95f+Vector3.up*size*(1.2f+(i%3)*.35f);
            Triangle(root,mid-side,tip);Triangle(root,tip,mid+side);Triangle(tip,mid-side,root);Triangle(mid+side,tip,root);
        }
        MeshObject("Coastal leaves",FinishMesh(),p,leaf,false);
    }
    static Material Mat(string name,Color color,bool unlit=false)
    {
        string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m!=null)return m;
        m=new Material(Shader.Find(unlit?"Unlit/Color":"Standard")){name=name,color=color,enableInstancing=true};
        if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.1f);AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject Box(string name,Vector3 p,Vector3 size,Material material,bool collision=true)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(region,false);go.transform.localPosition=p;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;return go;
    }
    static void Sphere(string name,Vector3 p,float radius,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(region,false);go.transform.localPosition=p;go.transform.localScale=Vector3.one*radius*2;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;
    }
    static GameObject MeshObject(string name,Mesh mesh,Vector3 p,Material material,bool collision)
    {
        if(!AssetDatabase.Contains(mesh)){string safe=region.name.Replace("/","-")+"-"+(meshNumber++).ToString("D3")+"-"+name;mesh=SaveMesh(safe,mesh);}
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(region,false);go.transform.localPosition=p;
        go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
        if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;go.isStatic=true;return go;
    }
    static void Text(string name,Vector3 p,string value,float size,Material color)
    {
        var go=new GameObject(name);go.transform.SetParent(region,false);go.transform.localPosition=p;
        var t=go.AddComponent<TextMesh>();t.text=value;t.characterSize=size*.12f;t.fontSize=48;t.anchor=TextAnchor.MiddleCenter;t.color=color.color;
    }
    static void Stairs(string name,Vector3 from,Vector3 to,float width,int count)
    {
        Transform old=region;region=new GameObject(name).transform;region.SetParent(old,false);region.localPosition=from;to-=from;from=Vector3.zero;
        Vector3 delta=to-from,flat=new Vector3(delta.x,0,delta.z);float run=flat.magnitude;Vector3 dir=flat/run;
        float rise=delta.y/count,depth=run/count;
        ResetMesh();
        for(int i=0;i<count;i++)
        {
            float top=rise*(i+1);
            AppendBox(dir*(depth*(i+.5f))+Vector3.up*(top-.16f),new Vector3(width,.32f,depth+.003f),Quaternion.LookRotation(dir));
        }
        AppendBox(to*.5f-Vector3.up*.32f,new Vector3(width,.32f,to.magnitude),Quaternion.LookRotation(to));
        MeshObject("Connected stair treads",FinishMesh(),Vector3.zero,stone,true);
        Vector3 side=Vector3.Cross(Vector3.up,dir)*(width*.5f+.06f);
        GuardLine(name+" outer rail",from+side,to+side,white);GuardLine(name+" inner rail",from-side,to-side,white);
        region=old;
    }
    static void GuardLine(string name,Vector3 a,Vector3 b,Material material)
    {
        Vector3 d=b-a;ResetMesh();AppendBox(d*.5f+Vector3.up*1.05f,new Vector3(.13f,.13f,d.magnitude),Quaternion.LookRotation(d));
        int n=Mathf.CeilToInt(d.magnitude/2);
        for(int i=0;i<=n;i++)AppendBox(d*((float)i/n)+Vector3.up*.53f,new Vector3(.065f,1.06f,.065f),Quaternion.identity);
        MeshObject(name,FinishMesh(),a,material,false);
        // Continuous collision boundary; transparent to rendering, 1.05m tall.
        var barrier=Box(name+" safety boundary",(a+b)*.5f+Vector3.up*.52f,new Vector3(.10f,1.05f,d.magnitude),material);barrier.transform.rotation=Quaternion.LookRotation(d);barrier.GetComponent<Renderer>().enabled=false;
    }
    static void GuardEllipse(string name,Vector3 center,float rx,float rz,int count,Material material,float minZ=float.NegativeInfinity)
    {
        ResetMesh();
        for(int i=0;i<count;i++)
        {
            float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
            Vector3 x=new Vector3(Mathf.Cos(a)*rx,0,Mathf.Max(minZ,Mathf.Sin(a)*rz)),y=new Vector3(Mathf.Cos(b)*rx,0,Mathf.Max(minZ,Mathf.Sin(b)*rz)),d=y-x;
            AppendBox((x+y)*.5f+Vector3.up*1.05f,new Vector3(.13f,.13f,d.magnitude),Quaternion.LookRotation(d));
            AppendBox(x+Vector3.up*.53f,new Vector3(.065f,1.06f,.065f),Quaternion.identity);
        }
        MeshObject(name,FinishMesh(),center,material,false);ResetMesh();
        for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;Vector3 x=new Vector3(Mathf.Cos(a)*rx,0,Mathf.Max(minZ,Mathf.Sin(a)*rz)),y=new Vector3(Mathf.Cos(b)*rx,0,Mathf.Max(minZ,Mathf.Sin(b)*rz));Quad(x,y,y+Vector3.up*1.1f,x+Vector3.up*1.1f);Quad(y,x,x+Vector3.up*1.1f,y+Vector3.up*1.1f);}
        MeshObject(name+" safety boundary",FinishMesh(),center,material,true).GetComponent<Renderer>().enabled=false;
    }
    static Mesh Load(string name){return AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Meshes/"+name+".asset");}
    static Mesh SaveMesh(string name,Mesh mesh)
    {
        string path=Folder+"/Meshes/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        mesh.name=name;if(saved==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        // Update the authored channels through Mesh's public API. Keep the
        // asset identity without copying native upload/collision bookkeeping.
        saved.Clear();
        saved.indexFormat=mesh.indexFormat;
        saved.vertices=mesh.vertices;
        saved.normals=mesh.normals;
        saved.triangles=mesh.triangles;
        saved.bounds=mesh.bounds;
        EditorUtility.SetDirty(saved);
        UnityEngine.Object.DestroyImmediate(mesh);
        return saved;
    }
    static void ResetMesh(){vertices.Clear();triangles.Clear();}
    static void Triangle(Vector3 a,Vector3 b,Vector3 c)
    {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
    static void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
    static void AppendBox(Vector3 center,Vector3 size,Quaternion rotation)
    {
        Func<float,float,float,Vector3> p=(x,y,z)=>center+rotation*Vector3.Scale(new Vector3(x,y,z),size*.5f);
        var a=p(-1,-1,-1);var b=p(1,-1,-1);var c=p(1,1,-1);var d=p(-1,1,-1);var e=p(-1,-1,1);var f=p(1,-1,1);var g=p(1,1,1);var h=p(-1,1,1);
        Quad(a,d,c,b);Quad(e,f,g,h);Quad(a,e,h,d);Quad(b,c,g,f);Quad(d,h,g,c);Quad(a,b,f,e);
    }
    static Mesh FinishMesh(bool smooth=false)
    {
        var m=new Mesh();m.SetVertices(vertices);m.SetTriangles(triangles,0);m.RecalculateNormals();
        if(smooth){var normals=m.normals;var sums=new Dictionary<Vector3,Vector3>();for(int i=0;i<vertices.Count;i++){if(!sums.ContainsKey(vertices[i]))sums[vertices[i]]=Vector3.zero;sums[vertices[i]]+=normals[i];}for(int i=0;i<vertices.Count;i++)normals[i]=sums[vertices[i]].normalized;m.normals=normals;}
        m.RecalculateBounds();return m;
    }
    static Mesh Portal(float halfWidth,float height,float radius,float cy,int n,float crownSpring=-1,float crownHeight=0)
    {
        ResetMesh();
        for(int i=0;i<n;i++)
        {
            float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;Vector3 u=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0),v=new Vector3(Mathf.Cos(b),Mathf.Sin(b),0),c=new Vector3(0,cy,0);
            float ru=PortalOuterRadius(u,halfWidth,height,cy,crownSpring,crownHeight);
            float rv=PortalOuterRadius(v,halfWidth,height,cy,crownSpring,crownHeight);
            Vector3 ia=c+u*radius,ib=c+v*radius,oa=c+u*Mathf.Max(radius,ru),ob=c+v*Mathf.Max(radius,rv);
            Quad(ia,ib,ob,oa);Quad(oa+Vector3.forward*.4f,ob+Vector3.forward*.4f,ib+Vector3.forward*.4f,ia+Vector3.forward*.4f);
            Quad(ia,ia+Vector3.forward*.4f,ib+Vector3.forward*.4f,ib);
        }return FinishMesh();
    }
    static float PortalOuterRadius(Vector3 d,float halfWidth,float height,float cy,float spring,float crown)
    {
        float r=Mathf.Min(halfWidth/Mathf.Max(Mathf.Abs(d.x),.00001f),(d.y>0?height-cy:cy+.5f)/Mathf.Max(Mathf.Abs(d.y),.00001f));
        if(spring>=0 && crown>0 && cy+d.y*r>spring)
        {
            float dy=cy-spring,a=d.x*d.x/(halfWidth*halfWidth)+d.y*d.y/(crown*crown),b=2*dy*d.y/(crown*crown),c=dy*dy/(crown*crown)-1;
            r=Mathf.Min(r,(-b+Mathf.Sqrt(Mathf.Max(0,b*b-4*a*c)))/(2*a));
        }
        return r;
    }
    static Mesh Vault(float rx,float ry,float cy,float length,int n)
    {
        ResetMesh();for(int i=0;i<n;i++)
        {float a=i*Mathf.PI/n,b=(i+1)*Mathf.PI/n;Vector3 x=new Vector3(Mathf.Cos(a)*rx,cy+Mathf.Sin(a)*ry,-length*.5f),y=new Vector3(Mathf.Cos(b)*rx,cy+Mathf.Sin(b)*ry,-length*.5f);Quad(x,x+Vector3.forward*length,y+Vector3.forward*length,y);Quad(x+Vector3.up*.25f,y+Vector3.up*.25f,y+Vector3.forward*length+Vector3.up*.25f,x+Vector3.forward*length+Vector3.up*.25f);}
        return FinishMesh(true);
    }
    static Mesh Column(Vector3[] rings,int n)
    {
        var curve=new List<Vector3>();
        for(int j=0;j<rings.Length-1;j++)for(int k=0;k<6;k++)
        {
            float t=k/6f;Vector3 p=rings[Mathf.Max(0,j-1)],a=rings[j],b=rings[j+1],q=rings[Mathf.Min(rings.Length-1,j+2)];
            Vector3 v=.5f*(2*a+(b-p)*t+(2*p-5*a+4*b-q)*t*t+(-p+3*a-3*b+q)*t*t*t);
            // Monotone height avoids looped sections for strongly edited profiles.
            v.y=Mathf.Lerp(a.y,b.y,t);v.z=Mathf.Max(.05f,v.z);curve.Add(v);
        }
        curve.Add(rings[rings.Length-1]);ResetMesh();for(int j=0;j<curve.Count-1;j++)for(int i=0;i<n;i++)
        {float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;Vector3 p=curve[j],q=curve[j+1];Quad(new Vector3(p.x+p.z*Mathf.Cos(a),p.y,p.z*Mathf.Sin(a)),new Vector3(q.x+q.z*Mathf.Cos(a),q.y,q.z*Mathf.Sin(a)),new Vector3(q.x+q.z*Mathf.Cos(b),q.y,q.z*Mathf.Sin(b)),new Vector3(p.x+p.z*Mathf.Cos(b),p.y,p.z*Mathf.Sin(b)));}
        return FinishMesh(true);
    }
    static Mesh Slab(float rx,float rz,float depth,int n)
    {
        ResetMesh();for(int i=0;i<n;i++)
        {float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;var u=new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);var v=new Vector3(Mathf.Cos(b)*rx,0,Mathf.Sin(b)*rz);Triangle(Vector3.zero,v,u);Triangle(Vector3.down*depth,u-Vector3.up*depth,v-Vector3.up*depth);Quad(u,v,v-Vector3.up*depth,u-Vector3.up*depth);}
        return FinishMesh();
    }
    static Mesh Ring(float outer,float inner,float start,float span,int n,float depth)
    {
        ResetMesh();for(int i=0;i<n;i++)
        {float a=(start+span*i/n)*Mathf.Deg2Rad,b=(start+span*(i+1)/n)*Mathf.Deg2Rad;Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));Quad(x*inner,y*inner,y*outer,x*outer);Quad(x*outer,y*outer,y*outer-Vector3.up*depth,x*outer-Vector3.up*depth);Quad(x*inner-Vector3.up*depth,y*inner-Vector3.up*depth,y*inner,x*inner);}
        return FinishMesh();
    }
    static Mesh SlabWithVoid(float rx,float rz,float depth,int n,Rect hole,float minZ=float.NegativeInfinity)
    {
        ResetMesh();for(int i=0;i<n;i++)
        {
            float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;
            Vector3 u=new Vector3(Mathf.Cos(a)*rx,0,Mathf.Max(minZ,Mathf.Sin(a)*rz)),v=new Vector3(Mathf.Cos(b)*rx,0,Mathf.Max(minZ,Mathf.Sin(b)*rz));
            var triangle=new List<Vector3>{Vector3.zero,v,u};
            var parts=new[]{Clip(triangle,0,hole.xMin,false),Clip(triangle,0,hole.xMax,true),Clip(Clip(Clip(triangle,0,hole.xMin,true),0,hole.xMax,false),2,hole.yMin,false),Clip(Clip(Clip(triangle,0,hole.xMin,true),0,hole.xMax,false),2,hole.yMax,true)};
            foreach(var poly in parts)for(int j=1;j<poly.Count-1;j++){Triangle(poly[0],poly[j],poly[j+1]);Triangle(poly[0]-Vector3.up*depth,poly[j+1]-Vector3.up*depth,poly[j]-Vector3.up*depth);}
            Vector3 pu=u,pv=v;foreach(var band in new[]{new Vector2(.3f,1.025f),new Vector2(depth*.65f,1.035f),new Vector2(depth,1)}){Vector3 nu=u*band.y-Vector3.up*band.x,nv=v*band.y-Vector3.up*band.x;Quad(pu,pv,nv,nu);pu=nu;pv=nv;}
        }
        return FinishMesh();
    }
    static List<Vector3> Clip(List<Vector3> points,int axis,float bound,bool above)
    {
        var result=new List<Vector3>();if(points.Count==0)return result;
        for(int i=0;i<points.Count;i++)
        {Vector3 a=points[i],b=points[(i+1)%points.Count];bool ia=above?a[axis]>=bound:a[axis]<=bound,ib=above?b[axis]>=bound:b[axis]<=bound;if(ia)result.Add(a);if(ia!=ib)result.Add(Vector3.Lerp(a,b,(bound-a[axis])/(b[axis]-a[axis])));}
        return result;
    }
    static Mesh HorizontalHole(float minX,float maxX,float minZ,float maxZ,Vector2 center,float radius,float height,int n)
    {
        ResetMesh();for(int i=0;i<n;i++)
        {float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;Vector3 c=new Vector3(center.x,height,center.y);Vector3 u=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),v=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));float ru=Mathf.Min((u.x>0?maxX-center.x:center.x-minX)/Mathf.Max(Mathf.Abs(u.x),.00001f),(u.z>0?maxZ-center.y:center.y-minZ)/Mathf.Max(Mathf.Abs(u.z),.00001f));float rv=Mathf.Min((v.x>0?maxX-center.x:center.x-minX)/Mathf.Max(Mathf.Abs(v.x),.00001f),(v.z>0?maxZ-center.y:center.y-minZ)/Mathf.Max(Mathf.Abs(v.z),.00001f));Quad(c+u*radius,c+v*radius,c+v*rv,c+u*ru);}
        return FinishMesh();
    }
    static Mesh Ribbon(float r,float width,int n)
    {
        ResetMesh();for(int i=0;i<n;i++)
        {float a=i*Mathf.PI*2/n,b=(i+1)*Mathf.PI*2/n;Func<float,float,Vector3> p=(t,s)=>new Vector3((r+s*Mathf.Cos(t*1.5f))*Mathf.Cos(t),(r+s*Mathf.Cos(t*1.5f))*Mathf.Sin(t),s*Mathf.Sin(t*1.5f));var x=p(a,-width);var y=p(a,width);var z=p(b,width);var w=p(b,-width);Quad(x,y,z,w);Quad(w,z,y,x);}
        return FinishMesh();
    }
    static Mesh Ridge()
    {
        ResetMesh();float[] xs={-140,-70,-40,-32,-29},ys={60,105,85,25,-38};
        for(int j=0;j<24;j++)for(int k=0;k<4;k++)
        {Func<int,int,Vector3> p=(i,l)=>{float z=-500+i*45,bend=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Abs(z)-80)/160))*35*Mathf.Sin(z*.004f);return new Vector3(xs[l]+Mathf.Sin(i*.7f)*3+bend,ys[l]+Mathf.Sin(i*1.7f+l)*8,z);};Quad(p(j,k),p(j+1,k),p(j+1,k+1),p(j,k+1));}
        return FinishMesh();
    }
    static Mesh Island()
    {
        ResetMesh();for(int j=0;j<3;j++)for(int i=0;i<18;i++)Quad(IslandRing(i,j),IslandRing(i,j+1),IslandRing(i+1,j+1),IslandRing(i+1,j));
        return FinishMesh();
    }
    static Vector3 IslandRing(int index,int ring)
    {
        float t=(index%18)*Mathf.PI*2/18;
        float radius=new[]{1,.91f,.76f,.50f}[ring]*(1+.15f*Mathf.Sin(t*3)+.1f*Mathf.Cos(t*5));
        float y=new[]{0,10,49,67}[ring]+(ring==0?0:9*Mathf.Sin(t+.4f)+3*Mathf.Sin(t*3));
        return new Vector3(Mathf.Cos(t)*65*radius+ring*4,y,Mathf.Sin(t)*45*radius);
    }
    static Mesh IslandTop()
    {
        ResetMesh();for(int i=0;i<18;i++)Triangle(new Vector3(8,68,0),IslandRing(i+1,3),IslandRing(i,3));return FinishMesh();
    }
    static Mesh Fish()
    {
        ResetMesh();Quad(new Vector3(0,0,.5f),new Vector3(.18f,.02f,0),new Vector3(0,0,-.32f),new Vector3(-.18f,.02f,0));Triangle(new Vector3(0,0,-.25f),new Vector3(-.2f,0,-.5f),new Vector3(.2f,0,-.5f));return FinishMesh();
    }
}
#endif
