using System;
using UnityEngine;

namespace VoxelToolkit
{
    /// <summary>
    /// Handles loading of some model to the dynamic voxel object
    /// </summary>
    [RequireComponent(typeof(DynamicVoxelObject))]
    [ExecuteAlways]
    public class DynamicVoxelObjectModelLoader : MonoBehaviour
    {
        [SerializeField] private Model model;

        private void Awake()
        {
            var dynamicVoxelObject = GetComponent<DynamicVoxelObject>();
            dynamicVoxelObject.Clear();
            OnValidate();
        }

        private void OnEnable()
        {
            OnValidate();
        }

        private void OnDestroy()
        {
            var dynamicVoxelObject = GetComponent<DynamicVoxelObject>();
            dynamicVoxelObject.Clear();
        }

        private void OnValidate()
        {
            var dynamicVoxelObject = GetComponent<DynamicVoxelObject>();
            if (model == null)
                dynamicVoxelObject.Clear();
            else
                dynamicVoxelObject.SetupFromModel(model, model.ParentAsset.Palette);
        }
    }
}