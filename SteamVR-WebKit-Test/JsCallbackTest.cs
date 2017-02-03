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
            Console.WriteLine("Called a test from JS!");
        }

        public bool TestMoreStuff(string str)
        {
            Console.WriteLine(str);
            return false;
        }
    }
}
