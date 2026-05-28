using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Serialization;

namespace VoxelToolkit.Editor
{
    [ScriptedImporter(13, new[] { "vox" }, importQueueOffset: 4000, AllowCaching = false)]
    public class MagicaVoxelImporter : VoxelImporter
    {
        [Tooltip("The amount of scaling to be applied to the emission values of the voxel materials.")]
        [SerializeField] private float emissionScaleFactor = 1.8f;
        
        [Tooltip("If .vox file uses IMAP chunk which states specific voxel indices, they will be remapped to match the visual look of palette in the Magica Voxel")]
        [SerializeField] private bool remapVoxelIndices = true;
        
        [System.Serializable]
        public class AnimationChunkSettings
        {
            [SerializeField] private string name;
            [SerializeField] private int startFrame;
            [SerializeField] private int duration;
            [SerializeField] private bool loop;

            public string Name => name;
            public int StartFrame => startFrame;
            public int Duration => duration;
            public bool Loop => loop;
        }

        [System.Serializable]
        public class AnimationRootSettings
        {
            [SerializeField] private string rootPath;
            public string RootPath => rootPath;
            
            [SerializeField] private bool looped;
            public bool Looped => looped;

            [SerializeField] private List<AnimationChunkSettings> chunks = new List<AnimationChunkSettings>();
            
            public IReadOnlyList<AnimationChunk> Chunks => chunks.ConvertAll(x => new AnimationChunk(x.Name, x.StartFrame, x.Duration, x.Loop)).AsReadOnly();
        }
        
        [Tooltip("Specifies which items of the asset should be considered as an animation root")]
        [SerializeField] private List<AnimationRootSettings> animationRoots = new List<AnimationRootSettings>();

        [Tooltip("The duration of a frame of animation")]
        [SerializeField, Range(0.001f, 10.0f)] private float animationFrameDuration = 0.25f;
        
        protected override GameObjectGenerationParameters GetGenerationParameters()
        {
            return new GameObjectGenerationParameters(animationRoots.ConvertAll(x => new AnimationProperties(x.RootPath, x.Looped, animationFrameDuration, x.Chunks)));
        }

        protected override VoxelAsset ImportAsset(AssetImportContext ctx)
        {
            using (var stream = new FileStream(ConvertProjectPathToSystemPath(ctx.assetPath), FileMode.Open, FileAccess.Read))
            {
                using (var binaryReader = new BinaryReader(stream, Encoding.Default))
                {
                    var assetImporter = new VoxelToolkit.MagicaVoxelImporter();
                    assetImporter.EmissionScaleFactor = emissionScaleFactor;
                    assetImporter.RemapVoxelIndices = remapVoxelIndices;

                    var asset = assetImporter.ImportAsset(binaryReader);
                    asset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);

                    return asset;
                }
            }
        }
    }
}