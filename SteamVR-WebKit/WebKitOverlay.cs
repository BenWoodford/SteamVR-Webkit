using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;
using OpenTK.Graphics.OpenGL;
using Valve.VR;
using System.Drawing;
using System.Drawing.Imaging;
using CefSharp.Internals;

namespace SteamVR_WebKit
{
    public class WebKitOverlay
    {
        Uri _uri;
        Overlay _overlay;
        int _glTextureId = 0;
        Texture_t _textureData;
        string _cachePath;
        double _zoomLevel;
        int _windowWidth;
        int _windowHeight;
        bool _isRendering = false;
        ChromiumWebBrowser _browser;

        public Uri Uri
        {
            get { return _uri; }
        }

        public Overlay Overlay
        {
            get { return _overlay; }
        }

        public bool IsRendering
        {
            get { return _isRendering; }
        }

        public int GLTextureID
        {
            get { return _glTextureId; }
        }

        public string CachePath { get { return _cachePath; } set { _cachePath = value; } }
        public double ZoomLevel { get { return _zoomLevel; } set { _zoomLevel = value; } }

        public WebKitOverlay(Uri uri, int windowWidth, int windowHeight, string overlayKey, string overlayName, float overlayWidth = 2f, bool isInGameOverlay = false)
        {
            _uri = uri;
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
            _overlay = new Overlay(overlayKey, overlayName, overlayWidth, isInGameOverlay);

            SteamVR_WebKit.Overlays.Add(this);

            SteamVR_WebKit.OverlayManager.ShowDashboard(overlayKey);

            SetupTextures();
        }

        public void StartBrowser()
        {
            AsyncBrowser();
        }

        protected virtual async void AsyncBrowser()
        {
            BrowserSettings browserSettings = new BrowserSettings();
            browserSettings.WindowlessFrameRate = 30;

            RequestContextSettings reqSettings = new RequestContextSettings { CachePath = CachePath };

            using (RequestContext context = new RequestContext(reqSettings))
            {
                _browser = new ChromiumWebBrowser(Uri.ToString(), browserSettings, context);
                _browser.Size = new Size((int)_windowWidth, (int)_windowHeight);
                _browser.NewScreenshot += Browser_NewScreenshot;
                if (_zoomLevel > 1)
                {
                    _browser.FrameLoadStart += (s, argsi) =>
                    {
                        if (argsi.Frame.IsMain)
                        {
                            ((ChromiumWebBrowser)s).SetZoomLevel(_zoomLevel);
                        }
                    };
                }

                await LoadPageAsync(_browser);
            }
        }

        public static Task LoadPageAsync(ChromiumWebBrowser browser, string address = null)
        {
            //If using .Net 4.6 then use TaskCreationOptions.RunContinuationsAsynchronously
            //and switch to tcs.TrySetResult below - no need for the custom extension method
            var tcs = new TaskCompletionSource<bool>();

            EventHandler<LoadingStateChangedEventArgs> handler = null;
            handler = (sender, args) =>
            {
                //Wait for while page to finish loading not just the first frame
                if (!args.IsLoading)
                {
                    browser.LoadingStateChanged -= handler;
                    //This is required when using a standard TaskCompletionSource
                    //Extension method found in the CefSharp.Internals namespace
                    tcs.TrySetResultAsync(true);
                }
            };

            browser.LoadingStateChanged += handler;

            if (!string.IsNullOrEmpty(address))
            {
                browser.Load(address);
            }
            return tcs.Task;
        }

        private void Browser_NewScreenshot(object sender, EventArgs e)
        {
            ChromiumWebBrowser browser = (ChromiumWebBrowser)sender;

            if (browser.Bitmap != null)
                _isRendering = true;
        }

        protected virtual void SetupTextures()
        {
            GL.BindTexture(TextureTarget.Texture2D, 0);
            _glTextureId = GL.GenTexture();

            _textureData = new Texture_t();
            _textureData.eColorSpace = EColorSpace.Linear;
            _textureData.eType = ETextureType.OpenGL;
            _textureData.handle = (IntPtr)_glTextureId;
        }

        public virtual void UpdateTexture()
        {
            if (_browser.Bitmap == null)
                return;

            lock(_browser.BitmapLock) {
                // Eugh. I hate this, but it'll do till I can work out how to flip it more efficiently.
                Bitmap bmp = new Bitmap(_browser.Bitmap);
                bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

                BitmapData bmpData = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                    );

                GL.BindTexture(TextureTarget.Texture2D, _glTextureId);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp.Width, bmp.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                bmp.UnlockBits(bmpData);

                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        public virtual void Update()
        {
            if (!Overlay.IsVisible())
                return;

            // We'll handle mouse events here eventually.
        }

        public virtual void Draw()
        {
            if (!Overlay.IsVisible())
                return;

            UpdateTexture();
            Overlay.SetTexture(ref _textureData);
            Overlay.Show();
        }
    }
}