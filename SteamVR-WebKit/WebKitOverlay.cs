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
        bool _wasVisible = false;
        VREvent_t ovrEvent;
        BrowserSettings _browserSettings;

        public event EventHandler BrowserPreInit;
        public event EventHandler BrowserReady;
        public event EventHandler BrowserRenderUpdate;
        public event EventHandler PageReady;

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

        public BrowserSettings BrowserSettings
        {
            get { if (_browser != null) return _browser.BrowserSettings; else return _browserSettings; }
            set { _browserSettings = value; }
        }

        public ChromiumWebBrowser Browser
        {
            get { return _browser; }
        }

        public string CachePath { get { return _cachePath; } set { _cachePath = value; } }
        public double ZoomLevel { get { return _zoomLevel; } set { _zoomLevel = value; } }

        public WebKitOverlay(Uri uri, int windowWidth, int windowHeight, string overlayKey, string overlayName, float overlayWidth = 2f, bool isInGameOverlay = false)
        {
            _browserSettings = new BrowserSettings();
            _browserSettings.WindowlessFrameRate = 60;
            _uri = uri;
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
            _overlay = new Overlay(overlayKey, overlayName, overlayWidth, isInGameOverlay);

            SteamVR_WebKit.Overlays.Add(this);
            SteamVR_WebKit.OverlayManager.ShowDashboard(overlayKey);

            SetupTextures();
        }

        public void ToggleAudio()
        {
            throw new NotImplementedException("I'll find the option to change the audio in CEF eventually.");
        }

        public void StartBrowser()
        {
            AsyncBrowser();
        }

        protected virtual async void AsyncBrowser()
        {
            RequestContextSettings reqSettings = new RequestContextSettings { CachePath = CachePath };

            using (RequestContext context = new RequestContext(reqSettings))
            {
                _browser = new ChromiumWebBrowser(Uri.ToString(), _browserSettings, context);
                BrowserPreInit?.Invoke(_browser, new EventArgs());
                _browser.Size = new Size((int)_windowWidth, (int)_windowHeight);
                _browser.NewScreenshot += Browser_NewScreenshot;

                _browser.BrowserInitialized += _browser_BrowserInitialized;

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

        private void _browser_BrowserInitialized(object sender, EventArgs e)
        {
            BrowserReady?.Invoke(_browser, new EventArgs());
        }

        public Task LoadPageAsync(ChromiumWebBrowser browser, string address = null)
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
                    Console.WriteLine("Page Loaded.");
                    PageReady?.Invoke(browser, new EventArgs());

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

            BrowserRenderUpdate?.Invoke(sender, e);
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
                Bitmap copyBitmap = new Bitmap(_browser.Bitmap);

                BitmapData bmpData = copyBitmap.LockBits(
                    new Rectangle(0, 0, copyBitmap.Width, copyBitmap.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                    );

                GL.BindTexture(TextureTarget.Texture2D, _glTextureId);
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _browser.Bitmap.Width, _browser.Bitmap.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                //_browser.Bitmap.UnlockBits(bmpData);
                copyBitmap.UnlockBits(bmpData);

                GL.BindTexture(TextureTarget.Texture2D, 0);
            }
        }

        public virtual void Update()
        {
            if (!_isRendering)
                return;

            if (!Overlay.IsVisible())
            {
                if (_wasVisible)
                {
                    _wasVisible = false;
                    HandleMouseLeaveEvent();
                }
                return;
            }

            _wasVisible = true;

            // We'll handle mouse events here eventually.

            while(Overlay.PollEvent(ref ovrEvent))
            {
                HandleEvent();
            }
        }

        public virtual void HandleEvent()
        {
            switch((EVREventType)ovrEvent.eventType)
            {
                case EVREventType.VREvent_MouseMove:
                    HandleMouseMoveEvent(ovrEvent);
                    break;

                case EVREventType.VREvent_MouseButtonDown:
                    HandleMouseButtonDownEvent(ovrEvent);
                    break;

                case EVREventType.VREvent_MouseButtonUp:
                    HandleMouseButtonUpEvent(ovrEvent);
                    break;
            }
        }

        MouseButtonType GetMouseButtonType(uint button)
        {
            switch ((EVRMouseButton)button)
            {
                case EVRMouseButton.Left:
                    return MouseButtonType.Left;

                case EVRMouseButton.Right:
                    return MouseButtonType.Right;

                case EVRMouseButton.Middle:
                    return MouseButtonType.Middle;
            }
            return MouseButtonType.Left;
        }

        void HandleMouseMoveEvent(VREvent_t ev)
        {
            _browser.GetBrowser().GetHost().SendMouseMoveEvent((int)(_windowWidth * ev.data.mouse.x), (int)(_windowHeight * ev.data.mouse.y), false, CefEventFlags.None);
        }

        void HandleMouseButtonDownEvent(VREvent_t ev)
        {
            _browser.GetBrowser().GetHost().SendMouseClickEvent((int)(_windowWidth * ev.data.mouse.x), (int)(_windowHeight * ev.data.mouse.y), GetMouseButtonType(ev.data.mouse.button), false, 1, CefEventFlags.None);
        }

        void HandleMouseButtonUpEvent(VREvent_t ev)
        {
            _browser.GetBrowser().GetHost().SendMouseClickEvent((int)(_windowWidth * ev.data.mouse.x), (int)(_windowHeight * ev.data.mouse.y), GetMouseButtonType(ev.data.mouse.button), true, 1, CefEventFlags.None);
        }

        void HandleMouseLeaveEvent()
        {
            _browser.GetBrowser().GetHost().SendMouseMoveEvent(0, 0, true, CefEventFlags.None);
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