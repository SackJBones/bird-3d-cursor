using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GoGetEffectColor : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // get the seteffectcolors component of the parent and set this gameobject's color to the default color of the seteffectcolors component
        SetEffectColors setEffectColors = transform.parent.GetComponent<SetEffectColors>();
        //set both the material albedo and emission colors to the default color
        GetComponent<Renderer>().material.SetColor("_Color", setEffectColors.defaultColor);
        GetComponent<Renderer>().material.SetColor("_EmissionColor", setEffectColors.defaultColor * setEffectColors.lightIntensityFactor);
    }
}
