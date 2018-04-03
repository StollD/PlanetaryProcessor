using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kopernicus;
using Kopernicus.Components;
using Kopernicus.Configuration;
using Kopernicus.OnDemand;
using KSP.Localization;
using UnityEngine;
using Events = Kopernicus.Events;
using Gradient = UnityEngine.Gradient;
using Logger = Kopernicus.Logger;

namespace PlanetaryProcessor.Unity
{
    /// <summary>
    /// The entrypoint into the UnityExecution
    /// </summary>
    public class Entrypoint : MonoBehaviour
    {
        /// <summary>
        /// The instance of the application
        /// </summary>
        public static Entrypoint Instance { get; private set; }

        /// <summary>
        /// The connection to the controller
        /// </summary>
        private PipeClient _client;
        
        void Awake()
        {
            Instance = this;
            
            // Init KSP and Kopernicus
            InitKSP();
            InitKopernicus();
            
            // Create the pipe to the controller
            String[] args = Environment.GetCommandLineArgs();
            String id = args.First(s => s.StartsWith("-id:"));
            id = id.Substring(4);
            _client = new PipeClient(id);
            
            Logger.Default.Log("1");
            _client.ReadMessage("GENERATE-MAPS-RAW",
                channel => _client.ReadMessage(channel, s => GenerateRawPlanetMaps(channel, s)));
            _client.ReadMessage("GENERATE-MAPS-ENCODED",
                channel => _client.ReadMessage(channel, s => GenerateEncodedPlanetMaps(channel, s)));
            _client.ReadMessage("KEEPALIVE", s => _client.SendMessage("KEEPALIVE", s));
        }

        /// <summary>
        /// Init required KSP code
        /// </summary>
        private void InitKSP()
        {
            // Kickstart the localizer
            PropertyInfo localizerInstance =
                typeof(Localizer).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            if (localizerInstance != null)
            {
                localizerInstance.SetValue(null, new Localizer(), null);
            }
        }

        /// <summary>
        /// Init required Kopernicus Code
        /// </summary>
        private void InitKopernicus()
        {
            // Kickstart the events
            gameObject.AddComponent<Events>();
            gameObject.AddComponent<Kopernicus.Components.Events>();
            gameObject.AddComponent<Kopernicus.OnDemand.Events>();

            // Kickstart the assembly loader
            List<Assembly> assemblies = new List<Assembly>();
            assemblies.Add(typeof(PQSMod).Assembly);
            assemblies.Add(typeof(Injector).Assembly);
            assemblies.Add(typeof(Ring).Assembly);
            assemblies.Add(typeof(MapSODemand).Assembly);
            assemblies.Add(typeof(Parser).Assembly);
            FieldInfo parserModTypes =
                typeof(Parser).GetField("_modTypes", BindingFlags.Static | BindingFlags.NonPublic);
            if (parserModTypes != null)
            {
                parserModTypes.SetValue(null, assemblies.SelectMany(a => a.GetTypes()).ToList());
            }

            // Prepare a PSystem
            GameObject pSystem = new GameObject("PSystem");
            pSystem.SetActive(false);
            Injector.StockSystemPrefab = pSystem.AddComponent<PSystem>();

            // Prepare a root body
            GameObject rootBody = new GameObject("Root");
            rootBody.SetActive(false);
            PSystemBody root = rootBody.AddComponent<PSystemBody>();
            root.celestialBody = rootBody.AddComponent<CelestialBody>();
            root.celestialBody.bodyName = "Root";
            root.celestialBody.bodyDisplayName = "Root";
            root.celestialBody.bodyAdjectiveDisplayName = "Root";
            Injector.StockSystemPrefab.rootBody = root;

            // Prepare a Jool, for the reference geosphere
            GameObject joolBody = new GameObject("Jool");
            joolBody.SetActive(false);
            PSystemBody jool = joolBody.AddComponent<PSystemBody>();
            jool.celestialBody = joolBody.AddComponent<CelestialBody>();
            jool.celestialBody.bodyName = "Jool";
            jool.scaledVersion = joolBody;
            jool.scaledVersion.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            root.children.Add(jool);

            // Init Laythe for cloning PQS
            GameObject laytheBody = new GameObject("Laythe");
            laytheBody.SetActive(false);
            PSystemBody laythe = laytheBody.AddComponent<PSystemBody>();
            laythe.celestialBody = laytheBody.AddComponent<CelestialBody>();
            laythe.celestialBody.bodyName = "Laythe";
            laythe.scaledVersion = laytheBody;
            laythe.scaledVersion.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            laythe.pqsVersion = laytheBody.AddComponent<PQS>();
            root.children.Add(laythe);

            // Fake Mun for cloning Voronoi Craters
            GameObject munBody = new GameObject("Mun");
            munBody.SetActive(false);
            PSystemBody mun = munBody.AddComponent<PSystemBody>();
            mun.celestialBody = munBody.AddComponent<CelestialBody>();
            mun.celestialBody.bodyName = "Mun";
            mun.scaledVersion = munBody;
            mun.scaledVersion.AddComponent<MeshFilter>().sharedMesh = new Mesh();
            mun.pqsVersion = munBody.AddComponent<PQS>();
            PQSMod_VoronoiCraters craters = new GameObject("Craters").AddComponent<PQSMod_VoronoiCraters>();
            craters.craterColourRamp = new Gradient();
            craters.craterColourRamp.SetKeys(
                new[] {new GradientColorKey(Color.black, 0), new GradientColorKey(Color.white, 1)},
                new[] {new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1)});
            craters.transform.parent = mun.pqsVersion.transform;
            root.children.Add(mun);
            
            // Enable prefab loading
            PropertyInfo injectorIsInPrefab = typeof(Injector).GetProperty("IsInPrefab");
            if (injectorIsInPrefab != null)
            {
                injectorIsInPrefab.SetValue(null, true, null);
            }
            
            // Enable logging
            Logger.Default.SetAsActive();
            
            // Defer the scaled space mesh of parsed bodies
            // Explanation: The ScaledVersion { } node breaks because we don't have access to the shaders 
            // referenced there. Therefor we can't set this value in the config directly
            Events.OnBodyApply.Add((b, c) => b.scaledVersion.deferMesh = true);
        }

        /// <summary>
        /// Generates the maps of a planet, and saves them as a color array
        /// </summary>
        private void GenerateRawPlanetMaps(String channel, String config)
        {
            try
            {
                // Load the config
                ConfigNode pqsNode = ConfigNode.Parse(File.ReadAllText(config));
                File.Delete(config);

                // Load the body
                Body body = GenerateBody(pqsNode, out PQSMod[] mods);
                
                // Special settings
                Boolean hasOcean = Boolean.Parse(pqsNode.GetValue("__hasOcean"));
                Double oceanHeight = Double.Parse(pqsNode.GetValue("__oceanHeight"));
                Color oceanColor = ConfigNode.ParseColor(pqsNode.GetValue("__oceanColor"));
                Single normalStrength = Single.Parse(pqsNode.GetValue("__normalStrength"));

                // Create the textures
                Int32 width = Int32.Parse(pqsNode.GetValue("__resolution"));
                GenerateMaps(width, body, mods, hasOcean, oceanHeight, oceanColor, normalStrength,
                    out Texture2D colorMap, out Texture2D heightMap, out Texture2D normalMap);
                
                // Generate the files
                String colorPath = Path.GetTempFileName();
                String heightPath = Path.GetTempFileName();
                String normalPath = Path.GetTempFileName();
                FileStream colorFile = File.OpenWrite(colorPath);
                FileStream heightFile = File.OpenWrite(heightPath);
                FileStream normalFile = File.OpenWrite(normalPath);
                
                // Write the color values
                for (Int32 y = 0; y < width / 2; y++)
                {
                    for (Int32 x = 0; x < width; x++)
                    {
                        // Color
                        Color c = colorMap.GetPixel(x, y);
                        colorFile.Write(BitConverter.GetBytes(c.r), 0, 4);
                        colorFile.Write(BitConverter.GetBytes(c.g), 0, 4);
                        colorFile.Write(BitConverter.GetBytes(c.b), 0, 4);
                        colorFile.Write(BitConverter.GetBytes(c.a), 0, 4);
                        
                        // Height
                        c = heightMap.GetPixel(x, y);
                        heightFile.Write(BitConverter.GetBytes(c.r), 0, 4);
                        heightFile.Write(BitConverter.GetBytes(c.g), 0, 4);
                        heightFile.Write(BitConverter.GetBytes(c.b), 0, 4);
                        heightFile.Write(BitConverter.GetBytes(c.a), 0, 4);
                        
                        // Normal
                        c = normalMap.GetPixel(x, y);
                        normalFile.Write(BitConverter.GetBytes(c.r), 0, 4);
                        normalFile.Write(BitConverter.GetBytes(c.g), 0, 4);
                        normalFile.Write(BitConverter.GetBytes(c.b), 0, 4);
                        normalFile.Write(BitConverter.GetBytes(c.a), 0, 4);
                    }
                    
                    // Flush
                    colorFile.Flush();
                    heightFile.Flush();
                    normalFile.Flush();
                }
                
                // Send an answer
                _client.SendMessage(channel, colorPath);
                _client.SendMessage(channel, heightPath);
                _client.SendMessage(channel, normalPath);
                _client.SendMessage(channel, width.ToString());
                
                // No error occured
                _client.SendMessage(channel + "-ERR", "NONE");
                
                // Dispose the body
                DestroyImmediate(body.generatedBody.gameObject);
                DestroyImmediate(colorMap);
                DestroyImmediate(heightMap);
                DestroyImmediate(normalMap);
                colorFile.Close();
                colorFile.Dispose();
                heightFile.Close();
                heightFile.Dispose();
                normalFile.Close();
                normalFile.Dispose();
            }
            catch (Exception e)
            {
                _client.SendMessage(channel + "-ERR", e.Message);
            }
        }
        
        /// <summary>
        /// Generates the maps of a planet, and saves them as a encoded png texture
        /// </summary>
        private void GenerateEncodedPlanetMaps(String channel, String config)
        {
            Logger.Default.Log("1");
            try
            {
                // Load the config
                ConfigNode pqsNode = ConfigNode.Parse(File.ReadAllText(config));
                File.Delete(config);

                // Load the body
                Body body = GenerateBody(pqsNode, out PQSMod[] mods);
                
                // Special settings
                Boolean hasOcean = Boolean.Parse(pqsNode.GetValue("__hasOcean"));
                Double oceanHeight = Double.Parse(pqsNode.GetValue("__oceanHeight"));
                Color oceanColor = ConfigNode.ParseColor(pqsNode.GetValue("__oceanColor"));
                Single normalStrength = Single.Parse(pqsNode.GetValue("__normalStrength"));

                // Create the textures
                Int32 width = Int32.Parse(pqsNode.GetValue("__resolution"));
                GenerateMaps(width, body, mods, hasOcean, oceanHeight, oceanColor, normalStrength,
                    out Texture2D colorMap, out Texture2D heightMap, out Texture2D normalMap);
                
                // Generate the files
                String colorPath = Path.GetTempFileName();
                String heightPath = Path.GetTempFileName();
                String normalPath = Path.GetTempFileName();
                File.WriteAllBytes(colorPath, colorMap.EncodeToPNG());
                File.WriteAllBytes(heightPath, heightMap.EncodeToPNG());
                File.WriteAllBytes(normalPath, normalMap.EncodeToPNG());
                
                // Send an answer
                _client.SendMessage(channel, colorPath);
                _client.SendMessage(channel, heightPath);
                _client.SendMessage(channel, normalPath);
                
                // No error occured
                _client.SendMessage(channel + "-ERR", "NONE");
                
                // Dispose the body
                DestroyImmediate(body.generatedBody.gameObject);
                DestroyImmediate(colorMap);
                DestroyImmediate(heightMap);
                DestroyImmediate(normalMap);
            }
            catch (Exception e)
            {
                _client.SendMessage(channel + "-ERR", e.Message);
            }
        }

        /// <summary>
        /// Generates a body from a pqs config
        /// </summary>
        private Body GenerateBody(ConfigNode pqsNode, out PQSMod[] mods)
        {
            // Generate a generic body config
            ConfigNode bodyNode = new ConfigNode("Body");
            bodyNode.AddValue("name", Guid.NewGuid().ToString());
                
            ConfigNode debugNode = bodyNode.AddNode("Debug");
            debugNode.AddValue("exportMesh", "false");
            debugNode.AddValue("update", "false");

            ConfigNode propertiesNode = bodyNode.AddNode("Properties");
            propertiesNode.AddValue("description", Guid.NewGuid().ToString());
            propertiesNode.AddValue("geeASL", "1");
            propertiesNode.AddValue("radius", pqsNode.GetValue("__radius"));

            ConfigNode pqsWrapper = bodyNode.AddNode("PQS");
            pqsWrapper.AddData(pqsNode);
                
            // Load the body
            Body body = Loader.currentBody = new Body();
            Logger.Default.Log(body);
            Logger.Default.Log(bodyNode);
            Logger.Default.Flush();
            Parser.LoadObjectFromConfigurationNode(body, bodyNode);
                
            // Remove the celestial body transform
            foreach (PQSMod_CelestialBodyTransform cbt in body.pqs.Value
                .GetComponentsInChildren<PQSMod_CelestialBodyTransform>(true))
            {
                DestroyImmediate(cbt.gameObject);
            }
                
            // Fetch the PQSMods
            mods = body.pqs.Value.GetComponentsInChildren<PQSMod>(true).OrderBy(m => m.order)
                .Where(m => m.modEnabled).ToArray();
                
            // Setup the sphere
            body.pqs.Value.radiusMin = body.pqs.Value.radius;
            body.pqs.Value.radiusMax = body.pqs.Value.radius;
            for (Int32 i = 0; i < mods.Length; i++)
            {
                body.pqs.Value.radiusMin += mods[i].GetVertexMinHeight();
                body.pqs.Value.radiusMax += mods[i].GetVertexMaxHeight();
            }
            body.pqs.Value.radiusDelta = body.pqs.Value.radiusMax - body.pqs.Value.radiusMin;
                
            // Setup the mods
            for (Int32 i = 0; i < mods.Length; i++)
            {
                mods[i].OnSetup();
                Logger.Default.Log(mods[i]);
            }

            return body;
        }

        /// <summary>
        /// Generates planet maps from a PQS
        /// </summary>
        private void GenerateMaps(Int32 width, Body body, PQSMod[] mods, Boolean hasOcean, Double oceanHeight,
            Color oceanColor, Single normalStrength, out Texture2D colorMap, out Texture2D heightMap, out Texture2D normalMap)
        {
            // Generate the textures
            Int32 height = width / 2;
            colorMap = new Texture2D(width, height, TextureFormat.ARGB32, true);
            heightMap = new Texture2D(width, height, TextureFormat.RGB24, true);

            // Create a VertexBuildData
            PQS.VertexBuildData data = new PQS.VertexBuildData();

            // Build the textures
            for (Int32 y = 0; y < height; y++)
            {
                for (Int32 x = 0; x < width; x++)
                {
                    // Update the build data
                    data.directionFromCenter = QuaternionD.AngleAxis(360d / width * x, Vector3d.up) *
                                               QuaternionD.AngleAxis(90d - 180d / height * y, Vector3d.right) *
                                               Vector3d.forward;
                    data.vertHeight = body.pqs.Value.radius;

                    // Build the height data
                    for (Int32 i = 0; i < mods.Length; i++)
                    {
                        mods[i].OnVertexBuildHeight(data);
                    }

                    // Build the color data
                    for (Int32 i = 0; i < mods.Length; i++)
                    {
                        mods[i].OnVertexBuild(data);
                    }

                    // Process the height
                    Single h = (Single) ((data.vertHeight - body.pqs.Value.radius) *
                                         (1d / body.pqs.Value.mapMaxHeight));
                    if (h < 0)
                        h = 0;
                    else if (h > 1)
                        h = 1;
                    heightMap.SetPixel(x, y, new Color(h, h, h));

                    // Process the color
                    if (hasOcean)
                    {
                        if (h <= oceanHeight)
                        {
                            colorMap.SetPixel(x, y, oceanColor.A(1f));
                        }
                        else
                        {
                            colorMap.SetPixel(x, y, data.vertColor.A(0f));
                        }
                    }
                    else
                    {
                        colorMap.SetPixel(x, y, data.vertColor.A(1f));
                    }
                }
            }

            // Generate the normalmap
            normalMap = Utility.BumpToNormalMap(heightMap, normalStrength);
        }
    }
}