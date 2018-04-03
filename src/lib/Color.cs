using System;

namespace PlanetaryProcessor
{
    /// <summary>
    /// Simple Unity-alike Color implementation
    /// </summary>
    public struct Color
    {
        public Single r;
        public Single g;
        public Single b;
        public Single a;

        public override String ToString()
        {
            return r + ", " + g + ", " + b + ", " + a;
        }

        /// <summary>
        /// Creates a color using a string
        /// </summary>
        public static Color FromString(String s)
        {
            Color c = new Color();
            String[] split = s.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries);
            Single.TryParse(split[0].Trim(), out c.r);
            Single.TryParse(split[1].Trim(), out c.g);
            Single.TryParse(split[2].Trim(), out c.b);
            Single.TryParse(split[3].Trim(), out c.a);
            return c;
        }
    }
}