using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Valve.VR;

namespace SteamVR_WebKit
{
    public class TransformUtils
    {
        // Borrowed from https://github.com/Marlamin/VROverlayTest/blob/master/VROverlayTest/Program.cs
        public static Matrix3x4 OpenVRMatrixToOpenTKMatrix(HmdMatrix34_t matrix)
        {
            var newmatrix = new Matrix3x4();

            newmatrix.M11 = matrix.m0;
            newmatrix.M12 = matrix.m1;
            newmatrix.M13 = matrix.m2;
            newmatrix.M14 = matrix.m3;

            newmatrix.M21 = matrix.m4;
            newmatrix.M22 = matrix.m5;
            newmatrix.M23 = matrix.m6;
            newmatrix.M24 = matrix.m7;

            newmatrix.M31 = matrix.m8;
            newmatrix.M32 = matrix.m9;
            newmatrix.M33 = matrix.m10;
            newmatrix.M34 = matrix.m11;

            return newmatrix;
        }

        // Also borrowed from https://github.com/Marlamin/VROverlayTest/blob/master/VROverlayTest/Program.cs
        public static HmdMatrix34_t OpenTKMatrixToOpenVRMatrix(Matrix3x4 matrix)
        {
            var newmatrix = new HmdMatrix34_t();

            newmatrix.m0 = matrix.M11;
            newmatrix.m1 = matrix.M12;
            newmatrix.m2 = matrix.M13;
            newmatrix.m3 = matrix.M14;

            newmatrix.m4 = matrix.M21;
            newmatrix.m5 = matrix.M22;
            newmatrix.m6 = matrix.M23;
            newmatrix.m7 = matrix.M24;

            newmatrix.m8 = matrix.M31;
            newmatrix.m9 = matrix.M32;
            newmatrix.m10 = matrix.M33;
            newmatrix.m11 = matrix.M34;

            return newmatrix;
        }
    }
}
