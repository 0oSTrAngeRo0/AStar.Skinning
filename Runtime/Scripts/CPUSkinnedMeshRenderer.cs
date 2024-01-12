using System;
using UnityEngine;

namespace AStar.Skinning
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class CPUSkinnedMeshRenderer : MonoBehaviour
    {
        private SkinnedMeshRenderer m_Smr;
        private Mesh m_Mesh;

        private Vector3[] m_OriginPositions;
        private Matrix4x4[] m_BindPoses;
        private BoneWeight[] m_BoneWeights;

        private Vector3[] m_BlendShapeDeltaPositionsCache;
        private Vector3[] m_FinalPositionsCache;
        private Matrix4x4[] m_CurrentPosesCache;

        private void Awake()
        {
            m_Smr = GetComponent<SkinnedMeshRenderer>();
            m_Mesh = Instantiate(m_Smr.sharedMesh);
            m_Smr.sharedMesh = m_Mesh;
            
            m_OriginPositions = m_Mesh.vertices;
            m_BindPoses = Array.ConvertAll(m_Smr.bones, bone => bone.worldToLocalMatrix);
            m_BoneWeights = m_Mesh.boneWeights;

            m_BlendShapeDeltaPositionsCache = new Vector3[m_OriginPositions.Length];
            m_FinalPositionsCache = new Vector3[m_OriginPositions.Length];
            m_CurrentPosesCache = new Matrix4x4[m_BindPoses.Length];
        }

        private void Update()
        {
            RefreshVertices();
        }

        private void RefreshVertices()
        {
            Vector3[] vertices = m_FinalPositionsCache;

            // update pose matrix
            Transform[] bones = m_Smr.bones;
            for (int i = 0; i < m_CurrentPosesCache.Length; i++)
                m_CurrentPosesCache[i] = bones[i].localToWorldMatrix * m_BindPoses[i];

            // initialize vertex buffer
            Array.Copy(m_OriginPositions, vertices, m_OriginPositions.Length);

            // apply blend shape
            if (m_Mesh.blendShapeCount > 0)
            {
                for (int i = 0; i < m_Mesh.blendShapeCount; i++)
                {
                    float weight = m_Smr.GetBlendShapeWeight(i) * 0.01f;
                    m_Mesh.GetBlendShapeFrameVertices(i, 0, m_BlendShapeDeltaPositionsCache, null, null);
                    if (weight == 0.0f) continue;
                    for (int j = 0; j < vertices.Length; j++)
                        vertices[j] += m_BlendShapeDeltaPositionsCache[j] * weight;
                }
            }

            // linear blend skinning
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 origin = vertices[i];
                Vector3 position = Vector3.zero;
                m_BoneWeights[i].ForEach((index, bone, weight) =>
                {
                    Vector4 homogeneous = new Vector4(origin.x, origin.y, origin.z, 1.0f); // homogeneous coordinate
                    homogeneous = m_CurrentPosesCache[bone] * homogeneous * weight;
                    position += new Vector3(homogeneous.x, homogeneous.y, homogeneous.z);
                });
                vertices[i] = position;
            }

            m_Mesh.SetVertices(vertices);
            m_Mesh.RecalculateBounds();
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateTangents();
            Graphics.DrawMesh(m_Mesh, Matrix4x4.identity, m_Smr.material, 0);
        }
    }

    public static class BoneWeightsExtensions
    {
        public static void ForEach(this BoneWeight weight, Action<int, int, float> action)
        {
            if (action == null) return;
            action.Invoke(0, weight.boneIndex0, weight.weight0);
            action.Invoke(1, weight.boneIndex1, weight.weight1);
            action.Invoke(2, weight.boneIndex2, weight.weight2);
            action.Invoke(3, weight.boneIndex3, weight.weight3);
        }
    }
}