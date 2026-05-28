using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VoxelToolkit.Demo
{
    [RequireComponent(typeof(DynamicVoxelObject))]
    public class ExplodeVoxelDemoParticles : MonoBehaviour
    {
        [SerializeField] private VoxelAsset asset;
        
        private void Update()
        {
            var voxelObject = GetComponent<DynamicVoxelObject>();
            if (!Input.GetMouseButtonDown(0))
                return;

            var camera = Camera.main;
            var ray = camera.ScreenPointToRay(Input.mousePosition);
            
            if (!voxelObject.Raycast(ray, out RaycastHit hit))
                return;
            
            var voxelPosition = hit.Location;

            var particleSystem = GetComponentInChildren<ParticleSystem>();

            var radius = 5;
            var query = new List<VoxelQueryResult>();
            voxelObject.QueryVoxelsInSphere(new Vector3Int(voxelPosition.x, voxelPosition.y, voxelPosition.z), radius, query);

            var palette = asset.Palette;
            foreach (var result in query)
            {
                var parameters = new ParticleSystem.EmitParams();
                parameters.startColor = palette[result.Voxel.Material].color;
                parameters.position = voxelObject.TransformVoxelToLocal(result.Position);
                parameters.velocity = Random.insideUnitSphere * Random.Range(0.5f, 1.0f);
                parameters.angularVelocity3D = Random.insideUnitSphere * Random.Range(0.0f, 180.0f);
                
                particleSystem.Emit(parameters, 1);
            }
            
            voxelObject.SetSphere(new Vector3Int(voxelPosition.x, voxelPosition.y, voxelPosition.z), radius, new Voxel());
        }
    }
}