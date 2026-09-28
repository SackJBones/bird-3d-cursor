#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

public partial class UnityCoastalWorldChecks
{
    public static void RepairR06()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            EditorSceneManager.OpenScene(BirdCoastalWorldAuthoring.ScenePath);Physics.SyncTransforms();
            int misses=0;
            for(int i=0;i<192;i++)
            {
                float a=(i+.25f)*Mathf.PI*2/192;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                if(!Physics.Raycast(new Vector3(-2,.9f,26)+d*5.1f,d,.35f)) misses++;
            }
            File.WriteAllText(Folder+"/r06-before.txt","Existing upper pit riser: "+misses+" / 192 outward rays miss all surfaces in the 0.8–1.0 m height interval.\n");
            BirdCoastalWorldAuthoring.ApplyR06();
            Finish("coastal-r06",true,"Saved scoped pit/rear-wall/ridge-shoulder repairs. Baseline missing-riser rays="+misses+"/192.");
        }
        catch(Exception e){Finish("coastal-r06",false,e.ToString());}
    }
    static void CheckR06Repairs()
    {
        var bowl=GameObject.Find("03 Supported coastal terraces/Closed conversation pit steps");
        if(bowl==null)return;
        int solidEdges=0,rays=0;
        foreach(string path in new[]{"03 Supported coastal terraces/Closed conversation pit steps","03 Supported coastal terraces/Conversation floor",
            "03 Supported coastal terraces/Conversation bench","03 Supported coastal terraces/Conversation cushion","03 Supported coastal terraces/Conversation back",
            "01 Arrival cavern/Plain rear wall","05 Coastal ridge and distant island/Arrival ridge shoulder"})
        {
            var go=GameObject.Find(path);Require(go!=null,"Saved repair object exists: "+path);
            var mesh=go.GetComponent<MeshFilter>().sharedMesh;
            solidEdges+=CheckClosedR06(mesh);
            var collision=go.GetComponent<MeshCollider>();
            if(collision!=null)Require(collision.sharedMesh==mesh,"Visible repair and collision agree: "+path);
        }
        foreach(float height in new[]{.9f,.7f,.5f,.3f,.1f}) for(int i=0;i<192;i++)
        {
            float radius=5.2f-.25f*Mathf.Round((.9f-height)/.2f);
            float a=(i+.25f)*Mathf.PI*2/192;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
            Require(Physics.Raycast(new Vector3(-2,height,26)+d*(radius-.1f),d,out var hit,.16f)&&hit.collider.gameObject==bowl,"Pit riser continuous at height="+height+" angle="+i);
            rays++;
        }
        // Dense tread/collar sampling includes every joint phase. Furniture is
        // inside these rings; a passing destination path cannot cover this area.
        foreach(float radius in new[]{5.35f,5.1f,4.85f,4.6f,4.35f}) for(int i=0;i<192;i++)
        {
            float a=(i+.25f)*Mathf.PI*2/192;var foot=new Vector3(-2+radius*Mathf.Cos(a),2,26+radius*Mathf.Sin(a));
            Require(Physics.Raycast(foot,Vector3.down,out var hit,2.1f)&&hit.collider.gameObject==bowl&&hit.normal.y>.99f,"Visible supporting pit tread at "+foot);rays++;
        }
        var rock=GameObject.Find("05 Coastal ridge and distant island/Arrival ridge shoulder").GetComponent<MeshCollider>();
        var rear=GameObject.Find("01 Arrival cavern/Plain rear wall").GetComponent<MeshCollider>();
        for(int x=-14;x<=14;x++) for(int z=-17;z<=3;z+=2)
        {
            float crown=4+9*Mathf.Sqrt(1-x*x/225f);
            Vector3 point=new Vector3(x,crown+.27f,z);
            Require(rock.Raycast(new Ray(point,Vector3.up),out var hit,100),"Rock covers arrival crown: "+point);rays++;
        }
        for(int x=-14;x<=14;x++)
        {
            float crown=4+9*Mathf.Sqrt(1-x*x/225f);
            Require(rear.Raycast(new Ray(new Vector3(x,crown-.08f,-16),Vector3.back),out var hit,3),"White rear wall closes its own vault junction");rays++;
        }
        foreach(float x in new[]{-23f,-20,20,23}) foreach(float z in new[]{-1f,1,3})
            Require(rock.Raycast(new Ray(new Vector3(x,6.2f,z),Vector3.up),out var hit,100),"Rock covers social-wing shell");
        for(int x=-14;x<=14;x+=2)for(int z=-16;z<=2;z+=2)
            Require(!rock.bounds.Contains(new Vector3(x,0,z)) || !rock.Raycast(new Ray(new Vector3(x,.1f,z),Vector3.up),out var hit,2),"No rock intrudes into standing arrival volume");
        File.WriteAllText(Folder+"/r06-repair-checks.txt","PASS: seven repair meshes closed with consistently paired directed edges ("+solidEdges+" edges); "+rays+" riser/tread/crown/rear-wall rays, wing roof coverage and standing-volume rock exclusion. Saved rendering and collision share meshes. This is sampled geometry, not physical comfort.\n");
    }
    public static void RefineRockR06()
    {
        try {BirdCoastalWorldAuthoring.RefineRockR06();Finish("coastal-r06-rock",true,"Refined only the saved R06 rock mesh; asset identity and architecture retained.");}
        catch(Exception e){Finish("coastal-r06-rock",false,e.ToString());}
    }
    static int CheckClosedR06(Mesh mesh)
    {
        var counts=new Dictionary<string,int>();var winding=new Dictionary<string,int>();
        var v=mesh.vertices;var t=mesh.triangles;
        Func<Vector3,string> key=p=>Mathf.RoundToInt(p.x*10000)+":"+Mathf.RoundToInt(p.y*10000)+":"+Mathf.RoundToInt(p.z*10000);
        for(int i=0;i<t.Length;i+=3)
        {
            Require(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude>1e-12f,"Nondegenerate triangle in "+mesh.name);
            for(int e=0;e<3;e++)
            {
                string a=key(v[t[i+e]]),b=key(v[t[i+(e+1)%3]]);int order=string.CompareOrdinal(a,b);
                string edge=order<0?a+"/"+b:b+"/"+a;
                if(!counts.ContainsKey(edge)){counts[edge]=0;winding[edge]=0;}
                counts[edge]++;winding[edge]+=order<0?1:-1;
            }
        }
        Require(counts.All(e=>e.Value==2&&winding[e.Key]==0),"Closed, consistently oriented mesh: "+mesh.name);
        return counts.Count;
    }
    static void CaptureR06Repairs()
    {
        if(GameObject.Find("03 Supported coastal terraces/Closed conversation pit steps")==null)return;
        Capture("22-pit-descent",new Vector3(2.2f,2.65f,21.8f),new Vector3(-2,.4f,26),80);
        Capture("23-pit-seated",new Vector3(-3.7f,1.25f,27.8f),new Vector3(1,1,23),90);
        Capture("24-pit-risers",new Vector3(.1f,.75f,23.9f),new Vector3(2.3f,.7f,21.7f),80);
        Capture("25-arrival-roof",new Vector3(30,45,25),new Vector3(-2,15,-12),75);
        Capture("26-arrival-east",new Vector3(70,20,-10),new Vector3(0,10,-10),70);
        Capture("27-arrival-rear",new Vector3(90,65,-155),new Vector3(-20,8,-30),75);
        // Above and outside the original ridge, not inside its landward mass.
        Capture("28-arrival-west",new Vector3(-65,150,-35),new Vector3(-2,8,-5),75);
        Capture("29-pit-entry-return",new Vector3(0,.95f,24),new Vector3(3,1.8f,21),85);
    }
}
#endif
