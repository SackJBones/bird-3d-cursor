using UnityEngine;

public class SetEffectColors : MonoBehaviour
{
    public Color defaultColor;
    public bool onStartup;
    public float lightIntensityFactor = 0.9f;

    public void Start() {
        if (onStartup)
            SetChildrenColors(transform, defaultColor);
    }

    public void TurnRed()
    {
        SetChildrenColors(transform, Color.red);
    }

    public void TurnBlue()
    {
        SetChildrenColors(transform, Color.blue);
    }

    public void TurnGreen()
    {
        SetChildrenColors(transform, Color.green);
    }

    public void TurnYellow()
    {
        SetChildrenColors(transform, Color.yellow);
    }

    public void TurnWhite()
    {
        SetChildrenColors(transform, Color.white);
    }

    public void TurnBlack()
    {
        SetChildrenColors(transform, Color.black);
    }

    public void TurnOrange()
    {
        SetChildrenColors(transform, new Color(1f, .64f, 0f));
    }

    public void TurnCyan()
    {
        SetChildrenColors(transform, Color.cyan);
    }

    public void TurnMagenta()
    {
        SetChildrenColors(transform, Color.magenta);
    }

    public void TurnPurple()
    {
        SetChildrenColors(transform, new Color(.78f, .16f, .82f));
    }

    public void TurnLightBlue()
    {
        SetChildrenColors(transform, new Color(0.5f, 0.5f, 1));
    }

    public void TurnLightGreen()
    {
        SetChildrenColors(transform, new Color(0.5f, 1, 0.5f));
    }

    private void SetChildrenColors(Transform parent, Color targetColor)
    {
        foreach (Transform child in parent)
        {
            // Change the color of Particle System
            ParticleSystem ps = child.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                // handle different types of startcolors differently
                if (main.startColor.mode == ParticleSystemGradientMode.Color)
                    main.startColor = targetColor;
                else if (main.startColor.mode == ParticleSystemGradientMode.TwoColors)
                {
                    Color color1 = main.startColor.colorMin;
                    Color color2 = main.startColor.colorMax;

                    // Check which color is "smaller"
                    float sum1 = color1.r + color1.g + color1.b;
                    float sum2 = color2.r + color2.g + color2.b;

                    if (sum1 < sum2)
                    {
                        // color1 is the "first" color
                        main.startColor = new ParticleSystem.MinMaxGradient(targetColor, color2);
                    }
                    else
                    {
                        // color2 is the "first" color
                        main.startColor = new ParticleSystem.MinMaxGradient(color1, targetColor);
                    }
                }
                else if (main.startColor.mode == ParticleSystemGradientMode.Gradient) // change the first color to the target color and leave all the others unchanged
                {
                    Gradient grad = main.startColor.gradient;
                    grad.colorKeys[0].color = targetColor;
                    main.startColor = grad;
                }
                // change the color of trails if present
                if (ps.trails.enabled)
                {
                    var trails = ps.trails;
                    // make sure trail fades out to zero alpha at the end
                    trails.colorOverTrail = new ParticleSystem.MinMaxGradient(targetColor, new Color(targetColor.r, targetColor.g, targetColor.b, 0));
                }
            }

            // Change the color of Lights
            Light light = child.GetComponent<Light>();
            if (light != null)
            {
                light.color = Color.Lerp(targetColor, Color.white, lightIntensityFactor);
            }

            // change the color of linerenderers
            LineRenderer lr = child.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.material.color = targetColor;
            }

            // Continue the recursion if the child has children
            if (child.childCount > 0)
            {
                SetChildrenColors(child, targetColor);
            }
        }
    }
}
