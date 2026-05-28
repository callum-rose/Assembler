using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelToolkit.Editor
{
    public abstract class GenericListPropertyDrawer<T> : PropertyDrawer where T : class
    {
        private ReorderableList reorderableList;
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (reorderableList == null)
            {
                var values = property.FindPropertyRelative("values");

                reorderableList = new ReorderableList(values.serializedObject, values);

                reorderableList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, label);

                reorderableList.onAddDropdownCallback = (rect, list) =>
                    {
                        var menu = new GenericMenu();

                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.IsAbstract)
                                    continue;
                                
                                if (!type.IsSubclassOf(typeof(T)))
                                    continue;
                                
                                menu.AddItem(new GUIContent(type.Name), false, () =>
                                   {
                                       var index = list.serializedProperty.arraySize;
                                       values.InsertArrayElementAtIndex(index);

                                       list.index = index;

                                       var element = values.GetArrayElementAtIndex(index);
                                       element.managedReferenceValue = Activator.CreateInstance(type);
                                           
                                       values.serializedObject.ApplyModifiedProperties();
                                   });
                            }
                        }
                        
                        menu.ShowAsContext();
                    };

                reorderableList.drawElementCallback = (rect, index, active, focused) =>
                      {
                          if (index < 0 || index >= reorderableList.serializedProperty.arraySize)
                              return;
                          
                          var padding = 12;
                          var position = rect;
                          position.x += padding;
                          position.width -= padding;
                          var element = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                          if (element == null)
                              return;
                          
                          EditorGUI.PropertyField(position, element, new GUIContent(element.managedReferenceFullTypename), true);
                      };

                reorderableList.elementHeightCallback = index =>
                    {
                        if (index < 0 || index >= reorderableList.serializedProperty.arraySize)
                            return EditorGUIUtility.singleLineHeight;
                        
                        var element = reorderableList.serializedProperty.GetArrayElementAtIndex(index);
                        if (element == null)
                            return EditorGUIUtility.singleLineHeight;
                        
                        var height = EditorGUI.GetPropertyHeight(element, new GUIContent(element.managedReferenceFullTypename), true);

                        return height;
                    };
            }
            
            reorderableList.DoList(position);
            property.serializedObject.ApplyModifiedProperties();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return reorderableList?.GetHeight() ?? 0;
        }
    }

    [CustomPropertyDrawer(typeof(ObjectGeneratorsList))]
    public class GeneratorListPropertyDrawer : GenericListPropertyDrawer<VoxelObjectModifier>
    {

    }
}