using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AStar.Skinning
{
    public class SkinningShader : IDisposable
    {
        private readonly ComputeShader m_SkinningShader;
        private readonly int m_KernelIndex;
        private readonly int m_VertexCount;
        private readonly bool m_EnableSkinning;
        private readonly bool m_EnableBlendShape;

        private GraphicsBuffer m_OriginalPositionVertexBuffer;
        private GraphicsBuffer m_PositionVertexBuffer;
        private GraphicsBuffer m_ShadingVertexBuffer;
        private GraphicsBuffer m_BoneVertexBuffer;
        private GraphicsBuffer m_BlendShapeBuffer;
        private GraphicsBuffer m_BindPosesBuffer;
        private GraphicsBuffer m_CurrentPosesBuffer;
        private GraphicsBuffer m_BlendShapeWeightsBuffer;

        private static readonly int _CurrentPosesBuffer = Shader.PropertyToID("CurrentPosesBuffer");
        private static readonly int _BlendShapeWeightsBuffer = Shader.PropertyToID("BlendShapeWeightsBuffer");
        private static readonly int _BlendShapeBuffer = Shader.PropertyToID("BlendShapeBuffer");
        private static readonly int _OriginalPositionVertexBuffer = Shader.PropertyToID("OriginalPositionVertexBuffer");
        private static readonly int _PositionVertexBuffer = Shader.PropertyToID("PositionVertexBuffer");
        private static readonly int _ShadingVertexBuffer = Shader.PropertyToID("ShadingVertexBuffer");
        private static readonly int _BoneVertexBuffer = Shader.PropertyToID("BoneVertexBuffer");
        private static readonly int _BindPosesBuffer = Shader.PropertyToID("BindPosesBuffer");

        private static readonly SkinningResources _Resources = Resources.Load<SkinningResources>("SkinningResources");

        private static readonly string[] _BoneWeightCountKeywords = new[]
        {
            "BONE_WEIGHT_COUNT_0", "BONE_WEIGHT_COUNT_1", "BONE_WEIGHT_COUNT_2", "BONE_WEIGHT_COUNT_3",
            "BONE_WEIGHT_COUNT_4",
        };

        public struct CreateInfo
        {
            public NativeArray<Matrix4x4> BindPoses;
            public int VertexCount;
            public int BlendShapeCount;
            public int BoneWeightCount;
            public GraphicsBuffer PositionVertexBuffer;
            public GraphicsBuffer ShadingVertexBuffer;
            public GraphicsBuffer BoneVertexBuffer;
            public GraphicsBuffer BlendShapeBuffer;
        }

        public struct DispatchInfo
        {
            public NativeArray<Matrix4x4> CurrentPoses;
            public NativeArray<float> BlendShapeWeights;
        }

        public SkinningShader(CreateInfo info)
        {
            m_SkinningShader = Object.Instantiate(_Resources.Shaders.SkinningShader);
            m_KernelIndex = m_SkinningShader.FindKernel("CSMain");
            m_VertexCount = info.VertexCount;
            m_EnableSkinning = info.BoneWeightCount > 0;
            m_EnableBlendShape = info.BlendShapeCount > 0;

            CreateBuffers(info);

            if (m_EnableBlendShape) 
                m_SkinningShader.EnableKeyword("ENABLE_BLEND_SHAPE");
            m_SkinningShader.EnableKeyword(_BoneWeightCountKeywords[info.BoneWeightCount]);

            SetBuffers();
        }

        private void CreateBuffers(CreateInfo info)
        {
            m_PositionVertexBuffer = info.PositionVertexBuffer;
            m_ShadingVertexBuffer = info.ShadingVertexBuffer;
            m_OriginalPositionVertexBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.CopyDestination,
                m_PositionVertexBuffer.count,
                m_PositionVertexBuffer.stride);

            if (m_EnableSkinning)
            {
                m_BoneVertexBuffer = info.BoneVertexBuffer;

                NativeArray<Matrix4x4> bindPoses = info.BindPoses;
                m_BindPosesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bindPoses.Length,
                    UnsafeUtility.SizeOf<Matrix4x4>());
                m_BindPosesBuffer.SetData(bindPoses);
                bindPoses.Dispose();

                m_CurrentPosesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bindPoses.Length,
                    UnsafeUtility.SizeOf<Matrix4x4>());
            }

            if (m_EnableBlendShape)
            {
                m_BlendShapeBuffer = info.BlendShapeBuffer;
                m_BlendShapeWeightsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, info.BlendShapeCount,
                    UnsafeUtility.SizeOf<float>());
            }

            Graphics.CopyBuffer(m_PositionVertexBuffer, m_OriginalPositionVertexBuffer);
        }

        private void SetBuffers()
        {
            m_SkinningShader.SetBuffer(m_KernelIndex, _OriginalPositionVertexBuffer,
                m_OriginalPositionVertexBuffer);
            m_SkinningShader.SetBuffer(m_KernelIndex, _PositionVertexBuffer, m_PositionVertexBuffer);
            m_SkinningShader.SetBuffer(m_KernelIndex, _ShadingVertexBuffer, m_ShadingVertexBuffer);
            if (m_EnableSkinning)
            {
                m_SkinningShader.SetBuffer(m_KernelIndex, _BindPosesBuffer, m_BindPosesBuffer);
                m_SkinningShader.SetBuffer(m_KernelIndex, _BoneVertexBuffer, m_BoneVertexBuffer);
            }

            if (m_EnableBlendShape)
                m_SkinningShader.SetBuffer(m_KernelIndex, _BlendShapeBuffer, m_BlendShapeBuffer);
        }

        public void Dispatch(DispatchInfo info)
        {
            if (m_EnableSkinning)
            {
                m_CurrentPosesBuffer.SetData(info.CurrentPoses);
                m_SkinningShader.SetBuffer(m_KernelIndex, _CurrentPosesBuffer, m_CurrentPosesBuffer);
            }

            if (m_EnableBlendShape)
            {
                m_BlendShapeWeightsBuffer.SetData(info.BlendShapeWeights);
                m_SkinningShader.SetBuffer(m_KernelIndex, _BlendShapeWeightsBuffer, m_BlendShapeWeightsBuffer);
            }

            m_SkinningShader.Dispatch(m_KernelIndex, m_VertexCount / 16, 1, 1);
        }

        public void Dispose()
        {
            m_OriginalPositionVertexBuffer.Dispose();
            m_PositionVertexBuffer.Dispose();
            m_ShadingVertexBuffer.Dispose();

            if (m_EnableSkinning)
            {
                m_BoneVertexBuffer.Dispose();
                m_BindPosesBuffer.Dispose();
                m_CurrentPosesBuffer.Dispose();
            }

            if (m_EnableBlendShape)
            {
                m_BlendShapeWeightsBuffer.Dispose();
                m_BlendShapeBuffer.Dispose();
            }
        }
    }
}