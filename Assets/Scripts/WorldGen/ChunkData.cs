using UnityEngine;

namespace WorldGen
{
    /// <summary>
    /// Stores every block in one chunk as a flat byte array.
    /// Memory: ChunkWidth × ChunkHeight × ChunkDepth bytes (default 16 KB).
    /// Fully thread-safe for read/write — no Unity API calls.
    /// </summary>
    public sealed class ChunkData
    {
        public readonly Vector2Int Coord;
        public readonly byte[] Blocks;

        public ChunkData(Vector2Int coord)
        {
            Coord  = coord;
            Blocks = new byte[WorldConstants.ChunkWidth
                             * WorldConstants.ChunkHeight
                             * WorldConstants.ChunkDepth];
        }

        // ── Overloaded accessors ──────────────────────────────────────

        /// <summary>Get the block type at local chunk coordinates.</summary>
        public BlockType GetBlock(int x, int y, int z)
        {
            if (x < 0 || x >= WorldConstants.ChunkWidth  ||
                y < 0 || y >= WorldConstants.ChunkHeight ||
                z < 0 || z >= WorldConstants.ChunkDepth)
                return BlockType.Air;

            return (BlockType)Blocks[FlatIndex(x, y, z)];
        }

        /// <summary>Get the block type using a Vector3Int.</summary>
        public BlockType GetBlock(Vector3Int pos)
        {
            return GetBlock(pos.x, pos.y, pos.z);
        }

        /// <summary>Set the block type at local chunk coordinates.</summary>
        public void SetBlock(int x, int y, int z, BlockType type)
        {
            if (x < 0 || x >= WorldConstants.ChunkWidth  ||
                y < 0 || y >= WorldConstants.ChunkHeight ||
                z < 0 || z >= WorldConstants.ChunkDepth)
                return;

            Blocks[FlatIndex(x, y, z)] = (byte)type;
        }

        /// <summary>Set the block type using a Vector3Int.</summary>
        public void SetBlock(Vector3Int pos, BlockType type)
        {
            SetBlock(pos.x, pos.y, pos.z, type);
        }

        // ── Internal helpers ──────────────────────────────────────────

        /// <summary>
        /// Converts 3-D coordinates to a flat index.
        /// Layout: x + z * Width + y * Width * Depth  (y is the slowest axis
        /// so that horizontal slices are contiguous — cache-friendly for
        /// terrain generation which iterates x/z first).
        /// </summary>
        private static int FlatIndex(int x, int y, int z)
        {
            return x
                 + z * WorldConstants.ChunkWidth
                 + y * WorldConstants.ChunkWidth * WorldConstants.ChunkDepth;
        }
    }
}
