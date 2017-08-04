using CefSharp;
using OpenTK;
using System;
using System.Collections.Generic;
using Valve.VR;
using OpenTK.Graphics.OpenGL;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

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

        public static WebKitOverlay ActiveKeyboardOverlay = null;

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

            gw = new GameWindow(300, 30); // Invisible GL Context
            GL.Enable(EnableCap.Texture2D);

            if (Cef.IsInitialized)
                Cef.Shutdown();

            Cef.Initialize(settings);

            bool tryAgain = true;

            while(tryAgain && !_doStop) {
                try
                {
                    InitOpenVR();
                    tryAgain = false;
                } catch (Exception e)
                {
                    Log(e.Message);
                    Log("Trying again in 3 seconds");
                    Thread.Sleep(3000);
                }
            }

            if (_doStop)
            {
                CefShutdown();
                return;
            }

            _system = OpenVR.System;
            _compositor = OpenVR.Compositor;
            _overlay = OpenVR.Overlay;
            _applications = OpenVR.Applications;
            
            _controllerManager = new SteamVR_ControllerManager();

            SteamVR_WebKit.Log("SteamVR_WebKit Initialised");

            _initialised = true;

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
            SteamVR_Event.Listen("KeyboardDone", OnKeyboardDone);
            SteamVR_Event.Listen("KeyboardCharInput", OnKeyboardCharInput);
            SteamVR_Event.Listen("KeyboardClosed", OnKeyboardClosed);
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
            VREvent_t eventData = new VREvent_t();
            uint vrEventSize = (uint)Marshal.SizeOf<VREvent_t>();

            while (!_doStop)
            {
                fpsWatch.Restart();

                UpdatePoses();

                PreUpdateCallback?.Invoke(null, null);

                foreach (WebKitOverlay overlay in Overlays)
                {
                    overlay.Update();
                }
                while (OpenVR.System.PollNextEvent(ref eventData, vrEventSize))
                {
                    SteamVR_Event.Send(((EVREventType)eventData.eventType).ToString().Replace("VREvent_", ""), eventData);
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

            CefShutdown();
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

        private static void OnKeyboardCharInput(params object[] args)
        {
            SteamVR_WebKit.Log("Keyboard Input: " + ((char)((VREvent_t)args[0]).data.keyboard.cNewInput0));

            byte[] characters = new byte[8] {
                ((VREvent_t)args[0]).data.keyboard.cNewInput0,
                ((VREvent_t)args[0]).data.keyboard.cNewInput1,
                ((VREvent_t)args[0]).data.keyboard.cNewInput2,
                ((VREvent_t)args[0]).data.keyboard.cNewInput3,
                ((VREvent_t)args[0]).data.keyboard.cNewInput4,
                ((VREvent_t)args[0]).data.keyboard.cNewInput5,
                ((VREvent_t)args[0]).data.keyboard.cNewInput6,
                ((VREvent_t)args[0]).data.keyboard.cNewInput7,
            };

            if(SteamVR_WebKit.ActiveKeyboardOverlay != null)
            {
                SteamVR_WebKit.ActiveKeyboardOverlay.KeyboardInput(characters);
            }

            // TODO: Take input and send to overlay
        }

        private static void OnKeyboardClosed(params object[] args)
        {
            SteamVR_WebKit.Log("Keyboard Closed");
        }

        private static void OnKeyboardDone(params object[] args)
        {
            SteamVR_WebKit.Log("Keyboard Done");
        }
        #endregion
    }
}
