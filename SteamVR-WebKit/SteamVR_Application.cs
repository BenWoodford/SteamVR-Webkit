using System;
using System.IO;
using System.Text;
using Valve.VR;
using Newtonsoft.Json;

namespace SteamVR_WebKit
{
    /// <summary>
    /// Registers applications to SteamVR/OpenVR and allows to set autostart (starts with SteamVR). A .vrmanifest file is needed.
    /// No file is copied. Instead OepnVR is provided with the current working directory. Removing the directory will unregister
    /// the application on next start.
    /// </summary>
    public class SteamVR_Application
    {
        private string _applicationKey;
        private string _manifestFullPath;
        private string _manifestFileName;

        public SteamVR_Application(string manifestPath = "manifest.vrmanifest")
        {
            _manifestFullPath = Path.GetFullPath(manifestPath);
            _manifestFileName = Path.GetFileName(_manifestFullPath);
            string manifestJSON = File.ReadAllText(manifestPath);
            JsInterop.Applications.VRManifest manifest = JsonConvert.DeserializeObject<JsInterop.Applications.VRManifest>(manifestJSON);
            if (manifest.applications.Count != 0)
            {
                _applicationKey = manifest.applications[0].app_key;
            } else
            {
                throw new Exception("No application found in VR manifest file: " + _manifestFullPath);
            }
        }

        /// <summary>
        /// Installs/Registers the application. This does not copy any files, but instead points OpenVR to the current working directory.
        /// </summary>
        /// <param name="cleanInstall">If an existing installation is found, setting cleanInstall to true will remove the old registration first (no files are deleted).</param>
        /// <param name="autoStart">If true, the registered application will start with SteamVR</param>
        public void InstallManifest(bool cleanInstall = false, bool autoStart = true)
        {
            if (File.Exists(_manifestFullPath))
            {
                bool alreadyInstalled = false;
                if (OpenVR.Applications.IsApplicationInstalled(_applicationKey))
                {
                    Console.WriteLine("Found existing installation.");
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
                                Console.WriteLine("Clean install: Removing old manifest.");
                                OpenVR.Applications.RemoveApplicationManifest(oldManifestPath);
                                Console.WriteLine(oldManifestPath);
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
                else
                {
                    Console.WriteLine("Could not find existing installation. Installing now...");
                }
                EVRApplicationError error = OpenVR.Applications.AddApplicationManifest(_manifestFullPath, false);
                if (error != EVRApplicationError.None)
                {
                    throw new Exception("Could not add application manifest: " + error.ToString());
                }
                else if (autoStart && (!alreadyInstalled || cleanInstall))
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

        /// <summary>
        /// Uninstalls/Unregisters the application. No files are deleted.
        /// </summary>
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

        /// <summary>
        /// Sets the autostart property.
        /// </summary>
        /// <param name="value">If true, the application will start with SteamVR</param>
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