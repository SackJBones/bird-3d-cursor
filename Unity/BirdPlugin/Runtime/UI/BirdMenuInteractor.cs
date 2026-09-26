using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bird3DCursor.UI
{
    /// <summary>Routes one action per new pointer sample. All hits are evaluated before callbacks mutate menus.</summary>
    [AddComponentMenu("Bird/UI/Menu Interactor")]
    [DisallowMultipleComponent]
    public sealed class BirdMenuInteractor : MonoBehaviour
    {
        [SerializeField] BirdPointerInput[] pointers = new BirdPointerInput[0];
        [SerializeField] BirdMenuElement[] elements = new BirdMenuElement[0];
        readonly Dictionary<BirdPointerInput, uint> revisions = new Dictionary<BirdPointerInput, uint>();
        BirdMenuElement[] candidates = new BirdMenuElement[0];
        bool stepping;

        void Awake() { if (elements.Length == 0) RefreshElements(); }
        void OnEnable() { SeedRevisions(); }
        void LateUpdate() { Process(); }
        void OnDisable()
        {
            revisions.Clear();
            foreach (var element in elements)
            {
                if (element == null) continue;
                if (element.Panel != null) element.Panel.CloseAll();
                element.ForgetPointers(); element.BeginFrame(); element.FinishFrame();
            }
        }

        [ContextMenu("Refresh Child Menu Elements")]
        public void RefreshElements() { Configure(pointers,GetComponentsInChildren<BirdMenuElement>(true)); }

        public void Configure(BirdPointerInput[] inputs, BirdMenuElement[] targets)
        {
            if (stepping) throw new InvalidOperationException("Configure the interactor between input updates, not inside an action callback.");
            foreach (var element in elements) if (element != null) element.ForgetPointers();
            pointers = inputs != null ? (BirdPointerInput[])inputs.Clone() : new BirdPointerInput[0];
            elements = targets != null ? (BirdMenuElement[])targets.Clone() : new BirdMenuElement[0];
            candidates = new BirdMenuElement[pointers.Length];
            SeedRevisions();
        }

        void SeedRevisions()
        {
            revisions.Clear();
            foreach (var pointer in pointers) if (pointer != null) revisions[pointer] = pointer.Revision;
        }

        public void Process()
        {
            if (stepping || !isActiveAndEnabled) return;
            stepping = true;
            try
            {
                if (candidates.Length != pointers.Length) candidates = new BirdMenuElement[pointers.Length];
                foreach (var element in elements)
                {
                    if (element == null) continue;
                    var panel = element.Panel != null ? element.Panel.Root : null;
                    if (panel != null && panel.State != BirdMenuPanel.PanelState.Closed &&
                        panel.CloseWhenFocusLost && !OwnerTracked(panel.Owner)) panel.Close();
                    element.BeginFrame();
                }
                for (int i=0;i<pointers.Length;i++)
                {
                    var pointer = pointers[i]; candidates[i] = null;
                    if (pointer == null) continue;
                    uint revision;
                    bool fresh = !revisions.TryGetValue(pointer,out revision) || revision != pointer.Revision;
                    revisions[pointer] = pointer.Revision;
                    float nearest = float.PositiveInfinity;
                    foreach (var element in elements)
                    {
                        float distance;
                        if (element == null || !element.Evaluate(pointer,fresh,out distance)) continue;
                        var best = candidates[i];
                        if (best == null || element.Priority > best.Priority || (element.Priority == best.Priority && distance < nearest))
                        { candidates[i] = element; nearest = distance; }
                    }
                }
                for (int i=0;i<pointers.Length;i++)
                    if (isActiveAndEnabled && candidates[i] != null) candidates[i].Invoke(pointers[i]);
                foreach (var element in elements) if (element != null) element.FinishFrame();
            }
            finally { stepping = false; }
        }

        bool OwnerTracked(string owner)
        {
            foreach (var pointer in pointers) if (pointer != null && pointer.IsTracked && pointer.UserId == owner) return true;
            return false;
        }
    }
}
