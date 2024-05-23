using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bird3DCursor
{
    public class BirdManager : MonoBehaviour
    {
        public static event Action<BirdProvider> OnBirdCreated;
        public static event Action<BirdProvider> OnBirdDestroyed;
        public static List<BirdProvider> birds = new List<BirdProvider>();

        // Define custom trigger events that include the Bird
        public static event Action<BirdProvider, Collider> BirdTriggerEnter;
        public static event Action<BirdProvider, Collider> BirdTriggerExit;

        public static void RegisterBird(BirdProvider bird)
        {
            OnBirdCreated?.Invoke(bird);
            birds.Add(bird);
        }

        // Create Bird detector for a previously registered Bird
        public static void CreateBirdDetector(BirdProvider bird)
        {
            // error if this bird is not already registered
            if (!birds.Contains(bird))
            {
                Debug.LogError($"Bird {bird.GetAssociatedUser()} has not been registered with BirdManager. Please register the Bird before creating a BirdDetector.");
                return;
            }
            // do nothing if there is already a detector for this bird
            if (GameObject.Find(DetectorName(bird)) != null) return;
            GameObject birdDetector = new GameObject(DetectorName(bird));
            birdDetector.transform.SetParent(bird.transform);
            birdDetector.transform.localPosition = Vector3.zero;

            int birdDetectorLayer = LayerMask.NameToLayer("BirdDetectorLayer");
            if (birdDetectorLayer == -1)
            {
                Debug.LogWarning("The BirdDetectorLayer has not been set up in the Unity editor. Please add a new layer called BirdDetectorLayer.");
                return;
            }

            birdDetector.layer = birdDetectorLayer;

            Rigidbody rigidbody = birdDetector.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;

            SphereCollider collider = birdDetector.AddComponent<SphereCollider>();
            collider.isTrigger = true;

            // Add a BirdTriggerHandler component to handle Unity's trigger events
            BirdTriggerHandler triggerHandler = birdDetector.AddComponent<BirdTriggerHandler>();
            triggerHandler.Initialize(bird);  // Pass the Bird to the handler
        }

        public static void DestroyBirdDetector(BirdProvider bird)
        {
            // do nothing if there is no detector for this bird
            GameObject detector = GameObject.Find(DetectorName(bird));
            if (detector == null) return;
            // clear out registered unity actions
            for (int i = BirdTriggerEnter.GetInvocationList().Length - 1; i >= 0; i--)
            {
                if (ReferenceEquals(BirdTriggerEnter.GetInvocationList()[i].Target, bird))
                {
                    BirdTriggerEnter -= (Action<BirdProvider, Collider>)BirdTriggerEnter.GetInvocationList()[i];
                }
            }
            for (int i = BirdTriggerExit.GetInvocationList().Length - 1; i >= 0; i--)
            {
                if (ReferenceEquals(BirdTriggerExit.GetInvocationList()[i].Target, bird))
                {
                    BirdTriggerExit -= (Action<BirdProvider, Collider>)BirdTriggerExit.GetInvocationList()[i];
                }
            }
            Destroy(detector);
        }

        // call this method after removing references to Birds that have been set up with bird detectors to destroy the detectors if no references remain in any methods registered with the unity actions BirdTriggerEnter or BirdTriggerExit
        public static void DestroyUnusedBirdDetectors()
        {
            foreach (BirdProvider bird in birds)
            {
                if (bird == null) continue;
                //filter for this bird to see if any are left
                if (Array.FindAll(BirdTriggerExit.GetInvocationList(), action => ReferenceEquals(action.Target, bird)).Length == 0
                    && Array.FindAll(BirdTriggerExit.GetInvocationList(), action => ReferenceEquals(action.Target, bird)).Length == 0)
                {
                    DestroyBirdDetector(bird);
                }
            }
        }

        private static string DetectorName(BirdProvider bird)
        {
            return $"BirdDetector_{bird.GetAssociatedUser()}_{bird.chiralityStr}";
        }

        public static void UnregisterBird(BirdProvider bird)
        {
            DestroyBirdDetector(bird);
            OnBirdDestroyed?.Invoke(bird);
            if (birds.Contains(bird))
            {
                birds.Remove(bird);
            }
        }

        public static List<BirdProvider> GetAllBirds()
        {
            return birds.FindAll(bird => bird != null);
        }

        // get the birds associated with a particular user. Likely returns two Birds, but may only return one if only one Bird script is active
        public static List<BirdProvider> GetBirdsForUser(string userId)
        {
            return birds.FindAll(bird => bird != null && bird.GetAssociatedUser() == userId);
        }

        // Class to handle Unity's trigger events and relay them to BirdManager's custom events
        private class BirdTriggerHandler : MonoBehaviour
        {
            private BirdProvider associatedBird;

            public void Initialize(BirdProvider bird)
            {
                associatedBird = bird;
            }

            private void OnTriggerEnter(Collider other)
            {
                BirdTriggerEnter?.Invoke(associatedBird, other);
            }

            private void OnTriggerExit(Collider other)
            {
                BirdTriggerExit?.Invoke(associatedBird, other);
            }
        }
    }
}


