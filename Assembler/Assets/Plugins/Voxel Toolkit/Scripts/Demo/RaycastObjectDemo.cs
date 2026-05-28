using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit.Demo
{
    [RequireComponent(typeof(DynamicVoxelObject))]
    public class RaycastObjectDemo : MonoBehaviour
    {
        private Voxel? previousVoxel;
        private int3 previousVoxelPosition;
        
        private void Update()
        {
            var voxelObject = GetComponent<DynamicVoxelObject>();

            var camera = Camera.main;
            var ray = camera.ScreenPointToRay(Input.mousePosition);
            
            if (previousVoxel.HasValue)
                voxelObject[previousVoxelPosition] = previousVoxel.Value;

            previousVoxel = null;
            
            if (!voxelObject.Raycast(ray, out RaycastHit hit))
                return;
            
            previousVoxel = voxelObject[hit.Location];
            previousVoxelPosition = hit.Location;
            voxelObject[hit.Location] = new Voxel(0);
        }
    }
}