using System.Collections.Generic;
using UnityEngine;
using VoxelToolkit;

namespace VoxelToolkit
{
    [System.Serializable]
    public abstract class VoxelObjectModifier
    {
        public abstract void Apply(VoxelAsset target);
    }
}