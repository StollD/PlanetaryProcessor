using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace PlanetaryProcessor
{
    public class ApplicationBuilder
    {
        /// <summary>
        /// All files that are custom to the PlanetaryProcessor Application 
        /// </summary>
        private static readonly String[] _files =
        {
            "app.info",
            "boot.config",
            "globalgamemanagers",
            "globalgamemanagers.assets",
            "level0",
            "level0.resS",
            "sharedassets0.assets",
            "Managed/Mono.Posix.dll",
            "Managed/Mono.Security.dll",
            "Managed/mscorlib.dll",
            "Managed/PlanetaryProcessor.Unity.dll",
            "Managed/System.Core.dll",
            "Managed/System.dll",
            "Managed/System.Xml.dll",
            "Managed/UnityEngine.dll",
            "Managed/UnityEngine.Networking.dll",
            "Managed/UnityEngine.Timeline.dll",
            "Managed/UnityEngine.UI.dll",
            "Managed/Kopernicus.dll",
            "Managed/Kopernicus.Components.dll",
            "Managed/Kopernicus.OnDemand.dll",
            "Managed/Kopernicus.Parser.dll"
        };
        
        /// <summary>
        /// Builds the unity application for the processor
        /// </summary>
        /// <param name="path">The path to the processor app</param>
        /// <param name="kspPath">The path to the KSP installation</param>
        public static async Task BuildApplication(String path, String kspPath)
        {
            // Extract the required files
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                await BuildApplicationWindows(path, kspPath);
            }

            // Linux has an universal build, so we don't need special versions for x86 and x64
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                await BuildApplicationLinux(path, kspPath);
            }
        }

        /// <summary>
        /// Builds the application on windows
        /// </summary>
        /// <param name="path">The path to the PlanetaryProcessor app</param>
        /// <param name="kspPath">The path to KSP</param>
        private static async Task BuildApplicationWindows(String path, String kspPath)
        {
            Assembly assembly = typeof(ApplicationBuilder).Assembly;

            // Build both bitsizes
            foreach (String bitSize in new[] {"x86", "x64"})
            {
                String bitPath = Utility.Combine(path, bitSize);

                // Extract all custom files
                foreach (String file in _files)
                {
                    // Build the resource path
                    String resource = "PlanetaryProcessor.Resources." + Environment.OSVersion.Platform + "." +
                                      bitSize + file.Replace("/", ".");

                    // Build the file path
                    String filePath = Utility.Combine(bitPath, "PlanetaryProcessor.App_Data", file) ?? "";
                    String directoryPath = Path.GetDirectoryName(filePath);
                    if (String.IsNullOrEmpty(directoryPath))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(directoryPath);

                    // Grab the file and save it to disk
                    using (Stream resourceStream = assembly.GetManifestResourceStream(resource))
                    {
                        // Is the stream null?
                        if (resourceStream == null)
                        {
                            throw new NullReferenceException();
                        }

                        using (FileStream fileStream = File.OpenWrite(filePath))
                        {
                            await resourceStream.CopyToAsync(fileStream);
                            await fileStream.FlushAsync();
                        }
                    }
                }

                // Copy neccessary files from KSP

                // Unity Player
                String programName = bitSize == "x86" ? "KSP" : "KSP_x64";
                File.Copy(Utility.Combine(kspPath, programName + ".exe"),
                    Utility.Combine(bitPath, "PlanetaryProcessor.App.exe"));

                // Mono Runtime
                foreach (String f in Directory.GetFiles(Utility.Combine(kspPath, programName + "_Data", "Mono"),
                    "*", SearchOption.AllDirectories))
                {
                    String destination = f.Replace(Utility.Combine(kspPath, programName + "_Data"),
                        Utility.Combine(bitPath, "PlanetaryProcessor.App_Data"));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(f, destination);
                }

                // Weird default files
                foreach (String f in Directory.GetFiles(
                    Utility.Combine(kspPath, programName + "_Data", "Resources"),
                    "*", SearchOption.AllDirectories))
                {
                    String destination = f.Replace(Utility.Combine(kspPath, programName + "_Data"),
                        Utility.Combine(bitPath, "PlanetaryProcessor.App_Data"));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(f, destination);
                }

                // Copy dlls
                String managedKSP = Utility.Combine(kspPath, programName + "_Data", "Managed");
                String managedPP = Utility.Combine(bitPath, "PlanetaryProcessor.App_Data", "Managed");

                CopyDll(managedKSP, managedPP, "Assembly-CSharp.dll");
                CopyDll(managedKSP, managedPP, "Assembly-CSharp-firstpass.dll");
                CopyDll(managedKSP, managedPP, "Ionic.Zip.dll");
                CopyDll(managedKSP, managedPP, "KSPAssets.dll");
                CopyDll(managedKSP, managedPP, "KSPTrackIR.dll");
                CopyDll(managedKSP, managedPP, "Mono.Cecil.dll");
                CopyDll(managedKSP, managedPP, "RedShellSDK.dll"); // Why
            }
        }


        /// <summary>
        /// Builds the application on linux
        /// </summary>
        /// <param name="path">The path to the PlanetaryProcessor app</param>
        /// <param name="kspPath">The path to KSP</param>
        private static async Task BuildApplicationLinux(String path, String kspPath)
        {
            Assembly assembly = typeof(ApplicationBuilder).Assembly;

            // Extract all custom files
            foreach (String file in _files)
            {
                // Build the resource path
                String resource = "PlanetaryProcessor.Resources." + Environment.OSVersion.Platform + "." +
                                  file.Replace("/", ".");

                // Build the file path
                String filePath = Utility.Combine(path, "PlanetaryProcessor.App_Data", file) ?? "";
                String directoryPath = Path.GetDirectoryName(filePath);
                if (String.IsNullOrEmpty(directoryPath))
                {
                    continue;
                }

                Directory.CreateDirectory(directoryPath);

                // Grab the file and save it to disk
                using (Stream resourceStream = assembly.GetManifestResourceStream(resource))
                {
                    // Is the stream null?
                    if (resourceStream == null)
                    {
                        throw new NullReferenceException();
                    }

                    using (FileStream fileStream = File.OpenWrite(filePath))
                    {
                        await resourceStream.CopyToAsync(fileStream);
                        await fileStream.FlushAsync();
                    }
                }
            }
            
            // Copy neccessary files from KSP

            // Unity Player
            File.Copy(Utility.Combine(kspPath, "KSP.x86"),
                Utility.Combine(path, "PlanetaryProcessor.App.x86"));
            File.Copy(Utility.Combine(kspPath, "KSP.x86_64"),
                Utility.Combine(path, "PlanetaryProcessor.App.x86_64"));

            // Mono Runtime
            foreach (String f in Directory.GetFiles(
                Utility.Combine(kspPath, "KSP_Data", "Mono"),
                "*", SearchOption.AllDirectories))
            {
                String destination = f.Replace(Utility.Combine(kspPath, "KSP_Data"),
                    Utility.Combine(path, "PlanetaryProcessor.App_Data"));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(f, destination);
            }

            // Weird default files
            foreach (String f in Directory.GetFiles(
                Utility.Combine(kspPath, "KSP_Data", "Resources"),
                "*", SearchOption.AllDirectories))
            {
                String destination = f.Replace(Utility.Combine(kspPath, "KSP_Data"),
                    Utility.Combine(path, "PlanetaryProcessor.App_Data"));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(f, destination);
            }

            // Copy dlls
            String managedKSP = Utility.Combine(kspPath, "KSP_Data", "Managed");
            String managedPP = Utility.Combine(path, "PlanetaryProcessor.App_Data", "Managed");

            CopyDll(managedKSP, managedPP, "Assembly-CSharp.dll");
            CopyDll(managedKSP, managedPP, "Assembly-CSharp-firstpass.dll");
            CopyDll(managedKSP, managedPP, "Ionic.Zip.dll");
            CopyDll(managedKSP, managedPP, "KSPAssets.dll");
            CopyDll(managedKSP, managedPP, "KSPTrackIR.dll");
            CopyDll(managedKSP, managedPP, "Mono.Cecil.dll");
            CopyDll(managedKSP, managedPP, "RedShellSDK.dll"); // Why
        }

        /// <summary>
        /// Copys a dll from KSP into PP
        /// </summary>
        private static void CopyDll(String kspManaged, String ppManaged, String dll)
        {
            File.Copy(Utility.Combine(kspManaged, dll), Utility.Combine(ppManaged, dll));
        }
    }
}