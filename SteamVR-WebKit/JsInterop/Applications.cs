using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace SteamVR_WebKit.JsInterop
{
    public class Applications
    {
        private CVRApplications _applicationsInstance;

        public class Application
        {
            public string AppKey { get; set; }
            public string Name { get; set; }
            public string ImagePath { get; set; }

            public Application(string appKey)
            {
                AppKey = appKey;

                PopulateData();
            }

            public void PopulateData()
            {
                if (OpenVR.Applications == null)
                    return;

                Name = GetPropertyString(EVRApplicationProperty.Name_String);
                ImagePath = GetPropertyString(EVRApplicationProperty.ImagePath_String);
            }

            public string GetPropertyString(EVRApplicationProperty prop)
            {
                EVRApplicationError err = EVRApplicationError.None;
                StringBuilder propertyBuffer = new StringBuilder(255);

                OpenVR.Applications.GetApplicationPropertyString(AppKey, prop, propertyBuffer, 255, ref err);

#if DEBUG
                if(err != EVRApplicationError.None)
                    SteamVR_WebKit.Log("EVRApplicationError on " + AppKey + " property " + prop.ToString() + ": " + err.ToString());
#endif

                //SteamVR_WebKit.Log(propertyBuffer.ToString());

                return propertyBuffer.ToString();
            }
        }

        public Applications()
        {
            _applicationsInstance = OpenVR.Applications;
        }

        public string GetApplicationsList()
        {
            List<Application> apps = new List<Application>();

            StringBuilder keyBuffer = new StringBuilder(255);
            EVRApplicationError err = EVRApplicationError.None;

            for(int i = 0; i < _applicationsInstance.GetApplicationCount(); i++)
            {
                err = _applicationsInstance.GetApplicationKeyByIndex((uint)i, keyBuffer, 255);

                if (err != EVRApplicationError.None)
                    throw new Exception("EVRApplicationError: " + err.ToString());

                Application newApp = new Application(keyBuffer.ToString());
                apps.Add(newApp);
            }

            string ret = JsonConvert.SerializeObject(apps);
            return ret;
        }
    }
}
