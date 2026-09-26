using UnityEngine;

namespace Bird3DCursor.UI
{
    /// <summary>Optional Built-in _Color feedback on a separate visual child, without changing hit geometry.</summary>
    [AddComponentMenu("Bird/UI/Menu Feedback")]
    public sealed class BirdMenuFeedback : MonoBehaviour
    {
        [SerializeField] BirdMenuElement source;
        [SerializeField] Renderer visual;
        [SerializeField] Color idle = new Color(.13f,.2f,.27f);
        [SerializeField] Color highlighted = new Color(.1f,.85f,1);
        [SerializeField] Color activated = new Color(1,.65f,.2f);
        [SerializeField] Color background = new Color(.08f,.1f,.13f);
        [Min(0)] [SerializeField] float transitionSeconds = .12f;
        Renderer bound;
        MaterialPropertyBlock original, working;
        Color color;
        static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public void Configure(BirdMenuElement element, Renderer target)
        {
            Restore(); source = element; visual = target;
        }

        void LateUpdate() { Apply(Time.unscaledDeltaTime); }
        public void Apply(float dt)
        {
            if (visual == null || source == null) return;
            if (bound != visual)
            {
                Restore(); bound = visual;
                original = new MaterialPropertyBlock(); working = new MaterialPropertyBlock();
                bound.GetPropertyBlock(original); bound.GetPropertyBlock(working); color = idle;
            }
            Color target = source.State == BirdMenuVisualState.Activated ? activated :
                source.State == BirdMenuVisualState.Highlighted ? highlighted :
                source.State == BirdMenuVisualState.Background || source.State == BirdMenuVisualState.Inactive ? background : idle;
            float t = transitionSeconds <= 0 ? 1 : 1-Mathf.Exp(-Mathf.Max(0,dt)/transitionSeconds);
            color = Color.Lerp(color,target,t);
            working.SetColor(ColorProperty,color); bound.SetPropertyBlock(working);
        }

        void OnDisable() { Restore(); }
        void Restore() { if (bound != null) bound.SetPropertyBlock(original); bound = null; }
    }
}
