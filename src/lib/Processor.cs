using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PlanetaryProcessor
{
    public class Processor : IDisposable
    {
        /// <summary>
        /// The version of the processor
        /// </summary>
        public const String Version = "1.4.3-2";

        /// <summary>
        /// The Kopernicus version that was bundled with the processor
        /// </summary>
        public const String KopernicusVersion = "1.4.3-1";

        /// <summary>
        /// The proces
        /// </summary>
        private Process _process;

        /// <summary>
        /// The pipe to the unity process
        /// </summary>
        private PipeServer _server;

        /// <summary>
        /// Whether the Processor was disposed
        /// </summary>
        private Boolean _isDisposed;
        
        /// <summary>
        /// Create a new planetary processor
        /// </summary>
        internal Processor()
        {
        }
        
        /// <summary>
        /// Create a new planetary processor
        /// </summary>
        public static async Task<Processor> Create(String kspPath)
        {
            Processor processor = new Processor();

            await Task.Run(async () =>
            {
                // Check if the platform is supported
                if (Environment.OSVersion.Platform != PlatformID.Win32NT &&
                    Environment.OSVersion.Platform != PlatformID.Unix)
                {
                    throw new InvalidOperationException("PlanetaryProcessor is only supported on Windows and Linux.");
                }

                // Check if the supplied path is a valid KSP installation
                if (!Utility.IsKspDirectory(kspPath))
                {
                    throw new ArgumentException("Not a valid KSP installation!", nameof(kspPath));
                }

                // Assemble the Unity Application from the KSP installation
                String path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PlanetaryProcessor", Version);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);

                    // Build the application
                    await ApplicationBuilder.BuildApplication(path, kspPath);
                }

                // Prepare the communication between this and the unity application
                Int32 port = Utility.GetAvailablePort(5000);
                processor._server = new PipeServer(port);

                // Start the Process
                String programName = "";
                String programDirectory = "";
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    programName = "PlanetaryProcessor.App.exe";
                    if (IntPtr.Size == 8)
                    {
                        programDirectory = Path.Combine(path, "x64");
                    }
                    else
                    {
                        programDirectory = Path.Combine(path, "x86");
                    }
                }
                else
                {
                    programDirectory = path;
                    if (IntPtr.Size == 8)
                    {
                        programName = "PlanetaryProcessor.App.x86_64";
                    }
                    else
                    {
                        programName = "PlanetaryProcessor.App.x86";
                    }
                }

                ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(programDirectory, programName),
                    "-nographics -batchmode");
                startInfo.WorkingDirectory = programDirectory;
                startInfo.EnvironmentVariables.Add("LC_ALL", "C");
                startInfo.EnvironmentVariables.Add("PP_PORT", port.ToString());
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                processor._process = Process.Start(startInfo);

                // Wait until the application connects
                await processor._server.WaitForConnection();
                
                // Keep the connection alive
                Task.Run(async () =>
                {
                    while (!processor._isDisposed)
                    {
                        await processor._server.SendMessage("KEEPALIVE", port.ToString());
                        await Task.Delay(5000);
                    }
                });
            });

            return processor;
        }

        /// <summary>
        /// Generates the maps for the supplied PQS configuration and returns them as raw color arrays
        /// </summary>
        public async Task<RawTextureData> GenerateMapsRaw(NodeTree pqsConfig)
        {
            // I am going to use files for the communication, because I am not sure how the pip handles large data
            String configPath = Path.GetTempFileName();
            File.WriteAllText(configPath, pqsConfig.ToString());
            
            // Start a new channel
            String channel = Guid.NewGuid().ToString();
            await _server.SendMessage("GENERATE-MAPS-RAW", channel);
            await _server.SendMessage(channel, configPath);
            
            // Does the application return any errors?
            String error = await _server.ReadMessage(channel + "-ERR");
            if (error != "NONE")
            {
                throw new Exception(error);
            }
            
            // Request the paths to the exported maps
            String color = await _server.ReadMessage(channel);
            String height = await _server.ReadMessage(channel);
            String normal = await _server.ReadMessage(channel);
            String size = await _server.ReadMessage(channel);
            
            RawTextureData data = new RawTextureData();
            Int32.TryParse(size, out Int32 texWidth);
            Int32 texHeight = texWidth / 2;
            
            // Create the color arrays
            data.Color = new Color[texWidth, texHeight];
            data.Height = new Color[texWidth, texHeight];
            data.Normal = new Color[texWidth, texHeight];
            
            // Load the maps
            FileStream colorData = File.OpenRead(color);
            FileStream heightData = File.OpenRead(height);
            FileStream normalData = File.OpenRead(normal);
            
            // Load the colormap
            Byte[] buffer = new Byte[16];
            for (Int32 y = 0; y < texHeight; y++)
            {
                for (Int32 x = 0; x < texWidth; x++)
                {
                    // Color
                    await colorData.ReadAsync(buffer, 0, 16);
                    Color c = new Color();
                    c.r = BitConverter.ToSingle(buffer, 0);
                    c.g = BitConverter.ToSingle(buffer, 4);
                    c.b = BitConverter.ToSingle(buffer, 8);
                    c.a = BitConverter.ToSingle(buffer, 12);
                    data.Color[x, y] = c;
                    
                    // Height
                    await heightData.ReadAsync(buffer, 0, 16);
                    c = new Color();
                    c.r = BitConverter.ToSingle(buffer, 0);
                    c.g = BitConverter.ToSingle(buffer, 4);
                    c.b = BitConverter.ToSingle(buffer, 8);
                    c.a = BitConverter.ToSingle(buffer, 12);
                    data.Height[x, y] = c;
                    
                    // Normal
                    await normalData.ReadAsync(buffer, 0, 16);
                    c = new Color();
                    c.r = BitConverter.ToSingle(buffer, 0);
                    c.g = BitConverter.ToSingle(buffer, 4);
                    c.b = BitConverter.ToSingle(buffer, 8);
                    c.a = BitConverter.ToSingle(buffer, 12);
                    data.Normal[x, y] = c;
                }
            }
            
            // Unload the maps
            colorData.Close(); 
            colorData.Dispose();
            heightData.Close();
            heightData.Dispose();
            normalData.Close();
            normalData.Dispose();

            // Return the maps
            return data;
        }

        /// <summary>
        /// Generates the maps for the supplied PQS configuration and returns them as encoded PNG bytes
        /// </summary>
        public async Task<EncodedTextureData> GenerateMapsEncoded(NodeTree pqsConfig)
        {
            // I am going to use files for the communication, because I am not sure how the pip handles large data
            String configPath = Path.GetTempFileName();
            File.WriteAllText(configPath, pqsConfig.ToString());
            
            // Start a new channel
            String channel = Guid.NewGuid().ToString();
            await _server.SendMessage("GENERATE-MAPS-ENCODED", channel);
            await _server.SendMessage(channel, configPath);
            
            // Does the application return any errors?
            String error = await _server.ReadMessage(channel + "-ERR");
            if (error != "NONE")
            {
                throw new Exception(error);
            }
            
            // Request the paths to the exported maps
            String color = await _server.ReadMessage(channel);
            String height = await _server.ReadMessage(channel);
            String normal = await _server.ReadMessage(channel);
            
            // Load the maps and return them
            return new EncodedTextureData
            {
                Color = new DestructableFileStream(File.OpenRead(color), color),
                Height = new DestructableFileStream(File.OpenRead(height), height),
                Normal = new DestructableFileStream(File.OpenRead(normal), normal)
            };
        }

        /// <summary>
        /// Dispose the processor
        /// </summary>
        public async void Dispose()
        {
            await _server.SendMessage("KILL", "KILL");
            _server.Dispose();
            _isDisposed = true;
        }
        
        /// <summary>
        /// The maps that were exported for a PQS configuration
        /// </summary>
        public struct EncodedTextureData
        {
            public Stream Color;
            public Stream Height;
            public Stream Normal;
        }

        /// <summary>
        /// The maps that were exported for a PQS configuration, but as 2D color arrays
        /// </summary>
        public struct RawTextureData
        {
            public Color[,] Color;
            public Color[,] Height;
            public Color[,] Normal;
        }

        /// <summary>
        /// Generates a path that is relative to the imaginary GameData directory of the application
        /// </summary>
        public static String TransformPath(String path)
        {
            String appPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PlanetaryProcessor", Version, "GameData");
            Uri appUri = new Uri(appPath);
            Uri targetUri = new Uri(Path.GetFullPath(path));
            return Path.Combine(appPath, appUri.MakeRelativeUri(targetUri).ToString());
        }
    }
}