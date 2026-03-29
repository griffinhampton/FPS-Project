using UnityEngine;

namespace WorldGen
{
    /// <summary>
    /// Generates terrain height and block layers using multi-octave Perlin noise.
    /// Every method is pure math — safe to call from any thread.
    /// </summary>
    public static class TerrainGenerator
    {
        // ── Public API ────────────────────────────────────────────────

        /// <summary>
        /// Fill a ChunkData with terrain blocks.  Three noise layers are
        /// blended: gentle hills, mountains, and fine surface detail.
        /// </summary>
        public static void Generate(ChunkData chunk, int seed)
        {
            int worldX0 = chunk.Coord.x * WorldConstants.ChunkWidth;
            int worldZ0 = chunk.Coord.y * WorldConstants.ChunkDepth;

            // Seed offsets – irrational-ish multipliers so each seed
            // produces a completely different world.
            float sx = seed * 0.71237f;
            float sz = seed * 0.38917f;

            for (int x = 0; x < WorldConstants.ChunkWidth; x++)
            {
                for (int z = 0; z < WorldConstants.ChunkDepth; z++)
                {
                    float wx = worldX0 + x;
                    float wz = worldZ0 + z;

                    int surfaceY = ComputeSurfaceHeight(wx, wz, sx, sz);
                    surfaceY = Mathf.Clamp(surfaceY, 1, WorldConstants.ChunkHeight - 1);

                    FillColumn(chunk, x, z, surfaceY);
                }
            }

            // Tree pass — runs after all columns are filled
            TreeGenerator.PlaceTrees(chunk, seed);
        }

        /// <summary>
        /// Returns the surface height at the given world X/Z.
        /// Can be called externally (e.g. to place the player on the ground).
        /// </summary>
        public static int GetSurfaceHeight(float worldX, float worldZ, int seed)
        {
            float sx = seed * 0.71237f;
            float sz = seed * 0.38917f;
            int h = ComputeSurfaceHeight(worldX, worldZ, sx, sz);
            return Mathf.Clamp(h, 1, WorldConstants.ChunkHeight - 1);
        }

        // ── Internal helpers ──────────────────────────────────────────

        private static int ComputeSurfaceHeight(float wx, float wz,
                                                 float sx, float sz)
        {
            // Layer 1 – rolling hills
            float hills = OctavePerlin(
                wx * WorldConstants.HillScale + sx,
                wz * WorldConstants.HillScale + sz,
                WorldConstants.Octaves,
                WorldConstants.Persistence,
                WorldConstants.Lacunarity);

            // Layer 2 – broad mountain ranges (low frequency, high amplitude)
            float mountains = OctavePerlin(
                wx * WorldConstants.MountainScale + sx + 5000f,
                wz * WorldConstants.MountainScale + sz + 5000f,
                2, 0.5f, 2.0f);

            // Use mountains noise as a blend factor so peaks are localised
            float mountainFactor = Mathf.Clamp01(mountains + 0.3f);

            // Layer 3 – fine detail bumps
            float detail = OctavePerlin(
                wx * WorldConstants.DetailScale + sx + 12000f,
                wz * WorldConstants.DetailScale + sz + 12000f,
                2, 0.5f, 2.0f);

            float height = WorldConstants.BaseHeight
                         + hills     * WorldConstants.HillAmplitude
                         + mountains * WorldConstants.MountainAmplitude * mountainFactor
                         + detail    * WorldConstants.DetailAmplitude;

            return Mathf.RoundToInt(height);
        }

        /// <summary>
        /// Fill one vertical column of blocks with the proper depth layers.
        /// </summary>
        private static void FillColumn(ChunkData chunk, int x, int z, int surfaceY)
        {
            for (int y = 0; y <= surfaceY; y++)
            {
                BlockType type;
                int depthBelowSurface = surfaceY - y;

                if (y < WorldConstants.BedrockHeight)
                    type = BlockType.Bedrock;
                else if (depthBelowSurface > WorldConstants.DirtLayerDepth)
                    type = BlockType.Stone;
                else if (depthBelowSurface > WorldConstants.GrassLayerDepth - 1)
                    type = BlockType.Dirt;
                else
                    type = BlockType.Grass;

                chunk.SetBlock(x, y, z, type);
            }
        }

        /// <summary>
        /// Sums multiple Perlin octaves and returns a value in roughly [-1, 1].
        /// Mathf.PerlinNoise is a pure math function — thread-safe.
        /// </summary>
        private static float OctavePerlin(float x, float z,
                                           int octaves,
                                           float persistence,
                                           float lacunarity)
        {
            float total     = 0f;
            float frequency = 1f;
            float amplitude = 1f;
            float maxValue  = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total    += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            // Normalise to [-1, 1]
            return (total / maxValue) * 2f - 1f;
        }
    }
}
