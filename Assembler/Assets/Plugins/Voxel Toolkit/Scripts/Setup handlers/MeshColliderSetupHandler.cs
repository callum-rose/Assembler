using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelToolkit
{
    public class MeshColliderSetupHandler : MonoBehaviour, IVoxelObjectPartSetupHandler
    {
        public void SetupVoxelObjectPart(GameObject target, PartInfo partInfo)
        {
            var collider = target.GetComponent<MeshCollider>();
            if (collider == null)
                collider = target.AddComponent<MeshCollider>();

            collider.sharedMesh = partInfo.MeshDescriptor.Mesh;
        }
    }
}