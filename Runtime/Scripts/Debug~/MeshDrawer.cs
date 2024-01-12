using System;
using UnityEngine;

namespace AStar.Skinning
{
    public class MeshDrawer : MonoBehaviour
    {
        [SerializeField] private MeshFilter m_Mesh;
        [SerializeField, Range(1e-5f, 1e-2f)] private float m_Radius;

        private void OnDrawGizmos()
        {
            if (Application.isPlaying == false) return;
            m_Mesh = GetComponent<MeshFilter>(); if (m_Mesh == null) return;
            if (m_Mesh.mesh == null) return;
            Vector3[] vertices = m_Mesh.mesh.vertices;
            for (int i = 0, end = vertices.Length; i < end; i++)
                Gizmos.DrawSphere(vertices[i], m_Radius);
        }
    }
}