using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleByRange : MonoBehaviour
{
    public Bird bird;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.localScale = bird.range * Vector3.one;
    }
}
