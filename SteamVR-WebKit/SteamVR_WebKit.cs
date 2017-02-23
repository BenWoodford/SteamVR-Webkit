using CefSharp;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using OpenTK.Graphics.OpenGL;
using System.Threading;
using System.Diagnostics;

namespace SteamVR_WebKit
{
    public class SteamVR_WebKit
    {
        static SteamVR _vr;
        static CVRSystem _system;
        static CVRCompositor _compositor;
        static CVROverlay _overlay;
        static int _frameSleep;
        static int _fps;
        static bool _doStop = false;

        static bool _initialised = false;

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

        public static GameWindow gw;

        public static void Init(CefSettings settings = null)
        {
            Overlays = new List<WebKitOverlay>();

            if (settings == null)
                settings = new CefSettings();
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            gw = new GameWindow(300, 30); // Invisible GL Context
            GL.Enable(EnableCap.Texture2D);

            Cef.Initialize(settings);

            InitOpenVR();

            _system = OpenVR.System;
            _compositor = OpenVR.Compositor;
            _overlay = OpenVR.Overlay;

            Console.WriteLine("SteamVR_WebKit Initialised");

            _initialised = true;

        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Cef.Shutdown();
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
        }

        public static void RunOverlays()
        {
            Stopwatch fpsWatch = new Stopwatch();
            while (!_doStop)
            {
                fpsWatch.Restart();
                foreach (WebKitOverlay overlay in Overlays)
                {
                    overlay.Update();
                    overlay.Draw();
                }
                fpsWatch.Stop();
                Thread.Sleep(fpsWatch.ElapsedMilliseconds >= _frameSleep ? 0 : (int)(_frameSleep - fpsWatch.ElapsedMilliseconds));
            }
        }
    }
}
