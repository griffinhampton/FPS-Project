using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace WorldGen
{
    /// <summary>
    /// Manages a pool of background threads that generate chunk terrain and
    /// build mesh data off the main thread.  Communication with the main
    /// thread is entirely through lock-free concurrent queues — no contention.
    ///
    /// Usage (main thread only):
    ///   worker.Enqueue(coord, seed);
    ///   while (worker.TryDequeue(out result)) { /* apply mesh */ }
    ///   worker.Dispose();   // shuts threads down cleanly
    /// </summary>
    public sealed class ChunkWorker : IDisposable
    {
        // ── Nested types ──────────────────────────────────────────────

        /// <summary>Request placed into the pending queue.</summary>
        public struct ChunkRequest
        {
            public Vector2Int Coord;
            public int        Seed;
        }

        /// <summary>Completed result ready for the main thread.</summary>
        public sealed class ChunkResult
        {
            public Vector2Int   Coord;
            public ChunkData    Data;
            public ChunkMeshData MeshData;
        }

        // ── Fields ────────────────────────────────────────────────────

        private readonly ConcurrentQueue<ChunkRequest> _pending   = new ConcurrentQueue<ChunkRequest>();
        private readonly ConcurrentQueue<ChunkResult>  _completed = new ConcurrentQueue<ChunkResult>();
        private readonly Thread[] _threads;
        private volatile bool     _running = true;

        // ── Constructor ───────────────────────────────────────────────

        public ChunkWorker(int threadCount)
        {
            threadCount = Mathf.Max(1, threadCount);
            _threads = new Thread[threadCount];

            for (int i = 0; i < threadCount; i++)
            {
                _threads[i] = new Thread(WorkerLoop)
                {
                    Name         = $"ChunkWorker-{i}",
                    IsBackground = true          // won't prevent app exit
                };
                _threads[i].Start();
            }
        }

        // Overload: use default thread count from WorldConstants
        public ChunkWorker() : this(WorldConstants.WorkerThreadCount) { }

        // ── Public API (call from main thread) ────────────────────────

        /// <summary>Queue a chunk for generation.</summary>
        public void Enqueue(Vector2Int coord, int seed)
        {
            _pending.Enqueue(new ChunkRequest { Coord = coord, Seed = seed });
        }

        /// <summary>Overload accepting ints.</summary>
        public void Enqueue(int chunkX, int chunkZ, int seed)
        {
            Enqueue(new Vector2Int(chunkX, chunkZ), seed);
        }

        /// <summary>Try to grab a finished result.  Non-blocking.</summary>
        public bool TryDequeue(out ChunkResult result)
        {
            return _completed.TryDequeue(out result);
        }

        /// <summary>Number of chunks still waiting to be generated.</summary>
        public int PendingCount => _pending.Count;

        /// <summary>Number of results waiting to be consumed.</summary>
        public int CompletedCount => _completed.Count;

        // ── Shutdown ──────────────────────────────────────────────────

        public void Dispose()
        {
            _running = false;

            for (int i = 0; i < _threads.Length; i++)
            {
                if (_threads[i] != null && _threads[i].IsAlive)
                    _threads[i].Join(500);    // wait up to 500 ms
            }
        }

        // ── Worker loop (runs on background thread) ───────────────────

        private void WorkerLoop()
        {
            while (_running)
            {
                if (_pending.TryDequeue(out ChunkRequest req))
                {
                    try
                    {
                        // 1. Generate block data
                        ChunkData data = new ChunkData(req.Coord);
                        TerrainGenerator.Generate(data, req.Seed);

                        // 2. Build mesh from block data
                        ChunkMeshData meshData = ChunkMeshBuilder.Build(data);

                        // 3. Push result for main thread
                        _completed.Enqueue(new ChunkResult
                        {
                            Coord    = req.Coord,
                            Data     = data,
                            MeshData = meshData
                        });
                    }
                    catch (Exception ex)
                    {
                        // Log but don't crash the worker
                        Debug.LogError($"[ChunkWorker] Error generating chunk " +
                                       $"{req.Coord}: {ex}");
                    }
                }
                else
                {
                    // Nothing to do — sleep briefly to avoid busy-spin
                    Thread.Sleep(5);
                }
            }
        }
    }
}
