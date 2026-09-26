using UnityEngine;

public class InactivateParticles : MonoBehaviour
{
    private BaseMenuElement parentBaseMenuElement;
    private ParticleSystem[] childParticleSystems;
    private MenuElementState previousState;

    private void Awake()
    {
        parentBaseMenuElement = GetComponentInParent<BaseMenuElement>();
        childParticleSystems = GetComponentsInChildren<ParticleSystem>();
    }

    private void Start()
    {
        DoSetActivation(parentBaseMenuElement.GetState() != MenuElementState.Inactive);
    }

    private void Update()
    {
        MenuElementState state = parentBaseMenuElement.GetState();
        if (state != previousState)
        {
            DoSetActivation(state != MenuElementState.Inactive);
        }
        previousState = state;
    }

    private void DoSetActivation(bool active)
    {
        foreach (ParticleSystem ps in childParticleSystems)
        {
            if (active)
            {
                ps.Play();
            }
            else
            {
                ps.Stop();
            }
        }
    }
}
