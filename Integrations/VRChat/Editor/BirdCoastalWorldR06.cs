#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class BirdCoastalWorldAuthoring
{
    [MenuItem("Bird/Coastal world/Apply R06 pit and arrival enclosure repair once")]
    public static void ApplyR06()
    {
        string[] names={"01 Arrival cavern","03 Supported coastal terraces","05 Coastal ridge and distant island"};
        var roots=new GameObject[names.Length];
        try
        {
            for(int i=0;i<names.Length;i++) roots[i]=PrefabUtility.LoadPrefabContents(Folder+"/Prefabs/"+names[i]+".prefab");
            if(roots[0].transform.Find("R06 enclosed arrival")!=null) throw new InvalidOperationException("R06 already applied; edit its ordinary saved assets.");
            foreach(string name in new[]{"Plain rear wall","Smooth cavern vault"}) RequiredR05(roots[0],name);
            foreach(string name in new[]{"Main terrace around conversation pit","Conversation floor","Conversation bench","Conversation cushion","Conversation back","Pit shallow step 0","Pit shallow step 1","Pit shallow step 2","Pit shallow step 3"}) RequiredR05(roots[1],name);
            RequiredR05(roots[2],"Long steep coastal ridge");
            foreach(string name in new[]{"R06 Stepped pit bowl","R06 Arrival rear closure","R06 Arrival ridge shoulder"})
                if(Load(name)!=null) throw new InvalidOperationException("Reserved R06 mesh already exists: "+name);
            white=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Plaster.mat");
            rock=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Coastal rock.mat");
            if(white==null||rock==null) throw new InvalidOperationException("Required saved materials missing.");
            // A single watertight solid owns the collar, missing upper riser,
            // four treads and their underside. All circular joins use 96 chords.
            var terrace=RequiredR05(roots[1],"Main terrace around conversation pit");
            ReplaceMeshR05(terrace,SaveMesh(terrace.GetComponent<MeshFilter>().sharedMesh.name,
                HorizontalHole(-15,17,4,34,new Vector2(-2,26),5.4f,1,96)));
            region=roots[1].transform;
            MeshObject("Closed conversation pit steps",SaveMesh("R06 Stepped pit bowl",PitBowlR06()),new Vector3(-2,0,26),white,true);
            for(int i=0;i<4;i++) RequiredR05(roots[1],"Pit shallow step "+i).gameObject.SetActive(false);
            var floor=RequiredR05(roots[1],"Conversation floor");
            ReplaceMeshR05(floor,SaveMesh(floor.GetComponent<MeshFilter>().sharedMesh.name,Slab(4.2f,4.2f,.35f,96)));
            foreach(string name in new[]{"Conversation bench","Conversation cushion","Conversation back"})
            {
                var t=RequiredR05(roots[1],name);
                float outer=name=="Conversation bench"?3.75f:name=="Conversation cushion"?3.70f:3.85f;
                float inner=name=="Conversation bench"?2.95f:name=="Conversation cushion"?3f:3.62f;
                float depth=name=="Conversation bench"?.5f:name=="Conversation cushion"?.12f:.75f;
                ReplaceMeshR05(t,SaveMesh(t.GetComponent<MeshFilter>().sharedMesh.name,ClosedRingR06(outer,inner,15,255,48,depth)));
            }
            // The old 13 m box stopped at y=12.5; the inner vault reaches y=13.
            // Match its actual 48-chord crown instead of hiding the gap with rock.
            var rear=RequiredR05(roots[0],"Plain rear wall");
            UnityEngine.Object.DestroyImmediate(rear.GetComponent<BoxCollider>());
            rear.gameObject.AddComponent<MeshCollider>();
            rear.localPosition=new Vector3(0,0,-18);rear.localScale=Vector3.one;
            ReplaceMeshR05(rear,SaveMesh("R06 Arrival rear closure",ArrivalRearR06()));
            region=roots[2].transform;
            MeshObject("Arrival ridge shoulder",SaveMesh("R06 Arrival ridge shoulder",ArrivalRockR06()),Vector3.zero,rock,true);
            new GameObject("R06 enclosed arrival").transform.SetParent(roots[0].transform,false);
            for(int i=0;i<names.Length;i++) PrefabUtility.SaveAsPrefabAsset(roots[i],Folder+"/Prefabs/"+names[i]+".prefab");
            AssetDatabase.SaveAssets();
        }
        finally { foreach(var root in roots) if(root!=null) PrefabUtility.UnloadPrefabContents(root); region=null; }
    }

    static Mesh PitBowlR06()
    {
        var section=new[]{new Vector2(5.4f,1),new Vector2(5.2f,1),new Vector2(5.2f,.8f),
            new Vector2(4.95f,.8f),new Vector2(4.95f,.6f),new Vector2(4.7f,.6f),new Vector2(4.7f,.4f),
            new Vector2(4.45f,.4f),new Vector2(4.45f,.2f),new Vector2(4.2f,.2f),new Vector2(4.2f,0),
            new Vector2(4.2f,-.35f),new Vector2(5.4f,-.35f)};
        ResetMesh();
        for(int i=0;i<96;i++) for(int j=0;j<section.Length;j++)
        {
            float a=i*Mathf.PI*2/96,b=((i+1)%96)*Mathf.PI*2/96;
            Vector2 p=section[j],q=section[(j+1)%section.Length];
            Vector3 pa=new Vector3(Mathf.Cos(a)*p.x,p.y,Mathf.Sin(a)*p.x),pb=new Vector3(Mathf.Cos(b)*p.x,p.y,Mathf.Sin(b)*p.x);
            Vector3 qa=new Vector3(Mathf.Cos(a)*q.x,q.y,Mathf.Sin(a)*q.x),qb=new Vector3(Mathf.Cos(b)*q.x,q.y,Mathf.Sin(b)*q.x);
            Quad(pa,qa,qb,pb);
        }
        return FinishMesh();
    }
    static Mesh ClosedRingR06(float outer,float inner,float start,float span,int n,float depth)
    {
        ResetMesh();
        for(int i=0;i<n;i++)
        {
            float a=(start+span*i/n)*Mathf.Deg2Rad,b=(start+span*(i+1)/n)*Mathf.Deg2Rad;
            Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
            Vector3 ia=x*inner,ib=y*inner,oa=x*outer,ob=y*outer,d=Vector3.down*depth;
            Quad(ia,ib,ob,oa);Quad(oa,ob,ob+d,oa+d);Quad(ia+d,ib+d,ib,ia);Quad(ia+d,oa+d,ob+d,ib+d);
            if(i==0) Quad(ia,oa,oa+d,ia+d);
            if(i==n-1) Quad(ib,ib+d,ob+d,ob);
        }
        return FinishMesh();
    }
    static Mesh ArrivalRearR06()
    {
        var edge=new List<Vector3>{new Vector3(-15,-.5f,0),new Vector3(15,-.5f,0)};
        for(int i=0;i<=48;i++) {float a=i*Mathf.PI/48;edge.Add(new Vector3(15*Mathf.Cos(a),4.25f+9*Mathf.Sin(a),0));}
        ResetMesh();
        for(int i=0;i<edge.Count;i++)
        {
            Vector3 a=edge[i],b=edge[(i+1)%edge.Count],front=Vector3.forward*.3f,back=-front,center=Vector3.up*4;
            Triangle(center+front,a+front,b+front);Triangle(center+back,b+back,a+back);
            Quad(a+front,a+back,b+back,b+front);
        }
        return FinishMesh();
    }
    [MenuItem("Bird/Coastal world/Refine only R06 saved rock shoulder")]
    public static void RefineRockR06()
    {
        if(Load("R06 Arrival ridge shoulder")==null) throw new InvalidOperationException("Apply R06 first.");
        SaveMesh("R06 Arrival ridge shoulder",ArrivalRockR06());
        AssetDatabase.SaveAssets();
    }
    static Mesh ArrivalRockR06()
    {
        // One closed ridge extension with a real hollow behind the white rooms.
        // Front lip follows their envelope; the outer shoulder retreats/upslopes
        // into the existing long ridge. No interior rock or relocated architecture.
        var inner=new List<Vector3>{new Vector3(-26.4f,-.6f,4.1f),new Vector3(0,-.6f,4.1f),new Vector3(26.4f,-.6f,4.1f),new Vector3(26.4f,6.35f,4.1f)};
        // A slightly oversized twelve-chord cavity clears the white vault's
        // finer silhouette. Broad geological facets avoid narrow radial strips.
        for(int i=0;i<=12;i++) {float a=i*Mathf.PI/12;inner.Add(new Vector3(15.5f*Mathf.Cos(a),Mathf.Max(6.35f,4.4f+9.4f*Mathf.Sin(a)),4.1f));}
        inner.Add(new Vector3(-26.4f,6.35f,4.1f));
        var outer=new Vector3[4,inner.Count];Vector3 center=new Vector3(0,3,0);
        float[] right={34,44,42,32},left={44,58,75,90},rise={18,28,37,48};
        float[] retreat={-1,-20,-45,-85},shift={0,-3,-12,-28};
        for(int i=0;i<inner.Count;i++)
        {
            Vector3 d=(inner[i]-new Vector3(0,3,4.1f)).normalized;
            for(int ring=0;ring<4;ring++)
            {
                float rx=d.x>=0?right[ring]:left[ring];
                float ry=d.y>=0?rise[ring]:41f;
                float r=1/Mathf.Sqrt(d.x*d.x/(rx*rx)+d.y*d.y/(ry*ry));
                Vector3 p=center+d*r;
                if(inner[i].y<0) p=new Vector3(inner[i].x/26.4f*rx,-38,0);
                p.x+=shift[ring];
                // The coastal lip is low; the ridge rises and recedes inland.
                // All rear-ring vertices share a plane for a closed end cap.
                p.z=retreat[ring]+(ring==3?0:2.5f*Mathf.Sin(d.x*5+ring)*Mathf.Max(0,d.y));
                outer[ring,i]=p;
            }
        }
        ResetMesh();
        for(int i=0;i<inner.Count;i++)
        {
            int j=(i+1)%inner.Count;Vector3 a=inner[i],b=inner[j],depth=Vector3.back*23.3f;
            Quad(a,outer[0,i],outer[0,j],b);
            for(int ring=0;ring<3;ring++) Quad(outer[ring,i],outer[ring+1,i],outer[ring+1,j],outer[ring,j]);
            Quad(a,b,b+depth,a+depth);
            Triangle(new Vector3(0,3,-19.2f),a+depth,b+depth);
            Triangle(new Vector3(-28,3,-85),outer[3,j],outer[3,i]);
        }
        return FinishMesh();
    }
}
#endif
