namespace WorldGen
{
    /// <summary>
    /// Central place for every tunable constant.  Change values here and
    /// the whole pipeline (generator, mesh builder, worker) picks them up.
    /// </summary>
    public static class WorldConstants
    {
        // ── Chunk dimensions (in blocks) ──────────────────────────────
        public const int ChunkWidth  = 16;
        public const int ChunkHeight = 64;
        public const int ChunkDepth  = 16;

        // ── Terrain shape ─────────────────────────────────────────────
        public const float BaseHeight      = 28f;   // average surface Y
        public const float HillAmplitude   = 12f;   // gentle rolling hills
        public const float MountainAmplitude = 26f;  // mountain peaks
        public const float DetailAmplitude = 3f;     // small surface bumps

        public const float HillScale       = 0.012f;
        public const float MountainScale   = 0.004f;
        public const float DetailScale     = 0.06f;

        public const int   Octaves         = 4;
        public const float Persistence     = 0.5f;
        public const float Lacunarity      = 2.0f;

        // ── Depth layers (counted down from surface) ──────────────────
        public const int GrassLayerDepth   = 1;    // 1 block of grass on top
        public const int DirtLayerDepth    = 5;    // next 5 blocks are dirt
        public const int BedrockHeight     = 1;    // bottom-most layer

        // ── Trees ─────────────────────────────────────────────────────
        public const float TreeNoiseScale  = 0.35f;  // sampling frequency
        public const float TreeThreshold   = 0.72f;  // higher = fewer trees
        public const int   TreeTrunkMin    = 4;       // min trunk height
        public const int   TreeTrunkMax    = 7;       // max trunk height
        public const int   TreeLeafRadius  = 2;       // leaf sphere radius

        // ── Performance ───────────────────────────────────────────────
        public const int MaxChunksAppliedPerFrame = 4;  // mesh uploads / frame
        public const int WorkerThreadCount        = 2;  // background threads
        public const int MeshVertexCapacity       = 16000;
        public const int MeshTriangleCapacity     = 24000;
    }
}
