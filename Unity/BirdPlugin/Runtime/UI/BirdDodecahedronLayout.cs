using System;
using UnityEngine;

namespace Bird3DCursor.UI
{
    /// <summary>Twelve dodecahedral face directions; independent of scrolling, selection and visuals.</summary>
    [AddComponentMenu("Bird/UI/Dodecahedron Layout")]
    public sealed class BirdDodecahedronLayout : MonoBehaviour
    {
        [SerializeField] Transform[] items = new Transform[12];
        [Tooltip("Actual center-to-item distance in local meters.")]
        [Min(0)] [SerializeField] float radius = .5f;

        public void Configure(Transform[] targets, float distance)
        {
            items=targets != null ? (Transform[])targets.Clone() : new Transform[0]; radius=distance;
            Apply();
        }

        void Start() { Apply(); }
        [ContextMenu("Apply Layout")]
        public void Apply()
        {
            if (items.Length != 12 || !BirdSphereContact.Finite(radius) || radius < 0) return;
            for (int i=0;i<12;i++)
                if (items[i] != null) items[i].position=transform.TransformPoint(Direction(i)*radius);
        }

        public static Vector3 Direction(int index)
        {
            if (index < 0 || index >= 12) throw new ArgumentOutOfRangeException("index");
            float phi=(1+Mathf.Sqrt(5))*.5f;
            float a=(index%4 < 2 ? -1 : 1), b=(index%2 == 0 ? -phi : phi);
            return (index < 4 ? new Vector3(0,b,a) : index < 8 ? new Vector3(a,0,b) : new Vector3(b,a,0)).normalized;
        }
    }
}
