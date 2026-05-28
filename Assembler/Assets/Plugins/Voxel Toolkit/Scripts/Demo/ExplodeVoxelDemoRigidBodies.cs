using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace VoxelToolkit.Demo
{
    [RequireComponent(typeof(DynamicVoxelObject))]
    public class ExplodeVoxelDemoRigidBodies : MonoBehaviour
    {
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int Alpha = Shader.PropertyToID("_Alpha");
        [SerializeField] private VoxelAsset asset;

        private UnityEngine.Material opaqueMaterial;
        private UnityEngine.Material transparentMaterial;

        private UnityEngine.Material OpaqueOpaqueMaterial
        {
            get
            {
                if (opaqueMaterial != null)
                    return opaqueMaterial;

                opaqueMaterial = Resources.Load<UnityEngine.Material>("Voxel Toolkit/Materials/XRP/UnitOpaque");
                return opaqueMaterial;
            }
        }
        
        private UnityEngine.Material TransparentOpaqueMaterial
        {
            get
            {
                if (transparentMaterial != null)
                    return transparentMaterial;

                transparentMaterial = Resources.Load<UnityEngine.Material>("Voxel Toolkit/Materials/XRP/UnitTransparent");
                return transparentMaterial;
            }
        }
        
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

            var radius = 5;
            var query = new List<VoxelQueryResult>();
            voxelObject.QueryVoxelsInSphere(new Vector3Int(voxelPosition.x, voxelPosition.y, voxelPosition.z), radius, query);

            var palette = asset.Palette;
            foreach (var result in query)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var rigidBody = part.AddComponent<Rigidbody>();
                var renderer = part.GetComponent<Renderer>();
                var voxelMaterial = palette[result.Voxel.Material];
                renderer.material = new UnityEngine.Material(voxelMaterial.materialType == MaterialType.Transparent ? TransparentOpaqueMaterial : OpaqueOpaqueMaterial);
                renderer.material.color = voxelMaterial.color.ApplyTransformation(asset.HueShift, asset.Saturation, asset.Brightness);
                renderer.material.SetFloat(Metallic, 1.0f - voxelMaterial.Plastic);
                renderer.material.SetFloat(Smoothness, 1.0f - voxelMaterial.Roughness);
                renderer.material.SetFloat(Alpha, voxelMaterial.color.a);
                part.transform.position = voxelObject.TransformVoxelToWorld(result.Position);
                rigidBody.linearVelocity = Random.insideUnitSphere * Random.Range(0.5f, 1.0f);
                rigidBody.angularVelocity = Random.insideUnitSphere * Random.Range(0.0f, 180.0f);
                rigidBody.transform.localScale = Vector3.one * voxelObject.Scale;
            }
            
            voxelObject.SetSphere(new Vector3Int(voxelPosition.x, voxelPosition.y, voxelPosition.z), radius, new Voxel());
        }
    }
}