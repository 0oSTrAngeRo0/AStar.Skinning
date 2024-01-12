using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AStar.Skinning
{
    [Serializable]
    #if UNITY_EDITOR
    [CreateAssetMenu(menuName = "Skinning/SkinningResources", fileName = nameof(SkinningResources))]
    #endif
    public sealed class SkinningResources : ScriptableObject
    {
        [Serializable, ReloadGroup]
        public sealed class ShaderResources
        {
            public ComputeShader SkinningShader;
        }

        public ShaderResources Shaders;
    }
}