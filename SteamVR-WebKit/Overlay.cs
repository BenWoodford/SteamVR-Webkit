using OpenTK;
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
        float _alpha;

        AttachmentType _attachmentType = AttachmentType.Absolute;
        Vector3 _position = Vector3.Zero;
        Quaternion _rotation = Quaternion.Identity;

        bool _ingame = false;
        uint eventSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t));

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

        public float Alpha
        {
            get { return _alpha; }
            set { _alpha = value; UpdateAlpha(); }
        }

        public int WidthInCm
        {
            get { return (int)(_width * 100f); }
            set { _width = (float)value / 100; UpdateWidth(); }
        }
        
        /// <summary>
        /// Create object for an existing overlay by overlay key. Useful to gain access to the stock overlays.
        /// </summary>
        /// <param name="pchOverlayKey"></param>
        public Overlay(string pchOverlayKey)
        {
            EVROverlayError ovrErr = EVROverlayError.None;

            ovrErr = SteamVR_WebKit.OverlayManager.FindOverlay(pchOverlayKey, ref _handle);

            if (ovrErr != EVROverlayError.None)
            {
                throw new Exception("Failed to create overlay: " + ovrErr.ToString());
            }

            _key = pchOverlayKey;

            StringBuilder sb = new StringBuilder();
            SteamVR_WebKit.OverlayManager.GetOverlayName(_handle, new StringBuilder(), 1000, ref ovrErr);
            _name = sb.ToString();

            SteamVR_WebKit.OverlayManager.GetOverlayWidthInMeters(_handle, ref _width);

            SteamVR_WebKit.OverlayManager.GetOverlayAlpha(_handle, ref _alpha);
        }

        public Overlay(string key, string name, float width = 2.0f, bool isInGameOverlay = false)
        {
            _key = key;
            _name = name;
            _ingame = isInGameOverlay;

            CreateOverlayInSteamVR();
            Width = width;
            Alpha = 1.0f;

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_handle);
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_thumbnailHandle);
        }

        public void SetHighQuality()
        {
            if (_ingame)
                SteamVR_WebKit.OverlayManager.SetHighQualityOverlay(_handle);
            else
                throw new Exception("Dashboard overlays cannot be high quality!");
        }

        public void SetCurved(float minDistance, float maxDistance)
        {
            SetHighQuality();

            SteamVR_WebKit.OverlayManager.SetOverlayAutoCurveDistanceRangeInMeters(_handle, minDistance, maxDistance);
        }

        public void Rotate(double pitch, double yaw, double roll)
        {
            _rotation *= Quaternion.FromEulerAngles((float)pitch, (float)yaw, (float)roll);
            SetDeviceAttachment(_attachmentType, _position, _rotation);
        }

        public void MoveBy(double x, double y, double z)
        {
            _position += new Vector3((float)x, (float)y, (float)z);
            SetDeviceAttachment(_attachmentType, _position, _rotation);
        }

        public void MoveAbsolute(double x, double y, double z)
        {
            _position = new Vector3((float)x, (float)y, (float)z);
            SetDeviceAttachment(_attachmentType, _position, _rotation);
        }

        public void SetDeviceAttachment(AttachmentType attachmentType, Vector3 position, Quaternion rotation)
        {
            if (!_ingame)
                throw new Exception("Cannot set attachment for dashboard overlay");

            if(attachmentType == AttachmentType.Absolute)
            {
                _position = position;
                _rotation = rotation;
                Matrix3x4 translationMatrix = Matrix3x4.CreateTranslation(position);
                Matrix3x4 rotationMatrix = Matrix3x4.CreateFromQuaternion(rotation);

                HmdMatrix34_t matrix = TransformUtils.OpenTKMatrixToOpenVRMatrix(translationMatrix * rotationMatrix);

                SteamVR_WebKit.OverlayManager.SetOverlayTransformAbsolute(_handle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref matrix);
            } else if(attachmentType == AttachmentType.Hmd)
            {
                SetDeviceAttachment((uint)0, position, rotation);
            } else
            {
                SetDeviceAttachment(SteamVR_WebKit.OVRSystem.GetTrackedDeviceIndexForControllerRole(attachmentType == AttachmentType.LeftController ? ETrackedControllerRole.LeftHand : ETrackedControllerRole.RightHand), position, rotation);
            }
        }

        public void SetDeviceAttachment(uint index, Vector3 position, Quaternion rotation)
        {
            if (!_ingame)
                throw new Exception("Cannot set attachment for dashboard overlay");

            _position = position;
            _rotation = rotation;

            Matrix3x4 translationMatrix = Matrix3x4.CreateTranslation(position);
            Matrix3x4 rotationMatrix = Matrix3x4.CreateFromQuaternion(rotation);

            HmdMatrix34_t matrix = TransformUtils.OpenTKMatrixToOpenVRMatrix(translationMatrix * rotationMatrix);

            SteamVR_WebKit.OverlayManager.SetOverlayTransformTrackedDeviceRelative(_handle, index, ref matrix);
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
            {
                ovrErr = SteamVR_WebKit.OverlayManager.CreateOverlay(Key, Name, ref _handle);
                SteamVR_WebKit.OverlayManager.SetOverlayInputMethod(_handle, VROverlayInputMethod.None);
            }
            else
            {
                ovrErr = SteamVR_WebKit.OverlayManager.CreateDashboardOverlay(Key, Name, ref _handle, ref _thumbnailHandle);
                SteamVR_WebKit.OverlayManager.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse);
                SteamVR_WebKit.OverlayManager.SetOverlayFlag(_handle, VROverlayFlags.ShowTouchPadScrollWheel, true);
                SteamVR_WebKit.OverlayManager.SetOverlayFlag(_handle, VROverlayFlags.SendVRScrollEvents, true);
            }

            Console.WriteLine("Overlay Handle " + _handle + ", Thumbnail Handle: " + _thumbnailHandle);

            if (ovrErr != EVROverlayError.None)
            {
                throw new Exception("Failed to create overlay: " + ovrErr.ToString());
            }

            SteamVR_WebKit.OverlayManager.SetOverlayAlpha(_handle, 1.0f);
            SteamVR_WebKit.OverlayManager.SetOverlayColor(_handle, 1.0f, 1.0f, 1.0f);


            // Because it'll be upside down otherwise.
            VRTextureBounds_t bounds;
            bounds.vMax = 0; bounds.vMin = 1;  // Flip the Y

            // Leave as defaults
            bounds.uMin = 0;
            bounds.uMax = 1;

            SteamVR_WebKit.OverlayManager.SetOverlayTextureBounds(_handle, ref bounds);
        }

        public void SetTextureSize(float width, float height)
        {
            HmdVector2_t scale;
            scale.v0 = width;
            scale.v1 = height;
            SteamVR_WebKit.OverlayManager.SetOverlayMouseScale(_handle, ref scale);
        }

        public void SetThumbnail(string filePath)
        {
            SetThumbnailPath(System.IO.Path.IsPathRooted(filePath) ? filePath : Environment.CurrentDirectory + "\\" + filePath);
        }

        void UpdateWidth()
        {
            SteamVR_WebKit.OverlayManager.SetOverlayWidthInMeters(_handle, _width);
        }

        void UpdateAlpha()
        {
            SteamVR_WebKit.OverlayManager.SetOverlayAlpha(_handle, _alpha);
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

        public void Hide()
        {
            SteamVR_WebKit.OverlayManager.HideOverlay(_handle);
        }

        public void ForceShow()
        {
            SteamVR_WebKit.OverlayManager.ShowDashboard(_key);
        }

        public void Destroy()
        {
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_handle);

            if(_thumbnailHandle > 0)
                SteamVR_WebKit.OverlayManager.DestroyOverlay(_thumbnailHandle);
        }

        public bool IsVisible()
        {
            return SteamVR_WebKit.OverlayManager.IsOverlayVisible(_handle);
        }

        public bool PollEvent(ref VREvent_t ovrEvent)
        {
            return SteamVR_WebKit.OverlayManager.PollNextOverlayEvent(_handle, ref ovrEvent, eventSize);
        }
    }
}
