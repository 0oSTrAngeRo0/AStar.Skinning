using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AStar.Skinning
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class JobSkinnedMeshRenderer : MonoBehaviour
    {
        private SkinnedMeshRenderer m_Smr;
        private Mesh m_Mesh;

        private NativeArray<BoneWeight> m_BoneWeights;
        private NativeArray<float3> m_OriginPositions;
        private int m_BlendShapeCount;
        private NativeArray<float3> m_BlendShapePositions;
        private NativeArray<Matrix4x4> m_BindPoses;

        private void Awake()
        {
            m_Smr = GetComponent<SkinnedMeshRenderer>();
            m_Mesh = Instantiate(m_Smr.sharedMesh);
            m_Smr.sharedMesh = m_Mesh;

            m_OriginPositions = new NativeArray<Vector3>(m_Mesh.vertices, Allocator.Persistent).Reinterpret<float3>();
            m_BoneWeights = new NativeArray<BoneWeight>(m_Mesh.boneWeights, Allocator.Persistent);
            m_BlendShapeCount = m_Mesh.blendShapeCount;
            Matrix4x4[] bindPoses = m_Smr.bones.Select(bone => (Matrix4x4)bone.worldToLocalMatrix).ToArray();
            m_BindPoses = new NativeArray<Matrix4x4>(bindPoses, Allocator.Persistent);

            float3[] bsPositions = GetBlendShapePositionsPerVertex(m_Mesh);
            m_BlendShapePositions = new NativeArray<float3>(bsPositions, Allocator.Persistent);
        }

        private void Update()
        {
            int vertexCount = m_Mesh.vertexCount;
            NativeArray<float4x4> poses = GetCurrentPoses(m_Smr, m_BindPoses, Allocator.TempJob);
            NativeArray<float> weights = GetCurrentBlendShapeWeights(m_Smr, Allocator.TempJob);
            NativeArray<float3> positions = new NativeArray<float3>(vertexCount, Allocator.TempJob);
            new LinearBlendSkinningJob()
            {
                BoneWeights = m_BoneWeights,
                OriginPositions = m_OriginPositions,
                CurrentPoses = poses,
                BlendShapeWeights = weights,
                BlendShapeCount = m_BlendShapeCount,
                BlendShapePositions = m_BlendShapePositions,
                TargetPositions = positions,
            }.Schedule(vertexCount, 16).Complete();

            m_Mesh.SetVertices(positions);
            m_Mesh.RecalculateBounds();
            m_Mesh.RecalculateNormals();
            m_Mesh.RecalculateTangents();
            Graphics.DrawMesh(m_Mesh, Matrix4x4.identity, m_Smr.material, 0);

            poses.Dispose();
            weights.Dispose();
            positions.Dispose();
        }

        private void OnDestroy()
        {
            m_BoneWeights.Dispose();
            m_OriginPositions.Dispose();
            m_BlendShapePositions.Dispose();
            m_BindPoses.Dispose();
        }

        private static float3[] GetBlendShapePositionsPerVertex(Mesh mesh)
        {
            int vertexCount = mesh.vertexCount;
            int bsCount = mesh.blendShapeCount;
            Vector3[][] bsPositions = new Vector3[bsCount][];
            for (int i = 0; i < bsCount; i++)
            {
                bsPositions[i] = new Vector3[vertexCount];
                mesh.GetBlendShapeFrameVertices(i, 0, bsPositions[i], null, null);
            }

            float3[] bsFlattenPositions = new float3[vertexCount * bsCount];
            for (int i = 0; i < vertexCount; i++)
            {
                int start = i * bsCount;
                for (int j = 0; j < bsCount; j++)
                    bsFlattenPositions[start + j] = bsPositions[j][i];
            }

            return bsFlattenPositions;
        }

        private static NativeArray<float> GetCurrentBlendShapeWeights(SkinnedMeshRenderer smr, Allocator allocator)
        {
            int bsCount = smr.sharedMesh.blendShapeCount;
            NativeArray<float> weights = new NativeArray<float>(bsCount, allocator);
            for (int i = 0; i < bsCount; i++)
                weights[i] = smr.GetBlendShapeWeight(i) * 0.01f;
            return weights;
        }

        private static NativeArray<float4x4> GetCurrentPoses(SkinnedMeshRenderer smr, NativeArray<Matrix4x4> bindposes,
            Allocator allocator)
        {
            Transform[] bones = smr.bones;
            int boneCount = bones.Length;
            NativeArray<float4x4> poses = new NativeArray<float4x4>(boneCount, allocator);
            for (int i = 0; i < boneCount; i++)
                poses[i] = math.mul(bones[i].localToWorldMatrix, bindposes[i]);
            return poses;
        }
        
    }

    [BurstCompile]
    public struct LinearBlendSkinningJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BoneWeight> BoneWeights;
        [ReadOnly] public NativeArray<float3> OriginPositions;
        [ReadOnly] public NativeArray<float4x4> CurrentPoses;
        
        // Blend Shape Data
        [ReadOnly] public int BlendShapeCount;
        [ReadOnly] public NativeArray<float> BlendShapeWeights;
        [ReadOnly] public NativeArray<float3> BlendShapePositions;

        [WriteOnly] public NativeArray<float3> TargetPositions;

        public void Execute(int index)
        {
            float3 origin = OriginPositions[index];
            
            // apply blend shape
            int start = index * BlendShapeCount;
            for (int i = 0; i < BlendShapeCount; i++)
                origin += BlendShapeWeights[i] * BlendShapePositions[start + i];

            // skinning
            BoneWeight bone = BoneWeights[index];
            float3 position = float3.zero;
            position += math.mul(CurrentPoses[bone.boneIndex0], new float4(origin, 1.0f)).xyz * bone.weight0;
            position += math.mul(CurrentPoses[bone.boneIndex1], new float4(origin, 1.0f)).xyz * bone.weight1;
            position += math.mul(CurrentPoses[bone.boneIndex2], new float4(origin, 1.0f)).xyz * bone.weight2;
            position += math.mul(CurrentPoses[bone.boneIndex3], new float4(origin, 1.0f)).xyz * bone.weight3;

            TargetPositions[index] = position;
        }
    }
}