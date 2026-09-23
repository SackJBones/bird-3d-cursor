using UnityEngine;

namespace Bird3DCursor
{
    /// <summary>Optional visual feedback for an interactable. Assign a separate visual child so
    /// scale feedback does not change the hit collider. Owns that renderer's property block while enabled.</summary>
    public sealed class BirdInteractableVisual : MonoBehaviour
    {
        public BirdInteractable source;
        public Renderer visual;
        public Color idleColor = new Color(0.35f, 0.4f, 0.5f);
        public Color hoverColor = new Color(0.3f, 0.85f, 1f);
        public Color selectedColor = new Color(1f, 0.65f, 0.25f);
        [Min(0)] public float duration = 0.12f;
        [Min(0)] public float hoverScale = 1.08f;
        [Min(0)] public float selectedScale = 0.94f;

        private Renderer boundVisual;
        private MaterialPropertyBlock original, working;
        private Vector3 originalScale, fromScale;
        private Color currentColor, fromColor;
        private float elapsed;
        private int state = -1;
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private void LateUpdate()
        {
            if (boundVisual == null)
            {
                if (visual == null) return;
                boundVisual = visual;
                originalScale = visual.transform.localScale;
                original = new MaterialPropertyBlock();
                working = new MaterialPropertyBlock();
                visual.GetPropertyBlock(original);
                visual.GetPropertyBlock(working);
                currentColor = idleColor;
            }
            bool active = source != null && source.isActiveAndEnabled;
            int next = !active ? 0 : source.IsSelected ? 2 : source.IsHovered ? 1 : 0;
            if (next != state)
            {
                state = next;
                elapsed = 0;
                fromScale = boundVisual.transform.localScale;
                fromColor = currentColor;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0 ? Mathf.Clamp01(elapsed / duration) : 1;
            t = t * t * (3 - 2 * t);
            Color targetColor = state == 2 ? selectedColor : state == 1 ? hoverColor : idleColor;
            float targetScale = state == 2 ? selectedScale : state == 1 ? hoverScale : 1;
            currentColor = Color.LerpUnclamped(fromColor, targetColor, t);
            boundVisual.transform.localScale = Vector3.LerpUnclamped(fromScale, originalScale * targetScale, t);
            working.SetColor(ColorProperty, currentColor);
            boundVisual.SetPropertyBlock(working);
        }

        private void OnDisable()
        {
            if (boundVisual != null)
            {
                boundVisual.transform.localScale = originalScale;
                boundVisual.SetPropertyBlock(original);
            }
            boundVisual = null;
            state = -1;
        }
    }
}
