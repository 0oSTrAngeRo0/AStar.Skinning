using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using System.Text;
#endif

namespace AStar.Skinning
{
    [RequireComponent(typeof(SkinnedMeshRenderer), typeof(MeshFilter), typeof(MeshRenderer))]
    public class GPUSkinnedMeshRenderer : MonoBehaviour
    {
        private Mesh m_Mesh;
        private SkinnedMeshRenderer m_Smr;
        private Transform[] m_Bones;
        private SkinningShader m_SkinningShader;
        private VertexAttributeDescriptor[] m_VertexAttributes;
        private bool m_IsSkinnedMeshRenderer;

        private void Start()
        {
            m_Smr = GetComponent<SkinnedMeshRenderer>();
            m_Smr.enabled = false;
            Mesh mesh = Instantiate(m_Smr.sharedMesh);

            m_Bones = m_Smr.bones;
            m_VertexAttributes = GetVertexAttributes(mesh);

            int boneWeightCount = GetBoneWeightCount(m_VertexAttributes);
            int bsCount = mesh.blendShapeCount;
            bool isSkin = boneWeightCount > 0;
            m_IsSkinnedMeshRenderer = boneWeightCount > 0 || bsCount > 0;

            if (!m_IsSkinnedMeshRenderer) Destroy(this);

            m_Mesh = mesh;
            MeshFilter filter = GetComponent<MeshFilter>();
            filter.sharedMesh = m_Mesh;
            filter.mesh = m_Mesh;
            GetComponent<MeshRenderer>().enabled = true;

            m_SkinningShader = new SkinningShader(new SkinningShader.CreateInfo
            {
                BindPoses = isSkin ? m_Mesh.GetBindposes() : default,
                VertexCount = m_Mesh.vertexCount,
                BlendShapeCount = bsCount,
                BoneWeightCount = boneWeightCount,
                PositionVertexBuffer = m_Mesh.GetVertexBuffer(0),
                ShadingVertexBuffer = m_Mesh.GetVertexBuffer(1),
                BoneVertexBuffer = isSkin ? m_Mesh.GetVertexBuffer(2) : null,
                BlendShapeBuffer = bsCount > 0 ? m_Mesh.GetBlendShapeBuffer(BlendShapeBufferLayout.PerVertex) : null,
            });
        }

        private void Update()
        {
            NativeArray<Matrix4x4> currentPoses = new NativeArray<Matrix4x4>(m_Bones.Length, Allocator.Temp);
            for (int i = 0, end = m_Bones.Length; i < end; i++)
                currentPoses[i] = m_Bones[i].localToWorldMatrix;

            NativeArray<float> bsWeights = new NativeArray<float>(m_Mesh.blendShapeCount, Allocator.Temp);
            for (int i = 0, end = m_Mesh.blendShapeCount; i < end; i++)
                bsWeights[i] = m_Smr.GetBlendShapeWeight(i) * 0.01f;

            m_SkinningShader.Dispatch(new SkinningShader.DispatchInfo
            {
                CurrentPoses = currentPoses,
                BlendShapeWeights = bsWeights,
            });
            currentPoses.Dispose();
            bsWeights.Dispose();
        }

        private void OnDestroy()
        {
            m_SkinningShader?.Dispose();
        }

        private int GetBoneWeightCount(VertexAttributeDescriptor[] attributes)
        {
            if (attributes == null) return 0;
            int indexCount = 0;
            int weightCount = 0;
            for (int i = 0, end = attributes.Length; i < end; i++)
            {
                VertexAttributeDescriptor attribute = attributes[i];
                if (attribute.attribute == VertexAttribute.BlendWeight) weightCount = attribute.dimension;
                if (attribute.attribute == VertexAttribute.BlendIndices) indexCount = attribute.dimension;
            }

            if ((indexCount == 0) && (weightCount == 0)) return 0;
            if (indexCount != weightCount) return 0;
            return indexCount;
        }

        private static VertexAttributeDescriptor[] GetVertexAttributes(Mesh mesh)
        {
            VertexAttributeDescriptor[] attributes = new VertexAttributeDescriptor[mesh.vertexAttributeCount];
            for (int i = 0, end = attributes.Length; i < end; i++)
                attributes[i] = mesh.GetVertexAttribute(i);
            return attributes;
        }

        #if UNITY_EDITOR
        [ContextMenu(nameof(PrintVertexAttributes))]
        private void PrintVertexAttributes()
        {
            SkinnedMeshRenderer smr = m_Smr;
            if (smr == null) smr = GetComponent<SkinnedMeshRenderer>();
            if (smr == null) return;
            Mesh mesh = smr.sharedMesh;
            if (mesh == null) return;

            StringBuilder builder = new StringBuilder();
            for (int i = 0, end = mesh.vertexAttributeCount; i < end; i++)
            {
                VertexAttributeDescriptor attribute = mesh.GetVertexAttribute(i);
                builder.Append(attribute);
                builder.Append("\n");
            }

            Debug.Log(builder);
        }
        #endif
    }
}