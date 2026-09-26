using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Bird3DCursor;
using DG.Tweening;

[Serializable]
public class VisualFeatureProperties
{
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector3 scale;
    public Color color;
    // Add other visual properties you might need
}

// declaration for all of the VisualFeature state names that other classes will use to select behaviors
public enum MenuElementState
{
    Inactive, // not visible, not selectable
    Enabled, // interactable, most likely visible
    Highlighted, // temporarily visually different, e.g. when a Bird is pointing at it
    Activated, // semi-permanently visually different, e.g. checkbox is checked
    Background // mostly passive, ceded attention to other elements. possibly partially visible and/or interactable e.g. "back"
}

[Serializable]
public class VisualFeature
{
    public GameObject visualObject;
    [HideInInspector]
    public VisualFeatureProperties InactiveState;
    [HideInInspector]
    public VisualFeatureProperties EnabledState;
    [HideInInspector]
    public VisualFeatureProperties HighlightedState;
    [HideInInspector]
    public VisualFeatureProperties ActivatedState;
    [HideInInspector]
    public VisualFeatureProperties BackgroundState;

    public VisualFeatureProperties GetPropertiesForState(MenuElementState state)
    {
        switch (state)
        {
            case MenuElementState.Inactive:
                return InactiveState;
            case MenuElementState.Enabled:
                return EnabledState;
            case MenuElementState.Highlighted:
                return HighlightedState;
            case MenuElementState.Activated:
                return ActivatedState;
            case MenuElementState.Background:
                return BackgroundState;
            default:
                return null;
        }
    }
}

// serializable class that allows all of the options for actions that can be triggered by a collider.
// it allows the collider to be specified in the inspector, and also allows the user to specify all of the following attributes:
// - drop-down selection of an trigger type: touch, point at, select, and select behind
// - drop-down selection of a menu element state to transition to, or to leave the element state unchanged
// - special options, including specifying that this is a "close" collider, which will close the menu when the collider is triggered and reactivate the parent
//   - if it is a close collider, it is optional whether this should trigger a recursive close, which will close the menu and all of its parents
// - in the case that touch activation is selected, specify an optional 3D vector in local coordinates to indicate the direction the collider must be touched in, in order to be activated
// - a Unity Event that is invoked when the collider is triggered
// the advanced options are hidden by default, and can be shown by clicking the "Advanced" button in the inspector
[Serializable]
public class ColliderTriggerAction
{
    public enum TriggerType
    {
        Touch,
        PointAt,
        StopPointAt,
        PointThrough,
        StopPointThrough,
        SelectInBounds,
        SelectBehind
    }

    public enum Direction
    {
        Any,
        Up,
        Down,
        Left,
        Right,
        Forward,
        Backward
    }

    public Collider TriggerCollider;
    public TriggerType triggerType;
    public Boolean transition = true;
    public MenuElementState transitionState;
    [Tooltip("If true, this action and its collider will still be active even when the menu element is in the background state. If false, this action will be ignored in the background state.")]
    public bool backgroundActive;
    public bool close;
    public bool recursive;
    public Direction LocalTouchDirection;
    public List<BaseMenuElement> MenuElementsToSummon;
    public UnityEvent OnTriggered;
    public Dictionary<BirdProvider, bool> wasPointingAt = new Dictionary<BirdProvider, bool>();
    public Dictionary<BirdProvider, bool> wasPointingThrough = new Dictionary<BirdProvider, bool>();

    public Vector3 DirectionVector
    {
        get
        {
            switch (LocalTouchDirection)
            {
                case Direction.Up:
                    return Vector3.up;
                case Direction.Down:
                    return Vector3.down;
                case Direction.Left:
                    return Vector3.left;
                case Direction.Right:
                    return Vector3.right;
                case Direction.Forward:
                    return Vector3.forward;
                case Direction.Backward:
                    return Vector3.back;
                default:
                    return Vector3.zero;
            }
        }
    }
}




public class BaseMenuElement : MonoBehaviour
{
    // specify whether active by default when scene loads
    [SerializeField] private bool EnabledByDefault = false;
    [SerializeField] private Collider focusCollider;
    [SerializeField] private bool ChildCanClose = true;
    // for inspector: specify the actions that should be triggered when the collider is touched
    public List<ColliderTriggerAction> ColliderTriggerActions;
    // for inspector: specify the visual features that should be updated when the menu element state changes
    public List<VisualFeature> VisualFeatures;
    [SerializeField] private bool testVisualStates = false;

    private MenuElementState currentState = MenuElementState.Inactive;
    private MenuElementState previousState = MenuElementState.Inactive;
    private bool acceptingNewFocus = false;
    private BaseMenuElement parentMenuElement;
    private List<BaseMenuElement> summonedElements = new List<BaseMenuElement>();
    private List<BirdProvider> birdsInFocus = new List<BirdProvider>();


    void Start()
    {
        Console.WriteLine("Started!");

        if (EnabledByDefault)
        {
            acceptingNewFocus = true;
            SetState(MenuElementState.Enabled);
        }
        else
        {
            SetState(MenuElementState.Inactive);
            UpdateVisualFeatures(); // not executed by default because the transition is inactive -> inactive, so we need to call it manually
        }
    }

    void Update()
    {
        if (testVisualStates)
        {
            TestVisualStates();
            return;
        }
        HandleFocus();
        HandleActions();
    }

    void HandleActions()
    {
        if (currentState == MenuElementState.Inactive) 
            return;
        foreach (ColliderTriggerAction action in ColliderTriggerActions)
        {
            if (!action.backgroundActive && currentState == MenuElementState.Background)
                continue;
            foreach (BirdProvider bird in birdsInFocus)
            {
                bool wasPointingAt = false;
                bool wasPointingThrough = false;
                action.wasPointingAt.TryGetValue(bird, out wasPointingAt);
                action.wasPointingThrough.TryGetValue(bird, out wasPointingThrough);
                bool isTriggered = false;
                bool anyDirection = action.LocalTouchDirection == ColliderTriggerAction.Direction.Any;

                switch (action.triggerType)
                {
                    case ColliderTriggerAction.TriggerType.Touch:
                        // the direction vector accepted by WentThrough is in world coordinates, but the direction vector for the UI element
                        // action is in local coordinates, so we need to convert it to world coordinates
                        isTriggered = (anyDirection && bird.WentThrough(action.TriggerCollider)) ||
                                       (!anyDirection && bird.WentThrough(action.TriggerCollider, transform.TransformDirection(action.DirectionVector)));
                        break;

                    case ColliderTriggerAction.TriggerType.PointAt:
                    case ColliderTriggerAction.TriggerType.StopPointAt:
                        RaycastHit hit;
                        bool isPointingAt = action.TriggerCollider.Raycast(bird.GetRay(), out hit, 1000);
                        isTriggered = (action.triggerType == ColliderTriggerAction.TriggerType.PointAt && isPointingAt && !wasPointingAt) ||
                                      (action.triggerType == ColliderTriggerAction.TriggerType.StopPointAt && !isPointingAt && wasPointingAt);
                        action.wasPointingAt[bird] = isPointingAt;
                        break;

                    case ColliderTriggerAction.TriggerType.PointThrough:
                    case ColliderTriggerAction.TriggerType.StopPointThrough:
                        bool isPointingThrough = action.TriggerCollider.Raycast(bird.GetRay(), out hit, 1000) && hit.distance < bird.GetRange();
                        isTriggered = (action.triggerType == ColliderTriggerAction.TriggerType.PointThrough && isPointingThrough && !wasPointingThrough) ||
                                      (action.triggerType == ColliderTriggerAction.TriggerType.StopPointThrough && !isPointingThrough && wasPointingThrough);
                        action.wasPointingThrough[bird] = isPointingThrough;
                        break;

                    case ColliderTriggerAction.TriggerType.SelectInBounds:
                        isTriggered = bird.GetClickDown() && action.TriggerCollider.bounds.Contains(bird.transform.position);
                        break;

                    case ColliderTriggerAction.TriggerType.SelectBehind:
                        bool isPointingAtSelectBehind = action.TriggerCollider.Raycast(bird.GetRay(), out hit, 1000);
                        isTriggered = bird.GetClickDown() && isPointingAtSelectBehind && hit.distance < bird.GetRange();
                        break;
                }

                if (isTriggered)
                {
                    // invoke the Unity Event
                    action.OnTriggered.Invoke();
                    if (action.transition)
                        SetState(action.transitionState);
                    if (action.close)
                    {
                        if (action.recursive)
                        {
                            CloseRecursive();
                        }
                        else
                        {
                            Close();
                        }
                    }
                    // new menu elements are always summoned even if this menu element just closed itself (if other order, then the new menu elements would be closed immediately)
                    foreach (BaseMenuElement menuElement in action.MenuElementsToSummon)
                    {
                        Summon(menuElement);
                    }
                }
            }
        }
    }

    // this is used to create a delegate that is passed to the BirdManager, which will call it when a bird enters a collider
    Action<BirdProvider, Collider> ColliderEnterDelegate(ColliderTriggerAction colliderTriggerAction)
    {
        switch (colliderTriggerAction.triggerType)
        {
            case ColliderTriggerAction.TriggerType.Touch:
                return (bird, collider) =>
                {
                    if (collider != colliderTriggerAction.TriggerCollider || !birdsInFocus.Contains(bird)) return;

                };
            // case ColliderTriggerAction.TriggerType.PointAt:
            //     return null; // don't do anything special when the bird enters the collider
            // case ColliderTriggerAction.TriggerType.Select:
            //     return SelectDelegate(colliderTriggerAction);
            // case ColliderTriggerAction.TriggerType.SelectBehind:
            //     return SelectBehindDelegate(colliderTriggerAction);
            default:
                return null;
        }
    }


    // set the state and, if it's different from before, record the previous state
    // unless it is highlighted, in which case it's temporary and does not need to be recorded.
    public void SetState(MenuElementState newState)
    {
        if (newState != currentState)
        {
            if (newState != MenuElementState.Highlighted)
            {
                previousState = currentState;
            }
            currentState = newState;
            UpdateVisualFeatures();
        }
    }

    public MenuElementState GetState()
    {
        return currentState;
    }

    // use summon to enable new menu elements while keeping track of the tree of activation for closing
    void Summon(BaseMenuElement menuElement)
    {
        if (menuElement == null) return;
        menuElement.parentMenuElement = this;
        summonedElements.Add(menuElement);
        // the menu element inherits this element's focus
        menuElement.SetFocus(birdsInFocus);
        menuElement.SetState(MenuElementState.Enabled);
    }

    // use enable() to enable this menu element directly without necessarily referencing other elements. suitable for unityevents.
    public void Enable()
    {
        SetState(MenuElementState.Enabled);
    }

    // Called when a bird gains focus
    void GainFocus(BirdProvider bird)
    {
        if (!birdsInFocus.Contains(bird))
        {
            birdsInFocus.Add(bird);
            // TODO BirdManager.CreateBirdDetector(bird);
            // Trigger gain focus animation...
            // Update internal state...
        }
    }

    // Called when a bird loses focus
    void LoseFocus(BirdProvider bird)
    {
        if (birdsInFocus.Contains(bird))
        {
            birdsInFocus.Remove(bird);

            // TODO destroy the bird detector
            // Trigger lose focus animation...
            // Update bird state for cursor/hand appearance change if desired...
        }
        // if no more birds left, open to accepting new focus
        if (birdsInFocus.Count == 0)
        {
            acceptingNewFocus = true;
        }
    }

    // set this element's focus and override previous focus. create a warning if this is called when the element is not accepting new focus.
    void SetFocus(List<BirdProvider> birds)
    {
        if (!acceptingNewFocus)
        {
            Debug.LogWarning("SetFocus called on a menu element that is not accepting new focus.");
        }
        // call GainFocus on all birds (if redundant will be ignored), and if any currently focused birds are missing in the new focus, call LoseFocus
        foreach (BirdProvider bird in birds)
        {
            GainFocus(bird);
        }
        foreach (BirdProvider bird in birdsInFocus)
        {
            if (!birds.Contains(bird))
            {
                LoseFocus(bird);
            }
        }
        acceptingNewFocus = false;
    }

    private void HandleFocus()
    {
        // By default, gain focus for all birds with the same user and prevent focus for all others
        if (!acceptingNewFocus || focusCollider == null)
            return;
        foreach (BirdProvider bird in BirdManager.GetAllBirds())
        {
            if (!acceptingNewFocus)
                return;
            if (bird.WentThrough(focusCollider))
            {
                foreach (BirdProvider usersBird in BirdManager.GetBirdsForUser(bird.GetAssociatedUser()))
                {
                    GainFocus(usersBird);
                }
                acceptingNewFocus = false;
                break;
            }
        }
    }

    void Close(bool upwardRecursive)
    {
        // close menus summoned by this element.
        foreach (BaseMenuElement menuElement in summonedElements)
        {
            menuElement.Close(); // recursive closing of summoned children is always standard.
                                 // if this behavior is not wanted, enable your menu element directly using Enable()
                                 // e.g. in a unityevent, not by using summon().
        }
        // if this is a recursive close and the element cannot be closed this way, stop
        if (upwardRecursive && !ChildCanClose)
        {
            return;
        }
        summonedElements.Clear();
        // close menu. If upward-recursive, attempt to close the parent menu (no issue if the parent menu cannot recursively close)
        SetState(MenuElementState.Inactive);
        if (upwardRecursive && parentMenuElement != null)
        {
            parentMenuElement.Close(true);
        }
    }

    // no-argument method instead of optional parameter so that this can be used for unityevents
    public void Close() { Close(false); }

    // close and attempt to recursively close the parent menu.
    // different from just calling Close(true) because it will definitely close now
    // even if it cannot be closed recursively by its own child menus.
    public void CloseRecursive()
    {
        Close();
        Close(true);
    }

    // unhighlight this element. If it is not highlighted, do nothing; if it is highlighted, unhighlight it and return to the previous state.
    void Unhighlight()
    {
        if (currentState == MenuElementState.Highlighted)
        {
            SetState(previousState);
        }
    }

    // Override UpdateVisualFeatures to set tweens between states of each VisualFeatureProperties and the new state.
    void UpdateVisualFeatures()
    {
        Console.WriteLine("Updating visual features");
        foreach (VisualFeature feature in VisualFeatures)
        {
            // get the visual feature properties associated with the current state
            VisualFeatureProperties properties = feature.GetPropertiesForState(currentState);
            // use the DOTween library to change all of the visual elements' positions, scales, and colors to new values.
            float transitionTime = 0.2f;
            // default easing function
            Ease ease = Ease.InOutQuad;
            // position:
            feature.visualObject.transform.DOLocalMove(properties.localPosition, transitionTime).SetEase(ease);
            // rotation:
            feature.visualObject.transform.DOLocalRotateQuaternion(properties.localRotation, transitionTime).SetEase(ease);
            // scale:
            feature.visualObject.transform.DOScale(properties.scale, transitionTime).SetEase(ease);
            // color:
            Renderer renderer = feature.visualObject.GetComponent<Renderer>();

            if (renderer != null)
            {
                renderer.material.DOColor(properties.color, transitionTime).SetEase(ease);
            }
        }
    }

    // Change menu element states on an automatic timer.
    void TestVisualStates()
    {
        if (Time.frameCount % 120 == 0)
        {
            switch (currentState)
            {
                case MenuElementState.Inactive:
                    SetState(MenuElementState.Enabled);
                    break;
                case MenuElementState.Enabled:
                    SetState(MenuElementState.Highlighted);
                    break;
                case MenuElementState.Highlighted:
                    SetState(MenuElementState.Activated);
                    break;
                case MenuElementState.Activated:
                    SetState(MenuElementState.Background);
                    break;
                case MenuElementState.Background:
                    SetState(MenuElementState.Inactive);
                    break;
            }
        }

    }
}
