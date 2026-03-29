using System.Collections.Generic;
using UnityEngine;

namespace WorldGen
{
    /// <summary>
    /// Intermediate mesh data produced on a worker thread.
    /// Contains shared vertex data plus per-submesh triangle lists so
    /// each BlockType gets its own material slot on the MeshRenderer.
    /// </summary>
    public sealed class ChunkMeshData
    {
        public readonly List<Vector3>  Vertices;
        public readonly List<int>      Triangles;
        public readonly List<Vector3>  Normals;
        public readonly List<Color32>  Colors;
        public readonly List<Vector2>  UVs;

        public ChunkMeshData()
        {
            Vertices  = new List<Vector3> (WorldConstants.MeshVertexCapacity);
            Triangles = new List<int>     (WorldConstants.MeshTriangleCapacity);
            Normals   = new List<Vector3> (WorldConstants.MeshVertexCapacity);
            Colors    = new List<Color32> (WorldConstants.MeshVertexCapacity);
            UVs       = new List<Vector2> (WorldConstants.MeshVertexCapacity);
        }
    }

    /// <summary>
    /// Converts a ChunkData into renderable mesh data using culled face
    /// meshing — only faces adjacent to Air are emitted.
    /// Produces one submesh per BlockType so the main thread can assign
    /// a different material (from dragged-in prefabs) to each.
    /// Entirely thread-safe (no Unity API calls that require the main thread).
    /// </summary>
    public static class ChunkMeshBuilder
    {
        // ── Face data tables (indexed 0-5) ────────────────────────────
        // Order: Top(+Y), Bottom(-Y), North(+Z), South(-Z), East(+X), West(-X)

        private static readonly Vector3Int[] Directions =
        {
            new Vector3Int( 0, 1, 0),   // 0 Top
            new Vector3Int( 0,-1, 0),   // 1 Bottom
            new Vector3Int( 0, 0, 1),   // 2 North
            new Vector3Int( 0, 0,-1),   // 3 South
            new Vector3Int( 1, 0, 0),   // 4 East
            new Vector3Int(-1, 0, 0),   // 5 West
        };

        private static readonly Vector3[][] FaceVerts =
        {
            // Top (+Y)
            new[] { new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,1,0) },
            // Bottom (-Y)
            new[] { new Vector3(0,0,1), new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,0,1) },
            // North (+Z)
            new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(0,1,1), new Vector3(0,0,1) },
            // South (-Z)
            new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,0,0) },
            // East (+X)
            new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(1,1,1), new Vector3(1,0,1) },
            // West (-X)
            new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(0,0,0) },
        };

        private static readonly float[] FaceBrightness =
        {
            1.0f,   // Top
            0.4f,   // Bottom
            0.7f,   // North
            0.7f,   // South
            0.8f,   // East
            0.6f,   // West
        };

        // ── Public API ────────────────────────────────────────────────

        /// <summary>Build mesh data from chunk block data.</summary>
        public static ChunkMeshData Build(ChunkData data)
        {
            var mesh = new ChunkMeshData();
            int w = WorldConstants.ChunkWidth;
            int h = WorldConstants.ChunkHeight;
            int d = WorldConstants.ChunkDepth;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    for (int z = 0; z < d; z++)
                    {
                        BlockType block = data.GetBlock(x, y, z);
                        if (block == BlockType.Air) continue;

                        Vector3 pos = new Vector3(x, y, z);
                        Color32 baseColor = BlockColors.GetColor(block);

                        for (int face = 0; face < 6; face++)
                        {
                            Vector3Int dir = Directions[face];
                            if (data.GetBlock(x + dir.x, y + dir.y, z + dir.z) == BlockType.Air)
                            {
                                AddFace(mesh, pos, face, baseColor);
                            }
                        }
                    }
                }
            }

            return mesh;
        }

        // ── Internals ─────────────────────────────────────────────────

        // Standard UVs for each quad face: maps the full texture (0,0)→(1,1)
        private static readonly Vector2[] FaceUVs =
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
        };

        private static void AddFace(ChunkMeshData mesh, Vector3 blockPos,
                                     int faceIdx, Color32 baseColor)
        {
            int vStart = mesh.Vertices.Count;
            Vector3 normal = (Vector3)Directions[faceIdx];
            Color32 color  = TintColor(baseColor, FaceBrightness[faceIdx]);

            Vector3[] offsets = FaceVerts[faceIdx];
            for (int i = 0; i < 4; i++)
            {
                mesh.Vertices.Add(blockPos + offsets[i]);
                mesh.Normals.Add(normal);
                mesh.Colors.Add(color);
                mesh.UVs.Add(FaceUVs[i]);
            }

            mesh.Triangles.Add(vStart);
            mesh.Triangles.Add(vStart + 1);
            mesh.Triangles.Add(vStart + 2);
            mesh.Triangles.Add(vStart);
            mesh.Triangles.Add(vStart + 2);
            mesh.Triangles.Add(vStart + 3);
        }

        private static Color32 TintColor(Color32 c, float brightness)
        {
            return new Color32(
                (byte)(c.r * brightness),
                (byte)(c.g * brightness),
                (byte)(c.b * brightness),
                c.a);
        }
    }
}
