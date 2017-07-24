using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Valve.VR
{
    public partial class OpenVR
    {
        public partial class COpenVRContext
        {
            public CVRNotifications VRNotifications()
            {
                CheckClear();
                if (m_pVRNotifications == null)
                {
                    var eError = EVRInitError.None;
                    var pInterface = OpenVRInterop.GetGenericInterface(FnTable_Prefix + IVRNotifications_Version, ref eError);
                    if (pInterface != IntPtr.Zero && eError == EVRInitError.None)
                        m_pVRNotifications = new CVRNotifications(pInterface);
                }

                return m_pVRNotifications;
            }

            private CVRNotifications m_pVRNotifications;
        }

        public static CVRNotifications Notifications { get { return OpenVRInternal_ModuleContext.VRNotifications(); } }
    }
}