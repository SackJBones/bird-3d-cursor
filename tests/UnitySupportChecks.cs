#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class UnityCoastalWorldChecks
{
    public static void AddSupport()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            SupportViews(Folder+"/Support01/Before");
            BirdCoastalSupportAuthoring.Add();
            Finish("coastal-support-author",true,"Added one saved swept support, existing walking surfaces unchanged; rebake required.");
        }
        catch(Exception e){Finish("coastal-support-author",false,e.ToString());}
    }
    public static void UpdateSupport()
    {
        try{BirdCoastalSupportAuthoring.UpdateMesh();Finish("coastal-support-mesh",true,"Updated only saved support mesh, preserving asset identity and prefab edits; rebake required.");}
        catch(Exception e){Finish("coastal-support-mesh",false,e.ToString());}
    }
    public static void SealPondBed()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            var p=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(BirdCoastalPondAuthoring.ProfilePath);
            var school=UnityEngine.Object.FindObjectOfType<BirdPondSchool>(true);
            Require(Mathf.Abs(p.basinDepthBelowWater-school.waterDepth)<.001f,"Profile and actual bed depth agree");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(BirdCoastalPondAuthoring.Folder+"/Basin and fascia.asset");
            var v=mesh.vertices;float oldY=p.floorHeight-p.waterDepth-.08f,newY=p.floorHeight-p.waterDepth-p.basinDepthBelowWater-.02f;int changed=0;
            for(int i=0;i<v.Length;i++)if(Mathf.Abs(v[i].y-oldY)<.001f){v[i].y=newY;changed++;}
            Require(changed>0,"Original short basin wall bottoms found; do not replay this repair");
            mesh.vertices=v;mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);
            EditorUtility.SetDirty(mesh);EditorUtility.SetDirty(p);AssetDatabase.SaveAssets();
            Finish("coastal-pond-bed-seal",true,"Extended "+changed+" submerged wall vertices to "+newY+"; all other vertices and indices retained. Re-bake required.");
        }
        catch(Exception e){Finish("coastal-pond-bed-seal",false,e.ToString());}
    }
    public static void CheckSupport()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);Physics.SyncTransforms();
            var root=GameObject.Find("04 Lower water and hidden lounge/Pond terrace support");
            Require(root!=null&&PrefabUtility.IsAnyPrefabInstanceRoot(root),"Ordinary saved support prefab");
            Require(root.GetComponentsInChildren<MonoBehaviour>().Length==0&&root.GetComponentsInChildren<Light>().Length==0,"No support runtime/light");
            var mesh=root.GetComponent<MeshFilter>().sharedMesh;
            Require(root.GetComponent<MeshCollider>().sharedMesh==mesh&&!root.GetComponent<MeshCollider>().isTrigger,"Exact visible solid collider");
            int edges=CheckClosedR06(mesh); var v=mesh.vertices.Select(root.transform.TransformPoint).ToArray();var t=mesh.triangles;
            Require(v.All(p=>!float.IsNaN(p.sqrMagnitude)&&!float.IsInfinity(p.sqrMagnitude)&&p.y<=-2.30f),"Finite support entirely below occupied walking floor");
            Require(mesh.uv2.Length==mesh.vertexCount&&t.Length/3<3000,"UV2 and bounded support geometry");
            double volume=0;for(int i=0;i<t.Length;i+=3)volume+=Vector3.Dot(v[t[i]],Vector3.Cross(v[t[i+1]],v[t[i+2]]))/6.0;
            Require(volume>1,"Outward-facing positive closed volume");
            var floor=GameObject.Find("04 Lower water and hidden lounge/Curved pond promenade/Continuous walk and terrace").GetComponent<MeshCollider>();
            float top=v.Max(p=>p.y),bottom=v.Min(p=>p.y); int attached=0,embedded=0,waterChecks=0;
            foreach(var p in v.Where(p=>Mathf.Abs(p.y-top)<.001f))
            {Require(floor.Raycast(new Ray(p+Vector3.up,Vector3.down),out var hit,1.01f)&&hit.point.y>p.y+.1f&&hit.point.y<p.y+.5f,"Top embedded in actual dry terrace");attached++;}
            // Temporary exact-mesh probes do not alter authored terrain/water collision.
            var rock=GameObject.Find("05 Coastal ridge and distant island/Long steep coastal ridge");
            var water=GameObject.Find("04 Lower water and hidden lounge/Curved pond promenade/Pond shoals/Submerged basin bed");
            var rockProbe=ProbeMesh(rock);var waterProbe=ProbeMesh(water);
            try
            {
                Physics.SyncTransforms();
                foreach(var p in v.Where(p=>Mathf.Abs(p.y-bottom)<.001f))
                {Require(rockProbe.Raycast(new Ray(new Vector3(100,p.y,p.z),Vector3.left),out var hit,300)&&p.x<hit.point.x-.2f,"Root buried behind actual ridge surface");embedded++;}
                // Vertices and triangle centroids sample the complete upper shell.
                Action<Vector3> basin=q=>{if(waterProbe.Raycast(new Ray(new Vector3(q.x,1,q.z),Vector3.down),out var h,5)){Require(q.y<h.point.y-.05f,"Support stays below actual basin bed: "+q);waterChecks++;}};
                foreach(var p in v)basin(p);
                for(int i=0;i<t.Length;i+=3)basin((v[t[i]]+v[t[i+1]]+v[t[i+2]])/3);
            }
            finally{UnityEngine.Object.DestroyImmediate(rockProbe.gameObject);UnityEngine.Object.DestroyImmediate(waterProbe.gameObject);}
            string folder=Folder+"/Support01/"+EditorUserBuildSettings.activeBuildTarget;
            SupportViews(folder);
            string inventory=edges+" closed oriented edges; "+(t.Length/3)+" triangles; "+attached+" dry-slab attachment vertices; "+embedded+" root vertices behind cliff; "+waterChecks+" sampled basin exclusions; one collider/renderer, no runtime/lights. Views01-04 are free cameras;05-06 are standing/seated context. No physical acceptance.";
            File.WriteAllText(folder+"/inventory.txt",inventory);Finish("coastal-support-check",true,inventory);
        }
        catch(Exception e){Finish("coastal-support-check",false,e.ToString());}
    }
    static MeshCollider ProbeMesh(GameObject source)
    {
        var go=new GameObject("Temporary exact mesh probe");go.transform.SetParent(source.transform,false);
        var c=go.AddComponent<MeshCollider>();c.sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;return c;
    }
    static void SupportViews(string folder)
    {
        Directory.CreateDirectory(folder);
        PondCapture(folder,"01-seaward-composition",new Vector3(14,8,87),new Vector3(4,-2,52),75);
        PondCapture(folder,"02-low-seaward-free",new Vector3(4,-11,88),new Vector3(-14,-7,50),65);
        PondCapture(folder,"03-oblique-free",new Vector3(42,-9,73),new Vector3(-12,-7,50),65);
        PondCapture(folder,"04-wide-exterior-free",new Vector3(65,10,95),new Vector3(0,-2,38),70);
        PondCapture(folder,"05-west-approach",new Vector3(-13.5f,-.35f,44.2f),new Vector3(-6,-2,51),85);
        PondCapture(folder,"06-seated-water",new Vector3(2.9f,-.85f,57.18f),new Vector3(5,-2.7f,62),78);
    }
}
#endif
