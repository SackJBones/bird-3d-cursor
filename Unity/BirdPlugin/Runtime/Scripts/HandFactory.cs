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
    }

    public static class HandFactory
    {
        public static Hand CreateHand(Hand.Chirality1 chirality, BirdHandAPI api = default(BirdHandAPI)) 
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
                default:
                    return null;
            }
        }
    }
}
