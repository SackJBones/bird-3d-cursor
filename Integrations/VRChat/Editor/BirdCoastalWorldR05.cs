#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static partial class BirdCoastalWorldAuthoring
{
    // A one-time, deliberately scoped migration of the authored R04 prefabs.
    // It never recreates the scene or replaces unrelated children/overrides.
    // Future design iterations should edit these ordinary assets directly.
    [MenuItem("Bird/Coastal world/Apply R05 doorway and tide room revision once")]
    public static void ApplyR05()
    {
        string[] names={"01 Arrival cavern","02 Arrival social wings","03 Supported coastal terraces","04 Lower water and hidden lounge","06 Experience anchors"};
        var roots=new GameObject[names.Length];
        try
        {
            for(int i=0;i<names.Length;i++)roots[i]=PrefabUtility.LoadPrefabContents(Folder+"/Prefabs/"+names[i]+".prefab");
            if(roots[0].transform.Find("R05 curved passages")!=null)throw new InvalidOperationException("R05 is already applied. Edit the existing authored prefabs; do not reapply this migration.");
            // Resolve every existing object before the first mutation.
            foreach(int s in new[]{-1,1})foreach(string n in new[]{"Side wing opening ","Wing connector floor ","Wing connector ceiling "})RequiredR05(roots[0],n+s);
            foreach(string n in new[]{"Lantern room","Fold gallery"}){RequiredR05(roots[1],n+"/Room threshold");RequiredR05(roots[1],n+"/Unshadowed vertex fill");}
            foreach(string n in new[]{"Inhabited cliff shoulder","Lounge threshold","Cliff lantern lounge/Room threshold","Cliff lantern lounge/Unshadowed vertex fill"})RequiredR05(roots[2],n);
            foreach(string n in new[]{"Discovery landing","Hidden tide room","Hidden tide room/Low table","Hidden tide room/Sculpture timber backdrop","Hidden tide room/Sculpture plinth","Hidden tide room/Folded ribbon sculpture","Hidden tide room/Warm pendant","Hidden tide room/Pendant cord","Hidden tide room/Unshadowed vertex fill","Hidden tide room/Sofa base -1","Hidden tide room/Sofa cushion -1","Hidden tide room/Sofa back -1"})RequiredR05(roots[3],n);
            RequiredR05(roots[4],"Hidden tide lounge");
            foreach(string n in new[]{"R05 Wing portal","R05 Wing room portal","R05 Wing throat","R05 Cliff shoulder"})
                if(Load(n)!=null)throw new InvalidOperationException("Reserved revision mesh exists before migration: "+n);
            white=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Materials/Plaster.mat");
            if(white==null)throw new InvalidOperationException("Missing authored plaster material.");

            // A single circular bore joins both portals. Its interior continues
            // through the thickness of each wall, with no flat beam in the mouth.
            var wingPortal=SaveMesh("R05 Wing portal",Portal(3,4.3f,2.6f,1.7f,64));
            var wingRoomPortal=SaveMesh("R05 Wing room portal",Portal(4.5f,6,2.6f,1.7f,64));
            var wingThroat=SaveMesh("R05 Wing throat",CircularThroatR05(2.6f,1.7f,1.2f,64));
            foreach(int s in new[]{-1,1})
            {
                var entry=RequiredR05(roots[0],"Side wing opening "+s);ReplaceMeshR05(entry,wingPortal);entry.localRotation=Quaternion.Euler(0,s*90,0);
                var floor=RequiredR05(roots[0],"Wing connector floor "+s);
                floor.localPosition=new Vector3(s*16,-.2f,1);floor.localScale=new Vector3(2,.4f,5.2f);
                var throat=RequiredR05(roots[0],"Wing connector ceiling "+s);
                throat.name="Wing curved passage "+s;throat.localPosition=new Vector3(s*16,0,1);throat.localRotation=Quaternion.Euler(0,90,0);throat.localScale=Vector3.one;
                UnityEngine.Object.DestroyImmediate(throat.GetComponent<BoxCollider>());
                throat.gameObject.AddComponent<MeshCollider>();ReplaceMeshR05(throat,wingThroat);
            }
            // Room openings already use the same 2.6 m circle / 1.7 m center.
            foreach(string n in new[]{"Lantern room","Fold gallery"}){ReplaceMeshR05(RequiredR05(roots[1],n+"/Room threshold"),wingRoomPortal);RequiredR05(roots[1],n+"/Unshadowed vertex fill").GetComponent<Light>().intensity=.7f;}

            // One facade owns the cliff opening. Retain the old inner object,
            // disabled, for inspection instead of nesting two competing arches.
            var cliff=RequiredR05(roots[2],"Inhabited cliff shoulder");
            ReplaceMeshR05(cliff,SaveMesh("R05 Cliff shoulder",Portal(14,18,2.6f,7.7f,64,13,5)));
            var inner=RequiredR05(roots[2],"Cliff lantern lounge/Room threshold");
            inner.GetComponent<Renderer>().enabled=false;inner.GetComponent<Collider>().enabled=false;
            var threshold=RequiredR05(roots[2],"Lounge threshold");threshold.localPosition=new Vector3(-14.25f,5.8f,20);threshold.localScale=new Vector3(1.5f,.4f,4);
            RequiredR05(roots[2],"Cliff lantern lounge/Unshadowed vertex fill").GetComponent<Light>().intensity=.7f;

            // The tide room now faces along the coast. Its entrance remains a
            // short turn off the lower promenade, clear of the discovery stair.
            var tide=RequiredR05(roots[3],"Hidden tide room");tide.localRotation=Quaternion.identity;
            var landing=RequiredR05(roots[3],"Discovery landing");landing.localPosition=new Vector3(-20.75f,-2.2f,43.5f);landing.localScale=new Vector3(5.5f,.4f,3);
            RequiredR05(roots[4],"Hidden tide lounge").localPosition=new Vector3(-19,-2,40);
            var sofa=RequiredR05(roots[3],"Hidden tide room/Sofa base -1");sofa.localPosition=new Vector3(-1,.25f,-2.4f);sofa.localScale=new Vector3(3.8f,.5f,1.1f);
            sofa=RequiredR05(roots[3],"Hidden tide room/Sofa cushion -1");sofa.localPosition=new Vector3(-1,.56f,-2.4f);sofa.localScale=new Vector3(3.7f,.13f,1.05f);
            sofa=RequiredR05(roots[3],"Hidden tide room/Sofa back -1");sofa.localPosition=new Vector3(-1,.9f,-2.9f);sofa.localScale=new Vector3(3.8f,.9f,.2f);
            RequiredR05(roots[3],"Hidden tide room/Low table").localPosition=new Vector3(-.4f,.28f,-.2f);
            var backdrop=RequiredR05(roots[3],"Hidden tide room/Sculpture timber backdrop");backdrop.localPosition=new Vector3(-4.31f,2.1f,-.8f);backdrop.localRotation=Quaternion.Euler(0,90,0);backdrop.localScale=new Vector3(3,3.5f,.08f);
            var plinth=RequiredR05(roots[3],"Hidden tide room/Sculpture plinth");plinth.localPosition=new Vector3(-3.4f,.6f,0);plinth.localScale=new Vector3(.8f,1.2f,.8f);
            var study=RequiredR05(roots[3],"Hidden tide room/Folded ribbon sculpture");study.localPosition=new Vector3(-3.4f,1.85f,0);study.localScale=Vector3.one*.65f;
            var lamp=new Vector3(-.8f,2.7f,-.8f);
            RequiredR05(roots[3],"Hidden tide room/Warm pendant").localPosition=lamp;
            RequiredR05(roots[3],"Hidden tide room/Pendant cord").localPosition=lamp+Vector3.up*1.3f;
            var fill=RequiredR05(roots[3],"Hidden tide room/Unshadowed vertex fill");fill.localPosition=lamp;fill.GetComponent<Light>().intensity=.7f;
            region=new GameObject("R05 tide room approach").transform;region.SetParent(roots[3].transform,false);meshNumber=0;
            GuardLine("Tide approach sea edge",new Vector3(-23.5f,-2,45),new Vector3(-18,-2,45),white);
            GuardLine("Tide approach cliff edge",new Vector3(-23.5f,-2,42),new Vector3(-23.5f,-2,45),white);
            new GameObject("R05 curved passages").transform.SetParent(roots[0].transform,false);
            for(int i=0;i<names.Length;i++)PrefabUtility.SaveAsPrefabAsset(roots[i],Folder+"/Prefabs/"+names[i]+".prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("R05 applied to five existing prefab assets; scene, profile mesh GUIDs and unrelated content retained.");
        }
        finally{foreach(var root in roots)if(root!=null)PrefabUtility.UnloadPrefabContents(root);region=null;}
        RefineR05();
    }
    // Also callable separately on the reviewed first-pass R05 assets. The
    // independent critic identified throat seams and an obstructed seated view.
    public static void RefineR05()
    {
        string[] names={"01 Arrival cavern","04 Lower water and hidden lounge","06 Experience anchors"};
        var roots=new GameObject[names.Length];
        try
        {
            for(int i=0;i<names.Length;i++)roots[i]=PrefabUtility.LoadPrefabContents(Folder+"/Prefabs/"+names[i]+".prefab");
            RequiredR05(roots[0],"R05 curved passages");
            if(roots[0].transform.Find("R05 aligned bores and coast frame")!=null)throw new InvalidOperationException("R05 refinement already applied; edit the authored assets instead.");
            foreach(string n in new[]{"Hidden tide room","Discovery landing","Lower promenade","R05 tide room approach/Tide approach sea edge","R05 tide room approach/Tide approach sea edge safety boundary","R05 tide room approach/Tide approach cliff edge","R05 tide room approach/Tide approach cliff edge safety boundary"})RequiredR05(roots[1],n);
            RequiredR05(roots[2],"Hidden tide lounge");
            foreach(string n in new[]{"R05 Wing portal","R05 Wing room portal","R05 Wing throat"})if(Load(n)==null)throw new InvalidOperationException("Missing first-pass mesh: "+n);
            // One bore owns the full passage interior, including wall thickness.
            // All circular boundaries use the exact same angular tessellation.
            SaveMesh("R05 Wing portal",Portal(3,4.3f,2.6f,1.7f,64,includeRim:false));
            SaveMesh("R05 Wing room portal",Portal(4.5f,6,2.6f,1.7f,64,includeRim:false));
            SaveMesh("R05 Wing throat",CircularThroatR05(2.6f,1.7f,2,64));
            var tide=RequiredR05(roots[1],"Hidden tide room");tide.localPosition=new Vector3(-20.5f,-2,38.8f);tide.localRotation=Quaternion.Euler(0,25,0);
            var landing=RequiredR05(roots[1],"Discovery landing");landing.localPosition=new Vector3(-19.5f,-2.22f,43);landing.localScale=new Vector3(11,.4f,5);
            // Stone underlaps the rotated timber floor 2 cm below its surface,
            // preventing coplanar flicker while keeping a shallow transition.
            var promenade=RequiredR05(roots[1],"Lower promenade");promenade.localPosition=new Vector3(2.5f,-2.27f,43);
            RequiredR05(roots[2],"Hidden tide lounge").localPosition=tide.localPosition+tide.localRotation*new Vector3(0,0,2.4f);
            var approach=RequiredR05(roots[1],"R05 tide room approach");
            UpdateGuardR05(approach,"Tide approach sea edge",new Vector3(-25,-2.02f,45.5f),new Vector3(-14,-2.02f,45.5f));
            UpdateGuardR05(approach,"Tide approach cliff edge",new Vector3(-25,-2.02f,40.5f),new Vector3(-25,-2.02f,45.5f));
            new GameObject("R05 aligned bores and coast frame").transform.SetParent(roots[0].transform,false);
            for(int i=0;i<names.Length;i++)PrefabUtility.SaveAsPrefabAsset(roots[i],Folder+"/Prefabs/"+names[i]+".prefab");
            AssetDatabase.SaveAssets();
        }
        finally{foreach(var root in roots)if(root!=null)PrefabUtility.UnloadPrefabContents(root);}
    }
    static void UpdateGuardR05(Transform parent,string name,Vector3 a,Vector3 b)
    {
        Vector3 d=b-a;ResetMesh();AppendBox(d*.5f+Vector3.up*1.05f,new Vector3(.13f,.13f,d.magnitude),Quaternion.LookRotation(d));
        int n=Mathf.CeilToInt(d.magnitude/2);
        for(int i=0;i<=n;i++)AppendBox(d*((float)i/n)+Vector3.up*.53f,new Vector3(.065f,1.06f,.065f),Quaternion.identity);
        var rail=parent.Find(name);SaveMesh(rail.GetComponent<MeshFilter>().sharedMesh.name,FinishMesh());rail.localPosition=a;
        var boundary=parent.Find(name+" safety boundary");boundary.localPosition=(a+b)*.5f+Vector3.up*.52f;boundary.localScale=new Vector3(.10f,1.05f,d.magnitude);boundary.localRotation=Quaternion.LookRotation(d);
    }
    static Transform RequiredR05(GameObject root,string path)
    {
        var child=root.transform.Find(path);if(child==null)throw new InvalidOperationException("R05 requires authored object: "+root.name+"/"+path);return child;
    }
    static void ReplaceMeshR05(Transform target,Mesh mesh)
    {
        target.GetComponent<MeshFilter>().sharedMesh=mesh;
        var collider=target.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=mesh;
    }
    static Mesh CircularThroatR05(float radius,float cy,float length,int count)
    {
        ResetMesh();
        // Match the portal's circle chords exactly, clipping only at the floor.
        for(int i=0;i<count;i++)
        {
            float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
            Vector3 x=new Vector3(radius*Mathf.Cos(a),cy+radius*Mathf.Sin(a),-length*.5f),y=new Vector3(radius*Mathf.Cos(b),cy+radius*Mathf.Sin(b),-length*.5f);
            Vector3 ox=new Vector3((radius+.25f)*Mathf.Cos(a),cy+(radius+.25f)*Mathf.Sin(a),-length*.5f),oy=new Vector3((radius+.25f)*Mathf.Cos(b),cy+(radius+.25f)*Mathf.Sin(b),-length*.5f);
            if(x.y<0&&y.y<0)continue;
            if(x.y<0)x=Vector3.Lerp(x,y,-x.y/(y.y-x.y));
            if(y.y<0)y=Vector3.Lerp(y,x,-y.y/(x.y-y.y));
            Quad(x,x+Vector3.forward*length,y+Vector3.forward*length,y);
            Quad(ox,oy,oy+Vector3.forward*length,ox+Vector3.forward*length);
        }
        return FinishMesh(true);
    }
}
#endif
