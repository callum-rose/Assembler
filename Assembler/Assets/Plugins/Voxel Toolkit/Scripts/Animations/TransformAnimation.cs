using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public class TransformAnimation : VoxelAnimation<TransformAnimation.Frame>
    {
        [SerializeField] private float unitSize = 0.1f;

        public float UnitSize
        {
            get => unitSize;
            set => unitSize = math.max(value, 0.0001f);
        }
        
        [System.Serializable]
        public struct Frame
        {
            [SerializeField] private float3 position;

            public float3 Position
            {
                get => position;
                set => position = value;
            }

            [SerializeField] private quaternion rotation;

            public quaternion Rotation
            {
                get => rotation;
                set => rotation = value;
            }

            [SerializeField] public float3 scale;

            public float3 Scale
            {
                get => scale;
                set => scale = value;
            }

            public Frame(float3 position, quaternion rotation, float3 scale)
            {
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
            }
        }

        protected override void ApplyFrame(Frame previousFrame, Frame frame, float delta)
        {
            var position = math.lerp(previousFrame.Position, frame.Position, delta);
            var snap = math.floor(position / UnitSize) * UnitSize;

            transform.localPosition = snap;
            transform.localRotation = previousFrame.Rotation;
            transform.localScale = previousFrame.Scale;
        }
    }
}