using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamVR_WebKit_Test
{
    class JsCallbackTest
    {
        public string TestString = "This is a test.";
        public void TestStuff()
        {
            SteamVR_WebKit.SteamVR_WebKit.Log("Called a test from JS!");
        }

        public bool TestMoreStuff(string str)
        {
            SteamVR_WebKit.SteamVR_WebKit.Log(str);
            return false;
        }
    }
}
