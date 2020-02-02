using System;
using System.Runtime.InteropServices;


namespace CommonUtils
{
    public static class NetworkHelper
    {
        const int ConnectInteractive = 0x00000008;
        const int ConnectPrompt = 0x00000010;
        const int ConnectUpdateProfile = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private class Netresource
        {
            public int dwScope = 0;
            public int dwType;
            public int dwDisplayType = 0;
            public int dwUsage = 0;
            public string lpLocalName = "";
            public string lpRemoteName = "";
            public string lpComment = "";
            public string lpProvider = "";
        }

        [DllImport("Mpr.dll")]
        private static extern int WNetUseConnection(
            IntPtr hwndOwner,
            Netresource lpNetResource,
            string lpPassword,
            string lpUserId,
            int dwFlags,
            string lpAccessName,
            string lpBufferSize,
            string lpResult
        );

        [DllImport("Mpr.dll")]
        private static extern int WNetCancelConnection2(
            string lpName,
            int dwFlags,
            bool fForce
        );

        public static int ConnectRemote(string remoteUnc, string username, string password, bool promptUser)
        {
            var nr = new Netresource
            {
                dwType = 0x00000001,
                lpRemoteName = remoteUnc
            };

            if (promptUser)
                return WNetUseConnection(IntPtr.Zero, nr, "", "", ConnectInteractive | ConnectPrompt, null, null, null);
            return WNetUseConnection(IntPtr.Zero, nr, password, username, 0, null, null, null);
        }

        public static bool CanConnect(int returnCode)
        {
            return returnCode == 0 || returnCode == 1219;
        }

        public static int DisconnectRemote(string remoteUnc)
        {
            return WNetCancelConnection2(remoteUnc, ConnectUpdateProfile, false);
        }
    }
}
