using System;

namespace Valve.VR
{
    public class SteamVR_Controller
    {
        public string Ident { get; private set; }
        public bool IsActive { get; private set; }

        public SteamVR_Controller(string ident)
        {
            Ident = ident;
            IsActive = true;
        }

        public void SetActive(bool state)
        {
            IsActive = state;
            SteamVR_WebKit.SteamVR_WebKit.Log("Controller manager set the active state of the " + Ident + " controller to " + state + ".");
        }
    }
}
