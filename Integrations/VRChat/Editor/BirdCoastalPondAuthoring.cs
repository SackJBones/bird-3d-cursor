#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit offline authoring; exported content is ordinary editable meshes and colliders.
public static class BirdCoastalPondAuthoring
{
    public const string Folder = "Assets/BirdWorld/CoastalPond";
    public const string ProfilePath = Folder + "/Pond profile.asset";
    public const string PrefabPath = Folder + "/Curved pond promenade.prefab";
    static readonly List<Vector3> vertices = new List<Vector3>();
    static readonly List<int> indices = new List<int>();
    static List<Vector2> center, right, waterLoop, outerLoop;
    static BirdCoastalPondProfile profile;
    static float outer;

    [MenuItem("Bird/Coastal world/Add curved pond once")]
    public static void Add()
    {
        if(File.Exists(PrefabPath))throw new InvalidOperationException("Pond already authored. Edit saved prefab/profile; do not recreate it.");
        var scene=EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
        var parent=GameObject.Find("04 Lower water and hidden lounge").transform;
        string[] replaced={"Lower overlook","Outer water walk","Pool front walk","Fish water","Pool inner edge","Pool outer edge","Sea edge","Lower front edge"};
        foreach(string name in replaced)if(parent.Find(name)==null)throw new Exception("Missing original pond object: "+name);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        profile=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(ProfilePath);
        if(profile==null){profile=ScriptableObject.CreateInstance<BirdCoastalPondProfile>();AssetDatabase.CreateAsset(profile,ProfilePath);}
        UpdateMeshes();
        var root=new GameObject("Curved pond promenade");
        try
        {
            string materials=BirdCoastalWorldAuthoring.Folder+"/Materials/";
            var stone=AssetDatabase.LoadAssetAtPath<Material>(materials+"Warm stone.mat");
            var white=AssetDatabase.LoadAssetAtPath<Material>(materials+"Plaster.mat");
            var water=AssetDatabase.LoadAssetAtPath<Material>(materials+"Water.mat");
            AddMesh(root.transform,"Continuous walk and terrace",stone,0,true,true);
            AddMesh(root.transform,"Curved water",water,0,false,true);
            AddMesh(root.transform,"Basin and fascia",white,0,true,true);
            foreach(string name in new[]{"Pond rail","Coast rail"})
            {
                AddMesh(root.transform,name,white,17,true,true);
                AddMesh(root.transform,name+" safety boundary",white,2,true,false);
            }
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.transform.SetParent(parent,false);
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        // Keep original source objects as disabled scene overrides, not a destructive regeneration.
        foreach(string name in replaced)
        {
            Disable(parent.Find(name));var guard=parent.Find(name+" safety boundary");if(guard!=null)Disable(guard);
        }
        // Existing still fish studies follow the extended basin; schooling is a separate runtime pass.
        for(int i=0;i<16;i++)
        {
            var fish=parent.Find("Fish study "+i);if(fish==null)continue;
            int sample=4+i*(center.Count-9)/16;
            fish.localPosition=new Vector3(center[sample].x,profile.floorHeight-profile.waterDepth+.035f,center[sample].y)+new Vector3(right[sample].x,0,right[sample].y)*((i%3)-1)*.65f;
            var tangent=new Vector3(-right[sample].y,0,right[sample].x);fish.localRotation=Quaternion.LookRotation(tangent)*Quaternion.Euler(0,(i%3-1)*20,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(fish);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
    }
    static void Disable(Transform t){t.gameObject.SetActive(false);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
    static void AddMesh(Transform parent,string name,Material material,int layer,bool collision,bool visible)
    {
        var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.layer=layer;
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/"+name+".asset");go.GetComponent<MeshFilter>().sharedMesh=mesh;
        var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.enabled=visible;r.receiveGI=ReceiveGI.Lightmaps;
        r.scaleInLightmap=name.Contains("rail")?4f:.6f;
        if(collision)go.AddComponent<MeshCollider>().sharedMesh=mesh;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|(visible?StaticEditorFlags.ContributeGI:0));
    }

    [MenuItem("Bird/Coastal world/Update pond meshes only")]
    public static void UpdateMeshes()
    {
        profile=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(ProfilePath);if(profile==null)throw new Exception("Create pond first.");
        Sample(profile,out center,out right);outer=profile.waterHalfWidth+profile.walkWidth;
        waterLoop=Loop(profile.waterHalfWidth);outerLoop=Loop(outer);
        // Build and validate every mesh before touching any saved mesh identity.
        var meshes=new Dictionary<string,Mesh>();
        Reset();Ribbon(-outer,-profile.waterHalfWidth,profile.floorHeight);Ribbon(profile.waterHalfWidth,outer,profile.floorHeight);
        Caps(profile.waterHalfWidth,outer,profile.floorHeight);
        var inland=center.Select((p,i)=>p-right[i]*outer).ToList();
        // Follow the exact cap edges to the inland promenade instead of leaving
        // crescent-shaped cracks between a straight apron and the rounded ends.
        inland.AddRange(InlandCapJoin(center.Last(),-right.Last(),-1).Skip(1));
        var startJoin=InlandCapJoin(center[0],-right[0],1);startJoin.Reverse();
        inland.AddRange(startJoin.Take(startJoin.Count-1));
        Polygon(inland,profile.floorHeight);meshes.Add("Continuous walk and terrace",Finish());
        Reset();Ribbon(-profile.waterHalfWidth,profile.waterHalfWidth,profile.floorHeight-profile.waterDepth);Caps(0,profile.waterHalfWidth,profile.floorHeight-profile.waterDepth);meshes.Add("Curved water",Finish());
        Reset();Wall(waterLoop,profile.floorHeight-profile.waterDepth-profile.basinDepthBelowWater-.02f,profile.floorHeight);Wall(outerLoop,profile.floorHeight-.5f,profile.floorHeight);
        // Close the terrace underneath as well, so its sea-facing slab has thickness.
        var floorMesh=meshes["Continuous walk and terrace"];var floorVertices=floorMesh.vertices;var floorIndices=floorMesh.triangles;
        for(int i=0;i<floorIndices.Length;i+=3)Tri(floorVertices[floorIndices[i]]-Vector3.up*.5f,floorVertices[floorIndices[i+2]]-Vector3.up*.5f,floorVertices[floorIndices[i+1]]-Vector3.up*.5f);
        meshes.Add("Basin and fascia",Finish());
        foreach(bool pond in new[]{true,false})
        {
            string name=pond?"Pond rail":"Coast rail";var segments=GuardSegments(pond);
            meshes.Add(name,BirdCoastalRailMesh.Build(segments,profile.floorHeight));Reset();
            foreach(var segment in segments){Vector3 a=V(segment[0],profile.floorHeight),b=V(segment[1],profile.floorHeight);Quad(a,b,b+Vector3.up*1.1f,a+Vector3.up*1.1f);Quad(b,a,a+Vector3.up*1.1f,b+Vector3.up*1.1f);}
            meshes.Add(name+" safety boundary",Finish());
        }
        foreach(var pair in meshes)
        {
            var m=pair.Value;if(m.vertexCount==0||m.vertices.Any(v=>!Finite(v))||m.normals.Any(n=>n.sqrMagnitude<.9f))throw new Exception("Invalid pond mesh: "+pair.Key);
            if(pair.Key=="Continuous walk and terrace"||pair.Key=="Curved water")if(m.normals.Any(n=>n.y<.99f))throw new Exception("Folded or inverted pond profile: "+pair.Key);
            if(!pair.Key.EndsWith(" safety boundary"))Unwrapping.GenerateSecondaryUVSet(m);
        }
        foreach(var pair in meshes)Save(pair.Key,pair.Value);
        profile.railRevision=1;EditorUtility.SetDirty(profile);
        // Refresh cooking in an open authoring scene after an explicit mesh edit.
        foreach(var collider in UnityEngine.Object.FindObjectsOfType<MeshCollider>(true))
        {
            var mesh=collider.sharedMesh;
            if(mesh==null||!AssetDatabase.GetAssetPath(mesh).StartsWith(Folder+"/",StringComparison.Ordinal))continue;
            collider.sharedMesh=null;collider.sharedMesh=mesh;
        }
        Physics.SyncTransforms();AssetDatabase.SaveAssets();
    }
    public static List<Vector2[]> RailSegments(BirdCoastalPondProfile p,bool pond)
    {
        profile=p;Sample(p,out center,out right);outer=p.waterHalfWidth+p.walkWidth;
        waterLoop=Loop(p.waterHalfWidth);outerLoop=Loop(outer);return GuardSegments(pond);
    }
    [MenuItem("Bird/Coastal world/Update pond rail meshes only")]
    public static void UpdateRailMeshes()
    {
        var p=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(ProfilePath);
        if(p==null)throw new Exception("Author the pond first.");
        var meshes=new Dictionary<string,Mesh>();
        foreach(bool pond in new[]{true,false})
        {
            var mesh=BirdCoastalRailMesh.Build(RailSegments(p,pond),p.floorHeight);
            UnwrapParam.SetDefaults(out var parameters);parameters.packMargin*=4;
            Unwrapping.GenerateSecondaryUVSet(mesh,parameters);meshes.Add(pond?"Pond rail":"Coast rail",mesh);
        }
        foreach(var pair in meshes)Save(pair.Key,pair.Value);
        foreach(var collider in UnityEngine.Object.FindObjectsOfType<MeshCollider>(true))
        {
            string path=AssetDatabase.GetAssetPath(collider.sharedMesh);
            if(path!=Folder+"/Pond rail.asset"&&path!=Folder+"/Coast rail.asset")continue;
            var saved=collider.sharedMesh;collider.sharedMesh=null;collider.sharedMesh=saved;
        }
        Physics.SyncTransforms();
        p.railRevision=1;EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();
    }
    [MenuItem("Bird/Coastal world/Finish original pond rails once")]
    public static void FinishRails()
    {
        var p=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(ProfilePath);
        if(p.railRevision!=0)throw new Exception("Original rail finish already replaced; edit the saved meshes/profile and explicitly update or rebake.");
        UpdateRailMeshes();var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            foreach(string name in new[]{"Pond rail","Coast rail"})root.transform.Find(name).GetComponent<MeshRenderer>().scaleInLightmap=4;
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    public static void Sample(BirdCoastalPondProfile p,out List<Vector2> points,out List<Vector2> normals)
    {
        if(p.centerline==null||p.centerline.Length<4||p.samplesPerSpan<6||p.samplesPerSpan>24||p.waterHalfWidth<1||p.walkWidth<3||p.waterDepth<.2f||p.basinDepthBelowWater<.1f||!Finite(new Vector3(p.basinDepthBelowWater,0,0))||!Finite(new Vector3(p.waterHalfWidth,p.walkWidth,p.floorHeight+p.waterDepth))||p.centerline.Any(v=>!Finite(new Vector3(v.x,0,v.y))))throw new Exception("Invalid pond profile.");
        points=new List<Vector2>();normals=new List<Vector2>();var c=p.centerline;
        for(int i=0;i<c.Length-1;i++)for(int j=0;j<p.samplesPerSpan;j++)
        {
            float t=(float)j/p.samplesPerSpan;Vector2 a=c[Mathf.Max(0,i-1)],b=c[i],d=c[i+1],e=c[Mathf.Min(c.Length-1,i+2)];
            points.Add(.5f*((2*b)+(-a+d)*t+(2*a-5*b+4*d-e)*t*t+(-a+3*b-3*d+e)*t*t*t));
        }
        points.Add(c.Last());
        for(int i=0;i<points.Count;i++){var tangent=(points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)]).normalized;if(tangent.sqrMagnitude<.9f)throw new Exception("Degenerate pond tangent.");normals.Add(new Vector2(tangent.y,-tangent.x));}
    }
    static List<Vector2> Loop(float radius)
    {
        var loop=center.Select((p,i)=>p+right[i]*radius).ToList();
        AddArc(loop,center.Last(),right.Last(),radius);
        for(int i=center.Count-2;i>=0;i--)loop.Add(center[i]-right[i]*radius);
        AddArc(loop,center[0],-right[0],radius);return loop;
    }
    static void AddArc(List<Vector2> points,Vector2 c,Vector2 start,float r)
    {for(int i=1;i<=24;i++){float angle=i*Mathf.PI/24;points.Add(c+(start*Mathf.Cos(angle)+new Vector2(-start.y,start.x)*Mathf.Sin(angle))*r);}}
    static List<Vector2[]> GuardSegments(bool pond)
    {
        var result=new List<Vector2[]>();var loop=pond?waterLoop:outerLoop;
        for(int i=0;i<loop.Count;i++)
        {
            Vector2 a=loop[i],b=loop[(i+1)%loop.Count];if(Vector2.Distance(a,b)<.001f)continue;
            // Inner outer-envelope edge borders the continuous terrace, not a drop.
            bool rightReach=i<center.Count-1;
            var mid=(a+b)*.5f;
            bool endCap=i>=center.Count-1&&i<center.Count+23&&Vector2.Dot(mid-center.Last(),right.Last())>=0;
            bool startCap=i>=center.Count*2+22&&Vector2.Dot(mid-center[0],right[0])>=0;
            bool cap=endCap||startCap;
            if(pond||rightReach||cap&&(mid.y>45.05f||mid.x>23.05f))result.Add(new[]{a,b});
        }
        return result;
    }
    static List<Vector2> InlandCapJoin(Vector2 c,Vector2 start,int direction)
    {
        var join=new List<Vector2>{c+start*outer};
        for(int i=1;i<=24;i++)
        {
            float angle=direction*i*Mathf.PI/24;
            var q=c+(start*Mathf.Cos(angle)+new Vector2(-start.y,start.x)*Mathf.Sin(angle))*outer;
            if(q.y<=44){var last=join.Last();join.Add(Vector2.Lerp(last,q,(last.y-44)/(last.y-q.y)));return join;}
            join.Add(q);
        }
        throw new Exception("Pond cap does not reach the inland promenade; revise the profile joins.");
    }
    static void Ribbon(float lo,float hi,float y)
    {for(int i=0;i<center.Count-1;i++)Quad(V(center[i]+right[i]*lo,y),V(center[i+1]+right[i+1]*lo,y),V(center[i+1]+right[i+1]*hi,y),V(center[i]+right[i]*hi,y));}
    static void Caps(float lo,float hi,float y)
    {
        foreach(int end in new[]{0,center.Count-1})
        {
            var start=end==0?-right[end]:right[end];var inner=new List<Vector2>{center[end]+start*lo};var outside=new List<Vector2>{center[end]+start*hi};
            AddArc(inner,center[end],start,lo);AddArc(outside,center[end],start,hi);
            for(int j=0;j<24;j++){if(lo>0)Tri(V(inner[j],y),V(inner[j+1],y),V(outside[j+1],y));Tri(V(inner[j],y),V(outside[j+1],y),V(outside[j],y));}
        }
    }
    static void Polygon(List<Vector2> p,float y)
    {
        // Simple inland polygon; ear clipping supports its gently concave shoulders.
        float area=0;for(int i=0;i<p.Count;i++)area+=Cross(p[i],p[(i+1)%p.Count]);if(area<0)p.Reverse();
        var ids=Enumerable.Range(0,p.Count).ToList();int limit=p.Count*p.Count;
        while(ids.Count>2&&limit-->0)
        {
            bool cut=false;for(int k=0;k<ids.Count;k++)
            {
                int a=ids[(k+ids.Count-1)%ids.Count],b=ids[k],c=ids[(k+1)%ids.Count];
                if(Cross(p[b]-p[a],p[c]-p[b])<.000001f)continue;
                if(ids.Any(j=>j!=a&&j!=b&&j!=c&&Inside(p[j],p[a],p[b],p[c])))continue;
                Tri(V(p[a],y),V(p[c],y),V(p[b],y));ids.RemoveAt(k);cut=true;break;
            }
            if(!cut)throw new Exception("Pond inland polygon intersects itself or has degenerate corners.");
        }
        if(ids.Count>2)throw new Exception("Pond triangulation incomplete.");
    }
    static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>Cross(b-a,p-a)>=-.000001f&&Cross(c-b,p-b)>=-.000001f&&Cross(a-c,p-c)>=-.000001f;
    static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
    static void Wall(List<Vector2> loop,float bottom,float top){for(int i=0;i<loop.Count;i++){var a=loop[i];var b=loop[(i+1)%loop.Count];if(Vector2.Distance(a,b)<.001f)continue;Quad(V(a,bottom),V(b,bottom),V(b,top),V(a,top));Quad(V(b,bottom),V(a,bottom),V(a,top),V(b,top));}}
    static void Box(Vector3 c,Vector3 size,Quaternion q)
    {
        var v=new Vector3[8];for(int i=0;i<8;i++)v[i]=c+q*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
        foreach(var f in new[]{new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,4,6,2},new[]{1,3,7,5},new[]{2,6,7,3},new[]{0,1,5,4}})Quad(v[f[0]],v[f[1]],v[f[2]],v[f[3]]);
    }
    static bool Finite(Vector3 v)=>!float.IsNaN(v.sqrMagnitude)&&!float.IsInfinity(v.sqrMagnitude);
    static Vector3 V(Vector2 v,float y)=>new Vector3(v.x,y,v.y);
    static void Reset(){vertices.Clear();indices.Clear();}
    static void Tri(Vector3 a,Vector3 b,Vector3 c){if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-12f)return;int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
    static void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Tri(a,b,c);Tri(a,c,d);}
    static Mesh Finish(){var m=new Mesh();m.SetVertices(vertices);m.SetTriangles(indices,0);m.RecalculateNormals();m.RecalculateBounds();m.uv=vertices.Select(v=>new Vector2(v.x,v.z)*.2f).ToArray();return m;}
    static void Save(string name,Mesh mesh){mesh.name=name;string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);}}
}
#endif
