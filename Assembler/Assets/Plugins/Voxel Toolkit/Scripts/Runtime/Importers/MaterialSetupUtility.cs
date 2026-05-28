using System;
using UnityEngine;

namespace VoxelToolkit
{
    internal static class MaterialSetupUtility
    {
        private static UnityEngine.Material[][] materialArrays = new UnityEngine.Material[][]
            {
                Array.Empty<UnityEngine.Material>(),
                new UnityEngine.Material[1],
                new UnityEngine.Material[2]
            };

        public static void Setup(MeshRenderer renderer, MeshDescriptor descriptor, UnityEngine.Material opaqueMaterial, UnityEngine.Material transparentMaterial)
        {
            var hasOpaque = (descriptor.MeshKind & MeshKind.Opaque) != MeshKind.Invalid;
            var hasTransparent = (descriptor.MeshKind & MeshKind.Transparent) != MeshKind.Invalid;
            var count = (hasOpaque ? 1 : 0) + (hasTransparent ? 1 : 0);
            var target = materialArrays[count];

            var index = 0;
            if (hasOpaque)
                target[index++] = opaqueMaterial;
            
            if (hasTransparent)
                target[index] = transparentMaterial;

            renderer.sharedMaterials = target;
        }
    }
}