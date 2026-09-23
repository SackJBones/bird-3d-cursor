using System;
using UnityEngine;

namespace Bird3DCursor
{
    /// <summary>A bounded world-space trail. The caller owns the renderer and material.
    /// Call Update every frame, including while stationary or untracked, using a monotonic clock.</summary>
    public sealed class BirdTrail
    {
        private readonly LineRenderer renderer;
        private readonly Vector3[] points;
        private readonly float[] times;
        private readonly float lifetime, sampleInterval, minimumDistance;
        private readonly Color tint;
        private int first, count;
        private float previousTime = float.NegativeInfinity;

        public int Count { get { return count; } }

        public BirdTrail(LineRenderer renderer, Color tint, int capacity = 128,
            float lifetime = 2.5f, float sampleInterval = 0.02f, float minimumDistance = 0.003f)
        {
            if (renderer == null) throw new ArgumentNullException(nameof(renderer));
            if (capacity < 2 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!Finite(lifetime) || lifetime <= 0) throw new ArgumentOutOfRangeException(nameof(lifetime));
            if (!Finite(sampleInterval) || sampleInterval < 0) throw new ArgumentOutOfRangeException(nameof(sampleInterval));
            if (!Finite(minimumDistance) || minimumDistance < 0) throw new ArgumentOutOfRangeException(nameof(minimumDistance));
            this.renderer = renderer;
            this.tint = tint;
            this.lifetime = lifetime;
            this.sampleInterval = sampleInterval;
            this.minimumDistance = minimumDistance;
            points = new Vector3[capacity];
            times = new float[capacity];
            renderer.useWorldSpace = true;
            renderer.loop = false;
            renderer.positionCount = 0;
        }

        public void Clear()
        {
            first = count = 0;
            renderer.positionCount = 0;
            previousTime = float.NegativeInfinity;
        }

        public void Update(Vector3 position, bool tracked, float time)
        {
            if (!tracked || !Finite(time) || !Finite(position.x) || !Finite(position.y) || !Finite(position.z))
            {
                Clear();
                return;
            }
            if (time < previousTime) Clear();
            previousTime = time;
            while (count > 0 && time - times[first] >= lifetime)
            {
                first = (first + 1) % points.Length;
                count--;
            }
            int last = (first + count - 1 + points.Length) % points.Length;
            if (count == 0 || (time - times[last] >= sampleInterval &&
                Vector3.Distance(position, points[last]) >= minimumDistance))
            {
                if (count == points.Length) { first = (first + 1) % points.Length; count--; }
                int slot = (first + count) % points.Length;
                points[slot] = position;
                times[slot] = time;
                count++;
            }
            renderer.positionCount = count;
            for (int i = 0; i < count; i++) renderer.SetPosition(i, points[(first + i) % points.Length]);
            Color start = tint, end = tint;
            start.a *= Mathf.Clamp01(1 - (time - times[first]) / lifetime);
            end.a *= Mathf.Clamp01(1 - (time - times[(first + count - 1) % points.Length]) / lifetime);
            renderer.startColor = start;
            renderer.endColor = end;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
