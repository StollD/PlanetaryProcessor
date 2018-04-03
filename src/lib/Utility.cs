using System;
using System.IO;
using System.Linq;

namespace PlanetaryProcessor
{
    public static class Utility
    {
        /// <summary>
        /// Path.Combine with params support
        /// </summary>
        public static String Combine(params String[] paths)
        {
            if (!paths.Any())
            {
                return null;
            }
            
            String path = paths[0];
            for (Int32 i = 1; i < paths.Length; i++)
            {
                path = Path.Combine(path, paths[i]);
            }

            return path;
        }

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