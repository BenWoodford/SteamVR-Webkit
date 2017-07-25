using CefSharp;
using OpenTK;
using System;
using System.Collections.Generic;
using Valve.VR;
using OpenTK.Graphics.OpenGL;
using System.Threading;
using System.Diagnostics;

namespace SteamVR_WebKit
{
    public class SteamVR_WebKit
    {
        static SteamVR _vr;
        static SteamVR_ControllerManager _controllerManager;
        static CVRSystem _system;
        static CVRCompositor _compositor;
        static CVROverlay _overlay;
        static CVRApplications _applications;
        static int _frameSleep;
        static int _fps;
        static bool _doStop = false;

        static bool _initialised = false;

        public delegate void LogEventDelegate(string line);

        public static event LogEventDelegate LogEvent;

        public static event EventHandler PreUpdateCallback;
        public static event EventHandler PostUpdateCallback;

        public static event EventHandler PreDrawCallback;
        public static event EventHandler PostDrawCallback;

        public static bool TraceLevel = false;

        public static bool Initialised { get { return _initialised; } }

        public static List<WebKitOverlay> Overlays;

        public static void Stop()
        {
            _doStop = true;
        }

        public static int FPS
        {
            get { return _fps; }
            set { _frameSleep = (int)((1f / (float)value) * 1000); _fps = value; }
        }

        public static SteamVR SteamVR
        {
            get { return _vr; }
            set { _vr = value; }
        }

        public static SteamVR_ControllerManager ControllerManager
        {
            get { return _controllerManager; }
            set { _controllerManager = value; }
        }

        public static CVRSystem OVRSystem
        {
            get { return _system; }
        }

        public static CVRCompositor OVRCompositor
        {
            get { return _compositor; }
        }

        public static CVROverlay OverlayManager
        {
            get { return _overlay; }
        }

        public static CVRApplications Applications
        {
            get { return _applications; }
        }

        public static GameWindow gw;

        public static void Init(CefSettings settings = null)
        {
            Overlays = new List<WebKitOverlay>();

            if (settings == null)
                settings = new CefSettings();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            gw = new GameWindow(300, 30); // Invisible GL Context
            GL.Enable(EnableCap.Texture2D);

            if (Cef.IsInitialized)
                Cef.Shutdown();

            Cef.Initialize(settings);

            InitOpenVR();

            _system = OpenVR.System;
            _compositor = OpenVR.Compositor;
            _overlay = OpenVR.Overlay;
            _applications = OpenVR.Applications;
            
            _controllerManager = new SteamVR_ControllerManager();

            SteamVR_WebKit.Log("SteamVR_WebKit Initialised");

            _initialised = true;

        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            CefShutdown();
        }

        public static void CefShutdown()
        {
            try
            {
                Cef.Shutdown();
            }
            catch (Exception e)
            {
                SteamVR_WebKit.Log("SteamVR_WebKit Cef.Shutdown failed: " + e.Message);
            }
        }

        static void InitOpenVR()
        {
            EVRInitError ovrError = EVRInitError.None;

            OpenVR.Init(ref ovrError, EVRApplicationType.VRApplication_Overlay);

            if (ovrError != EVRInitError.None)
            {
                throw new Exception("Failed to init OpenVR! " + ovrError.ToString());
            }

            OpenVR.GetGenericInterface(OpenVR.IVRCompositor_Version, ref ovrError);

            if (ovrError != EVRInitError.None)
            {
                throw new Exception("Failed to init Compositor! " + ovrError.ToString());
            }

            OpenVR.GetGenericInterface(OpenVR.IVROverlay_Version, ref ovrError);

            if (ovrError != EVRInitError.None)
            {
                throw new Exception("Failed to init Overlay!");
            }
            
            SteamVR_Event.Listen("new_poses", OnNewPoses);
        }
        
        private static readonly TrackedDevicePose_t[] _poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private static readonly TrackedDevicePose_t[] _gamePoses = new TrackedDevicePose_t[0];

        private static void UpdatePoses()
        {
            if (_compositor == null) return;
            _compositor.GetLastPoses(_poses, _gamePoses);
            SteamVR_Event.Send("new_poses", _poses);
            SteamVR_Event.Send("new_poses_applied");
        }

        public static void RunOverlays()
        {
            Stopwatch fpsWatch = new Stopwatch();
            while (!_doStop)
            {
                fpsWatch.Restart();

                UpdatePoses();

                PreUpdateCallback?.Invoke(null, null);

                foreach (WebKitOverlay overlay in Overlays)
                {
                    overlay.Update();
                }

                PostUpdateCallback?.Invoke(null, null);

                PreDrawCallback?.Invoke(null, null);

                foreach (WebKitOverlay overlay in Overlays)
                {
                    overlay.Draw();
                }

                PostDrawCallback?.Invoke(null, null);

                fpsWatch.Stop();
                Thread.Sleep(fpsWatch.ElapsedMilliseconds >= _frameSleep ? 0 : (int)(_frameSleep - fpsWatch.ElapsedMilliseconds));
            }
        }

        public static void Log(string message)
        {
            LogEvent?.Invoke(message);
        }
        
        #region Event callbacks
        private static void OnNewPoses(params object[] args)
        {
            var poses = (TrackedDevicePose_t[])args[0];

            for (int i = 0; i < poses.Length; i++)
            {
                var connected = poses[i].bDeviceIsConnected;
                if (connected != ControllerManager.connected[i])
                {
                    SteamVR_Event.Send("device_connected", i, connected);
                }
            }

            if (poses.Length > OpenVR.k_unTrackedDeviceIndex_Hmd)
            {
                var result = poses[OpenVR.k_unTrackedDeviceIndex_Hmd].eTrackingResult;

                var initializing = result == ETrackingResult.Uninitialized;
                if (initializing != SteamVR.initializing)
                {
                    SteamVR_Event.Send("initializing", initializing);
                }

                var calibrating =
                    result == ETrackingResult.Calibrating_InProgress ||
                    result == ETrackingResult.Calibrating_OutOfRange;
                if (calibrating != SteamVR.calibrating)
                {
                    SteamVR_Event.Send("calibrating", calibrating);
                }

                var outOfRange =
                    result == ETrackingResult.Running_OutOfRange ||
                    result == ETrackingResult.Calibrating_OutOfRange;
                if (outOfRange != SteamVR.outOfRange)
                {
                    SteamVR_Event.Send("out_of_range", outOfRange);
                }
            }
        }
        #endregion
    }
}
