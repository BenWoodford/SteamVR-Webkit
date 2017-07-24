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
        public const int SCROLL_AMOUNT_PER_SWIPE = 1500;

        Uri _uri;
        Overlay _dashboardOverlay;
        Overlay _inGameOverlay;
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
        bool _renderInGameOverlay;

        bool _browserDidUpdate;

        string _overlayKey;
        string _overlayName;

        bool _allowScrolling = true;

        public bool AllowScrolling
        {
            get { return _allowScrolling; } set { _allowScrolling = value; }
        }

        bool _isHolding = false;

        public event EventHandler BrowserPreInit;
        public event EventHandler BrowserReady;
        public event EventHandler BrowserRenderUpdate;
        public event EventHandler PageReady;

        public event EventHandler PreUpdateCallback;
        public event EventHandler PostUpdateCallback;

        public event EventHandler PreDrawCallback;
        public event EventHandler PostDrawCallback;

        public Uri Uri
        {
            get { return _uri; }
        }

        public Overlay DashboardOverlay
        {
            get { return _dashboardOverlay; }
        }

        public Overlay InGameOverlay
        {
            get { return _inGameOverlay; }
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

        public bool RenderInGameOverlay
        {
            get { return _renderInGameOverlay; }
            set
            {
                _renderInGameOverlay = value;
                if (InGameOverlay != null)
                {
                    if (_renderInGameOverlay)
                        InGameOverlay.Show();
                    else
                        InGameOverlay.Hide();
                }
            }
        }

        public ChromiumWebBrowser Browser
        {
            get { return _browser; }
        }

        public string CachePath { get { return _cachePath; } set { _cachePath = value; } }
        public double ZoomLevel { get { return _zoomLevel; } set { _zoomLevel = value; } }

        [Obsolete("Please use the newer constructor that lets you define whether to show in dashboard, in-game or both instead.")]
        public WebKitOverlay(Uri uri, int windowWidth, int windowHeight, string overlayKey, string overlayName, float overlayWidth = 2f, bool isInGameOverlay = false) : this(uri, windowWidth, windowHeight, overlayKey, overlayName, isInGameOverlay ? OverlayType.InGame : OverlayType.Dashboard)
        {

        }

        public WebKitOverlay(Uri uri, int windowWidth, int windowHeight, string overlayKey, string overlayName, OverlayType overlayType)
        {
            if (!SteamVR_WebKit.Initialised)
                SteamVR_WebKit.Init();

            _browserSettings = new BrowserSettings();
            _browserSettings.WindowlessFrameRate = 30;
            _uri = uri;
            _windowWidth = windowWidth;
            _windowHeight = windowHeight;
            _overlayKey = overlayKey;
            _overlayName = overlayName;

            if (overlayType == OverlayType.Dashboard)
                CreateDashboardOverlay();
            else if (overlayType == OverlayType.InGame)
                CreateInGameOverlay();
            else
            {
                CreateDashboardOverlay();
                CreateInGameOverlay();
            }

            SteamVR_WebKit.Overlays.Add(this);

            SetupTextures();
        }

        public void ToggleAudio()
        {
            throw new NotImplementedException("I'll find the option to change the audio in CEF eventually.");
        }

        public void CreateDashboardOverlay()
        {
            _dashboardOverlay = new Overlay("dashboard." + _overlayKey, _overlayName, 2.0f, false);
            _dashboardOverlay.SetTextureSize(_windowWidth, _windowHeight);
            //_dashboardOverlay.Show();
        }

        public void CreateInGameOverlay()
        {
            _inGameOverlay = new Overlay("ingame." + _overlayKey, _overlayName, 2.0f, true);
            _inGameOverlay.Show();
        }

        public void DestroyInGameOverlay()
        {
            _inGameOverlay.Destroy();
            _inGameOverlay = null;
        }

        public void DestroyDashboardOverlay()
        {
            _dashboardOverlay.Destroy();
            _dashboardOverlay = null;
        }

        public void StartBrowser(bool waitForAttachment = false)
        {
            //Allow the overlay to let us know when the controller showed up and we were able to attach to it
            if (waitForAttachment)
            {
                //Its possible that it happened before we got here if the controller was present at start
                if (_inGameOverlay.AttachmentSuccess)
                    AsyncBrowser();
                else
                    _inGameOverlay.OnAttachmentSuccess += AsyncBrowser;
            }
            else
                AsyncBrowser();
        }

        protected virtual async void AsyncBrowser()
        {
            RequestContextSettings reqSettings = new RequestContextSettings { CachePath = CachePath };

            SteamVR_WebKit.Log("Browser Initialising for " + _overlayKey);

            using (RequestContext context = new RequestContext(reqSettings))
            {
                _browser = new ChromiumWebBrowser(Uri.ToString(), _browserSettings, context, false);
                BrowserPreInit?.Invoke(_browser, new EventArgs());
                _browser.Size = new Size((int)_windowWidth, (int)_windowHeight);
                _browser.NewScreenshot += Browser_NewScreenshot;

                _browser.BrowserInitialized += _browser_BrowserInitialized;

                _browser.CreateBrowser(IntPtr.Zero);

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

            //If while we waited any JS commands were queued, then run those now
            ExecQueuedJS();
        }

        private void _browser_BrowserInitialized(object sender, EventArgs e)
        {
            SteamVR_WebKit.Log("Browser Initialised for " + _overlayKey);
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
                    SteamVR_WebKit.Log("Page Loaded for " + _overlayKey);
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

            _browserDidUpdate = true;

            BrowserRenderUpdate?.Invoke(sender, e);
        }

        protected virtual void SetupTextures()
        {
            SteamVR_WebKit.Log("Setting up texture for " + _overlayKey);
            GL.BindTexture(TextureTarget.Texture2D, 0);

            if(SteamVR_WebKit.TraceLevel)
                SteamVR_WebKit.Log("BindTexture: " + GL.GetError());

            _glTextureId = GL.GenTexture();

            if (SteamVR_WebKit.TraceLevel)
                SteamVR_WebKit.Log("GenTexture: " + GL.GetError());

            _textureData = new Texture_t();
            _textureData.eColorSpace = EColorSpace.Linear;
            _textureData.eType = ETextureType.OpenGL;
            _textureData.handle = (IntPtr)_glTextureId;

            SteamVR_WebKit.Log("Texture Setup complete for " + _overlayKey);
        }

        public virtual void UpdateTexture()
        {
            if (!_browserDidUpdate)
                return;

            _browserDidUpdate = false;

            if (_browser.Bitmap == null)
                return;

            lock (_browser.BitmapLock) {
                BitmapData bmpData = _browser.Bitmap.LockBits(
                    new Rectangle(0, 0, _browser.Bitmap.Width, _browser.Bitmap.Height),
                    ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb
                    );

                GL.BindTexture(TextureTarget.Texture2D, _glTextureId);

                if (SteamVR_WebKit.TraceLevel)
                    SteamVR_WebKit.Log("BindTexture: " + GL.GetError());

                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, _browser.Bitmap.Width, _browser.Bitmap.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);

                if (SteamVR_WebKit.TraceLevel)
                    SteamVR_WebKit.Log("TexImage2D: " + GL.GetError());

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                if (SteamVR_WebKit.TraceLevel)
                    SteamVR_WebKit.Log("TexParameter: " + GL.GetError());

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

                if (SteamVR_WebKit.TraceLevel)
                    SteamVR_WebKit.Log("TexParameter: " + GL.GetError());

                _browser.Bitmap.UnlockBits(bmpData);
                //copyBitmap.UnlockBits(bmpData);

                GL.BindTexture(TextureTarget.Texture2D, 0);

                if (SteamVR_WebKit.TraceLevel)
                    SteamVR_WebKit.Log("BindTexture: " + GL.GetError());
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
                    
                case EVREventType.VREvent_Scroll:
                    if(_allowScrolling)
                        HandleMouseScrollEvent(ovrEvent);
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
            //_browser.GetBrowser().GetHost().SendMouseMoveEvent((int)(_windowWidth * ev.data.mouse.x), (int)(_windowHeight * (1f - ev.data.mouse.y)), false, CefEventFlags.None);
            _browser.GetBrowser().GetHost().SendMouseMoveEvent((int)ev.data.mouse.x, _windowHeight - (int)ev.data.mouse.y, false, _isHolding ? CefEventFlags.LeftMouseButton : CefEventFlags.None);
        }

        void HandleMouseButtonDownEvent(VREvent_t ev)
        {
            if (ev.data.mouse.button != (uint)EVRMouseButton.Left)
                return;

            _browser.GetBrowser().GetHost().SendMouseClickEvent((int)ev.data.mouse.x, _windowHeight - (int)ev.data.mouse.y, GetMouseButtonType(ev.data.mouse.button), false, 1, CefEventFlags.None);

            if((EVRMouseButton)ev.data.mouse.button == EVRMouseButton.Left)
            {
                _isHolding = true;
            }
        }

        void HandleMouseButtonUpEvent(VREvent_t ev)
        {
            if (ev.data.mouse.button != (uint)EVRMouseButton.Left)
                return;

            _browser.GetBrowser().GetHost().SendMouseClickEvent((int)ev.data.mouse.x, _windowHeight - (int)ev.data.mouse.y, GetMouseButtonType(ev.data.mouse.button), true, 1, CefEventFlags.None);

            if ((EVRMouseButton)ev.data.mouse.button == EVRMouseButton.Left)
            {
                _isHolding = false;
            }
        }

        void HandleMouseLeaveEvent()
        {
            _browser.GetBrowser().GetHost().SendMouseMoveEvent(0, 0, true, CefEventFlags.None);
        }
        
        void HandleMouseScrollEvent(VREvent_t ev)
        {
            _browser.GetBrowser().GetHost().SendMouseWheelEvent(0, 0, (int)(ev.data.scroll.xdelta * SCROLL_AMOUNT_PER_SWIPE), (int)(ev.data.scroll.ydelta * SCROLL_AMOUNT_PER_SWIPE), CefEventFlags.None);
        }

        bool CanDoUpdates()
        {
            if (DashboardOverlay == null && InGameOverlay == null)
                return false; // We can go no further.

            //This prevents Draw() from failing on get of bitmap when attachment is delayed for controllers
            if (InGameOverlay != null && !InGameOverlay.AttachmentSuccess)
                return false;

            if (DashboardOverlay != null && DashboardOverlay.IsVisible())
                return true;

            if (InGameOverlay != null && InGameOverlay.IsVisible())
                return true;

            return false;
        }

        public virtual void Update()
        {
            if (!_isRendering)
                return;

            PreUpdateCallback?.Invoke(this, new EventArgs());

            // Mouse inputs are for dashboards only right now.

            if (DashboardOverlay != null && !DashboardOverlay.IsVisible())
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

            if (DashboardOverlay != null)
            {
                while (DashboardOverlay.PollEvent(ref ovrEvent))
                {
                    HandleEvent();
                }
            }

            PostUpdateCallback?.Invoke(this, new EventArgs());
        }

        public virtual void Draw()
        {
            if (!CanDoUpdates())
                return;

            PreDrawCallback?.Invoke(this, new EventArgs());

            UpdateTexture();

            if (DashboardOverlay != null && DashboardOverlay.IsVisible())
            {
                DashboardOverlay.SetTexture(ref _textureData);
                DashboardOverlay.Show();
            }

            if(InGameOverlay != null && InGameOverlay.IsVisible())
            {
                InGameOverlay.SetTexture(ref _textureData);
                InGameOverlay.Show();
            }

            PostDrawCallback?.Invoke(this, new EventArgs());
        }
        
        Queue<string> JSCommandQueue = new Queue<string>();

        private void ExecAsyncJS(string js)
        {
            Browser.GetBrowser().FocusedFrame.ExecuteJavaScriptAsync(js);
        }

        public void TryExecAsyncJS(string js)
        {
            if (_inGameOverlay == null || _inGameOverlay.AttachmentSuccess)
                ExecAsyncJS(js);
            else
                JSCommandQueue.Enqueue(js);
        }

        public void ExecQueuedJS()
        {
            foreach (string jsCmd in JSCommandQueue.ToList())
            {
                ExecAsyncJS(jsCmd);
                JSCommandQueue.Dequeue();
            }
        }
    }
}