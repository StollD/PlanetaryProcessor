using System;
using System.IO;
using System.Linq;

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
    }
}