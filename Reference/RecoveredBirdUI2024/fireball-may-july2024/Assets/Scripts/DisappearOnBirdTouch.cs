using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bird3DCursor;

public class DisappearOnBirdTouch : MonoBehaviour
{
    public BirdProvider bird;
    public Material paintMaterial;
    Collider coll;
    Renderer rend;

    // Start is called before the first frame update
    void Start()
    {
        coll = GetComponent<Collider>();
        rend = GetComponent<Renderer>();
    }

    // Update is called once per frame
    void Update()
    {
        if (coll != null && bird.WentThrough(coll))
        {
            if (paintMaterial == null)
            {
                gameObject.SetActive(false);
            }
            else
            {
                rend.material = paintMaterial;
            }
        }
    }

}
