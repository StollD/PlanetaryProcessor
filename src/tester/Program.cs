using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace PlanetaryProcessor.Tester
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            Run(args).Wait();
        }
        
        public static async Task Run(string[] args)
        {
            DateTime t = DateTime.Now;
            NodeTree config = new NodeTree();
            config.SetValue("__resolution", "2048");
            config.SetValue("__radius", "25000");
            config.SetValue("__hasOcean", "false");
            config.SetValue("__oceanHeight", "0");
            config.SetValue("__oceanColor", "0,0,0,0");
            config.SetValue("__normalStrength", "7");
            config.SetValue("mapMaxHeight", "9000");
            NodeTree mods = config.AddNode("Mods");
            NodeTree vertexSimplexHeightAbsolute = mods.AddNode("VertexSimplexHeightAbsolute");
            vertexSimplexHeightAbsolute.SetValue("deformity", "8000");
            vertexSimplexHeightAbsolute.SetValue("frequency", "0.5");
            vertexSimplexHeightAbsolute.SetValue("octaves", "3");
            vertexSimplexHeightAbsolute.SetValue("persistence", "0.5");
            vertexSimplexHeightAbsolute.SetValue("seed", "12");
            vertexSimplexHeightAbsolute.SetValue("order", "10");
            vertexSimplexHeightAbsolute.SetValue("enabled", "True");
            NodeTree vertexSimplexNoiseColor = mods.AddNode("VertexSimplexNoiseColor");
            vertexSimplexNoiseColor.SetValue("blend", "1");
            vertexSimplexNoiseColor.SetValue("colorStart", "0.641791046,0.51597774,0.488527536,1");
            vertexSimplexNoiseColor.SetValue("colorEnd", "0.291044772,0.273304433,0.216757119,1");
            vertexSimplexNoiseColor.SetValue("frequency", "1");
            vertexSimplexNoiseColor.SetValue("octaves", "8");
            vertexSimplexNoiseColor.SetValue("persistence", "0.5");
            vertexSimplexNoiseColor.SetValue("seed", "111453");
            vertexSimplexNoiseColor.SetValue("order", "100");
            vertexSimplexNoiseColor.SetValue("enabled", "True");
            NodeTree vertexHeightNoise = mods.AddNode("VertexHeightNoise");
            vertexHeightNoise.SetValue("deformity", "150");
            vertexHeightNoise.SetValue("frequency", "4");
            vertexHeightNoise.SetValue("octaves", "6");
            vertexHeightNoise.SetValue("persistence", "0.5");
            vertexHeightNoise.SetValue("seed", "111111112");
            vertexHeightNoise.SetValue("noiseType", "RidgedMultifractal");
            vertexHeightNoise.SetValue("mode", "Low");
            vertexHeightNoise.SetValue("lacunarity", "2.5");
            vertexHeightNoise.SetValue("order", "19");
            vertexHeightNoise.SetValue("enabled", "True");
            
            using (Processor processor = await Processor.Create("/home/dorian/Dokumente/KSP/1.4.2/Kerbal Space Program/"))
            {
                Processor.EncodedTextureData data = await processor.GenerateMapsEncoded(config);
                
                await SaveStream("color.png", data.Color);
                await SaveStream("height.png", data.Height);
                await SaveStream("normal.png", data.Normal);
            }
            Console.WriteLine(DateTime.Now - t);
        }

        public static async Task SaveStream(String file, Stream stream)
        {
            using (stream)
            {
                using (FileStream fs = File.OpenWrite(file))
                {
                    await stream.CopyToAsync(fs);
                }
            }
        }
    }
}