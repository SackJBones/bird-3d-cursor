using UnityEngine;
using UnityEngine.Events;
using Bird3DCursor;

public class BirdHitInvoke : MonoBehaviour
{
    [SerializeField] private UnityEvent eventAction;
    public BirdProvider bird;
    Collider coll;

    // Start is called before the first frame update
    void Start()
    {
        coll = GetComponent<Collider>();
    }

    // Update is called once per frame
    void Update()
    {
        if (coll != null && bird.WentThrough(coll)) {
            eventAction.Invoke();
        }
    }
}
