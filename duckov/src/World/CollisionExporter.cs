using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DuckovCraft.Configuration;
using DuckovCraft.Game;
using DuckovCraft.Link;
using UnityEngine;

namespace DuckovCraft.World
{
    internal sealed class CollisionExporter
    {
        private sealed class MeshData { public Vector3[] Vertices; public int[] Indices; }
        private sealed class TransformedMesh { public Matrix4x4 Matrix; public Mesh Mesh; public Vector3[] Vertices; }
        private sealed class Region { public Vector3Int Key; public uint Epoch; public byte[] Triangles, Voxels; }
        private sealed class MovingCollider { public Matrix4x4 Matrix; public Bounds Bounds; public bool Active; }
        private readonly WorldMapping world;
        private readonly BridgeSettings settings;
        private readonly BridgeLog log;
        private readonly Dictionary<Mesh, MeshData> meshes = new Dictionary<Mesh, MeshData>();
        private readonly Dictionary<MeshCollider, TransformedMesh> transformed = new Dictionary<MeshCollider, TransformedMesh>();
        private readonly HashSet<Mesh> failedMeshes = new HashSet<Mesh>();
        private readonly Dictionary<Vector3Int, float> sent = new Dictionary<Vector3Int, float>();
        private readonly Dictionary<Vector3Int, ulong> published = new Dictionary<Vector3Int, ulong>();
        private readonly HashSet<Vector3Int> dirty = new HashSet<Vector3Int>();
        private readonly Dictionary<Collider, MovingCollider> moving = new Dictionary<Collider, MovingCollider>();
        private readonly List<Collider> removed = new List<Collider>();
        private float nextMovingPoll;
        private readonly Collider[] nearby = new Collider[2048];
        private Task<Region> pending;
        private Region ready;
        private uint epoch;
        public int RegionsSent { get; private set; }
        public int TrianglesSent { get; private set; }

        public CollisionExporter(WorldMapping world, BridgeSettings settings, BridgeLog log)
        {
            this.world = world; this.settings = settings; this.log = log;
        }

        public void Reset(SharedLink link)
        {
            epoch = world.Epoch;
            sent.Clear(); published.Clear(); meshes.Clear(); transformed.Clear(); failedMeshes.Clear(); ready = null;
            dirty.Clear(); moving.Clear(); nextMovingPoll = 0;
            RegionsSent = TrianglesSent = 0;
            link.Collision(1, BitConverter.GetBytes(epoch));
        }

        public void Update(SharedLink link, Vector3 feet, int mask)
        {
            PollMovingColliders();
            if (pending != null)
            {
                if (!pending.IsCompleted) return;
                if (pending.IsFaulted) log.LogError(pending.Exception);
                else if (pending.Result.Epoch == epoch) ready = pending.Result;
                pending = null;
            }
            if (ready != null)
            {
                if (!link.Collision(3, ready.Triangles) || !link.Collision(2, ready.Voxels)) return;
                sent[ready.Key] = Time.unscaledTime;
                if (!published.ContainsKey(ready.Key)) published[ready.Key] = link.U64(Protocol.Collision);
                RegionsSent++;
                TrianglesSent += BitConverter.ToInt32(ready.Triangles, 28);
                ready = null;
            }
            Vector3 mc = world.ToMc(feet);
            var center = new Vector3Int(Mathf.FloorToInt(mc.x / 8), Mathf.FloorToInt(mc.y / 8), Mathf.FloorToInt(mc.z / 8));
            Vector3Int chosen = default;
            float best = float.MaxValue;
            bool found = false;
            int radius = settings.CollisionRadius;
            for (int x = -radius; x <= radius; x++)
                for (int y = -2; y <= 2; y++)
                    for (int z = -radius; z <= radius; z++)
                    {
                        var key = center + new Vector3Int(x, y, z);
                        bool known = sent.TryGetValue(key, out float time);
                        bool changed = dirty.Contains(key);
                        if (known && !changed && Time.unscaledTime - time < 2f) continue;
                        float priority = x * x + z * z + Mathf.Abs(y) * 0.3f + (changed ? -1000f : known ? 1000f : 0f);
                        if (priority >= best) continue;
                        best = priority; chosen = key; found = true;
                    }
            if (!found) return;
            var triangles = Collect(chosen, mask);
            dirty.Remove(chosen);
            uint currentEpoch = epoch;
            pending = Task.Run(() => Rasterize(chosen, currentEpoch, triangles));
        }

        public void Invalidate(Bounds bounds)
        {
            Vector3 a = world.ToMc(bounds.min), b = world.ToMc(bounds.max);
            Vector3 low = Vector3.Min(a, b), high = Vector3.Max(a, b);
            for (int x = Mathf.FloorToInt(low.x / 8); x <= Mathf.FloorToInt(high.x / 8); x++)
                for (int y = Mathf.FloorToInt(low.y / 8); y <= Mathf.FloorToInt(high.y / 8); y++)
                    for (int z = Mathf.FloorToInt(low.z / 8); z <= Mathf.FloorToInt(high.z / 8); z++)
                    {
                        var key = new Vector3Int(x, y, z); dirty.Add(key); published.Remove(key);
                    }
        }

        public bool Ready(SharedLink link, Vector3 feet, float width, float height)
        {
            Vector3 mc = world.ToMc(feet);
            ulong consumed = link.U64(Protocol.Collision + 64);
            return CollisionCoverage.Ready(mc.x, mc.y, mc.z, width, height,
                (x, y, z) => published.TryGetValue(new Vector3Int(x, y, z), out ulong end) && consumed >= end);
        }

        private void PollMovingColliders()
        {
            if (Time.unscaledTime < nextMovingPoll) return;
            nextMovingPoll = Time.unscaledTime + .05f;
            removed.Clear();
            foreach (var entry in moving)
            {
                Collider collider = entry.Key; MovingCollider previous = entry.Value;
                if (collider == null) { Invalidate(previous.Bounds); removed.Add(collider); continue; }
                bool active = collider.enabled && collider.gameObject.activeInHierarchy;
                Matrix4x4 matrix = collider.transform.localToWorldMatrix;
                if (active == previous.Active && matrix == previous.Matrix) continue;
                Invalidate(previous.Bounds);
                if (active) { previous.Bounds = collider.bounds; Invalidate(previous.Bounds); }
                previous.Active = active; previous.Matrix = matrix;
            }
            foreach (var collider in removed) moving.Remove(collider);
        }

        private List<Triangle> Collect(Vector3Int key, int mask)
        {
            Vector3 min = (Vector3)key * 8, max = min + Vector3.one * 8;
            Bounds bounds = new Bounds((min + max) * 0.5f, Vector3.one * 8.002f);
            Vector3 unityCenter = world.FromMc(bounds.center);
            int count = Physics.OverlapBoxNonAlloc(unityCenter, Vector3.one * (4.001f * world.Units), nearby, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
            if (count == nearby.Length) throw new InvalidOperationException("Collision query exceeded 2048 colliders; reduce CollisionRadius.");
            var output = new List<Triangle>();
            for (int i = 0; i < count; i++)
            {
                Collider collider = nearby[i];
                if (!collider.enabled || collider.isTrigger || collider.GetComponentInParent<CharacterMainControl>() != null || collider.GetComponentInParent<Rendering.BridgeGeometry>() != null) continue;
                if (!moving.ContainsKey(collider) && (collider.attachedRigidbody != null || collider.GetComponentInParent<Animator>() != null || collider.GetComponentInParent<InteractableBase>() != null))
                    moving[collider] = new MovingCollider { Matrix = collider.transform.localToWorldMatrix, Bounds = collider.bounds, Active = true };
                if (collider is TerrainCollider terrain)
                {
                    foreach (var triangle in TerrainCollision.Collect(terrain, bounds, world)) Add(output, triangle, bounds);
                }
                else if (collider is MeshCollider meshCollider && meshCollider.sharedMesh != null)
                {
                    Mesh mesh = meshCollider.sharedMesh;
                    if (!meshes.TryGetValue(mesh, out MeshData data))
                    {
                        if (failedMeshes.Contains(mesh)) continue;
                        try { data = new MeshData { Vertices = CollisionGeometry.ReadVertices(mesh), Indices = CollisionGeometry.ReadIndices(mesh) }; meshes.Add(mesh, data); }
                        catch (Exception e) { failedMeshes.Add(mesh); log.LogWarning("Cannot export collider mesh " + mesh.name + ": " + e.Message); continue; }
                    }
                    Matrix4x4 matrix = collider.transform.localToWorldMatrix;
                    if (!transformed.TryGetValue(meshCollider, out TransformedMesh snapshot) || snapshot.Matrix != matrix || snapshot.Mesh != mesh)
                    {
                        var vertices = new Vector3[data.Vertices.Length];
                        for (int j = 0; j < vertices.Length; j++) vertices[j] = world.ToMc(matrix.MultiplyPoint3x4(data.Vertices[j]));
                        snapshot = new TransformedMesh { Matrix = matrix, Mesh = mesh, Vertices = vertices }; transformed[meshCollider] = snapshot;
                    }
                    for (int j = 0; j + 2 < data.Indices.Length; j += 3)
                    {
                        Vector3 a = snapshot.Vertices[data.Indices[j]];
                        Vector3 b = snapshot.Vertices[data.Indices[j + 2]];
                        Vector3 c = snapshot.Vertices[data.Indices[j + 1]];
                        Add(output, new Triangle(a, b, c), bounds);
                    }
                }
                else if (collider is BoxCollider box)
                {
                    Vector3 half = box.size * 0.5f;
                    var corners = new Vector3[8];
                    for (int j = 0; j < 8; j++)
                        corners[j] = world.ToMc(box.transform.TransformPoint(box.center + Vector3.Scale(half, new Vector3((j & 1) == 0 ? -1 : 1, (j & 2) == 0 ? -1 : 1, (j & 4) == 0 ? -1 : 1))));
                    for (int j = 0; j < CollisionGeometry.BoxIndices.Length; j += 3)
                        Add(output, new Triangle(corners[CollisionGeometry.BoxIndices[j]], corners[CollisionGeometry.BoxIndices[j + 2]], corners[CollisionGeometry.BoxIndices[j + 1]]), bounds);
                }
                else
                {
                    // Sample the collider's real boundary; never turn a hollow mesh into its bounds.
                    SampleSurface(collider, bounds, output);
                }
            }
            return output;
        }

        private void SampleSurface(Collider collider, Bounds bounds, List<Triangle> output)
        {
            Bounds cb = collider.bounds;
            for (int axis = 0; axis < 3; axis++)
            {
                int u = (axis + 1) % 3, v = (axis + 2) % 3;
                for (int sign = -1; sign <= 1; sign += 2)
                    for (int a = 0; a < 8; a++)
                        for (int b = 0; b < 8; b++)
                        {
                            Vector3[] points = new Vector3[4];
                            bool hit = true;
                            for (int k = 0; k < 4; k++)
                            {
                                Vector3 p = bounds.min;
                                p[u] += a + ((k & 1) != 0 ? 1 : 0);
                                p[v] += b + ((k & 2) != 0 ? 1 : 0);
                                Vector3 origin = world.FromMc(p), direction = Vector3.zero;
                                direction[axis] = sign;
                                if (axis == 2) direction[axis] = -sign;
                                origin[axis] = sign == (axis == 2 ? -1 : 1) ? cb.min[axis] - world.Units : cb.max[axis] + world.Units;
                                if (!collider.Raycast(new Ray(origin, direction), out RaycastHit ray, cb.size[axis] + 2 * world.Units)) { hit = false; break; }
                                points[k] = world.ToMc(ray.point);
                            }
                            if (!hit) continue;
                            Add(output, new Triangle(points[0], points[1], points[2]), bounds);
                            Add(output, new Triangle(points[1], points[3], points[2]), bounds);
                            Add(output, new Triangle(points[0], points[2], points[1]), bounds);
                            Add(output, new Triangle(points[1], points[2], points[3]), bounds);
                        }
            }
        }

        private static void Add(List<Triangle> output, Triangle triangle, Bounds bounds)
        {
            Vector3 min = Vector3.Min(triangle.A, Vector3.Min(triangle.B, triangle.C));
            Vector3 max = Vector3.Max(triangle.A, Vector3.Max(triangle.B, triangle.C));
            Vector3 low = bounds.min, high = bounds.max;
            if (min.x <= high.x && max.x >= low.x && min.y <= high.y && max.y >= low.y && min.z <= high.z && max.z >= low.z) output.Add(triangle);
        }

        private static Region Rasterize(Vector3Int key, uint epoch, List<Triangle> triangles)
        {
            Vector3Int origin = key * 8;
            var blocks = new Dictionary<Vector3Int, ulong[]>();
            foreach (var triangle in triangles)
            {
                Vector3 min = Vector3.Min(triangle.A, Vector3.Min(triangle.B, triangle.C)), max = Vector3.Max(triangle.A, Vector3.Max(triangle.B, triangle.C));
                var from = new Vector3Int(Math.Max(origin.x * 8, Mathf.FloorToInt(min.x * 8) - 1), Math.Max(origin.y * 8, Mathf.FloorToInt(min.y * 8) - 1), Math.Max(origin.z * 8, Mathf.FloorToInt(min.z * 8) - 1));
                var to = new Vector3Int(Math.Min((origin.x + 8) * 8 - 1, Mathf.FloorToInt(max.x * 8)), Math.Min((origin.y + 8) * 8 - 1, Mathf.FloorToInt(max.y * 8)), Math.Min((origin.z + 8) * 8 - 1, Mathf.FloorToInt(max.z * 8)));
                for (int y = from.y; y <= to.y; y++)
                    for (int z = from.z; z <= to.z; z++)
                        for (int x = from.x; x <= to.x; x++)
                        {
                            if (!CollisionGeometry.Overlaps(triangle, new Vector3((x + 0.5f) / 8, (y + 0.5f) / 8, (z + 0.5f) / 8), 0.0626f)) continue;
                            var block = new Vector3Int(Mathf.FloorToInt(x / 8f), Mathf.FloorToInt(y / 8f), Mathf.FloorToInt(z / 8f));
                            if (!blocks.TryGetValue(block, out ulong[] layers)) blocks.Add(block, layers = new ulong[8]);
                            layers[y & 7] |= 1UL << ((z & 7) * 8 + (x & 7));
                        }
            }
            return new Region { Key = key, Epoch = epoch, Triangles = Payload(origin, epoch, triangles), Voxels = Payload(origin, epoch, blocks) };
        }

        private static void Header(BinaryWriter writer, Vector3Int origin, uint epoch, int count)
        {
            writer.Write(origin.x); writer.Write(origin.y); writer.Write(origin.z);
            writer.Write(origin.x + 7); writer.Write(origin.y + 7); writer.Write(origin.z + 7);
            writer.Write(epoch); writer.Write(count);
        }

        private static byte[] Payload(Vector3Int origin, uint epoch, List<Triangle> triangles)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                Header(writer, origin, epoch, triangles.Count);
                foreach (var triangle in triangles)
                {
                    foreach (var v in new[] { triangle.A, triangle.B, triangle.C }) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                    writer.Write(0u);
                }
                return stream.ToArray();
            }
        }

        private static byte[] Payload(Vector3Int origin, uint epoch, Dictionary<Vector3Int, ulong[]> blocks)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                Header(writer, origin, epoch, blocks.Count);
                foreach (var block in blocks)
                {
                    writer.Write(block.Key.x); writer.Write(block.Key.y); writer.Write(block.Key.z); writer.Write(0u);
                    foreach (ulong layer in block.Value) writer.Write(layer);
                }
                return stream.ToArray();
            }
        }
    }
}
