#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class UnityCoastalWorldChecks
{
    public static void FinishLookoutRail()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            LookoutRailViews(Folder+"/LookoutRail01/Before");
            string[] paths={BirdLookoutRailAuthoring.GuardPath,BirdCoastalWorldAuthoring.Folder+"/Meshes/Lookout slab.asset"};
            var before=paths.Select(File.ReadAllBytes).ToArray();
            var id=AssetDatabase.AssetPathToGUID(BirdLookoutRailAuthoring.MeshPath);
            BirdLookoutRailAuthoring.FinishOriginal();
            for(int i=0;i<paths.Length;i++)Require(before[i].SequenceEqual(File.ReadAllBytes(paths[i])),"Guard and walking slab bytes unchanged");
            Require(id==AssetDatabase.AssetPathToGUID(BirdLookoutRailAuthoring.MeshPath),"Rail GUID retained");
            Finish("coastal-lookout-rail-finish",true,"One continuous lookout rail; original path, 48 posts and dimensions, guard/slab mesh bytes and GUID retained. Focused bake scale 4; rebake required.");
        }
        catch(Exception e){Finish("coastal-lookout-rail-finish",false,e.ToString());}
    }

    public static void CheckLookoutRail()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);Physics.SyncTransforms();
            var rail=GameObject.Find(BirdLookoutRailAuthoring.ObjectPath).transform;
            var mesh=rail.GetComponent<MeshFilter>().sharedMesh;var collider=rail.GetComponent<MeshCollider>();
            var guard=rail.parent.Find("Lookout edge safety boundary");
            Require(rail.gameObject.layer==17&&collider.sharedMesh==mesh&&!collider.isTrigger&&collider.enabled,"Exact visible pointing mesh");
            Require(guard.gameObject.layer==2&&guard.GetComponent<Collider>().enabled&&!guard.GetComponent<Renderer>().enabled,"Separate active player guard");
            Require(Physics.GetIgnoreLayerCollision(17,9)&&Physics.GetIgnoreLayerCollision(17,10),"Pointing layer does not replace player containment");
            Require(rail.localPosition==new Vector3(4,21,31)&&rail.localScale==Vector3.one&&rail.localRotation==Quaternion.identity,"Authored placement retained");
            Require(mesh.vertices.All(v=>!float.IsNaN(v.sqrMagnitude)&&!float.IsInfinity(v.sqrMagnitude)&&v.y>=-.001f&&v.y<=1.1151f),"Finite original height envelope");
            Require(mesh.uv2.Length==mesh.vertexCount&&mesh.normals.All(n=>Mathf.Abs(n.sqrMagnitude-1)<.01f),"Lightmap UVs and unit normals");
            Require(rail.GetComponent<MeshRenderer>().sharedMaterial.name=="Plaster"&&rail.GetComponent<MeshRenderer>().scaleInLightmap==4,"Matte plaster and focused bake density");
            int edges=CheckClosedR06(mesh),tops=0,posts=0,gaps=0;
            var segments=BirdLookoutRailAuthoring.SavedSegments();
            foreach(var segment in segments)
            {
                var tangent=(segment[1]-segment[0]).normalized;var side=new Vector3(tangent.y,0,-tangent.x);
                foreach(float u in new[]{0,.01f,.5f,.99f})
                {
                    var q=Vector2.Lerp(segment[0],segment[1],u);var top=rail.TransformPoint(new Vector3(q.x,1.3f,q.y));
                    Require(collider.Raycast(new Ray(top,Vector3.down),out var hit,.3f)&&Mathf.Abs(hit.point.y-22.115f)<.002f,"Joined top at sample including original seams");tops++;
                }
                var midpoint=(segment[0]+segment[1])*.5f;var m=rail.TransformPoint(new Vector3(midpoint.x,.55f,midpoint.y));
                Require(!collider.Raycast(new Ray(m+side*.2f,-side),out var gapHit,.4f),"Original span clear below top");gaps++;
                var post=rail.TransformPoint(new Vector3(segment[0].x,.4f,segment[0].y));
                Require(collider.Raycast(new Ray(post+Vector3.right*.1f,Vector3.left),out var postHit,.2f),"Original post present");posts++;
                var sidePoint=rail.TransformPoint(new Vector3(midpoint.x,1.05f,midpoint.y));
                Require(collider.Raycast(new Ray(sidePoint+side*.2f,-side),out var widthHit,.4f)&&Mathf.Abs(widthHit.distance-.135f)<.002f,"Original 0.13 m square width");
            }
            Require(mesh.triangles.Length/3==960&&posts==48&&gaps==48,"Bounded single loop with original post rhythm");
            string folder=Folder+"/LookoutRail01/"+EditorUserBuildSettings.activeBuildTarget;LookoutRailViews(folder);
            string result="960 triangles; "+edges+" closed oriented edges; "+tops+" top samples across seams; "+posts+" original posts; "+gaps+" clear spans. Original 0.13 m section, height, placement and separate player guard; seven views. Physical acceptance separate.";
            File.WriteAllText(folder+"/inventory.txt",result);Finish("coastal-lookout-rail-check",true,result);
        }
        catch(Exception e){Finish("coastal-lookout-rail-check",false,e.ToString());}
    }

    static void LookoutRailViews(string folder)
    {
        Directory.CreateDirectory(folder);
        PondCapture(folder,"01-supported-return",new Vector3(13.7f,22.65f,35.7f),new Vector3(23.5f,1.2f,54),85);
        PondCapture(folder,"02-lookout-south",new Vector3(4,22.65f,36),new Vector3(4,22,41),75);
        PondCapture(folder,"03-east-tight-end",new Vector3(13.8f,22.2f,33.8f),new Vector3(16,22.05f,31),65);
        PondCapture(folder,"04-loop-closure-free",new Vector3(18,22.2f,33),new Vector3(16,22.05f,31),60);
        PondCapture(folder,"05-north-loop",new Vector3(4,22.65f,25),new Vector3(4,22,21),75);
        PondCapture(folder,"06-overall-lookout-free",new Vector3(27,31,52),new Vector3(4,21,31),65);
        PondCapture(folder,"07-arrival",new Vector3(0,1.65f,-10),new Vector3(0,2,8),75);
    }
}
#endif
