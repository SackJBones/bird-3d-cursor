#if UNITY_EDITOR
using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Bird/Coastal support profile")]
public class BirdCoastalSupportProfile : ScriptableObject
{
    [Serializable] public struct Section
    {
        public float x, height, halfWidth, halfLength;
        public Section(float x, float height, float width, float length)
        { this.x = x; this.height = height; halfWidth = width; halfLength = length; }
    }
    [Tooltip("World-aligned section centers and horizontal radii in metres, ordered bottom to top. Recheck cliff contact, basin clearance and rebake after editing.")]
    public Section[] sections = {
        new Section(-36, -20, 5, 5), new Section(-34, -15, 8, 5.5f),
        new Section(-27, -10, 12, 6), new Section(-14, -6, 13, 6),
        new Section(-3, -4.5f, 8, 5.5f), new Section(3, -3.25f, 6, 5),
        new Section(3, -2.35f, 6, 5)
    };
    public float centerZ = 51;
    [Range(16,64)] public int circumferenceSegments = 48;
    [Range(1,8)] public int samplesPerSpan = 4;
}
#endif
