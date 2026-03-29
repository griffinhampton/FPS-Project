using UnityEngine;

namespace WorldGen
{
    /// <summary>
    /// Every block type in the world. Stored as a single byte per voxel for
    /// minimal memory usage (16 × 64 × 16 chunk = only 16 KB).
    /// </summary>
    public enum BlockType : byte
    {
        Air      = 0,
        Grass    = 1,
        Dirt     = 2,
        Stone    = 3,
        Bedrock  = 4,
        Sand     = 5,
        Water    = 6,
        Log      = 7,
        Leaves   = 8
    }

    /// <summary>Total number of non-Air block types that can be rendered.</summary>
    public static class BlockTypeUtil
    {
        public const int RenderableCount = 8;   // Grass..Leaves

        /// <summary>
        /// Returns a 0-based submesh index for a renderable block.
        /// Returns -1 for Air.
        /// </summary>
        public static int SubmeshIndex(BlockType t)
        {
            return t == BlockType.Air ? -1 : (int)t - 1;
        }
    }

    /// <summary>
    /// Maps each BlockType to a vertex colour.  Defaults are built-in;
    /// call <see cref="SetColor"/> at startup to override with colours
    /// extracted from dragged-in prefabs.
    /// Thread-safe for reads (writes should only happen on Start).
    /// </summary>
    public static class BlockColors
    {
        private static readonly Color32[] Defaults = new Color32[]
        {
            new Color32(  0,   0,   0,   0),   // Air   – never rendered
            new Color32( 58, 157,  35, 255),   // Grass – bright green
            new Color32(139,  90,  43, 255),   // Dirt  – brown
            new Color32(136, 136, 136, 255),   // Stone – grey
            new Color32( 50,  50,  50, 255),   // Bedrock – dark grey
            new Color32(219, 211, 160, 255),   // Sand  – pale yellow
            new Color32( 28,  80, 200, 128),   // Water – translucent blue
            new Color32(101,  67,  33, 255),   // Log   – dark brown
            new Color32( 34, 120,  15, 200),   // Leaves – translucent green
        };

        // Runtime-overridable copy
        private static Color32[] _colors;

        static BlockColors()
        {
            _colors = (Color32[])Defaults.Clone();
        }

        public static Color32 GetColor(BlockType type)
        {
            int i = (int)type;
            return (i >= 0 && i < _colors.Length) ? _colors[i] : _colors[0];
        }

        /// <summary>
        /// Override the colour for a block type (call on main thread at startup).
        /// </summary>
        public static void SetColor(BlockType type, Color32 color)
        {
            int i = (int)type;
            if (i >= 0 && i < _colors.Length)
                _colors[i] = color;
        }

        /// <summary>Reset all colours to defaults.</summary>
        public static void ResetColors()
        {
            _colors = (Color32[])Defaults.Clone();
        }
    }
}
