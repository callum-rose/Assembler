using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    [ExecuteAlways]
    public abstract class VoxelAnimation<T> : VoxelAnimationBase, ISerializationCallbackReceiver
    {
        private int lastFrame = int.MinValue;
        private int firstFrame = int.MaxValue;
        [System.Serializable]
        private struct SerializedFrame
        {
            [SerializeField] private T frame;
            [SerializeField] private int index;

            public T Frame => frame;
            public int Index => index;

            public SerializedFrame(T frame, int index)
            {
                this.frame = frame;
                this.index = index;
            }
        }

        [SerializeField] private AnimationRange animationRange;
        [SerializeField] private List<SerializedFrame> serializedFrames = new List<SerializedFrame>();
        [SerializeField] private bool looped;
        protected SortedList<int, T> frames = new SortedList<int, T>();

        public bool Looped
        {
            get => looped;
            set => looped = value;
        }

        public T DefaultFrame => frames[firstFrame];

        public AnimationRange AnimationRange
        {
            get => animationRange;
            set => animationRange = value;
        }

        public override int MaxFrame => frames.Count > 0 ? frames.Keys[^1] : 0;

        public float Time
        {
            get => time;
            set
            {
                time = value;
                UpdateAnimation();
            }
        }

        public void OnBeforeSerialize()
        {
            serializedFrames.Clear();
            foreach (var frame in frames)
                serializedFrames.Add(new SerializedFrame(frame.Value, frame.Key)); 
        }

        public void OnAfterDeserialize()
        {
            frames.Clear();
            foreach (var serialized in serializedFrames)
                SetFrame(serialized.Frame, serialized.Index);
        }
        
        private static int BinarySearch(IList<int> list, int value)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));
            
            var comp = Comparer<int>.Default;
            var low = 0;
            var high = list.Count - 1;
            while (low < high) 
            {
                var median = (high + low) / 2;
                if (comp.Compare(list[median], value) < 0) 
                    low = median + 1;
                else 
                    high = median - 1;
            }
            
            if (comp.Compare(list[low], value) < 0) 
                low++;
            
            return low;
        }

        public void SetFrame(T frame, int index)
        {
            frames[index] = frame;
            firstFrame = Mathf.Min(index, firstFrame);
            lastFrame = Mathf.Max(index, lastFrame);
        }

        protected abstract void ApplyFrame(T previousFrame, T frame, float delta);

        private (T Previous, T Current, float Delta) GetFramePair(float evaluationTime)
        {
            var timeIndex = (int)math.ceil(looped ? (evaluationTime % (MaxFrame + 1)) : evaluationTime);
            var frame = AnimationRange.Clamp(timeIndex); 
            var foundIndex = BinarySearch(frames.Keys, frame);
            if (foundIndex < 0)
                throw new Exception("Wasn't able to get the frame");

            if (foundIndex == frames.Count)
                return (frames[frames.Keys[foundIndex - 1]], frames[frames.Keys[foundIndex - 1]], 0.0f);

            var previousFrameIndex = 0;
            if (foundIndex != 0 && previousFrameIndex != frame)
                previousFrameIndex = foundIndex - 1;

            var firstFrameIndex = frames.Keys[previousFrameIndex];
            var currentFrameIndex = frames.Keys[foundIndex];
            
            var difference = currentFrameIndex - firstFrameIndex;
            var delta = 1.0f - (difference > 0.0f ? (currentFrameIndex - evaluationTime) / difference : 0.0f);
            
            return timeIndex > AnimationRange.End ? 
                (frames[currentFrameIndex], frames[firstFrameIndex], 1.0f - delta) : 
                (frames[firstFrameIndex], frames[currentFrameIndex], delta);
        }

        public void OnDidApplyAnimationProperties()
        {
            UpdateAnimation();
        }

        public void UpdateAnimation()
        {
            var frameData = GetFramePair(time);
            ApplyFrame(frameData.Previous, frameData.Current, frameData.Delta);
        }
        
#if UNITY_EDITOR
        private void LateUpdate()
        {
            UpdateAnimation();
        }
#endif
    }
}