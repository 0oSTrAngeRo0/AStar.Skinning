using System;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
#endif

namespace AStar.Skinning
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class BlendShapeDrawer : MonoBehaviour
    {
        #if UNITY_EDITOR
        private SkinnedMeshRenderer m_Smr;

        private Vector3[][] m_BlendShapes;
        private Vector3[] m_Vertices;
        [SerializeField, Range(1e-5f, 1e-2f)] private float m_Radius;

        [SerializeField] private int m_BlendShapeIndex;
        private int m_BlendShapeCount;

        private void Awake()
        {
            m_Smr = GetComponent<SkinnedMeshRenderer>();
            Mesh mesh = m_Smr.sharedMesh;
            int bsCount = mesh.blendShapeCount;
            if (m_BlendShapeIndex < 0 || m_BlendShapeIndex >= bsCount) return;

            m_Vertices = mesh.vertices;
            m_BlendShapeCount = mesh.blendShapeCount;
        }

        private void Start()
        {
            int[] bufferData = GetBufferData(m_Smr.sharedMesh);
            RebuildData(bufferData, m_BlendShapeCount, m_Vertices.Length);
        }

        private int[] GetBufferData(Mesh mesh)
        {
            GraphicsBuffer buffer = mesh.GetBlendShapeBuffer(BlendShapeBufferLayout.PerVertex);
            int[] bufferData = new int[buffer.count];
            buffer.GetData(bufferData);
            buffer.Dispose();
            return bufferData;
        }

        private void RebuildData(int[] bufferData, int bsCount, int vertexCount)
        {
            m_BlendShapes = new Vector3[bsCount][];
            for (int i = 0, end = bsCount; i < end; i++)
                m_BlendShapes[i] = new Vector3[vertexCount];

            for (int i = 0, vertexLoopEnd = vertexCount; i < vertexLoopEnd; i++)
            {
                int start = bufferData[i];
                int end = bufferData[i + 1];
                while (start < end)
                {
                    BlendShapeData data = GetData(bufferData, ref start);
                    m_BlendShapes[data.Index][i] = data.Position;
                }
            }
        }

        private void OnDrawGizmos()
        {
            if (m_BlendShapes == null) return;
            if (m_BlendShapeIndex < 0 || m_BlendShapeIndex >= m_BlendShapeCount) return;
            Vector3[] vertices = m_BlendShapes[m_BlendShapeIndex];
            for (int i = 0, end = vertices.Length; i < end; i++)
                Gizmos.DrawSphere(vertices[i] + m_Vertices[i], m_Radius);
        }

        private BlendShapeData GetData(int[] raw, ref int start)
        {
            BlendShapeData data;
            data.Index = raw[start++];

            Vector3 position;
            position.x = UnsafeUtility.As<int, float>(ref raw[start++]);
            position.y = UnsafeUtility.As<int, float>(ref raw[start++]);
            position.z = UnsafeUtility.As<int, float>(ref raw[start++]);
            data.Position = position;

            Vector3 normal;
            normal.x = UnsafeUtility.As<int, float>(ref raw[start++]);
            normal.y = UnsafeUtility.As<int, float>(ref raw[start++]);
            normal.z = UnsafeUtility.As<int, float>(ref raw[start++]);
            data.Normal = normal;

            Vector3 tangent;
            tangent.x = UnsafeUtility.As<int, float>(ref raw[start++]);
            tangent.y = UnsafeUtility.As<int, float>(ref raw[start++]);
            tangent.z = UnsafeUtility.As<int, float>(ref raw[start++]);
            data.Tangent = tangent;

            return data;
        }
        #endif

        private struct BlendShapeData
        {
            public int Index;
            public Vector3 Position;
            public Vector3 Normal;
            public Vector3 Tangent;
        }
    }
}