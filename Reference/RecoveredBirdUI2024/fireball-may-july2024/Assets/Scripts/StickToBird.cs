using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bird3DCursor;

public class StickToBird : MonoBehaviour
{
    BirdProvider bird;
    public GameObject birdHand;
    bool stuck;
    Collider thisCollider;
    Renderer thisRenderer;

    // Start is called before the first frame update
    void Start()
    {


        bird = birdHand.GetComponent<BirdProvider>();

        if (bird == null)
        {
            return;
        }
        thisCollider = gameObject.GetComponent<Collider>();
        thisRenderer = gameObject.GetComponent<Renderer>();
        stuck = false;
    }

    // Update is called once per frame
    void Update()
    {
        if ((thisCollider != null && thisCollider.bounds.Contains(bird.GetPosition()))
           || (thisCollider == null && thisRenderer != null && thisRenderer.bounds.Contains(bird.GetPosition())))
        {
            stuck = true;
        }
        if (stuck)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                transform.position = bird.GetPosition();
            }
            else
            {
                rb.MovePosition(bird.GetPosition());
            }
        }
    }
}
