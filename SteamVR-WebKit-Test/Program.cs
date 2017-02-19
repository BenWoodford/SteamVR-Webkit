using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamVR_WebKit;
using System.Drawing;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;

namespace SteamVR_WebKit_Test
{
    class Program
    {
        // Tests basic overlay
        static WebKitOverlay basicOverlay;

        // Tests video overlay in-game
        static WebKitOverlay videoOverlay;

        // Tests OpenVR.Applications proxy and using Angular.
        static WebKitOverlay applicationsOverlay;

        static void Main(string[] args)
        {
            SteamVR_WebKit.SteamVR_WebKit.Init();
            SteamVR_WebKit.SteamVR_WebKit.FPS = 30;

            //Notifications.RegisterIcon("default", new Bitmap(Environment.CurrentDirectory + "\\Resources\\alert.png"));

            basicOverlay = new WebKitOverlay(new Uri("file://" + Environment.CurrentDirectory + "/Resources/index.html"), 1024, 1024, "webkitTest", "WebKit-Core", OverlayType.Dashboard);
            //overlay = new WebKitOverlay(new Uri("https://codepen.io/AtomicNoggin/full/yqwsG/"), 1024, 1024, "webkitTest", "WebKit", OverlayType.Dashboard);
            basicOverlay.DashboardOverlay.Width = 2.0f;
            basicOverlay.DashboardOverlay.SetThumbnail("Resources/webkit-logo.png");
            basicOverlay.BrowserPreInit += Overlay_BrowserPreInit;
            basicOverlay.BrowserReady += Overlay_BrowserReady;
            basicOverlay.StartBrowser();

            videoOverlay = new WebKitOverlay(new Uri("https://www.youtube.com/embed/d7Co9PyueSk"), 1920, 1080, "videoTest", "Video", OverlayType.Both);
            videoOverlay.InGameOverlay.SetDeviceAttachment(AttachmentType.Absolute, new Vector3(0f, 1.0f, -1f), Quaternion.FromEulerAngles(0f, 45f, 0f));
            videoOverlay.InGameOverlay.Width = 0.5f;
            videoOverlay.BrowserPreInit += VideoOverlay_BrowserPreInit;
            videoOverlay.BrowserReady += VideoOverlay_BrowserReady;
            videoOverlay.DashboardOverlay.Width = 2.0f;
            videoOverlay.StartBrowser();

            applicationsOverlay = new WebKitOverlay(new Uri("file://" + Environment.CurrentDirectory + "/Resources/applications.html"), 1024, 1024, "webkitTestApps", "WebKit-Apps", OverlayType.Dashboard);
            applicationsOverlay.DashboardOverlay.Width = 2.0f;
            applicationsOverlay.DashboardOverlay.SetThumbnail("Resources/webkit-logo.png");
            applicationsOverlay.BrowserPreInit += ApplicationsOverlay_BrowserPreInit;
            applicationsOverlay.BrowserReady += ApplicationsOverlay_BrowserReady;
            applicationsOverlay.StartBrowser();

            SteamVR_WebKit.SteamVR_WebKit.RunOverlays(); // Runs update/draw calls for all active overlays. And yes, it's blocking.
        }

        private static void ApplicationsOverlay_BrowserPreInit(object sender, EventArgs e)
        {
            applicationsOverlay.Browser.RegisterJsObject("applications", new SteamVR_WebKit.JsInterop.Applications());
        }

        private static void ApplicationsOverlay_BrowserReady(object sender, EventArgs e)
        {
            applicationsOverlay.Browser.GetBrowser().GetHost().ShowDevTools();
        }

        private static void VideoOverlay_BrowserReady(object sender, EventArgs e)
        {
            //videoOverlay.Browser.GetBrowser().GetHost().ShowDevTools();
        }

        private static void VideoOverlay_BrowserPreInit(object sender, EventArgs e)
        {
            videoOverlay.Browser.RegisterJsObject("overlay", videoOverlay);

            /* Doesn't work yet, need more info on how notifications work in OpenVR.
            JsNotifications jsNotifications = new JsNotifications(videoOverlay.DashboardOverlay);
            videoOverlay.Browser.RegisterJsObject("notifications", jsNotifications);
            */
        }

        private static void Overlay_BrowserReady(object sender, EventArgs e)
        {
            //basicOverlay.Browser.GetBrowser().GetHost().ShowDevTools();
        }

        private static void Browser_ConsoleMessage(object sender, CefSharp.ConsoleMessageEventArgs e)
        {
            string[] srcSplit = e.Source.Split('/'); // We only want the filename
            Console.WriteLine("[CONSOLE " + srcSplit[srcSplit.Length - 1] + ":" + e.Line + "] " + e.Message);
        }

        private static void Overlay_BrowserPreInit(object sender, EventArgs e)
        {
            Console.WriteLine("Browser is ready.");

            basicOverlay.Browser.ConsoleMessage += Browser_ConsoleMessage;
            basicOverlay.Browser.RegisterJsObject("testObject", new JsCallbackTest());
        }
    }
}
