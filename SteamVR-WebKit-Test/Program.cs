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

            overlay = new WebKitOverlay(new Uri("http://webglsamples.org/aquarium/aquarium.html"), 1024, 1024, "webkitTest", "WebKit", 2f, false);
            overlay.Overlay.SetThumbnail("Resources/webkit-logo.png");
            overlay.StartBrowser();

            SteamVR_WebKit.SteamVR_WebKit.RunOverlays(); // Runs update/draw calls for all active overlays

            //bool exit = false;

            /*while (!exit)
            {
                string readLine = Console.ReadLine();
                if (readLine == "q")
                    exit = true;
            }*/
        }

        private static void Gw_Load(object sender, EventArgs e)
        {
        }
    }
}
