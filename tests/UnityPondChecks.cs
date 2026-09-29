#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class UnityCoastalWorldChecks
{
    public static async void AddFish()
    {
        try{await BirdPondSchoolAuthoring.Add();Finish("coastal-fish-author",true,"Authored 24 cosmetic fish, shallow water/bed and one unsynced school with scene-local Bird binding.");}
        catch(Exception e){Finish("coastal-fish-author",false,e.ToString());}
    }
    public static void AddPond(){try{BirdCoastalPondAuthoring.Add();Finish("coastal-pond-author",true,"Authored curved basin and continuous winding promenade; existing stairs, rooms, beacons and Bird unchanged.");}catch(Exception e){Finish("coastal-pond-author",false,e.ToString());}}
    public static void UpdatePondMeshes(){try{BirdCoastalPondAuthoring.UpdateMeshes();Finish("coastal-pond-meshes",true,"Updated seven named pond meshes while retaining asset identities and authored prefab transforms.");}catch(Exception e){Finish("coastal-pond-meshes",false,e.ToString());}}
    public static void CheckPond()
    {
        try
        {
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);Physics.SyncTransforms();
            string folder=Folder+"/Pond01/"+EditorUserBuildSettings.activeBuildTarget;Directory.CreateDirectory(folder);
            var p=AssetDatabase.LoadAssetAtPath<BirdCoastalPondProfile>(BirdCoastalPondAuthoring.ProfilePath);
            BirdCoastalPondAuthoring.Sample(p,out var centers,out var normals);
            var root=GameObject.Find("04 Lower water and hidden lounge/Curved pond promenade");Require(root!=null,"Saved nested pond prefab");
            foreach(string name in new[]{"Lower overlook","Outer water walk","Pool front walk","Fish water","Pool inner edge","Pool outer edge","Sea edge","Lower front edge"})
                Require(GameObject.Find("04 Lower water and hidden lounge").transform.Find(name).gameObject.activeSelf==false,"Original rectangle retained but inactive: "+name);
            const int solid=(1<<0)|(1<<2)|(1<<11);int samples=0;
            Func<Vector2,Vector3> foot=q=>new Vector3(q.x,p.floorHeight+.035f,q.y);
            Action<Vector3> supported=q=>{
                Require(Physics.Raycast(q+Vector3.up*.1f,Vector3.down,out var hit,.25f,solid,QueryTriggerInteraction.Ignore)&&hit.normal.y>.98f,"Pond path has floor: "+q);
                Require(!Physics.CheckCapsule(q+Vector3.up*.3f,q+Vector3.up*1.6f,.25f,solid,QueryTriggerInteraction.Ignore),"Pond path standing clearance: "+q);samples++;
            };
            var loop=new List<Vector3>();
            for(int i=0;i<centers.Count;i++)
            {
                foreach(float r in new[]{p.waterHalfWidth+.4f,p.waterHalfWidth+p.walkWidth*.5f,p.waterHalfWidth+p.walkWidth-.4f})supported(foot(centers[i]+normals[i]*r));
                // No walking slab may accidentally fill the water while new scene overlays coexist.
                var water=foot(centers[i]);Require(!Physics.Raycast(water+Vector3.up*.05f,Vector3.down,.2f,solid,QueryTriggerInteraction.Ignore),"Water remains below walkway: "+water);
                loop.Add(new Vector3(centers[i].x+normals[i].x*(p.waterHalfWidth+p.walkWidth*.5f),p.floorHeight,centers[i].y+normals[i].y*(p.waterHalfWidth+p.walkWidth*.5f)));
            }
            // Round the western cap, cross the retained promenade, and rejoin the eastern end.
            float rmid=p.waterHalfWidth+p.walkWidth*.5f;
            var end=centers.Last();var endRight=normals.Last();
            for(int i=1;i<=12;i++){float a=i*Mathf.PI/12;var q=end+(endRight*Mathf.Cos(a)+new Vector2(-endRight.y,endRight.x)*Mathf.Sin(a))*rmid;loop.Add(new Vector3(q.x,p.floorHeight,q.y));supported(foot(q));}
            loop.Add(new Vector3(0,p.floorHeight,48));loop.Add(new Vector3(0,p.floorHeight,44));loop.Add(new Vector3(14,p.floorHeight,44));loop.Add(new Vector3(14,p.floorHeight,48));
            var start=centers[0];var left=-normals[0];
            for(int i=0;i<=12;i++){float a=i*Mathf.PI/12;var q=start+(left*Mathf.Cos(a)+new Vector2(-left.y,left.x)*Mathf.Sin(a))*rmid;loop.Add(new Vector3(q.x,p.floorHeight,q.y));supported(foot(q));}
            foreach(var q in new[]{new Vector3(23.5f,-1.965f,49),new Vector3(23.5f,-1.965f,53),new Vector3(15,-1.965f,47)})supported(q);
            File.WriteAllText(Folder+"/pond-walk-routes.json",JsonUtility.ToJson(new UnityCoastalWalkChecks.Routes{routes=new[]{new UnityCoastalWalkChecks.Route{name="Complete winding pond circuit",points=loop.ToArray()}}},true));
            foreach(var guard in root.GetComponentsInChildren<Collider>().Where(c=>c.name.EndsWith(" safety boundary")))
            {
                Require(guard.gameObject.layer==2&&!guard.isTrigger&&!guard.GetComponent<Renderer>().enabled,"Continuous player guard");
                var rail=guard.transform.parent.Find(guard.name.Replace(" safety boundary",""));Require(rail.gameObject.layer==17&&rail.GetComponent<MeshCollider>().sharedMesh==rail.GetComponent<MeshFilter>().sharedMesh,"Exact visible rail pointing proxy");
            }
            long triangles=root.GetComponentsInChildren<MeshFilter>().Sum(f=>(long)f.sharedMesh.triangles.Length/3);Require(triangles<16000,"Bounded pond mesh budget");
            PondCapture(folder,"01-water-stair",new Vector3(10,.5f,39),new Vector3(13,-2,57),85);
            PondCapture(folder,"02-east-walk",new Vector3(23.5f,-.35f,49),new Vector3(10,-1.9f,62),85);
            PondCapture(folder,"03-high-beacon-stance",new Vector3(13.7f,22.65f,35.7f),new Vector3(23.5f,-2,54),85);
            PondCapture(folder,"04-west-return",new Vector3(-10,-.35f,53),new Vector3(1,-1.5f,60),85);
            PondCapture(folder,"05-tide-room-seated",new Vector3(-22.082f,-.9f,37.773f),new Vector3(-9.404f,-.9f,64.962f),85);
            PondCapture(folder,"06-plan",new Vector3(4,55,55),new Vector3(4,-2,55.01f),65);
            PondCapture(folder,"07-seaward",new Vector3(14,8,87),new Vector3(4,-2,52),75);
            PondCapture(folder,"08-overlook-inspection",new Vector3(15,-.35f,47),new Vector3(130,15,220),78);
            PondCapture(folder,"09-west-promenade-join",new Vector3(-13.5f,-.35f,44.2f),new Vector3(-8,-1.9f,49),85);
            File.WriteAllText(folder+"/inventory.txt",samples+" supporting-floor/standing-clearance samples; "+triangles+" pond triangles; water not covered by floor; exact rail proxies; full loop route written. Normal quality="+QualitySettings.names[QualitySettings.GetQualityLevel()]+". Not client locomotion or physical acceptance.");
            Finish("coastal-pond-check",true,samples+" pond floor/standing samples; no slab over water; beacon/inspection support; rail policy; "+triangles+" triangles and nine actual renders. Full normal-frame walk remains separate.");
        }
        catch(Exception e){Finish("coastal-pond-check",false,e.ToString());}
    }
    static void PondCapture(string folder,string name,Vector3 eye,Vector3 look,float fov)
    {
        var go=new GameObject("Pond validation camera");var camera=go.AddComponent<Camera>();camera.transform.position=eye;camera.transform.LookAt(look);camera.fieldOfView=fov;camera.nearClipPlane=.03f;camera.farClipPlane=10000;camera.clearFlags=CameraClearFlags.Skybox;
        Render(camera,folder+"/"+name+".png");UnityEngine.Object.DestroyImmediate(go);
    }
}
#endif
