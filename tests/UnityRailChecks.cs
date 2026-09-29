#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class UnityCoastalWorldChecks
{
    public static void FinishRails()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);
            RailViews(Folder+"/Rail01/Before");
            string[] protectedPaths={"Continuous walk and terrace","Curved water","Basin and fascia","Pond rail safety boundary","Coast rail safety boundary"};
            var before=protectedPaths.Select(n=>File.ReadAllBytes(BirdCoastalPondAuthoring.Folder+"/"+n+".asset")).ToArray();
            var ids=new[]{"Pond rail","Coast rail"}.Select(n=>AssetDatabase.AssetPathToGUID(BirdCoastalPondAuthoring.Folder+"/"+n+".asset")).ToArray();
            BirdCoastalPondAuthoring.FinishRails();
            for(int i=0;i<protectedPaths.Length;i++)Require(before[i].SequenceEqual(File.ReadAllBytes(BirdCoastalPondAuthoring.Folder+"/"+protectedPaths[i]+".asset")),"Unchanged saved mesh bytes: "+protectedPaths[i]);
            for(int i=0;i<2;i++)Require(ids[i]==AssetDatabase.AssetPathToGUID(BirdCoastalPondAuthoring.Folder+"/"+(i==0?"Pond rail":"Coast rail")+".asset"),"Rail asset identity retained");
            Finish("coastal-rails-finish",true,"Joined two saved square rails and finished exposed ends; raised their bake scale. Five floor/water/fascia/player-guard mesh files byte-identical, rail GUIDs retained. Rebake required.");
        }
        catch(Exception e){Finish("coastal-rails-finish",false,e.ToString());}
    }
    public static void CheckRails()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);Physics.SyncTransforms();
            var p=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(BirdCoastalPondAuthoring.ProfilePath);Require(p.railRevision==1,"Authored continuous rails");
            var parent=GameObject.Find("04 Lower water and hidden lounge/Curved pond promenade").transform;
            int triangles=0,edges=0,tops=0,posts=0,gaps=0,ends=0;
            foreach(bool pond in new[]{true,false})
            {
                string name=pond?"Pond rail":"Coast rail";var rail=parent.Find(name);var collider=rail.GetComponent<MeshCollider>();var mesh=rail.GetComponent<MeshFilter>().sharedMesh;var renderer=rail.GetComponent<MeshRenderer>();
                Require(collider.sharedMesh==mesh&&rail.gameObject.layer==17&&collider.enabled&&!collider.isTrigger,"Exact visible pointing mesh");
                Require(Physics.GetIgnoreLayerCollision(17,9)&&Physics.GetIgnoreLayerCollision(17,10),"Visible rail does not replace player guard");
                var guard=parent.Find(name+" safety boundary");Require(guard.gameObject.layer==2&&guard.GetComponent<Collider>().enabled&&!guard.GetComponent<Renderer>().enabled,"Player guard retained");
                Require(mesh.vertices.All(v=>!float.IsNaN(v.sqrMagnitude)&&!float.IsInfinity(v.sqrMagnitude)&&v.y>=p.floorHeight-.001f&&v.y<=p.floorHeight+1.101f),"Finite original height envelope");
                Require(mesh.uv2.Length==mesh.vertexCount&&mesh.normals.All(n=>Mathf.Abs(n.sqrMagnitude-1)<.01f),"UV2 and unit normals");
                Require(renderer.sharedMaterial.name=="Plaster"&&renderer.scaleInLightmap==4,"Original matte material with focused bake density");
                edges+=CheckClosedR06(mesh);triangles+=mesh.triangles.Length/3;
                var segments=BirdCoastalPondAuthoring.RailSegments(p,pond);var locations=BirdCoastalRailMesh.Posts(segments);
                // The configurable sweep must retain the already accepted pond output.
                var generated=BirdCoastalRailMesh.Build(segments,p.floorHeight);
                try
                {
                    var a=generated.vertices;var b=mesh.vertices;
                    Require(generated.triangles.Select(i=>a[i]).SequenceEqual(mesh.triangles.Select(i=>b[i])),"Default sweep preserves saved pond triangle geometry");
                }
                finally{UnityEngine.Object.DestroyImmediate(generated);}
                foreach(var segment in segments)
                {
                    var mid=(segment[0]+segment[1])*.5f;var q=new Vector3(mid.x,p.floorHeight+1.2f,mid.y);
                    Require(collider.Raycast(new Ray(q,Vector3.down),out var hit,.25f)&&Mathf.Abs(hit.point.y-(p.floorHeight+1.1f))<.002f,"Continuous top follows original path");tops++;
                    if(locations.All(v=>Vector2.Distance(v,mid)>.12f))
                    {
                        var delta=(segment[1]-segment[0]).normalized;var side=new Vector3(delta.y,0,-delta.x);q.y=p.floorHeight+.55f;
                        Require(!collider.Raycast(new Ray(q+side*.15f,-side),out hit,.3f),"Point-through gaps remain open");gaps++;
                    }
                }
                foreach(var location in locations)
                {
                    var q=new Vector3(location.x,p.floorHeight+.4f,location.y);
                    Require(collider.Raycast(new Ray(q+Vector3.right*.1f,Vector3.left),out var hit,.2f),"Existing and terminal posts are present");posts++;
                }
                foreach(var path in BirdCoastalRailMesh.Paths(segments))if(Vector2.Distance(path[0],path.Last())>.001f)ends+=2;
            }
            Require(triangles<6000&&ends>0&&gaps>100,"Bounded rails with open point-through spans and genuine terminal ends");
            string folder=Folder+"/Rail01/"+EditorUserBuildSettings.activeBuildTarget;RailViews(folder);
            string message=triangles+" triangles; "+edges+" closed oriented edges; "+tops+" top path rays; "+posts+" posts; "+gaps+" open spans; "+ends+" capped/posted ends. Exact visible proxies and separate player guards; no runtime/light/material added. Seven normal-quality views, no physical acceptance.";
            File.WriteAllText(folder+"/inventory.txt",message);Finish("coastal-rails-check",true,message);
        }
        catch(Exception e){Finish("coastal-rails-check",false,e.ToString());}
    }
    static void RailViews(string folder)
    {
        Directory.CreateDirectory(folder);
        PondCapture(folder,"01-west-approach",new Vector3(-13.5f,-.35f,44.2f),new Vector3(-6,-2,51),85);
        PondCapture(folder,"02-seated-water",new Vector3(2.9f,-.85f,57.18f),new Vector3(5,-2.7f,62),78);
        PondCapture(folder,"03-west-terminal-inland",new Vector3(-13.5f,-.65f,44.2f),new Vector3(-12,-1,46.5f),65);
        PondCapture(folder,"04-west-terminal-outside-free",new Vector3(-19,-.65f,47),new Vector3(-12,-1,46.5f),65);
        PondCapture(folder,"05-high-return",new Vector3(13.7f,22.65f,35.7f),new Vector3(23.5f,-2,54),85);
        PondCapture(folder,"06-east-walk",new Vector3(23.5f,-.35f,49),new Vector3(10,-1.9f,62),85);
        PondCapture(folder,"07-seaward-free",new Vector3(14,8,87),new Vector3(4,-2,52),75);
    }
}
#endif
