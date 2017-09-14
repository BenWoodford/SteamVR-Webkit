using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SteamVR_WebKit
{
    public class KeyboardUtils
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short VkKeyScan(char ch);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short VkKeyScanEx(char ch);

        public static KeyEvent ConvertCharToVirtualKeyEvent(byte character)
        {
            char actualChar = Encoding.UTF8.GetString(new byte[] { character }, 0, 1).ToCharArray()[0];

            KeyEvent retVal = new KeyEvent();
            if (actualChar == '\b')
            {
                retVal.WindowsKeyCode = (int)Keys.Back;
                retVal.Type = KeyEventType.KeyDown;
                retVal.IsSystemKey = true;
                return retVal;
            } else if(actualChar == '\n')
            {
                retVal.WindowsKeyCode = (int)Keys.Return;
                retVal.Type = KeyEventType.Char;
                return retVal;
            }
            
            short vkey = VkKeyScan(actualChar);

            retVal.WindowsKeyCode = character;
            retVal.Type = KeyEventType.Char;

            int modifiers = vkey >> 8;
            if ((modifiers & 1) != 0) retVal.Modifiers |= CefEventFlags.ShiftDown;
            if ((modifiers & 2) != 0) retVal.Modifiers |= CefEventFlags.ControlDown;
            if ((modifiers & 4) != 0) retVal.Modifiers |= CefEventFlags.AltDown;

            return retVal;
        }
    }
}
