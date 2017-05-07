using System;
using System.IO;
using System.Text;
using Valve.VR;

namespace SteamVR_WebKit
{
    public class SteamVR_Application
    {
        private string _applicationKey;
        private string _manifestFullPath;
        private string _manifestFileName;

        public SteamVR_Application(string applicationKey, string manifestPath = "manifest.vrmanifest")
        {
            _applicationKey = applicationKey;
            _manifestFullPath = Path.GetFullPath(manifestPath);
            _manifestFileName = Path.GetFileName(_manifestFullPath);
        }

        public void InstallManifest(bool cleanInstall = false)
        {
            if (File.Exists(_manifestFullPath))
            {
                bool alreadyInstalled = false;
                if (OpenVR.Applications.IsApplicationInstalled(_applicationKey))
                {
                    if (cleanInstall)
                    {
                        StringBuilder buffer = new StringBuilder(1024);
                        EVRApplicationError appError = new EVRApplicationError();
                        OpenVR.Applications.GetApplicationPropertyString(_applicationKey, EVRApplicationProperty.WorkingDirectory_String, buffer, 1024, ref appError);

                        if (appError == EVRApplicationError.None)
                        {
                            string oldManifestPath = Path.Combine(buffer.ToString(), _manifestFileName);
                            if (!_manifestFullPath.Equals(oldManifestPath))
                            {
                                OpenVR.Applications.RemoveApplicationManifest(oldManifestPath);
                            }
                            else
                            {
                                alreadyInstalled = true;
                            }
                        }
                    }
                    else
                    {
                        alreadyInstalled = true;
                    }
                }
                EVRApplicationError error = OpenVR.Applications.AddApplicationManifest(_manifestFullPath, false);
                if (error != EVRApplicationError.None)
                {
                    throw new Exception("Could not add application manifest: " + error.ToString());
                }
                else if (!alreadyInstalled || cleanInstall)
                {
                    error = OpenVR.Applications.SetApplicationAutoLaunch(_applicationKey, true);
                    if (error != EVRApplicationError.None)
                    {
                        throw new Exception("Could not set autostart: " + error.ToString());
                    }
                }
            }
            else
            {
                throw new Exception("Could not find application manifest: " + _manifestFullPath);
            }
        }

        public void RemoveManifest()
        {
            if (File.Exists(_manifestFullPath))
            {
                if (OpenVR.Applications.IsApplicationInstalled(_applicationKey))
                {
                    EVRApplicationError error = OpenVR.Applications.RemoveApplicationManifest(_manifestFullPath);
                    if (error != EVRApplicationError.None)
                    {
                        throw new Exception("Could not remove application manifest: " + error.ToString());
                    }
                }
            }
            else
            {
                throw new Exception("Could not find application manifest: " + _manifestFullPath);
            }
        }


        public void SetAutoStartEnabled(bool value)
        {
            EVRApplicationError error = OpenVR.Applications.SetApplicationAutoLaunch(_applicationKey, value);
            if (error != EVRApplicationError.None)
            {
                Console.WriteLine("Could not set auto start: " + error.ToString());
            }
        }
    }
}
