using System;
using System.Collections.Generic;
using SulfurCraft.Game;
using SulfurCraft.Link;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SulfurCraft.Rendering
{
    internal sealed class BridgeGeometry : MonoBehaviour { }

    internal sealed class WorldView : IDisposable
    {
        private sealed class Surface : IDisposable
        {
            public readonly GameObject Object;
            public readonly Mesh Mesh;
            public readonly MeshRenderer Renderer;
            public Surface(Transform parent, string name, Material material)
            {
                Object = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(BridgeGeometry));
                Object.transform.SetParent(parent, false);
                Mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                Object.GetComponent<MeshFilter>().sharedMesh = Mesh;
                Renderer = Object.GetComponent<MeshRenderer>();
                Renderer.sharedMaterial = material;
                Renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            public void Texture(Texture texture)
            {
                var properties = new MaterialPropertyBlock();
                properties.SetTexture("_MainTex", texture);
                Renderer.SetPropertyBlock(properties);
            }
            public void Dispose() { UnityEngine.Object.Destroy(Mesh); UnityEngine.Object.Destroy(Object); }
        }

        private readonly GameObject root;
        private readonly BridgeAssets assets;
        private readonly WorldMapping world;
        private readonly Dictionary<Vector3Int, Surface[]> sections = new Dictionary<Vector3Int, Surface[]>();
        private readonly Dictionary<Vector3Int, Surface> solids = new Dictionary<Vector3Int, Surface>();
        private readonly Dictionary<uint, Texture2D> textures = new Dictionary<uint, Texture2D>();
        private readonly List<Surface> scene = new List<Surface>();
        private readonly List<Surface> avatar = new List<Surface>();
        private readonly Transform avatarRoot;
        private readonly List<Surface> items = new List<Surface>();
        private Texture2D atlas;
        private bool atlasDirty;
        private byte[] pendingScene;
        private byte[] pendingAvatar;
        private Bounds? dirtyNavigation;
        private float nextNavigation;
        public int Sections => sections.Count;
        public int Cracks { get; private set; }

        public WorldView(BridgeAssets assets, WorldMapping world)
        {
            this.assets = assets; this.world = world;
            root = new GameObject("SulfurCraft world");
            Object.DontDestroyOnLoad(root);
            avatarRoot = new GameObject("Minecraft player skin and armor").transform;
            avatarRoot.SetParent(root.transform, false);
            root.SetActive(false);
        }

        public void Visible(bool visible) => root.SetActive(visible);

        public void Receive(uint type, byte[] data)
        {
            switch (type)
            {
                case 1:
                    Require(data, 8); atlas = LoadTexture(atlas, I(data, 0), I(data, 4), data, 8);
                    foreach (var surfaces in sections.Values) foreach (var s in surfaces) s.Texture(atlas);
                    foreach (var item in items) item.Texture(atlas);
                    break;
                case 2: Section(data); break;
                case 3: Clear(); break;
                case 4:
                    Require(data, 16); uint id = U(data, 0); textures.TryGetValue(id, out Texture2D old);
                    textures[id] = LoadTexture(old, I(data, 4), I(data, 8), data, 16); break;
                case 5: pendingAvatar = data; break;
                case 6: pendingScene = data; break;
                case 7:
                    Require(data, 16);
                    int x = I(data, 0), y = I(data, 4), w = I(data, 8), h = I(data, 12);
                    if (atlas == null || x < 0 || y < 0 || w <= 0 || h <= 0 || x + w > atlas.width || y + h > atlas.height) return;
                    Require(data, checked(16 + w * h * 4));
                    var colors = new Color32[w * h];
                    for (int n = 0; n < colors.Length; n++) colors[n] = Color(data, 16 + n * 4);
                    atlas.SetPixels32(x, y, w, h, colors); atlasDirty = true; break;
                case 10: Solids(data); break;
            }
        }

        public void FinishFrame()
        {
            if (atlasDirty && atlas != null) atlas.Apply(false, false);
            atlasDirty = false;
            if (pendingScene != null) { Scene(pendingScene); pendingScene = null; }
            if (pendingAvatar != null)
            {
                Require(pendingAvatar, 8);
                Batches(pendingAvatar, 8, I(pendingAvatar, 0), I(pendingAvatar, 4), Vector3.zero, avatar, avatarRoot);
                pendingAvatar = null;
            }
        }

        public void SetPlayerFeet(Vector3 feet) => avatarRoot.position = feet;

        private void Section(byte[] data)
        {
            Require(data, 16); var key = new Vector3Int(I(data, 0), I(data, 4), I(data, 8));
            int count = I(data, 12); ValidateVertices(data, 16, count);
            if (sections.TryGetValue(key, out Surface[] previous)) foreach (var s in previous) s.Dispose();
            sections.Remove(key);
            if (count == 0) return;
            var surfaces = new[] { new Surface(root.transform, "Block section " + key, assets.Solid), new Surface(root.transform, "Transparent section " + key, assets.Transparent) };
            foreach (var s in surfaces) { s.Object.transform.position = world.FromMc((Vector3)key * 16); s.Texture(atlas); }
            BuildMesh(surfaces[0].Mesh, data, 16, 0, count, false);
            BuildMesh(surfaces[1].Mesh, data, 16, 0, count, true);
            sections[key] = surfaces;
        }

        private void Scene(byte[] data)
        {
            Require(data, 32);
            Vector3 origin = world.FromMc(BitConverter.ToDouble(data, 0), BitConverter.ToDouble(data, 8), BitConverter.ToDouble(data, 16));
            Batches(data, 32, I(data, 24), I(data, 28), origin, scene, root.transform);
        }

        private void Batches(byte[] data, int header, int batches, int vertices, Vector3 origin, List<Surface> surfaces, Transform parent)
        {
            if (batches < 0 || batches > 4096) throw new InvalidOperationException("Invalid scene batch count.");
            int start = checked(header + batches * 16); ValidateVertices(data, start, vertices);
            while (surfaces.Count > batches) { int last = surfaces.Count - 1; surfaces[last].Dispose(); surfaces.RemoveAt(last); }
            for (int b = 0; b < batches; b++)
            {
                int at = header + b * 16; uint textureId = U(data, at);
                int first = I(data, at + 4), count = I(data, at + 8); bool transparent = (U(data, at + 12) & 1) != 0;
                if (first < 0 || count < 0 || first + (long)count > vertices) throw new InvalidOperationException("Invalid scene batch range.");
                if (b == surfaces.Count) surfaces.Add(new Surface(parent, "Minecraft model " + b, assets.Solid));
                Surface s = surfaces[b]; s.Renderer.sharedMaterial = transparent ? assets.Transparent : assets.Solid;
                s.Object.transform.localPosition = origin;
                s.Texture(textureId == 0 ? atlas : textures.TryGetValue(textureId, out Texture2D texture) ? texture : atlas);
                BuildMesh(s.Mesh, data, start, first, count, null);
            }
        }

        private void BuildMesh(Mesh mesh, byte[] data, int start, int first, int count, bool? translucent)
        {
            var vertices = new List<Vector3>(count); var uv = new List<Vector2>(count); var colors = new List<Color32>(count);
            for (int n = first; n + 2 < first + count; n += 3)
            {
                if (translucent.HasValue && ((U(data, start + n * 32 + 28) & 2) != 0) != translucent.Value) continue;
                for (int corner = 0; corner < 3; corner++)
                {
                    int k = n + (corner == 0 ? 0 : 3 - corner);
                    int at = start + k * 32;
                    vertices.Add(new Vector3(F(data, at), F(data, at + 4), -F(data, at + 8)) * world.Units);
                    uv.Add(new Vector2(F(data, at + 12), F(data, at + 16)));
                    colors.Add(Color(data, at + 20));
                }
            }
            var indices = new int[vertices.Count]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
        }

        private void Solids(byte[] data)
        {
            Require(data, 16); var key = new Vector3Int(I(data, 0), I(data, 4), I(data, 8));
            if (solids.TryGetValue(key, out Surface old)) { MarkNavigation(old.Mesh.bounds, old.Object.transform.position); old.Dispose(); solids.Remove(key); }
            if (I(data, 12) == 0) return;
            Require(data, 528);
            var vertices = new List<Vector3>(); var indices = new List<int>();
            for (int y = 0; y < 16; y++) for (int z = 0; z < 16; z++) for (int x = 0; x < 16; x++)
            {
                if (!Solid(data, x, y, z)) continue;
                for (int face = 0; face < 6; face++)
                {
                    Vector3Int direction = Directions[face];
                    if (Solid(data, x + direction.x, y + direction.y, z + direction.z)) continue;
                    Quad(vertices, indices, new Vector3(x + .5f, y + .5f, z + .5f), Vector3.one, face, world.Units);
                }
            }
            var surface = new Surface(root.transform, "Block collision " + key, assets.Solid);
            surface.Object.layer = LayerMask.NameToLayer("Geometry") >= 0 ? LayerMask.NameToLayer("Geometry") : 0;
            surface.Object.transform.position = world.FromMc((Vector3)key * 16);
            surface.Renderer.enabled = false;
            surface.Mesh.SetVertices(vertices); surface.Mesh.SetTriangles(indices, 0); surface.Mesh.RecalculateBounds();
            surface.Object.AddComponent<MeshCollider>().sharedMesh = surface.Mesh;
            solids[key] = surface; MarkNavigation(surface.Mesh.bounds, surface.Object.transform.position);
        }

        public void UpdateEntities(SharedLink link)
        {
            long baseAt = Protocol.Entities; uint seq = link.U32(baseAt);
            if ((seq & 1) != 0) return;
            int count = Math.Min(160, Math.Max(0, link.I32(baseAt + 4)));
            var data = new byte[count * 96]; link.Copy(baseAt + 64, data, data.Length);
            if (seq != link.U32(baseAt)) return;
            int used = 0;
            Cracks = 0;
            for (int e = 0; e < count; e++)
            {
                int at = e * 96; uint kind = U(data, at);
                if (kind != 2 && kind != 4 && kind != 5) continue;
                if (used == items.Count) items.Add(new Surface(root.transform, "Dropped Minecraft item", assets.Solid));
                Surface item = items[used++]; item.Object.SetActive(true); item.Texture(atlas);
                item.Renderer.sharedMaterial = kind == 5 ? assets.Transparent : assets.Solid;
                item.Object.transform.position = world.FromMc(F(data, at + 8), F(data, at + 12), F(data, at + 16));
                item.Object.transform.rotation = Quaternion.Euler(0, F(data, at + 20), 0);
                var vertices = new List<Vector3>(); var indices = new List<int>(); var uv = new List<Vector2>();
                float size = F(data, at + 28);
                Vector3 extent = kind == 5 ? new Vector3(F(data, at + 32), F(data, at + 36), F(data, at + 40)) : Vector3.one * size;
                Vector3 center = kind == 5 ? extent * .5f : Vector3.zero;
                int faces = kind == 2 ? 1 : 6;
                if (kind == 5) Cracks++;
                for (int face = 0; face < faces; face++)
                {
                    Quad(vertices, indices, center, extent, kind == 2 ? 2 : face, world.Units);
                    int rect = at + 44 + (kind == 4 && face < 2 ? (face == 0 ? 2 : 1) * 16 : 0);
                    float u0 = F(data, rect), v0 = F(data, rect + 4), u1 = F(data, rect + 8), v1 = F(data, rect + 12);
                    uv.AddRange(new[] { new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0) });
                }
                uint tint = U(data, at + 92); Color32 color = tint == 0 ? new Color32(255, 255, 255, 255) : Color(data, at + 92);
                var colors = new Color32[vertices.Count]; for (int i = 0; i < colors.Length; i++) colors[i] = color;
                item.Mesh.Clear(); item.Mesh.SetVertices(vertices); item.Mesh.SetTriangles(indices, 0); item.Mesh.SetUVs(0, uv); item.Mesh.colors32 = colors; item.Mesh.RecalculateBounds();
            }
            for (int i = used; i < items.Count; i++) items[i].Object.SetActive(false);
            if (dirtyNavigation.HasValue && Time.unscaledTime >= nextNavigation && global::AstarPath.active != null)
            {
                global::AstarPath.active.UpdateGraphs(new Pathfinding.GraphUpdateObject(dirtyNavigation.Value));
                dirtyNavigation = null; nextNavigation = Time.unscaledTime + .5f;
            }
        }

        private void MarkNavigation(Bounds bounds, Vector3 position)
        {
            bounds.center += position; bounds.Expand(world.Units);
            if (dirtyNavigation.HasValue) { Bounds previous = dirtyNavigation.Value; previous.Encapsulate(bounds); dirtyNavigation = previous; }
            else dirtyNavigation = bounds;
        }

        private static readonly Vector3Int[] Directions = { Vector3Int.down, Vector3Int.up, new Vector3Int(0, 0, -1), new Vector3Int(0, 0, 1), Vector3Int.left, Vector3Int.right };
        private static readonly Vector3[] FaceU = { Vector3.right, Vector3.right, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        private static readonly Vector3[] FaceV = { Vector3.forward, Vector3.back, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        private static void Quad(List<Vector3> vertices, List<int> indices, Vector3 center, Vector3 size, int face, float units)
        {
            int first = vertices.Count; Vector3 normal = Directions[face];
            Vector3 c = center + Vector3.Scale(normal, size) * .5f, u = Vector3.Scale(FaceU[face], size) * .5f, v = Vector3.Scale(FaceV[face], size) * .5f;
            foreach (var p in new[] { c - u - v, c + u - v, c + u + v, c - u + v }) vertices.Add(new Vector3(p.x, p.y, -p.z) * units);
            indices.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
        }
        private static bool Solid(byte[] data, int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= 16 || y >= 16 || z >= 16) return false;
            int bit = x + z * 16 + y * 256; return (data[16 + (bit >> 3)] & (1 << (bit & 7))) != 0;
        }
        private static Texture2D LoadTexture(Texture2D old, int w, int h, byte[] data, int offset)
        {
            if (w <= 0 || h <= 0 || w > 16384 || h > 16384) throw new InvalidOperationException("Invalid texture dimensions.");
            int bytes = checked(w * h * 4); Require(data, checked(offset + bytes));
            if (old == null || old.width != w || old.height != h) { if (old != null) Object.Destroy(old); old = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp }; }
            var pixels = new byte[bytes]; Buffer.BlockCopy(data, offset, pixels, 0, bytes); old.LoadRawTextureData(pixels); old.Apply(false, false); return old;
        }
        private static void ValidateVertices(byte[] data, int start, int count) { if (count < 0 || count > 1500000) throw new InvalidOperationException("Invalid vertex count."); Require(data, checked(start + count * 32)); }
        private static void Require(byte[] data, int length) { if (data.Length < length) throw new InvalidOperationException("Truncated render message."); }
        private static int I(byte[] data, int at) => BitConverter.ToInt32(data, at);
        private static uint U(byte[] data, int at) => BitConverter.ToUInt32(data, at);
        private static float F(byte[] data, int at) => BitConverter.ToSingle(data, at);
        private static Color32 Color(byte[] data, int at) => new Color32(data[at], data[at + 1], data[at + 2], data[at + 3]);

        public void Clear()
        {
            pendingScene = pendingAvatar = null;
            foreach (var pair in sections.Values) foreach (var s in pair) s.Dispose(); sections.Clear();
            foreach (var s in solids.Values) { MarkNavigation(s.Mesh.bounds, s.Object.transform.position); s.Dispose(); } solids.Clear();
            foreach (var s in scene) s.Dispose(); scene.Clear();
            foreach (var s in avatar) s.Dispose(); avatar.Clear();
            foreach (var s in items) s.Dispose(); items.Clear();
        }
        public void Dispose() { Clear(); foreach (var texture in textures.Values) Object.Destroy(texture); if (atlas != null) Object.Destroy(atlas); Object.Destroy(root); }
    }
}
