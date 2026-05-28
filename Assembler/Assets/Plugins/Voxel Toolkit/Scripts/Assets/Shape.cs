using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public abstract class AnimationFrame<T>
    {
        [SerializeField] private int frame;
        [SerializeField] private T value;

        public int Frame => frame;
        public T Value => value;
        
        public AnimationFrame(int frame, T value)
        {
            this.frame = frame;
            this.value = value;
        }
    }
    
    public abstract class Animation<T, K> where K : AnimationFrame<T>
    {
        [SerializeField] private List<K> frames;
        [SerializeField] private bool looped = false;
        
        public K DefaultFrame => frames[0];
        
        public int FrameCount => frames.Count;

        public bool Looped
        {
            get => looped;
            set => looped = value;
        }

        public IReadOnlyList<K> Frames => frames;
        
        public K this[int index] => frames[index];

        public Animation()
        {
            frames = new List<K>();
        }
        
        public Animation(IReadOnlyList<K> frames)
        {
            var framesCopy = new List<K>(frames); 
            framesCopy.Sort((x, y) => x.Frame.CompareTo(y.Frame));
            this.frames = framesCopy;
        }

        public void AddFrame(K frame)
        {
            frames.RemoveAll(x => x.Frame == frame.Frame);
            frames.Add(frame);
            frames.Sort((x, y) => x.Frame.CompareTo(y.Frame));
        }
    }

    [System.Serializable]
    public class ModelAnimationFrame : AnimationFrame<Model>
    {
        public ModelAnimationFrame(int frame, Model value) : base(frame, value)
        {
        }
    }

    [System.Serializable]
    public class TransformAnimationFrame : AnimationFrame<int4x4>
    {
        public TransformAnimationFrame(int frame, int4x4 value) : base(frame, value)
        {
        }
    }

    [System.Serializable]
    public class ModelAnimation : Animation<Model, ModelAnimationFrame>
    {
        public ModelAnimation()
        {
            
        }
        
        public ModelAnimation(IReadOnlyList<ModelAnimationFrame> frames) : base(frames)
        {
        }
    }

    [System.Serializable]
    public class TransformationAnimation : Animation<int4x4, TransformAnimationFrame>
    {
        
    }
    
    /// <summary>
    /// Represents the shape object which consists of a certain number of #VoxelToolkit.Model
    /// </summary>
    [System.Serializable]
    public class Shape : HierarchyNode
    {
        [SerializeField] private ModelAnimation animation = new ModelAnimation();

        /// <summary>
        /// The animation of the shape
        /// </summary>
        public ModelAnimation Animation => animation;

        /// <summary>
        /// Adds the animation frame to the model
        /// </summary>
        /// <param name="frame">The frame to be added</param>
        public void AddAnimationFrame(ModelAnimationFrame frame)
        {
            animation.AddFrame(frame);
        }
    }
}