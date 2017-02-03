using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace SteamVR_WebKit
{
    public class Overlay
    {
        string _key;
        string _name;
        ulong _handle = 0;
        ulong _thumbnailHandle = 0;
        float _width;

        bool _ingame = false;

        public String Key
        {
            get { return _key; }
        }

        public String Name
        {
            get { return _name; }
        }

        public ulong Handle
        {
            get { return _handle; }
        }

        public ulong ThumbnailHandle
        {
            get { return _thumbnailHandle; }
        }

        public float Width
        {
            get { return _width; }
            set { _width = value;  UpdateWidth(); }
        }

        public Overlay(string key, string name, float width = 2.0f, bool isInGameOverlay = false)
        {
            _key = key;
            _name = name;
            _ingame = isInGameOverlay;

            CreateOverlayInSteamVR();
            Width = width;

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_handle);
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_thumbnailHandle);
        }

        void SetThumbnailPath(string path)
        {
            if(_thumbnailHandle == 0)
            {
                throw new Exception("Overlay not initialised");
            }

            EVROverlayError err = EVROverlayError.None;

            SteamVR_WebKit.OverlayManager.SetOverlayFromFile(_thumbnailHandle, path);

            if(err != EVROverlayError.None)
            {
                throw new Exception("Failed to set thumbnail: " + err.ToString());
            }
        }

        void CreateOverlayInSteamVR()
        {
            EVROverlayError ovrErr = EVROverlayError.None;

            if (SteamVR_WebKit.OverlayManager == null)
                SteamVR_WebKit.Init();

            if (_ingame)
                ovrErr = SteamVR_WebKit.OverlayManager.CreateOverlay(Key, Name, ref _handle);
            else
            {
                ovrErr = SteamVR_WebKit.OverlayManager.CreateDashboardOverlay(Key, Name, ref _handle, ref _thumbnailHandle);
            }

            Console.WriteLine("Overlay Handle " + _handle + ", Thumbnail Handle: " + _thumbnailHandle);

            if (ovrErr != EVROverlayError.None)
            {
                throw new Exception("Failed to create overlay: " + ovrErr.ToString());
            }

            SteamVR_WebKit.OverlayManager.SetOverlayAlpha(_handle, 1.0f);
            SteamVR_WebKit.OverlayManager.SetOverlayColor(_handle, 1.0f, 1.0f, 1.0f);
        }

        public void SetThumbnail(string filePath)
        {
            SetThumbnailPath(System.IO.Path.IsPathRooted(filePath) ? filePath : Environment.CurrentDirectory + "\\" + filePath);
        }

        void UpdateWidth()
        {
            SteamVR_WebKit.OverlayManager.SetOverlayWidthInMeters(_handle, _width);
        }

        public void SetTexture(ref Texture_t texture)
        {
            EVROverlayError err = SteamVR_WebKit.OverlayManager.SetOverlayTexture(_handle, ref texture);

            if (err != EVROverlayError.None)
                Console.WriteLine("Failed to send texture: " + err.ToString());
        }

        public void Show()
        {
            SteamVR_WebKit.OverlayManager.ShowOverlay(_handle);
        }

        public void ForceShow()
        {
            SteamVR_WebKit.OverlayManager.ShowDashboard(_key);
        }

        public bool IsVisible()
        {
            return SteamVR_WebKit.OverlayManager.IsOverlayVisible(_handle);
        }
    }
}
