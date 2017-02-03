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
        static WebKitOverlay overlay;
        static void Main(string[] args)
        {
            SteamVR_WebKit.SteamVR_WebKit.Init();

            overlay = new WebKitOverlay(new Uri("file://" + Environment.CurrentDirectory + "/Resources/index.html"), 1024, 1024, "webkitTest", "WebKit", 2f, false);
            overlay.Overlay.SetThumbnail("Resources/webkit-logo.png");
            overlay.BrowserPreInit += Overlay_BrowserPreInit;
            overlay.BrowserReady += Overlay_BrowserReady;
            overlay.StartBrowser();

            SteamVR_WebKit.SteamVR_WebKit.RunOverlays(); // Runs update/draw calls for all active overlays. And yes, it's blocking.
        }

        private static void Overlay_BrowserReady(object sender, EventArgs e)
        {
            overlay.Browser.GetBrowser().GetHost().ShowDevTools();
        }

        private static void Browser_ConsoleMessage(object sender, CefSharp.ConsoleMessageEventArgs e)
        {
            string[] srcSplit = e.Source.Split('/'); // We only want the filename
            Console.WriteLine("[CONSOLE " + srcSplit[srcSplit.Length - 1] + ":" + e.Line + "] " + e.Message);
        }

        private static void Overlay_BrowserPreInit(object sender, EventArgs e)
        {
            Console.WriteLine("Browser is ready.");

            overlay.Browser.ConsoleMessage += Browser_ConsoleMessage;
            overlay.Browser.RegisterJsObject("testObject", new JsCallbackTest());
        }
    }
}
