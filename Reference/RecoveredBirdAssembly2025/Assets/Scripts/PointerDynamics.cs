using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PointerDynamics : MonoBehaviour
{
    public Bird bird;
    public GameObject perspectiveOrigin;
    private float minSize = 0.03f; // 2cm
    private float scaleCurveMult = 100f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.localScale = Mathf.Max(minSize, minSize * Mathf.Log(bird.range/scaleCurveMult + 1)*scaleCurveMult) * Vector3.one;
        transform.rotation = Quaternion.LookRotation(bird.birdPosition - perspectiveOrigin.transform.position);
    }
}
