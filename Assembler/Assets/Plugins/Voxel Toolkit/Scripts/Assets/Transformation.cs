using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace VoxelToolkit
{
    /// <summary>
    /// Represents the transformation of the group/shape etc
    /// </summary>
    [System.Serializable]
    public class Transformation : HierarchyNode, INamedObject
    {
        [SerializeField] private HierarchyNode child;
        [SerializeField] private Layer layer;
        [SerializeField] private string transformationName;
        [SerializeField] private TransformationAnimation animation = new TransformationAnimation();
        
        public TransformationAnimation Animation => animation;

        /// <summary>
        /// Transformation name
        /// </summary>
        public string Name
        {
            get => transformationName;
            set => transformationName = value;
        }
        
        /// <summary>
        /// The related objects of the node
        /// </summary>
        public override ScriptableObject[] RelatedObjects =>
            child == null ? Array.Empty<ScriptableObject>() : new[] { child };

        /// <summary>
        /// The child of the transformation
        /// </summary>
        public HierarchyNode Child
        {
            get => child;
            set => child = value;
        }

        /// <summary>
        /// The layer of the transformation
        /// </summary>
        public Layer Layer
        {
            get => layer;
            set => layer = value;
        }
        /// <summary>
        /// Adds an animation frame to the sequence
        /// </summary>
        /// <param name="animationFrame">The frame to be added</param>
        public void AddAnimationFrame(TransformAnimationFrame animationFrame)
        {
            animation.AddFrame(animationFrame);
        }
    }
}