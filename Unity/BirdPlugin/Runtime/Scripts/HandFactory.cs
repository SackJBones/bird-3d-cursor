// HandFactory.cs

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
        public static Hand CreateHand(Hand.Chirality chirality, BirdHandAPI api = default(BirdHandAPI)) 
        {
            switch (api)
            {
                #if BIRD_LEAP_ENABLED
                case BirdHandAPI.Leap:
                    return new UltraLeapHand(chirality);
                #endif
                #if BIRD_OCULUS_OVR_ENABLED
                case BirdHandAPI.OculusOVR:
                    return new OculusOVRHand(chirality);
                #endif
                #if BIRD_OPENXR_ENABLED
                case BirdHandAPI.OpenXR:
                    return new OpenXRHand(chirality);
                #endif
                default:
                    throw new System.Exception("You must set the BirdHandAPI (Tools > Bird3DCursor > Configure Hand Tracking).");
            }
        }
    }
}
