using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VoxelToolkit;

namespace VoxelToolkit
{
    public interface IVoxelObjectPreprocessor
    {
        public VoxelObject Preprocess(VoxelObject target);
    }
}