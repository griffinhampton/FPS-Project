using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGen;

/// <summary>
/// Main orchestrator for infinite voxel terrain with trees.
///
/// INSPECTOR SETUP
/// ───────────────
/// 1. Drag your Player into the <c>player</c> slot.
/// 2. (Optional) Drag GameObjects / prefabs into the block-type slots
///    (grassBlock, dirtBlock, etc.).  The script will extract each
///    prefab's material and use it for that block type's submesh.
///    Any slot left empty falls back to the built-in vertex-colour shader.
/// 3. Set <c>seed</c> and <c>viewDistance</c>.
/// 4. Press Play — terrain + trees generate around the player, and the
///    player is teleported onto the surface so they never fall through.
/// </summary>
public class worldGenScript : MonoBehaviour
{
    // ── Inspector — References ────────────────────────────────────

    [Header("References")]
    [Tooltip("The player whose position drives chunk loading.")]
    public GameObject player;

    [Header("World Settings")]
    public int seed = 42;

    [Range(2, 16)]
    public int viewDistance = 6;

    // ── Inspector — Block Prefabs (drag GameObjects here) ─────────
    //    The MeshRenderer material on each prefab is used for that
    //    block type's submesh.  Leave any slot empty for a fallback.

    [Header("Block Prefabs (optional — material is extracted)")]
    [Tooltip("Prefab or GameObject whose material is used for Grass blocks.")]
    public GameObject grassBlock;

    [Tooltip("Prefab or GameObject whose material is used for Dirt blocks.")]
    public GameObject dirtBlock;

    [Tooltip("Prefab or GameObject whose material is used for Stone blocks.")]
    public GameObject stoneBlock;

    [Tooltip("Prefab or GameObject whose material is used for Bedrock blocks.")]
    public GameObject bedrockBlock;

    [Tooltip("Prefab or GameObject whose material is used for Sand blocks.")]
    public GameObject sandBlock;

    [Tooltip("Prefab or GameObject whose material is used for Water blocks.")]
    public GameObject waterBlock;

    [Tooltip("Prefab or GameObject whose material is used for Log blocks.")]
    public GameObject logBlock;

    [Tooltip("Prefab or GameObject whose material is used for Leaves blocks.")]
    public GameObject leavesBlock;

    // ── Runtime state ─────────────────────────────────────────────

    private ChunkWorker                        _worker;
    private Dictionary<Vector2Int, GameObject>  _activeChunks;
    private Dictionary<Vector2Int, ChunkData>   _chunkDataMap;
    private HashSet<Vector2Int>                 _pendingChunks;
    private Vector2Int                          _lastPlayerChunk;

    /// <summary>Single shared vertex-colour material for all chunks.</summary>
    private Material _chunkMaterial;

    // ── Lifecycle ─────────────────────────────────────────────────

    void Start()
    {
        // Apply prefab colour overrides and create the shared material
        ApplyPrefabColorOverrides();
        _chunkMaterial = CreateVertexColorMaterial();

        // Init collections
        _activeChunks  = new Dictionary<Vector2Int, GameObject>();
        _chunkDataMap  = new Dictionary<Vector2Int, ChunkData>();
        _pendingChunks = new HashSet<Vector2Int>();

        // ── Generate the initial chunk SYNCHRONOUSLY so the player
        //    has ground to stand on before the first frame renders ──
        _lastPlayerChunk = GetChunkCoord(player.transform.position);
        GenerateInitialChunks();

        // Teleport player onto the terrain surface
        PlacePlayerOnSurface();

        // Start worker threads for all subsequent async chunk loads
        _worker = new ChunkWorker(WorldConstants.WorkerThreadCount);
        RequestMissingChunks();
    }

    void Update()
    {
        // 1. Apply finished chunks from the worker (capped per frame)
        int applied = 0;
        while (applied < WorldConstants.MaxChunksAppliedPerFrame &&
               _worker.TryDequeue(out ChunkWorker.ChunkResult result))
        {
            ApplyChunk(result);
            applied++;
        }

        // 2. Detect chunk boundary crossing
        Vector2Int currentChunk = GetChunkCoord(player.transform.position);
        if (currentChunk != _lastPlayerChunk)
        {
            _lastPlayerChunk = currentChunk;
            RequestMissingChunks();
            DestroyDistantChunks();
        }
    }

    void OnDestroy()
    {
        _worker?.Dispose();
    }

    // ── Initial synchronous generation ────────────────────────────

    /// <summary>
    /// Generate a small ring of chunks on the main thread so the player
    /// has terrain under their feet immediately (no falling).
    /// </summary>
    private void GenerateInitialChunks()
    {
        int radius = 2;   // 5×5 = 25 chunks — fast enough synchronously
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                Vector2Int coord = new Vector2Int(
                    _lastPlayerChunk.x + x,
                    _lastPlayerChunk.y + z);

                if (_activeChunks.ContainsKey(coord)) continue;

                ChunkData data = new ChunkData(coord);
                TerrainGenerator.Generate(data, seed);
                ChunkMeshData meshData = ChunkMeshBuilder.Build(data);

                ApplyChunkDirect(coord, data, meshData);
            }
        }
    }

    /// <summary>
    /// Move the player to stand on the terrain surface at their current X/Z.
    /// </summary>
    private void PlacePlayerOnSurface()
    {
        Vector3 pos = player.transform.position;
        int surfaceY = TerrainGenerator.GetSurfaceHeight(pos.x, pos.z, seed);
        player.transform.position = new Vector3(pos.x, surfaceY + 2f, pos.z);
    }

    // ── Chunk coordinate helpers ──────────────────────────────────

    private Vector2Int GetChunkCoord(Vector3 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / WorldConstants.ChunkWidth),
            Mathf.FloorToInt(worldPos.z / WorldConstants.ChunkDepth));
    }

    // ── Queue / dequeue logic ─────────────────────────────────────

    private void RequestMissingChunks()
    {
        List<Vector2Int> needed = new List<Vector2Int>();
        int vd2 = viewDistance * viewDistance;

        for (int x = -viewDistance; x <= viewDistance; x++)
        {
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                if (x * x + z * z > vd2) continue;

                Vector2Int coord = new Vector2Int(
                    _lastPlayerChunk.x + x,
                    _lastPlayerChunk.y + z);

                if (!_activeChunks.ContainsKey(coord) &&
                    !_pendingChunks.Contains(coord))
                {
                    needed.Add(coord);
                }
            }
        }

        needed.Sort((a, b) =>
        {
            float da = (a - _lastPlayerChunk).sqrMagnitude;
            float db = (b - _lastPlayerChunk).sqrMagnitude;
            return da.CompareTo(db);
        });

        foreach (Vector2Int coord in needed)
        {
            _worker.Enqueue(coord, seed);
            _pendingChunks.Add(coord);
        }
    }

    private void DestroyDistantChunks()
    {
        int vd2 = (viewDistance + 1) * (viewDistance + 1);
        List<Vector2Int> toRemove = new List<Vector2Int>();

        foreach (var kvp in _activeChunks)
        {
            Vector2Int delta = kvp.Key - _lastPlayerChunk;
            if (delta.x * delta.x + delta.y * delta.y > vd2)
                toRemove.Add(kvp.Key);
        }

        foreach (Vector2Int key in toRemove)
        {
            Destroy(_activeChunks[key]);
            _activeChunks.Remove(key);
            _chunkDataMap.Remove(key);
        }

        _pendingChunks.RemoveWhere(c =>
        {
            Vector2Int d = c - _lastPlayerChunk;
            return d.x * d.x + d.y * d.y > vd2;
        });
    }

    // ── Applying chunk results ────────────────────────────────────

    /// <summary>Called from Update for async worker results.</summary>
    private void ApplyChunk(ChunkWorker.ChunkResult result)
    {
        _pendingChunks.Remove(result.Coord);

        Vector2Int d = result.Coord - _lastPlayerChunk;
        int vd2 = (viewDistance + 1) * (viewDistance + 1);
        if (d.x * d.x + d.y * d.y > vd2) return;

        ApplyChunkDirect(result.Coord, result.Data, result.MeshData);
    }

    /// <summary>
    /// Shared logic for both synchronous (Start) and async (Update) chunk application.
    /// </summary>
    private void ApplyChunkDirect(Vector2Int coord, ChunkData data, ChunkMeshData meshData)
    {
        if (_activeChunks.TryGetValue(coord, out GameObject old))
            Destroy(old);

        GameObject chunkGO = new GameObject($"Chunk_{coord.x}_{coord.y}");
        chunkGO.transform.SetParent(transform);
        chunkGO.transform.position = new Vector3(
            coord.x * WorldConstants.ChunkWidth,
            0f,
            coord.y * WorldConstants.ChunkDepth);

        // Build Unity Mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(meshData.Vertices);
        mesh.SetTriangles(meshData.Triangles, 0);
        mesh.SetNormals(meshData.Normals);
        mesh.SetColors(meshData.Colors);
        mesh.SetUVs(0, meshData.UVs);

        // Attach components
        MeshFilter   mf = chunkGO.AddComponent<MeshFilter>();
        MeshRenderer mr = chunkGO.AddComponent<MeshRenderer>();
        MeshCollider mc = chunkGO.AddComponent<MeshCollider>();

        mf.sharedMesh      = mesh;
        mr.sharedMaterial  = _chunkMaterial;
        mc.sharedMesh      = mesh;

        _activeChunks[coord] = chunkGO;
        _chunkDataMap[coord] = data;
    }

    // ── Prefab colour extraction ───────────────────────────────────

    /// <summary>
    /// For each Inspector prefab slot that is assigned, extract the main
    /// colour from its material and override the BlockColors table.
    /// This way the combined vertex-colour mesh picks up the look of each
    /// prefab without needing the prefab's atlas texture / UVs.
    /// </summary>
    private void ApplyPrefabColorOverrides()
    {
        BlockColors.ResetColors();

        TryOverrideColor(grassBlock,   BlockType.Grass);
        TryOverrideColor(dirtBlock,    BlockType.Dirt);
        TryOverrideColor(stoneBlock,   BlockType.Stone);
        TryOverrideColor(bedrockBlock, BlockType.Bedrock);
        TryOverrideColor(sandBlock,    BlockType.Sand);
        TryOverrideColor(waterBlock,   BlockType.Water);
        TryOverrideColor(logBlock,     BlockType.Log);
        TryOverrideColor(leavesBlock,  BlockType.Leaves);
    }

    private void TryOverrideColor(GameObject prefab, BlockType type)
    {
        if (prefab == null) return;

        Material mat = null;
        var mr = prefab.GetComponentInChildren<MeshRenderer>();
        if (mr != null) mat = mr.sharedMaterial;
        if (mat == null)
        {
            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) mat = smr.sharedMaterial;
        }
        if (mat == null) return;

        // Try the standard _Color property, then _BaseColor (URP/HDRP)
        Color c;
        if (mat.HasProperty("_Color"))
            c = mat.GetColor("_Color");
        else if (mat.HasProperty("_BaseColor"))
            c = mat.GetColor("_BaseColor");
        else
            return;

        BlockColors.SetColor(type, (Color32)c);
    }

    // ── Material setup ────────────────────────────────────────────

    /// <summary>
    /// Create the single vertex-colour material used by every chunk.
    /// </summary>
    private Material CreateVertexColorMaterial()
    {
        Shader shader = Shader.Find("Custom/BlockVertexColor");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        return new Material(shader);
    }
}
