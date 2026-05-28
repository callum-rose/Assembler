using UnityEngine;

namespace VoxelToolkit.Demo
{
    [RequireComponent(typeof(DynamicVoxelObject))]
    public class ModifyObjectInRuntimeDemo : MonoBehaviour
    {
        [SerializeField] private Transform target;
        
        private void Update()
        {
            var voxelObject = GetComponent<DynamicVoxelObject>();
            
            voxelObject.SetSphere(voxelObject.TransformWorldToVoxel(target.position), 15, new Voxel());
        }
    }
}