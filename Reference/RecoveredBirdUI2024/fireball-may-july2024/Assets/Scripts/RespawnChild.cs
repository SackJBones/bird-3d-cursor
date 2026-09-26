using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnChild : MonoBehaviour
{
    public GameObject child;
    Vector3 spawnPos;

    void Start()
    {
        spawnPos = transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (child != null && transform.childCount == 0)
        {
            Instantiate(child, spawnPos, transform.rotation, transform);
        }
    }

    // static method to destroy all of the children of all of the instances of RespawnChild, which will cause them all to respawn
    public static void DestroyAllChildren()
    {
        // get all of the instances of RespawnChild
        RespawnChild[] respawnChildren = FindObjectsOfType<RespawnChild>();
        // loop through all of the instances of RespawnChild
        foreach (RespawnChild respawnChild in respawnChildren)
        {
            // loop through all of the children of the instance of RespawnChild
            foreach (Transform child in respawnChild.transform)
            {
                // destroy the child
                Destroy(child.gameObject);
            }
        }
    }
}
