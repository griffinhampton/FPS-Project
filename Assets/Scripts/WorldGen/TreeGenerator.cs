using UnityEngine;

namespace WorldGen
{
    /// <summary>
    /// Places trees on the terrain using Perlin noise to decide locations
    /// and a deterministic hash for per-tree trunk height variation.
    /// Entirely thread-safe — no Unity API calls that require the main thread.
    /// </summary>
    public static class TreeGenerator
    {
        /// <summary>
        /// Scatter trees across the chunk.  Must be called AFTER terrain
        /// columns have been filled so we can read the surface height.
        /// </summary>
        public static void PlaceTrees(ChunkData chunk, int seed)
        {
            int worldX0 = chunk.Coord.x * WorldConstants.ChunkWidth;
            int worldZ0 = chunk.Coord.y * WorldConstants.ChunkDepth;

            float sx = seed * 0.5137f + 7777f;
            float sz = seed * 0.3491f + 7777f;

            for (int x = 0; x < WorldConstants.ChunkWidth; x++)
            {
                for (int z = 0; z < WorldConstants.ChunkDepth; z++)
                {
                    float wx = worldX0 + x;
                    float wz = worldZ0 + z;

                    // Perlin noise decides tree density regions
                    float noise = Mathf.PerlinNoise(
                        wx * WorldConstants.TreeNoiseScale + sx,
                        wz * WorldConstants.TreeNoiseScale + sz);

                    if (noise < WorldConstants.TreeThreshold) continue;

                    // Secondary hash to thin out and add randomness
                    int hash = Hash(worldX0 + x, worldZ0 + z, seed);
                    if ((hash & 3) != 0) continue;   // ~25 % of candidates pass

                    // Keep trees away from chunk edges so leaves don't clip
                    if (x < 2 || x > WorldConstants.ChunkWidth - 3 ||
                        z < 2 || z > WorldConstants.ChunkDepth  - 3)
                        continue;

                    // Find surface
                    int surfaceY = FindSurface(chunk, x, z);
                    if (surfaceY < 5 || surfaceY >= WorldConstants.ChunkHeight - 10)
                        continue;

                    // Only place on grass
                    if (chunk.GetBlock(x, surfaceY, z) != BlockType.Grass)
                        continue;

                    // Deterministic trunk height per tree
                    int trunkH = WorldConstants.TreeTrunkMin +
                                 (hash >> 4) % (WorldConstants.TreeTrunkMax -
                                                WorldConstants.TreeTrunkMin + 1);

                    BuildTree(chunk, x, surfaceY + 1, z, trunkH);
                }
            }
        }

        // ── Internal ──────────────────────────────────────────────────

        /// <summary>Find the highest non-Air block in a column.</summary>
        private static int FindSurface(ChunkData chunk, int x, int z)
        {
            for (int y = WorldConstants.ChunkHeight - 1; y >= 0; y--)
            {
                if (chunk.GetBlock(x, y, z) != BlockType.Air)
                    return y;
            }
            return 0;
        }

        /// <summary>Place a trunk + sphere of leaves.</summary>
        private static void BuildTree(ChunkData chunk, int bx, int by, int bz,
                                       int trunkHeight)
        {
            // Trunk
            for (int y = 0; y < trunkHeight; y++)
            {
                chunk.SetBlock(bx, by + y, bz, BlockType.Log);
            }

            // Leaf canopy centred on top of trunk
            int leafCY = by + trunkHeight;
            int r = WorldConstants.TreeLeafRadius;

            for (int lx = -r; lx <= r; lx++)
            {
                for (int ly = -r; ly <= r; ly++)
                {
                    for (int lz = -r; lz <= r; lz++)
                    {
                        // Spherical shape
                        if (lx * lx + ly * ly + lz * lz > r * r + 1)
                            continue;

                        int px = bx + lx;
                        int py = leafCY + ly;
                        int pz = bz + lz;

                        // Only place leaves in Air (don't overwrite trunk or terrain)
                        if (chunk.GetBlock(px, py, pz) == BlockType.Air)
                        {
                            chunk.SetBlock(px, py, pz, BlockType.Leaves);
                        }
                    }
                }
            }
        }

        /// <summary>Simple integer hash for deterministic pseudo-randomness.</summary>
        private static int Hash(int x, int z, int seed)
        {
            int h = seed;
            h ^= x * 73856093;
            h ^= z * 19349663;
            h ^= h >> 16;
            h *= 0x45d9f3b;
            h ^= h >> 16;
            return h & 0x7FFFFFFF;   // ensure positive
        }
    }
}
