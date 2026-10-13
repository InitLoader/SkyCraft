using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DuckovCraft.World
{
    internal struct Triangle
    {
        public Vector3 A, B, C;
        public Triangle(Vector3 a, Vector3 b, Vector3 c) { A = a; B = b; C = c; }
    }

    internal static class CollisionGeometry
    {
        public static readonly int[] BoxIndices = { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };

        public static Vector3[] ReadVertices(Mesh mesh)
        {
            if (mesh.isReadable) return mesh.vertices;
            int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
            int offset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            if (mesh.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32)
                throw new InvalidOperationException("Unsupported collision vertex format on " + mesh.name);
            using (var buffer = mesh.GetVertexBuffer(stream))
            {
                byte[] bytes = new byte[buffer.count * buffer.stride];
                buffer.GetData(bytes);
                int stride = mesh.GetVertexBufferStride(stream);
                var vertices = new Vector3[mesh.vertexCount];
                for (int i = 0; i < vertices.Length; i++)
                {
                    int at = i * stride + offset;
                    vertices[i] = new Vector3(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4), BitConverter.ToSingle(bytes, at + 8));
                }
                return vertices;
            }
        }

        public static int[] ReadIndices(Mesh mesh)
        {
            if (mesh.isReadable) return mesh.triangles;
            using (var buffer = mesh.GetIndexBuffer())
            {
                byte[] bytes = new byte[buffer.count * buffer.stride];
                buffer.GetData(bytes);
                int width = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
                var indices = new List<int>();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var desc = mesh.GetSubMesh(sub);
                    if (desc.topology != MeshTopology.Triangles) continue;
                    for (int i = 0; i < desc.indexCount; i++)
                    {
                        int at = (desc.indexStart + i) * width;
                        indices.Add((width == 2 ? BitConverter.ToUInt16(bytes, at) : BitConverter.ToInt32(bytes, at)) + desc.baseVertex);
                    }
                }
                return indices.ToArray();
            }
        }

        public static bool Overlaps(Triangle triangle, Vector3 center, float half)
        {
            Vector3 a = triangle.A - center, b = triangle.B - center, c = triangle.C - center;
            if (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) > half || Mathf.Max(a.x, Mathf.Max(b.x, c.x)) < -half
                || Mathf.Min(a.y, Mathf.Min(b.y, c.y)) > half || Mathf.Max(a.y, Mathf.Max(b.y, c.y)) < -half
                || Mathf.Min(a.z, Mathf.Min(b.z, c.z)) > half || Mathf.Max(a.z, Mathf.Max(b.z, c.z)) < -half) return false;
            Vector3 e0 = b - a, e1 = c - b, e2 = a - c;
            if (!Axis(Vector3.Cross(e0, e1), a, b, c, half)) return false;
            return Edges(e0, a, b, c, half) && Edges(e1, a, b, c, half) && Edges(e2, a, b, c, half);
        }

        private static bool Edges(Vector3 edge, Vector3 a, Vector3 b, Vector3 c, float half)
        {
            return Axis(new Vector3(0, edge.z, -edge.y), a, b, c, half)
                && Axis(new Vector3(-edge.z, 0, edge.x), a, b, c, half)
                && Axis(new Vector3(edge.y, -edge.x, 0), a, b, c, half);
        }

        private static bool Axis(Vector3 axis, Vector3 a, Vector3 b, Vector3 c, float half)
        {
            float x = Vector3.Dot(a, axis), y = Vector3.Dot(b, axis), z = Vector3.Dot(c, axis);
            float reach = half * (Mathf.Abs(axis.x) + Mathf.Abs(axis.y) + Mathf.Abs(axis.z));
            return Mathf.Min(x, Mathf.Min(y, z)) <= reach && Mathf.Max(x, Mathf.Max(y, z)) >= -reach;
        }
    }
}
