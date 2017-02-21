using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using System.Drawing.Imaging;
using System.Timers;

namespace SteamVR_WebKit.JsInterop
{
    /// <summary>
    /// Class to abstract out and pass through SteamVR Notification Support to Javascript
    /// </summary>
    public class Notifications
    {
        struct NotificationIcon
        {
            public Bitmap Bitmap;
            public int glTextureId;
        }

        private Overlay _overlay;
        private static Dictionary<string, NotificationIcon> _icons = new Dictionary<string, NotificationIcon>();

        public Notifications(Overlay overlay)
        {
            _overlay = overlay;
        }

        public bool SendNotification(string text, string bitmapKey = null, int displayForMs = 2000)
        {
            NotificationBitmap_t bmp = new NotificationBitmap_t();
            if (bitmapKey != null)
            {
                if (!_icons.ContainsKey(bitmapKey))
                    throw new Exception("Invalid Bitmap Key");

                bmp.m_nBytesPerPixel = 4;
                bmp.m_nHeight = _icons[bitmapKey].Bitmap.Height;
                bmp.m_nWidth = _icons[bitmapKey].Bitmap.Width;
                bmp.m_pImageData = (IntPtr)(_icons[bitmapKey].glTextureId);
            }

            uint notId = 0;
            EVRNotificationError err = EVRNotificationError.OK;
            err = OpenVR.Notifications.CreateNotification(_overlay.Handle, 0, EVRNotificationType.Transient, text, EVRNotificationStyle.Application, ref bmp, ref notId);
            
            if(err != EVRNotificationError.OK)
            {
                Console.WriteLine("Notification Failure: " + err.ToString());
                return false;
            }
            return true;
        }

        public static void RegisterIcon(string key, Bitmap bitmap)
        {
            NotificationIcon newIcon;
            newIcon.Bitmap = bitmap;
            newIcon.glTextureId = GL.GenTexture();

            _icons.Add(key, newIcon);

            BitmapData bmpData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb
            );

            GL.BindTexture(TextureTarget.Texture2D, newIcon.glTextureId);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bitmap.Width, bitmap.Height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmpData.Scan0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            bitmap.UnlockBits(bmpData);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
    }
}
