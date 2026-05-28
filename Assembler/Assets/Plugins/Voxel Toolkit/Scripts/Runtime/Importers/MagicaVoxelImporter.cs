using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Pool;
using VoxelToolkit.MagicaVoxel;

namespace VoxelToolkit
{
    /// <summary>
    /// Handles .vox files import
    /// </summary>
	public class MagicaVoxelImporter : VoxelImporter
	{
        /// <summary>
        /// Represents the scaler of the emission factor (1.8 is close to what you see in MagicaVoxel)
        /// </summary>
        public float EmissionScaleFactor { get; set; } = 1.8f;

        /// <summary>
        /// If .vox file uses IMAP chunk which states specific voxel indices, they will be remapped to match the visual look of palette in the Magica Voxel
        /// </summary>
        public bool RemapVoxelIndices { get; set; } = true;

        private void ParseMainChunk(VoxelAsset asset, Reader reader)
        {
            reader.ConsumeString("MAIN");
            var size = reader.ConsumeInt();
            var childrenChunksSize = reader.ConsumeInt();

            if (size != 0)
                throw new VoxelDataReadException("Expected zero size MAIN chunk");

            var models = new List<Model>();
            var hierarchyElements = new List<HierarchyElement>();
            
            SetDefaultPalette(asset);

            var modelID = 0;
            var imap = RemapVoxelIndices ? new Dictionary<byte, byte>() : null;
            while (reader.HasSomethingToRead)
            {
                var name = reader.ConsumeString(4);
                var chunkSize = reader.ConsumeInt();
                var childrenSize = reader.ConsumeInt();
                
                if (name == "nTRN")
                   ParseTransform(hierarchyElements, reader); 
                else if (name == "SIZE")
                    models.Add(ParseModelChunk(asset, reader, ref modelID));
                else if (name == "nGRP")
                    ParseGroup(hierarchyElements, reader);
                else if (name == "nSHP")
                    ParseShape(hierarchyElements, reader);
                else if (name == "LAYR")
                    ParseLayer(asset, reader);
                else if (name == "RGBA")
                    ParsePalette(asset, reader);
                else if (name == "MATL")
                    ParseMaterials(asset, reader);
                else if (name == "META")
                    ParseMeta(asset, reader);
                else if (RemapVoxelIndices && name == "IMAP")
                    ParseIMAP(reader, imap);
                else
                    reader.Skip(chunkSize);
            }

            asset.HierarchyRoot = FoldHierarchy(hierarchyElements, models).AddToAsset(asset, models);

            if (RemapVoxelIndices && imap?.Count == 256)
                asset.RemapMaterials(imap);
        }

        private void ParseIMAP(Reader reader, Dictionary<byte, byte> imap)
        {
            for (var index = 0; index < 256; index++)
            {
                var value = reader.ConsumeByte();
                imap.Add(value, (byte)index);
            }
        }

        private void ParseTransform(List<HierarchyElement> elements, Reader reader)
        {
            var id = reader.ConsumeInt();
            var attributes = reader.ConsumeDictionary();

            var childId = reader.ConsumeInt();
            var reservedId = reader.ConsumeInt();
            if (reservedId != -1)
                throw new VoxelDataReadException($"Expected reserved Id be equal to -1 but got {reservedId}");

            var layerId = reader.ConsumeInt();
            var numberOfFrames = reader.ConsumeInt();
            var frames = new Dictionary<string, string>[numberOfFrames];
            for (var index = 0; index < numberOfFrames; index++)
                frames[index] = reader.ConsumeDictionary();
            
            elements.Add(new TransformElement(id, childId, layerId, frames, attributes));
        }

        private void ParseMeta(VoxelAsset asset, Reader reader)
        {
            var attributes = reader.ConsumeDictionary();
            if (attributes.TryGetValue("_anim_range", out var value))
            {
                var split = value.Split(' ');
                var first = int.Parse(split[0]);
                var second = int.Parse(split[1]);

                asset.AnimationRange = new AnimationRange(first, second);
            }
        }

        private void SetDefaultPalette(VoxelAsset asset)
        {
            var defaultPalette = new uint[] 
                {
                    0x00000000, 0xffffffff, 0xffccffff, 0xff99ffff, 0xff66ffff, 0xff33ffff, 0xff00ffff, 0xffffccff, 0xffccccff, 0xff99ccff, 0xff66ccff, 0xff33ccff, 0xff00ccff, 0xffff99ff, 0xffcc99ff, 0xff9999ff,
                    0xff6699ff, 0xff3399ff, 0xff0099ff, 0xffff66ff, 0xffcc66ff, 0xff9966ff, 0xff6666ff, 0xff3366ff, 0xff0066ff, 0xffff33ff, 0xffcc33ff, 0xff9933ff, 0xff6633ff, 0xff3333ff, 0xff0033ff, 0xffff00ff,
                    0xffcc00ff, 0xff9900ff, 0xff6600ff, 0xff3300ff, 0xff0000ff, 0xffffffcc, 0xffccffcc, 0xff99ffcc, 0xff66ffcc, 0xff33ffcc, 0xff00ffcc, 0xffffcccc, 0xffcccccc, 0xff99cccc, 0xff66cccc, 0xff33cccc,
                    0xff00cccc, 0xffff99cc, 0xffcc99cc, 0xff9999cc, 0xff6699cc, 0xff3399cc, 0xff0099cc, 0xffff66cc, 0xffcc66cc, 0xff9966cc, 0xff6666cc, 0xff3366cc, 0xff0066cc, 0xffff33cc, 0xffcc33cc, 0xff9933cc,
                    0xff6633cc, 0xff3333cc, 0xff0033cc, 0xffff00cc, 0xffcc00cc, 0xff9900cc, 0xff6600cc, 0xff3300cc, 0xff0000cc, 0xffffff99, 0xffccff99, 0xff99ff99, 0xff66ff99, 0xff33ff99, 0xff00ff99, 0xffffcc99,
                    0xffcccc99, 0xff99cc99, 0xff66cc99, 0xff33cc99, 0xff00cc99, 0xffff9999, 0xffcc9999, 0xff999999, 0xff669999, 0xff339999, 0xff009999, 0xffff6699, 0xffcc6699, 0xff996699, 0xff666699, 0xff336699,
                    0xff006699, 0xffff3399, 0xffcc3399, 0xff993399, 0xff663399, 0xff333399, 0xff003399, 0xffff0099, 0xffcc0099, 0xff990099, 0xff660099, 0xff330099, 0xff000099, 0xffffff66, 0xffccff66, 0xff99ff66,
                    0xff66ff66, 0xff33ff66, 0xff00ff66, 0xffffcc66, 0xffcccc66, 0xff99cc66, 0xff66cc66, 0xff33cc66, 0xff00cc66, 0xffff9966, 0xffcc9966, 0xff999966, 0xff669966, 0xff339966, 0xff009966, 0xffff6666,
                    0xffcc6666, 0xff996666, 0xff666666, 0xff336666, 0xff006666, 0xffff3366, 0xffcc3366, 0xff993366, 0xff663366, 0xff333366, 0xff003366, 0xffff0066, 0xffcc0066, 0xff990066, 0xff660066, 0xff330066,
                    0xff000066, 0xffffff33, 0xffccff33, 0xff99ff33, 0xff66ff33, 0xff33ff33, 0xff00ff33, 0xffffcc33, 0xffcccc33, 0xff99cc33, 0xff66cc33, 0xff33cc33, 0xff00cc33, 0xffff9933, 0xffcc9933, 0xff999933,
                    0xff669933, 0xff339933, 0xff009933, 0xffff6633, 0xffcc6633, 0xff996633, 0xff666633, 0xff336633, 0xff006633, 0xffff3333, 0xffcc3333, 0xff993333, 0xff663333, 0xff333333, 0xff003333, 0xffff0033,
                    0xffcc0033, 0xff990033, 0xff660033, 0xff330033, 0xff000033, 0xffffff00, 0xffccff00, 0xff99ff00, 0xff66ff00, 0xff33ff00, 0xff00ff00, 0xffffcc00, 0xffcccc00, 0xff99cc00, 0xff66cc00, 0xff33cc00,
                    0xff00cc00, 0xffff9900, 0xffcc9900, 0xff999900, 0xff669900, 0xff339900, 0xff009900, 0xffff6600, 0xffcc6600, 0xff996600, 0xff666600, 0xff336600, 0xff006600, 0xffff3300, 0xffcc3300, 0xff993300,
                    0xff663300, 0xff333300, 0xff003300, 0xffff0000, 0xffcc0000, 0xff990000, 0xff660000, 0xff330000, 0xff0000ee, 0xff0000dd, 0xff0000bb, 0xff0000aa, 0xff000088, 0xff000077, 0xff000055, 0xff000044,
                    0xff000022, 0xff000011, 0xff00ee00, 0xff00dd00, 0xff00bb00, 0xff00aa00, 0xff008800, 0xff007700, 0xff005500, 0xff004400, 0xff002200, 0xff001100, 0xffee0000, 0xffdd0000, 0xffbb0000, 0xffaa0000,
                    0xff880000, 0xff770000, 0xff550000, 0xff440000, 0xff220000, 0xff110000, 0xffeeeeee, 0xffdddddd, 0xffbbbbbb, 0xffaaaaaa, 0xff888888, 0xff777777, 0xff555555, 0xff444444, 0xff222222, 0xff111111
                };

            for (var index = 0; index < defaultPalette.Length; index++)
            {
                var packed = defaultPalette[index];
                var color = new UnityEngine.Color32(
                    (byte)(packed & 0xFF),         
                    (byte)((packed >> 8) & 0xFF),  
                    (byte)((packed >> 16) & 0xFF), 
                    (byte)((packed >> 24) & 0xFF)  
                );

                var material = asset.GetPaletteMaterial(index);
                material.Color = color;
                asset.SetPaletteMaterial(index, material);
            } 
            
            for (var index = 0; index < 256; index++)
            {
                var material = asset.GetPaletteMaterial(index);

                material.MaterialType = material.Color.a < 0.99f ? 
                    MaterialType.Transparent :
                    MaterialType.Basic;
                
                material.Roughness = 1.0f;
                material.Attenuation = 0.0f;
                material.Specular = 0.0f;
                material.Emit = 0.0f;
                material.Flux = 0.0f;
                material.IOR = 1.0f;
                
                asset.SetPaletteMaterial(index, material);
            }
        }
        
        private void ParsePalette(VoxelAsset asset, Reader reader)
        {
            for (var index = 0; index < 256; index++)
            {
                var r = reader.ConsumeByte();
                var g = reader.ConsumeByte();
                var b = reader.ConsumeByte();
                var a = reader.ConsumeByte();
                
                var color = new UnityEngine.Color32(r, g, b, a);
                
                var material = asset.GetPaletteMaterial(index);
                material.Color = color;
                asset.SetPaletteMaterial(index, material);
            }
        }

        private void ParseMaterials(VoxelAsset asset, Reader reader)
        {
            var id = reader.ConsumeInt() - 1;
            var attributes = reader.ConsumeDictionary();
            var typeName = attributes.ContainsKey("_type") ? attributes["_type"] : "_diffuse";
            var type = MaterialType.Invalid;
            var isDiffuse = typeName == "_diffuse";
                
            var material = asset.GetPaletteMaterial(id);
            if (attributes.TryGetValue("_alpha", out var alpha) && !isDiffuse)
            {
                var color = material.Color;
                color.a = 1.0f - float.Parse(alpha, CultureInfo.InvariantCulture);
                material.Color = color;
            }
                
            if (attributes.TryGetValue("_weight", out var weight) && !isDiffuse)
            {
                var color = material.Color;
                color.a = 1.0f - float.Parse(weight, CultureInfo.InvariantCulture);
                material.Color = color;
            }

            if (isDiffuse || 
                typeName == "_metal" ||
                typeName == "_emit" ||
                typeName == "_plastic")
                type = MaterialType.Basic;
            else if (typeName == "_glass" || 
                     typeName == "_media")
                type = MaterialType.Transparent;
            else if (typeName == "_blend")
                type = material.Color.a < 1.0f ? MaterialType.Transparent : MaterialType.Basic;
            else
                throw new VoxelDataReadException($"Unexpected material type '{type}' '{typeName}'");

            material.MaterialType = type;

            if (attributes.TryGetValue("_rough", out var roughness))
                material.Roughness = isDiffuse ? 1.0f : float.Parse(roughness, CultureInfo.InvariantCulture);

            if (attributes.TryGetValue("_emit", out var emit))
                material.Emit = isDiffuse ? 0.0f : float.Parse(emit, CultureInfo.InvariantCulture) * EmissionScaleFactor;
                
            if (attributes.TryGetValue("_attr", out var attr))
                material.Attenuation = float.Parse(attr, CultureInfo.InvariantCulture);
                
            if (attributes.TryGetValue("_flux", out var flux))
                material.Flux = isDiffuse ? 0.0f : float.Parse(flux, CultureInfo.InvariantCulture) * EmissionScaleFactor;
                
            if (attributes.TryGetValue("_ior", out var ior))
                material.IOR = isDiffuse ? 0.0f : float.Parse(ior, CultureInfo.InvariantCulture);

            if (attributes.TryGetValue("_specular", out var specular))
                material.Specular = isDiffuse ? 0.0f : float.Parse(specular, CultureInfo.InvariantCulture);

            if (attributes.TryGetValue("_plastic", out var plastic))
                material.Plastic = isDiffuse ? 1.0f : float.Parse(plastic, CultureInfo.InvariantCulture);
            else
                material.Plastic = 1.0f;

            if (attributes.TryGetValue("_metal", out var metal))
                material.plastic = 1.0f - float.Parse(metal, CultureInfo.InvariantCulture);
                
            asset.SetPaletteMaterial(id, material);
        }

        private static void ParseLayer(VoxelAsset asset, Reader reader)
        {
            var id = reader.ConsumeInt();
            var attributes = reader.ConsumeDictionary();
            var reservedId = reader.ConsumeInt();
            if (reservedId != -1)
                throw new VoxelDataReadException($"$Reserved id should always be -1 but got {reservedId}");

            var name = attributes.TryGetValue("_name", out string resultName) ? resultName : $"Layer {id}";

            var layer = ScriptableObject.CreateInstance<Layer>();
            layer.name = name;
            layer.Name = name;
            layer.ID = id;
                
            asset.AddLayer(layer);
        }

        private void ParseGroup(List<HierarchyElement> elements, Reader reader)
        {
            var id = reader.ConsumeInt();
            var attributes = reader.ConsumeDictionary();
            var childCount = reader.ConsumeInt();

            var children = new int[childCount];
            for (var index = 0; index < childCount; index++)
                children[index] = reader.ConsumeInt();
            
            elements.Add(new GroupElement(id, attributes, children));
        }
        
        private void ParseShape(List<HierarchyElement> elements, Reader reader)
        {
            var id = reader.ConsumeInt();
            var attributes = reader.ConsumeDictionary();
            var modelsCount = reader.ConsumeInt();

            var children = new ModelReference[modelsCount];
            for (var index = 0; index < modelsCount; index++)
            {
                var modelId = reader.ConsumeInt();
                var modelAttributes = reader.ConsumeDictionary();
                children[index] = new ModelReference(modelId, modelAttributes);
            }

            elements.Add(new ShapeElement(id, attributes, children));
        }

        private HierarchyElement FoldHierarchy(List<HierarchyElement> elements, List<Model> models)
        {
            var tempElements = new List<HierarchyElement>(elements);
            foreach (var hierarchyElement in tempElements)
            {
                if (hierarchyElement is TransformElement transformElement)
                {
                    var child = elements.FindIndex(x => x.Id == transformElement.ChildId);
                    if (child != -1)
                        transformElement.Child = elements[child];
                }
                else if (hierarchyElement is GroupElement groupElement)
                {
                    groupElement.Children = new HierarchyElement[groupElement.ChildrenIds.Length];
                    var childIndex = 0;
                    foreach (var groupElementChildrenId in groupElement.ChildrenIds)
                    {
                        var child = elements.FindIndex(x => x.Id == groupElementChildrenId);
                        if (child != -1)
                        {
                            var item = elements[child];
                            groupElement.Children[childIndex] = item;
                        }

                        childIndex++;
                    }
                }
            }

            if (tempElements.Count != 0)
                return tempElements[0];

            var references = models
                .ConvertAll(x => new ModelReference(x.ID, new Dictionary<string, string>()))
                .ToArray();

            return new ShapeElement(0, new Dictionary<string, string>(), references);
        }

        private Model ParseModelChunk(VoxelAsset asset, Reader reader, ref int id)
        {
            var size = reader.ConsumeVector3IntInt32();
            (size.y, size.z) = (size.z, size.y);
                
            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new VoxelDataReadException($"Expected size larger than zero but got '{size}'");

            var xyziId = reader.ConsumeString(4);
            if (xyziId != "XYZI")
                throw new VoxelDataReadException($"Expected 'XYZI' chunk but got '{xyziId}'");

            var xyziChunkSize = reader.ConsumeInt();
            var xyziChunkChildrenSize = reader.ConsumeInt();

            if (xyziChunkSize < 0)
                throw new VoxelDataReadException("Expected 'XYZI' chunk size more or equal to zero");

            if (xyziChunkChildrenSize != 0)
                throw new VoxelDataReadException("Expected zero 'XYZI' children chunk size");

            var voxelsCount = reader.ConsumeInt();

            var voxelData = new List<VoxelData>();
            for (var index = 0; index < voxelsCount; index++)
            {
                var position = reader.ConsumeVector3IntByte();
                (position.z, position.y) = (position.y, position.z);
                    
                var color = reader.ConsumeByte() - 1;

                voxelData.Add(new VoxelData(position, color));
            }

            var model = ScriptableObject.CreateInstance<Model>();
            model.ID = id++;
            model.Size = size;
            model.SetVoxels(voxelData);
            model.name = $"Model {model.ID}";

            model.ParentAsset = asset;

            return model;
        }

        private void ParseHeader(VoxelAsset asset, Reader reader)
        {
            reader.ConsumeString("VOX ");

            asset.InputSource = "Magica voxel file format";
        }

        private void ParseVersion(VoxelAsset asset, Reader reader)
        {
            var version = reader.ConsumeInt();
            if (version < 150)
                throw new VoxelDataReadException("Expected version 150 but got " + version);

            asset.Version = version.ToString();
        }

        /// <summary>
        /// Imports the asset from a reader
        /// </summary>
        /// <param name="reader">The reader to be used to read the voxel asset</param>
        /// <returns>Voxel asset for the given vox asset</returns>
        public override VoxelAsset ImportAsset(BinaryReader reader)
        {
            var readerHelper = new Reader(reader);
            var asset = ScriptableObject.CreateInstance<VoxelAsset>();

            ParseHeader(asset, readerHelper);
            ParseVersion(asset, readerHelper);
            ParseMainChunk(asset, readerHelper);
            
            asset.HierarchyRoot.name = "Root";

            var counters = DictionaryPool<string, int>.Get();
            counters.Clear();
            Rename(asset.HierarchyRoot, counters);
            
            DictionaryPool<string, int>.Release(counters);
            
            return asset;
        }

        private string GetName(string target, Dictionary<string, int> counters)
        {
            if (!counters.TryGetValue(target, out int count))
            {
                count = 0;
                counters[target] = count;
            }

            var result = $"{target} {count}";
            
            count++;
            counters[target] = count;
            
            return result;
        }
        
        private void Rename(HierarchyNode target, Dictionary<string, int> counters)
        {
            var namedObject = target as INamedObject;

            foreach (var related in target.RelatedObjects)
            {
                if (related is Transformation transformation)
                {
                    var name = !string.IsNullOrEmpty(transformation.Name) ? $"{transformation.Name} Transformation" : "Transformation";
                    transformation.name = GetName(name, counters);
                }

                if (namedObject == null)
                {
                    Rename(related as HierarchyNode, counters);
                    continue;
                }
                
                if (related is Shape shape)
                {
                    shape.name = GetName($"{namedObject.Name} Shape", counters);

                    for (var index = 0; index < shape.Animation.FrameCount; index++)
                    {
                        var model = shape.Animation[index].Value;
                        model.name = GetName($"{namedObject.Name} Model", counters);
                    }
                }

                if (related is Group group)
                {
                    var nameToBeSet = GetName($"{namedObject.Name} Group", counters);
                    group.name = nameToBeSet;
                    group.Name = nameToBeSet;
                }

                Rename(related as HierarchyNode, counters);
            }
        }
    }
}