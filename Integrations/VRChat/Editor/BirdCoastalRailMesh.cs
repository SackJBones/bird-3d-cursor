#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Offline square-section sweeps. No runtime mesh generation or new components.
public static class BirdCoastalRailMesh
{
    public static List<List<Vector2>> Paths(List<Vector2[]> segments)
    {
        var paths=new List<List<Vector2>>();
        foreach(var s in segments)
        {
            if(paths.Count==0||Vector2.Distance(paths.Last().Last(),s[0])>.001f)paths.Add(new List<Vector2>{s[0]});
            paths.Last().Add(s[1]);
        }
        if(paths.Count>1&&Vector2.Distance(paths.Last().Last(),paths[0][0])<.001f)
        {var joined=paths.Last();joined.AddRange(paths[0].Skip(1));paths[0]=joined;paths.RemoveAt(paths.Count-1);}
        return paths;
    }
    public static List<Vector2> Posts(List<Vector2[]> segments)
    {
        // Preserve previous post positions; only finish the genuine open ends.
        var posts=new List<Vector2>();float distance=0;
        foreach(var s in segments){distance+=Vector2.Distance(s[0],s[1]);if(distance>=1.8f){posts.Add(s[0]);distance=0;}}
        foreach(var path in Paths(segments))if(Vector2.Distance(path[0],path.Last())>.001f)
            foreach(var end in new[]{path[0],path.Last()})if(posts.All(p=>Vector2.Distance(p,end)>.04f))posts.Add(end);
        return posts;
    }
    public static Mesh Build(List<Vector2[]> segments,float floor)
    {
        var v=new List<Vector3>();var normals=new List<Vector3>();var t=new List<int>();
        Action<Vector3,Vector3,Vector3,Vector3> quad=(a,b,c,d)=>{
            int k=v.Count;v.AddRange(new[]{a,b,c,d});var n=Vector3.Cross(b-a,c-a).normalized;
            normals.AddRange(new[]{n,n,n,n});t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});};
        foreach(var source in Paths(segments))
        {
            bool closed=Vector2.Distance(source[0],source.Last())<.001f;
            var points=closed?source.Take(source.Count-1).ToList():source;
            int n=points.Count;if(n<2)throw new Exception("Rail path too short");
            var miters=new Vector3[n];
            for(int i=0;i<n;i++)
            {
                var before=(points[i]-points[i==0?(closed?n-1:0):i-1]).normalized;
                var after=(points[i==n-1?(closed?0:n-1):i+1]-points[i]).normalized;
                if(!closed&&i==0)before=after;if(!closed&&i==n-1)after=before;
                var a=new Vector2(before.y,-before.x);var b=new Vector2(after.y,-after.x);var m=(a+b).normalized;
                float denominator=Vector2.Dot(m,b);if(denominator<.8f)throw new Exception("Rail bend is too sharp for bounded square miter");
                miters[i]=new Vector3(m.x,0,m.y)/denominator;
            }
            Vector2[] section={new Vector2(-.05f,-.05f),new Vector2(-.05f,.05f),new Vector2(.05f,.05f),new Vector2(.05f,-.05f)};
            Func<int,int,Vector3> corner=(i,j)=>new Vector3(points[i].x,floor+1.05f,points[i].y)+miters[i]*section[j].x+Vector3.up*section[j].y;
            for(int face=0;face<4;face++)
            {
                int start=v.Count;int rings=closed?n+1:n;
                for(int i=0;i<rings;i++)
                {
                    int k=i%n;var normal=face==0?-miters[k].normalized:face==1?Vector3.up:face==2?miters[k].normalized:Vector3.down;
                    v.Add(corner(k,face));v.Add(corner(k,(face+1)%4));normals.Add(normal);normals.Add(normal);
                }
                for(int i=0;i<rings-1;i++){int a=start+i*2,b=a+2;t.AddRange(new[]{a,b,b+1,a,b+1,a+1});}
            }
            if(!closed)
            {
                quad(corner(0,0),corner(0,1),corner(0,2),corner(0,3));
                quad(corner(n-1,3),corner(n-1,2),corner(n-1,1),corner(n-1,0));
            }
        }
        foreach(var p in Posts(segments))
        {
            var c=new Vector3(p.x,floor+.53f,p.y);var size=new Vector3(.055f,1.06f,.055f);var box=new Vector3[8];
            for(int i=0;i<8;i++)box[i]=c+Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            foreach(var f in new[]{new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,4,6,2},new[]{1,3,7,5},new[]{2,6,7,3},new[]{0,1,5,4}})quad(box[f[0]],box[f[1]],box[f[2]],box[f[3]]);
        }
        var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetTriangles(t,0);mesh.RecalculateBounds();mesh.uv=v.Select(q=>new Vector2(q.x,q.z)*.2f).ToArray();return mesh;
    }
}
#endif
