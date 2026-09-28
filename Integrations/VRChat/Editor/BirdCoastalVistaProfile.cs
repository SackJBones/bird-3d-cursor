#if UNITY_EDITOR
using UnityEngine;

// Authoring data only; the world exports ordinary meshes/materials.
[CreateAssetMenu(menuName = "Bird/Coastal vista profile")]
public class BirdCoastalVistaProfile : ScriptableObject
{
    [Tooltip("Cross sections: longitudinal position, sideways offset, half-width, crest height (metres). Keep positions increasing; ends taper below the sea.")]
    public Vector4[] middleCoast = {
        new Vector4(-650, 60, 10, -4), new Vector4(-510, 30, 120, 65),
        new Vector4(-365, -20, 170, 165), new Vector4(-230, 5, 125, 92),
        new Vector4(-80, 50, 170, 115), new Vector4(80, 20, 145, 245),
        new Vector4(210, -35, 180, 145), new Vector4(370, -20, 145, 72),
        new Vector4(530, 70, 80, 35), new Vector4(670, 110, 8, -4)
    };
    public Vector4[] farCoast = {
        new Vector4(-850, 70, 10, -4), new Vector4(-650, 20, 145, 70),
        new Vector4(-390, -15, 240, 180), new Vector4(-160, 50, 160, 105),
        new Vector4(90, 0, 210, 260), new Vector4(280, -50, 170, 170),
        new Vector4(540, 30, 220, 100), new Vector4(820, 100, 10, -4)
    };
    [Header("Vertex-painted atmospheric palette; saved material stays independently editable")]
    public Color middleRock = new Color(.42f, .55f, .59f);
    public Color middleLand = new Color(.46f, .58f, .51f);
    public Color farRock = new Color(.59f, .69f, .73f);
    public Color farLand = new Color(.59f, .70f, .68f);
    public Color nearWater = new Color(.12f, .38f, .48f);
    public Color farWater = new Color(.53f, .69f, .75f);
    [Range(3000, 8000)] public float waterRadius = 6500;
}
#endif
