using System.Collections.Generic;
using UnityEngine;

namespace VoxelToolkit
{
    public class MeshAnimation : VoxelAnimation<MeshAnimation.Frame>
    {
        [System.Serializable]
        public struct Frame
        {
            [SerializeField] private Mesh mesh;

            public Mesh Mesh
            {
                get => mesh;   
                set => mesh = value;
            }

            public Frame(Mesh mesh)
            {
                this.mesh = mesh;
            }
        }

        public List<Mesh> Meshes
        {
            get
            {
                var result = new List<Mesh>();
                foreach (var frame in frames)
                    result.Add(frame.Value.Mesh);

                return result;
            }
        }

        protected override void ApplyFrame(Frame previousFrame, Frame frame, float delta)
        {
            var frameToSet = previousFrame;
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
                meshFilter.sharedMesh = frameToSet.Mesh;
            
            var meshCollider = GetComponent<MeshCollider>();
            if (meshCollider != null)
                meshCollider.sharedMesh = frameToSet.Mesh;
        }
    }
}