using System;
using UnityEngine;

namespace Bird3DCursor
{
    /// <summary>Rotates incoming world-space samples around a fixed origin and axis.
    /// The caller owns all renderers and materials. History stays in world space.</summary>
    public sealed class BirdRadialTrail
    {
        private readonly BirdTrail[] trails;
        private readonly Quaternion[] rotations;
        private readonly Vector3 origin, axis;
        private int copies;

        public int Copies { get { return copies; } }
        public int Count { get { return trails[0].Count; } }

        public BirdRadialTrail(LineRenderer[] renderers, Color tint, Vector3 origin, Vector3 axis,
            int capacity = 128, float lifetime = 2.5f)
        {
            if (renderers == null) throw new ArgumentNullException(nameof(renderers));
            if (renderers.Length < 1 || renderers.Length > 12)
                throw new ArgumentOutOfRangeException(nameof(renderers), "Supply 1 to 12 distinct renderers.");
            if (!Finite(origin) || !Finite(axis) || axis.sqrMagnitude < 0.000001f ||
                float.IsInfinity(axis.sqrMagnitude)) throw new ArgumentException("A finite origin and nonzero finite axis are required.");
            // Validate ownership before changing any renderer.
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) throw new ArgumentException("Every renderer must exist.");
                for (int j = 0; j < i; j++)
                    if (renderers[i] == renderers[j]) throw new ArgumentException("Renderers must be distinct.");
            }
            this.origin = origin;
            this.axis = axis.normalized;
            trails = new BirdTrail[renderers.Length];
            rotations = new Quaternion[renderers.Length];
            for (int i = 0; i < trails.Length; i++)
                trails[i] = new BirdTrail(renderers[i], tint, capacity, lifetime);
            SetCopies(1);
        }

        // Changing multiplicity clears old strokes to avoid joining different radial layouts.
        public void SetCopies(int value)
        {
            if (value < 1 || value > trails.Length) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == copies) return;
            Clear();
            copies = value;
            for (int i = 0; i < copies; i++) rotations[i] = Quaternion.AngleAxis(360f * i / copies, axis);
        }

        public void Update(Vector3 position, bool tracked, float time)
        {
            for (int i = 0; i < copies; i++)
                trails[i].Update(origin + rotations[i] * (position - origin), tracked, time);
        }

        public void Clear() { foreach (var trail in trails) trail.Clear(); }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
