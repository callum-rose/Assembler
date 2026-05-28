using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelToolkit.Editor
{
    [CustomEditor(typeof(MagicaVoxelImporter), true)]
    public class MagicaVoxelImporterEditor : ScriptedImporterEditor
    {
        private ReorderableList animationsList;
        private ReorderableList chunksList;
        private HashSet<string> presentEntries = new HashSet<string>();
        
        public override void OnInspectorGUI()
        {
            presentEntries.Clear();
            var rootProperties = serializedObject.FindProperty("animationRoots");
            for (var index = 0; index < rootProperties.arraySize; index++)
                presentEntries.Add(rootProperties.GetArrayElementAtIndex(index).FindPropertyRelative("rootPath").stringValue);
            
            EditorGUILayout.HelpBox(
                "Please note that in order for the asset to store objects, it's better to name items inside .vox files with unique names as Magica Voxel doesn't provide any stable ID for us to use.",
                MessageType.Info);

            serializedObject.Update();
            using (new GroupScope("Edge shift options:"))
            {
                DrawProperty("opaqueEdgeShift");
                DrawProperty("transparentEdgeShift");
            }

            using (new GroupScope("File import options:"))
            {
                DrawProperty("indexFormat");
                DrawProperty("generateColliders");
                DrawProperty("generateLightmapUV");
                DrawProperty("remapVoxelIndices");
                DrawProperty("generationMode");
                var meshGenerationApproachValue = serializedObject.FindProperty("meshGenerationApproach").intValue;

                DrawProperty("meshGenerationApproach");
                if (meshGenerationApproachValue == (int)MeshGenerationApproach.Textured)
                {
                    DrawProperty("materialPropertiesEmbeddingMode");
                    DrawProperty("textureOptimizationMode");
                }
            }

            using (new GroupScope("Mesh generation options:"))
            {
                DrawProperty("originMode");
                DrawProperty("chunkSize");
                DrawProperty("scale");
            }

            using (new GroupScope("Visual enhancments:"))
            {
                DrawProperty("AOStrength");
                DrawProperty("hueShift");
                DrawProperty("brightness");
                DrawProperty("saturation");
                DrawProperty("emissionScaleFactor");
            }

            using (new GroupScope("Material overrides:"))
            {
                DrawProperty("opaqueMaterial");
                DrawProperty("transparentMaterial");
            }

            using (new GroupScope("Modifiers:"))
            {
                DrawProperty("modifiers");
            }

            if (!serializedObject.isEditingMultipleObjects)
            {
                using (new GroupScope("Animation"))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("animationFrameDuration"));
                    
                    EditorGUILayout.HelpBox("Please note that only entries with names assigned in Magica Voxel will be visible here", MessageType.Info);

                    DrawHierarchyList(ref animationsList, ref chunksList, rootProperties, presentEntries, path =>
                    {
                        var entryIndex = rootProperties.arraySize;
                        rootProperties.InsertArrayElementAtIndex(entryIndex);
                        var settings = rootProperties.GetArrayElementAtIndex(entryIndex);
                        settings.FindPropertyRelative("rootPath").stringValue = path.Replace("\\", "\\\\");
                        settings.FindPropertyRelative("looped").boolValue = true;
                        var chunks = settings.FindPropertyRelative("chunks");
                        chunks.ClearArray();
                        chunks.InsertArrayElementAtIndex(0);
                        var firstAnimation = chunks.GetArrayElementAtIndex(0);
                        firstAnimation.FindPropertyRelative("duration").intValue = int.MaxValue;
                        firstAnimation.FindPropertyRelative("loop").boolValue = true;
                        firstAnimation.FindPropertyRelative("name").stringValue = "Full animation";

                        serializedObject.ApplyModifiedProperties();
                    });
                }
            }
            else
            {
                using (new GroupScope("Animation"))
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("animationFrameDuration"));
                }
            }

            serializedObject.ApplyModifiedProperties();
            base.ApplyRevertGUI();
        }


        private void DrawHierarchyList(ref ReorderableList list, ref ReorderableList chunkList, SerializedProperty property,
            HashSet<string> presentEntries, Action<string> add)
        {
            if (list == null)
            {
                list = new ReorderableList(serializedObject, property);
                list.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, new GUIContent("Animation roots"));

                list.onAddDropdownCallback = (Rect buttonRect, ReorderableList l) =>
                {
                    var menu = new GenericMenu();
                    var importer = new VoxelToolkit.MagicaVoxelImporter();
                    var path = AssetDatabase.GetAssetPath(serializedObject.targetObject);
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        using (var reader = new BinaryReader(stream))
                        {
                            var asset = importer.ImportAsset(reader);
                            var root = asset.HierarchyRoot;
                            AddEntries(root, menu, string.Empty, presentEntries, true, add);

                            menu.ShowAsContext();
                        }
                    }
                };

                list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (index < 0 || index >= property.arraySize)
                        return;

                    var position = rect;
                    position.height = EditorGUIUtility.singleLineHeight;
                    var element = property.GetArrayElementAtIndex(index);
                    EditorGUI.LabelField(position,
                        new GUIContent(element.FindPropertyRelative("rootPath").stringValue.Replace("\r", "/")));

                    position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    EditorGUI.PropertyField(position, element.FindPropertyRelative("looped"));

                    position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                };

                list.elementHeight = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
                list.elementHeightCallback = (int index) =>
                {
                    return (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
                };
            }

            list.DoLayoutList();
            if (list.index < 0 || list.index >= list.serializedProperty.arraySize)
                return;

            var selectedEntry = list.serializedProperty.GetArrayElementAtIndex(list.index);
            var selected = selectedEntry.FindPropertyRelative("chunks");
            if (chunkList == null || chunkList.serializedProperty.propertyPath != selected.propertyPath) 
            {
                chunkList = new ReorderableList(serializedObject, selected);

                var name = selectedEntry.FindPropertyRelative("rootPath");
                chunkList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, new GUIContent($"Animation chunks of {name.stringValue.Replace("\r", "/")}"));
                
                chunkList.onAddCallback = (ReorderableList l) =>
                {
                    var index = l.serializedProperty.arraySize;
                    l.serializedProperty.InsertArrayElementAtIndex(index);
                    var entry = l.serializedProperty.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("name").stringValue = "Animation";
                    entry.FindPropertyRelative("startFrame").intValue = 0;
                    entry.FindPropertyRelative("duration").intValue = 60;
                    entry.FindPropertyRelative("loop").boolValue = true;
                };

                chunkList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    var position = rect;
                    position.y += EditorGUIUtility.standardVerticalSpacing;
                    position.height = EditorGUIUtility.singleLineHeight;

                    var chunk = selectedEntry.FindPropertyRelative("chunks").GetArrayElementAtIndex(index);
                    var nameProperty = chunk.FindPropertyRelative("name");
                    EditorGUI.DelayedTextField(position, nameProperty);
                    if (string.IsNullOrEmpty(nameProperty.stringValue))
                        nameProperty.stringValue = "Animation";

                    foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
                        nameProperty.stringValue = nameProperty.stringValue.Replace(invalidCharacter.ToString(), string.Empty);

                    position.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    var timePosition = position;
                    timePosition.width /= 6;
                    
                    var duration = chunk.FindPropertyRelative("duration");
                    if (duration.intValue != int.MaxValue)
                    {
                        EditorGUI.LabelField(timePosition, "  Start frame");
                        timePosition.x += timePosition.width;

                        var startFrame = chunk.FindPropertyRelative("startFrame");
                        EditorGUI.PropertyField(timePosition, startFrame, GUIContent.none);
                        if (startFrame.intValue < 0)
                            startFrame.intValue = 0;

                        timePosition.x += timePosition.width;
                    }
                    
                    if (duration.intValue == int.MaxValue)
                        EditorGUI.LabelField(timePosition, "Full range");
                    else
                    {
                        EditorGUI.LabelField(timePosition, "  Length");
                        timePosition.x += timePosition.width;
                        
                        EditorGUI.PropertyField(timePosition, duration, GUIContent.none);
                        if (duration.intValue <= 0)
                            duration.intValue = 1;
                        
                        timePosition.x += timePosition.width;

                        EditorGUI.LabelField(timePosition, "  Loop");
                        timePosition.x += timePosition.width;
                        
                        var loop = chunk.FindPropertyRelative("loop");
                        EditorGUI.PropertyField(timePosition, loop, GUIContent.none);
                    }
                };
                
                chunkList.elementHeight = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2;
            }
            
            chunkList.DoLayoutList();
        }

        private void AddEntries(HierarchyNode node, GenericMenu menu, string path, HashSet<string> presentEntries,
            bool isRoot, Action<string> add)
        {
            var entryName = isRoot ? "Root" : string.Empty;
            
            if (!isRoot && node is INamedObject namedObject)
            {
                if (string.IsNullOrEmpty(namedObject.Name))
                    return;

                entryName = namedObject.Name;
            }

            if (node is Group group)
            {
                for (var index = 0; index < group.ChildrenCount; index++)
                    AddEntries(group[index], menu, path, presentEntries, false, add);
            }
            else if (node is Transformation transformation)
            {
                AddEntries(transformation.Child, menu, string.IsNullOrEmpty(path) ? entryName : path + "\r" + entryName,
                    presentEntries, false, add);

                if (!string.IsNullOrEmpty(path))
                {
                    menu.AddItem(new GUIContent((path + " [Group]").Replace("\r", "/")), presentEntries.Contains(path),
                        presentEntries.Contains(path) ? null : () => add(path));
                }
                else if (isRoot)
                {
                    menu.AddItem(new GUIContent((entryName + " [Group]").Replace("\r", "/")), presentEntries.Contains(entryName),
                        presentEntries.Contains(entryName) ? null : () => add(entryName));
                } 
            }
            else if (node is Shape)
            {
                menu.AddItem(new GUIContent(path.Replace("\r", "/")), presentEntries.Contains(path),
                    presentEntries.Contains(path) ? null : () => add(path));
            }
        }

        private struct GroupScope : IDisposable
        {
            public GroupScope(string name)
            {
                EditorGUILayout.LabelField(new GUIContent(name));
                EditorGUI.indentLevel++;
            }

            public void Dispose()
            {
                EditorGUI.indentLevel--;
            }
        }

        private void DrawProperty(string propertyName)
        {
            var property = serializedObject.FindProperty(propertyName);
            EditorGUILayout.PropertyField(property);
        }
    }
}