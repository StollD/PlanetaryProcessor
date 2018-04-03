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
                String bitPath = Path.Combine(path, bitSize);

                // Extract all custom files
                foreach (String file in _files)
                {
                    // Build the resource path
                    String resource = "PlanetaryProcessor.Resources." + Environment.OSVersion.Platform + "." +
                                      bitSize + file.Replace("/", ".");

                    // Build the file path
                    String filePath = Path.Combine(bitPath, "PlanetaryProcessor.App_Data", file) ?? "";
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
                File.Copy(Path.Combine(kspPath, programName + ".exe"), Path.Combine(bitPath, "PlanetaryProcessor.App.exe"));

                // Mono Runtime
                CopyDir(Path.Combine(kspPath, programName + "_Data"), Path.Combine(bitPath, "PlanetaryProcessor.App_Data"), "Mono");

                // Weird default files
                CopyDir(Path.Combine(kspPath, programName + "_Data"), Path.Combine(bitPath, "PlanetaryProcessor.App_Data"), "Resources");

                // Copy dlls
                String managedKSP = Path.Combine(kspPath, programName + "_Data", "Managed");
                String managedPP = Path.Combine(bitPath, "PlanetaryProcessor.App_Data", "Managed");

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
                String filePath = Path.Combine(path, "PlanetaryProcessor.App_Data", file) ?? "";
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
            File.Copy(Path.Combine(kspPath, "KSP.x86"), Path.Combine(path, "PlanetaryProcessor.App.x86"));
            File.Copy(Path.Combine(kspPath, "KSP.x86_64"), Path.Combine(path, "PlanetaryProcessor.App.x86_64"));

            // Mono Runtime
            CopyDir(Path.Combine(kspPath, "KSP_Data"), Path.Combine(path, "PlanetaryProcessor.App_Data"), "Mono");

            // Weird default files
            CopyDir(Path.Combine(kspPath, "KSP_Data"), Path.Combine(path, "PlanetaryProcessor.App_Data"), "Resources");

            // Copy dlls
            String managedKSP = Path.Combine(kspPath, "KSP_Data", "Managed");
            String managedPP = Path.Combine(path, "PlanetaryProcessor.App_Data", "Managed");

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
            File.Copy(Path.Combine(kspManaged, dll), Path.Combine(ppManaged, dll));
        }

        /// <summary>
        /// Copys all files in a directory
        /// </summary>
        private static void CopyDir(String kspData, String ppData, String folder)
        {
            foreach (String f in Directory.GetFiles(Path.Combine(kspData, folder), "*", SearchOption.AllDirectories))
            {
                String destination = f.Replace(kspData, ppData);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(f, destination);
            }
        }
    }
}