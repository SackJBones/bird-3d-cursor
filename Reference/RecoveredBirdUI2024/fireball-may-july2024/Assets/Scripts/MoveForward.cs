using UnityEngine;


public class MoveForward : MonoBehaviour
{
    public void Move()
    {
        transform.position += transform.forward;
    }
}