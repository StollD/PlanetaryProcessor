using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace PlanetaryProcessor
{
    public static class Utility
    {
        /// <summary>
        /// Checks if the supplied directory is a valid KSP installation
        /// </summary>
        public static Boolean IsKspDirectory(String dir)
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                return Directory.Exists(Path.Combine(dir, "KSP_Data")) ||
                       Directory.Exists(Path.Combine(dir, "KSP_x64_Data"));
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                return Directory.Exists(Path.Combine(dir, "KSP_Data"));
            }

            return false;
        }
        
        /// <summary>
        /// Gets the next free port on the computer
        /// https://stackoverflow.com/questions/138043/find-the-next-tcp-port-in-net
        /// </summary>
        public static Int32 GetAvailablePort(Int32 startingPort)
        {
            List<Int32> portArray = new List<Int32>();

            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

            // Ignore active connections
            TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
            portArray.AddRange(connections.Where(n => n.LocalEndPoint.Port >= startingPort)
                .Select(n => n.LocalEndPoint.Port));

            // Ignore active tcp listners
            IPEndPoint[] endPoints = properties.GetActiveTcpListeners();
            portArray.AddRange(endPoints.Where(n => n.Port >= startingPort).Select(n => n.Port));

            // Ignore active udp listeners
            endPoints = properties.GetActiveUdpListeners();
            portArray.AddRange(endPoints.Where(n => n.Port >= startingPort).Select(n => n.Port));

            portArray.Sort();

            for (Int32 i = startingPort; i < UInt16.MaxValue; i++)
            {
                if (!portArray.Contains(i))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}