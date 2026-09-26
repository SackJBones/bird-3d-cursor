using System;
using UnityEngine;

namespace Bird3DCursor.UI
{
    /// <summary>Far-side contact of a finite logical pointer segment with a spherical volume.</summary>
    public static class BirdSphereContact
    {
        public static bool TryGetBackSurface(SphereCollider sphere, Vector3 origin, Vector3 point,
            float margin, out Vector3 normal, out Vector3 hit)
        {
            normal = hit = Vector3.zero;
            Vector3 center;
            float worldRadius;
            if (!TryGetSphere(sphere,out center,out worldRadius) ||
                !Finite(origin) || !Finite(point) || !Finite(margin) || margin < 0) return false;
            double radius=worldRadius;

            double dx=(double)point.x-origin.x, dy=(double)point.y-origin.y, dz=(double)point.z-origin.z;
            double length=Math.Sqrt(dx*dx+dy*dy+dz*dz);
            if (length <= 1e-7) return false;
            dx/=length; dy/=length; dz/=length;
            double cx=(double)center.x-origin.x, cy=(double)center.y-origin.y, cz=(double)center.z-origin.z;
            double along=cx*dx+cy*dy+cz*dz;
            double px=cx-along*dx, py=cy-along*dy, pz=cz-along*dz;
            // Closest approach avoids subtracting two huge squared distances for a remote origin.
            double radicand=radius*radius-(px*px+py*py+pz*pz);
            if (radicand <= radius*radius*1e-12) return false; // A tangent has no traversed interior.
            double beyond=Math.Sqrt(radicand), far=along+beyond;
            if (far <= 0 || length <= far+margin) return false;
            normal=new Vector3((float)((beyond*dx-px)/radius), (float)((beyond*dy-py)/radius), (float)((beyond*dz-pz)/radius)).normalized;
            hit=center+normal*(float)radius;
            return Finite(normal) && Finite(hit) && normal.sqrMagnitude > .5f;
        }

        internal static bool TryGetSphere(SphereCollider sphere, out Vector3 center, out float radius)
        {
            center=Vector3.zero; radius=0;
            if (sphere == null || !sphere.enabled || !sphere.gameObject.activeInHierarchy) return false;
            Vector3 scale=sphere.transform.lossyScale;
            radius=sphere.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            center=sphere.transform.TransformPoint(sphere.center);
            return Finite(scale) && Finite(center) && Finite(radius) && radius > 1e-7f;
        }

        internal static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        internal static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    }
}
