using OpenTK;
using System;
using System.Collections;
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

        const float rad2deg = (float)(180 / Math.PI);
        const float deg2rad = (float)(1.0 / rad2deg);

        AttachmentType _attachmentType = AttachmentType.Absolute;
        string _attachedTo = null;
        ulong _attachedToHandle = 0;
        Vector3 _position = Vector3.Zero;
        Vector3 _rotation = Vector3.Zero;

        public bool AttachmentSuccess { get { return _sentAttachmentSuccess; } }

        bool _controllerListenersSetup = false;
        bool _sentAttachmentSuccess = false;
        public Action OnAttachmentSuccess;

        bool _ingame = false;
        uint eventSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VREvent_t));
        bool _hasBackSide = false;

        ulong _backSideHandle;

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

        public ulong BackSideHandle
        {
            get { return _backSideHandle; }
        }

        public ulong ThumbnailHandle
        {
            get { return _thumbnailHandle; }
        }

        public ulong AttachmentHandle
        {
            get { return _attachedToHandle; }
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

        public bool EnableScrolling { get; set; } = true;
        
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

        public Overlay(string key, string name, float width = 2.0f, bool isInGameOverlay = false, bool hasBackSide = false)
        {
            _key = key;
            _name = name;
            _ingame = isInGameOverlay;

            if (!isInGameOverlay)
                hasBackSide = false;

            _hasBackSide = hasBackSide;

            CreateOverlayInSteamVR();
            Width = width;
            Alpha = 1.0f;

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_handle);
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_thumbnailHandle);

            if(_hasBackSide)
            {
                SteamVR_WebKit.OverlayManager.DestroyOverlay(_backSideHandle);
            }
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

        public void Rotate(double x, double y, double z)
        {
            _rotation = new Vector3((float)x, (float)y, (float)z);
            SetAttachment(_attachmentType, _position, _rotation, null);
        }

        public void SetRotation(double x, double y, double z)
        {
            _rotation = new Vector3((float)x, (float)y, (float)z);
            SetAttachment(_attachmentType, _position, _rotation, null);
        }

        public void MoveBy(double x, double y, double z)
        {
            _position += new Vector3((float)x, (float)y, (float)z);
            SetAttachment(_attachmentType, _position, _rotation, null);
        }

        public void MoveAbsolute(double x, double y, double z)
        {
            _position = new Vector3((float)x, (float)y, (float)z);
            SetAttachment(_attachmentType, _position, _rotation, null);
        }

        public Matrix3x4 GetAttachmentTransform()
        {
            Valve.VR.HmdMatrix34_t outMatrix = default(Valve.VR.HmdMatrix34_t);
            SteamVR_WebKit.OverlayManager.GetOverlayTransformOverlayRelative(Handle, ref _attachedToHandle, ref outMatrix);

            return TransformUtils.OpenVRMatrixToOpenTKMatrix(outMatrix);
        }

        public void SetAttachment(AttachmentType attachmentType, Vector3 position, Vector3 rotation, string attachmentKey = null)
        {
            if (!_ingame)
                throw new Exception("Cannot set attachment for dashboard overlay");

            _attachmentType = attachmentType;

            _position = position;
            _rotation = rotation;

            if (attachmentType == AttachmentType.Absolute)
            {
                HmdMatrix34_t matrix = GetMatrixFromPositionAndRotation(position, rotation);
                SteamVR_WebKit.OverlayManager.SetOverlayTransformAbsolute(_handle, ETrackingUniverseOrigin.TrackingUniverseStanding, ref matrix);
                _sentAttachmentSuccess = true;
            }
            else if (attachmentType == AttachmentType.Hmd)
            {
                SetDeviceAttachment((uint)0, position, rotation);
                _sentAttachmentSuccess = true;
            } else if(attachmentType == AttachmentType.Overlay)
            {
                ulong attachmentHandle = 0;

                if (attachmentType == AttachmentType.Overlay && attachmentKey == null)
                    attachmentKey = _attachedTo;

                if(_attachedTo != attachmentKey)
                {
                    SteamVR_WebKit.OverlayManager.FindOverlay(attachmentKey, ref attachmentHandle);
                    _attachedToHandle = attachmentHandle;
                }

                if (_attachedToHandle != 0)
                {
                    HmdMatrix34_t matrix = GetMatrixFromPositionAndRotation(position, rotation);
                    EVROverlayError err = SteamVR_WebKit.OverlayManager.SetOverlayTransformOverlayRelative(_handle, _attachedToHandle, ref matrix);

                    if (err != EVROverlayError.None)
                        SteamVR_WebKit.Log("Failed to attach " + Key + " to Overlay " + attachmentKey + " failed: " + err.ToString());

                    _sentAttachmentSuccess = true;
                } else
                {
                    SteamVR_WebKit.Log("Attempted to attach to " + attachmentKey + " but it could not be found.");
                }
            }
            else
            {
                SetDeviceAttachment(SteamVR_WebKit.OVRSystem.GetTrackedDeviceIndexForControllerRole(attachmentType == AttachmentType.LeftController ? ETrackedControllerRole.LeftHand : ETrackedControllerRole.RightHand), position, rotation);
                if (!_controllerListenersSetup)
                {
                    SteamVR_Event.Listen("TrackedDeviceRoleChanged", HandleDeviceRoleChanged);
                    SteamVR_Event.Listen("device_connected", HandleDeviceConnected);
                    _controllerListenersSetup = true;
                } else
                {
                    _sentAttachmentSuccess = true;
                }
            }
        }

        HmdMatrix34_t GetMatrixFromPositionAndRotation(Vector3 position, Vector3 rotation)
        {
            Matrix3x4 translationMatrix = Matrix3x4.CreateTranslation(position);
            Matrix3x4 rotationMatrix = Matrix3x4.CreateFromQuaternion(Quaternion.FromEulerAngles(rotation * deg2rad));

            return TransformUtils.OpenTKMatrixToOpenVRMatrix(translationMatrix * rotationMatrix);
        }

        [Obsolete("Use SetAttachment instead")]
        public void SetDeviceAttachment(AttachmentType attachmentType, Vector3 position, Vector3 rotation)
        {
            SetAttachment(attachmentType, position, rotation);
        }

        public void SetDeviceAttachment(uint index, Vector3 position, Vector3 rotation)
        {
            if (!_ingame)
                throw new Exception("Cannot set attachment for dashboard overlay");

            _position = position;
            _rotation = rotation;

            HmdMatrix34_t matrix = GetMatrixFromPositionAndRotation(position, rotation);

            EVROverlayError err = SteamVR_WebKit.OverlayManager.SetOverlayTransformTrackedDeviceRelative(_handle, index, ref matrix);

            if (err != EVROverlayError.None)
                SteamVR_WebKit.Log("Failed to attach " + Key + " to Device " + index + " failed: " + err.ToString());
        }

        private void HandleDeviceRoleChanged(params object[] args)
        {
            RefreshDevice();
        }

        void RefreshDevice()
        {
            uint changedDeviceIndex = SteamVR_WebKit.OVRSystem.GetTrackedDeviceIndexForControllerRole(_attachmentType == AttachmentType.LeftController ? ETrackedControllerRole.LeftHand : ETrackedControllerRole.RightHand);
            if (changedDeviceIndex != OpenVR.k_unTrackedDeviceIndexInvalid)
            {
                SetDeviceAttachment(changedDeviceIndex, _position, _rotation);
                if (!_sentAttachmentSuccess)
                {
                    OnAttachmentSuccess?.Invoke();
                    _sentAttachmentSuccess = true;
                }
            }
        }

        private void HandleDeviceConnected(params object[] args)
        {
            uint index = 0;

            if (args.Length > 0)
            {
                if (args[0] is VREvent_t)
                    index = ((VREvent_t)args[0]).trackedDeviceIndex;
                else
                    index = (uint)args[0];
            } else
            {
                return;
            }

            var system = OpenVR.System;
            if (system != null && system.GetTrackedDeviceClass(index) == ETrackedDeviceClass.Controller)
            {
                RefreshDevice();
            }
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
                ToggleInput(false);
            }
            else
            {
                ovrErr = SteamVR_WebKit.OverlayManager.CreateDashboardOverlay(Key, Name, ref _handle, ref _thumbnailHandle);
                ToggleInput(true);
            }

            SteamVR_WebKit.Log("Overlay Handle " + _handle + ", Thumbnail Handle: " + _thumbnailHandle);

            if (ovrErr != EVROverlayError.None)
            {
                throw new Exception("Failed to create overlay: " + ovrErr.ToString());
            }

            SteamVR_WebKit.OverlayManager.SetOverlayColor(_handle, 1.0f, 1.0f, 1.0f);

            if(_hasBackSide)
            {
                ovrErr = SteamVR_WebKit.OverlayManager.CreateOverlay(Key + "_backside", Name, ref _backSideHandle);

                if(ovrErr != EVROverlayError.None)
                {
                    throw new Exception("Failed to create backside of overlay: " + ovrErr.ToString());
                }
                SteamVR_WebKit.OverlayManager.SetOverlayColor(_backSideHandle, 1.0f, 1.0f, 1.0f);

                HmdMatrix34_t matrix = GetMatrixFromPositionAndRotation(new Vector3(0,0,0), new Vector3(0,180,0));
                SteamVR_WebKit.OverlayManager.SetOverlayTransformOverlayRelative(_backSideHandle, _handle, ref matrix);
            }

            Alpha = 1.0f;


            // Because it'll be upside down otherwise.
            VRTextureBounds_t bounds;
            bounds.vMax = 0; bounds.vMin = 1;  // Flip the Y

            // Leave as defaults
            bounds.uMin = 0;
            bounds.uMax = 1;

            SteamVR_WebKit.OverlayManager.SetOverlayTextureBounds(_handle, ref bounds);

            if (_hasBackSide)
            {
                bounds.uMin = 1; bounds.uMax = 0; // Flip the backside texture
                SteamVR_WebKit.OverlayManager.SetOverlayTextureBounds(_backSideHandle, ref bounds);
            }
        }

        public void ToggleInput(bool toggle)
        {
            if (toggle)
            {
                SteamVR_WebKit.OverlayManager.SetOverlayInputMethod(_handle, VROverlayInputMethod.Mouse);
                SteamVR_WebKit.OverlayManager.SetOverlayFlag(_handle, VROverlayFlags.ShowTouchPadScrollWheel, EnableScrolling);
                SteamVR_WebKit.OverlayManager.SetOverlayFlag(_handle, VROverlayFlags.SendVRScrollEvents, EnableScrolling);

                if(_hasBackSide)
                {
                    SteamVR_WebKit.OverlayManager.SetOverlayInputMethod(_backSideHandle, VROverlayInputMethod.Mouse);
                    SteamVR_WebKit.OverlayManager.SetOverlayFlag(_backSideHandle, VROverlayFlags.ShowTouchPadScrollWheel, EnableScrolling);
                    SteamVR_WebKit.OverlayManager.SetOverlayFlag(_backSideHandle, VROverlayFlags.SendVRScrollEvents, EnableScrolling);
                }
            }
            else
            {
                SteamVR_WebKit.OverlayManager.SetOverlayInputMethod(_handle, VROverlayInputMethod.None);
                SteamVR_WebKit.OverlayManager.SetOverlayFlag(_handle, VROverlayFlags.ShowTouchPadScrollWheel, false);
                SteamVR_WebKit.OverlayManager.SetOverlayFlag(_handle, VROverlayFlags.SendVRScrollEvents, false);

                if (_hasBackSide)
                {
                    SteamVR_WebKit.OverlayManager.SetOverlayInputMethod(_backSideHandle, VROverlayInputMethod.None);
                    SteamVR_WebKit.OverlayManager.SetOverlayFlag(_backSideHandle, VROverlayFlags.ShowTouchPadScrollWheel, false);
                    SteamVR_WebKit.OverlayManager.SetOverlayFlag(_backSideHandle, VROverlayFlags.SendVRScrollEvents, false);
                }
            }
        }

        public void SetTextureSize(float width, float height)
        {
            HmdVector2_t scale;
            scale.v0 = width;
            scale.v1 = height;
            SteamVR_WebKit.OverlayManager.SetOverlayMouseScale(_handle, ref scale);

            if(_hasBackSide)
                SteamVR_WebKit.OverlayManager.SetOverlayMouseScale(_backSideHandle, ref scale);
        }

        public void SetThumbnail(string filePath)
        {
            SetThumbnailPath(System.IO.Path.IsPathRooted(filePath) ? filePath : Environment.CurrentDirectory + "\\" + filePath);
        }

        void UpdateWidth()
        {
            SteamVR_WebKit.OverlayManager.SetOverlayWidthInMeters(_handle, _width);

            if(_hasBackSide)
                SteamVR_WebKit.OverlayManager.SetOverlayWidthInMeters(_backSideHandle, _width);
        }

        void UpdateAlpha()
        {
            SteamVR_WebKit.OverlayManager.SetOverlayAlpha(_handle, _alpha);

            if(_hasBackSide)
            {
                SteamVR_WebKit.OverlayManager.SetOverlayAlpha(_backSideHandle, _alpha);
            }
        }

        public void SetTexture(ref Texture_t texture)
        {
            EVROverlayError err = SteamVR_WebKit.OverlayManager.SetOverlayTexture(_handle, ref texture);

            if (err != EVROverlayError.None)
                SteamVR_WebKit.Log("Failed to send texture: " + err.ToString());

            if (_hasBackSide)
            {
                err = SteamVR_WebKit.OverlayManager.SetOverlayTexture(_backSideHandle, ref texture);

                if (err != EVROverlayError.None)
                    SteamVR_WebKit.Log("Failed to send texture: " + err.ToString());
            }
        }

        public void Show()
        {
            SteamVR_WebKit.OverlayManager.ShowOverlay(_handle);

            if(_hasBackSide)
            {
                SteamVR_WebKit.OverlayManager.ShowOverlay(_backSideHandle);
            }
        }

        public void Hide()
        {
            SteamVR_WebKit.OverlayManager.HideOverlay(_handle);

            if (_hasBackSide)
            {
                SteamVR_WebKit.OverlayManager.HideOverlay(_backSideHandle);
            }
        }

        public void ForceShow()
        {
            SteamVR_WebKit.OverlayManager.ShowDashboard(_key);
        }

        public void Destroy()
        {
            SteamVR_WebKit.OverlayManager.DestroyOverlay(_handle);

            if(_hasBackSide)
            {
                SteamVR_WebKit.OverlayManager.DestroyOverlay(_backSideHandle);
            }

            if(_thumbnailHandle > 0)
                SteamVR_WebKit.OverlayManager.DestroyOverlay(_thumbnailHandle);

            if (_controllerListenersSetup)
            {
                SteamVR_Event.Listen("TrackedDeviceRoleChanged", HandleDeviceConnected);
                SteamVR_Event.Listen("device_connected", HandleDeviceConnected);
            }
        }

        public bool IsVisible()
        {
            return SteamVR_WebKit.OverlayManager.IsOverlayVisible(_handle);
        }

        public bool PollEvent(ref VREvent_t ovrEvent)
        {
            return SteamVR_WebKit.OverlayManager.PollNextOverlayEvent(_handle, ref ovrEvent, eventSize) || SteamVR_WebKit.OverlayManager.PollNextOverlayEvent(_backSideHandle, ref ovrEvent, eventSize);
        }
    }
}
