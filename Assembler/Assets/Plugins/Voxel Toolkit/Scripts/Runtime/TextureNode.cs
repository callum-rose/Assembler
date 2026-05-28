using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public class TextureNode : IDisposable
    {
        public readonly int Id;
        public readonly Texture2D Texture;
        public float3x3 Location;
        private readonly List<TextureNode> Children = new List<TextureNode>();
        public int Area => Texture.width * Texture.height;
        public readonly TextureHandle Handle;
        
        public TextureNode(Texture2D texture, float3x3 location, int id, TextureOptimizationMode optimizationMode)
        {
            Texture = texture;
            Location = location;
            Id = id;
            Handle = new TextureHandle(texture.GetPixelData<int>(0), new int2(texture.width, texture.height), optimizationMode);
        }

        public void AddChildren(TextureNode node)
        {
            Children.Add(node);
        }

        private void UpdateLocation(TextureNode parent)
        {
            var stack = new Stack<(TextureNode Node, TextureNode Parent)>();
            stack.Push((this, parent));

            while (stack.Count > 0)
            {
                var (currentNode, parentNode) = stack.Pop();

                currentNode.Location = math.mul(parentNode.Location, currentNode.Location);

                foreach (var child in currentNode.Children)
                    stack.Push((child, currentNode));
            }
        }

        public void RecalculateUVs()
        {
            foreach (var child in Children)
                child.UpdateLocation(this);
        }
        
        public void FillLocations(NativeArray<float3x3> locations)
        {
            var stack = new Stack<TextureNode>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();

                locations[currentNode.Id] = currentNode.Location;

                foreach (var child in currentNode.Children)
                    stack.Push(child);
            }
        }
        
        public void Dispose()
        {
            var stack = new Stack<TextureNode>();
            stack.Push(this);

            while (stack.Count > 0)
            {
                var currentNode = stack.Pop();

                currentNode.Handle.Dispose();
                
                foreach (var child in currentNode.Children)
                    stack.Push(child);
            }
        }
    }
}