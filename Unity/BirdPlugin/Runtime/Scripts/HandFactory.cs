using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bird3DCursor{

    public enum BirdHandAPI
    {
    #if BIRD_LEAP_ENABLED
        Leap,
    #endif
    #if BIRD_OCULUS_OVR_ENABLED
        OculusOVR,
    #endif
    #if BIRD_OPENXR_ENABLED
        OpenXR,
    #endif
    }

    public static class HandFactory
    {
        private static readonly Dictionary<BirdHandAPI, Func<Hand.Chirality, Hand>> backends =
            new Dictionary<BirdHandAPI, Func<Hand.Chirality, Hand>>();

        // Also runs when entering Play Mode with domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBackends() { backends.Clear(); }

        /// <summary>Register a constructor on Unity's main thread, before creating hands.
        /// Re-registering the same API replaces its constructor without changing existing hands.</summary>
        public static void RegisterBackend(BirdHandAPI api, Func<Hand.Chirality, Hand> create)
        {
            if (create == null) throw new ArgumentNullException(nameof(create));
            backends[api] = create;
        }

        public static bool IsBackendRegistered(BirdHandAPI api) { return backends.ContainsKey(api); }

        public static bool UnregisterBackend(BirdHandAPI api) { return backends.Remove(api); }

        public static Hand CreateHand(Hand.Chirality chirality, BirdHandAPI api = default(BirdHandAPI)) 
        {
            Func<Hand.Chirality, Hand> create;
            if (!backends.TryGetValue(api, out create))
                throw new InvalidOperationException("Bird hand backend '" + api +
                    "' is not registered. Enable its SDK adapter and register it before creating hands.");
            var hand = create(chirality);
            if (hand == null) throw new InvalidOperationException("Bird hand backend '" + api + "' returned null.");
            return hand;
        }
    }
}
