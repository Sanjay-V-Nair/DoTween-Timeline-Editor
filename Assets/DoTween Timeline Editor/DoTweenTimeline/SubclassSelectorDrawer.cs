using System;
using System.Collections.Generic;
using Hitwicket.DoTweenTimeline;
using UnityEditor;
using UnityEngine;

namespace Hitwicket.Editor.DoTweenTimeline
{
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, Type[]> TypesByFieldTypename = new Dictionary<string, Type[]>();

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUI.GetPropertyHeight(property, label, true);

            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded || property.managedReferenceValue == null)
                return height;

            return height + EditorGUIUtility.standardVerticalSpacing + GetChildrenHeight(property);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                EditorGUI.EndProperty();
                return;
            }

            Rect foldoutRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
            Rect popupRect = new Rect(
                position.x + EditorGUIUtility.labelWidth,
                position.y,
                position.width - EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);
            DrawTypePopup(popupRect, property);

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                DrawChildren(property, position.x, position.width, ref y);
                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static void DrawTypePopup(Rect rect, SerializedProperty property)
        {
            Type[] types = GetAssignableTypes(property);
            var labels = new string[types.Length + 1];
            labels[0] = "(None)";
            int currentIndex = 0;

            Type currentType = property.managedReferenceValue?.GetType();
            for (int i = 0; i < types.Length; i++)
            {
                labels[i + 1] = NicifyTypeName(types[i]);
                if (types[i] == currentType)
                    currentIndex = i + 1;
            }

            int newIndex = EditorGUI.Popup(rect, currentIndex, labels);
            if (newIndex == currentIndex)
                return;

            if (newIndex == 0)
                property.managedReferenceValue = null;
            else
                property.managedReferenceValue = CreateInstance(types[newIndex - 1], property.managedReferenceValue);
        }

        private static object CreateInstance(Type type, object previousValue)
        {
            object instance = Activator.CreateInstance(type);
            if (previousValue is DoTweenBlock oldBlock && instance is DoTweenBlock newBlock)
            {
                newBlock.blockName = oldBlock.blockName;
                newBlock.startTime = oldBlock.startTime;
                newBlock.duration = oldBlock.duration;
                newBlock.easeType = oldBlock.easeType;
                newBlock.target = oldBlock.target;
                newBlock.isExpanded = oldBlock.isExpanded;
            }

            return instance;
        }

        private static Type[] GetAssignableTypes(SerializedProperty property)
        {
            string fieldTypename = property.managedReferenceFieldTypename;
            if (TypesByFieldTypename.TryGetValue(fieldTypename, out Type[] cached))
                return cached;

            Type baseType = ResolveType(fieldTypename);
            var list = new List<Type>();
            if (baseType != null)
            {
                foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
                {
                    if (IsInstantiatable(type))
                        list.Add(type);
                }

                if (IsInstantiatable(baseType))
                    list.Insert(0, baseType);
            }

            Type[] types = list.ToArray();
            TypesByFieldTypename[fieldTypename] = types;
            return types;
        }

        private static bool IsInstantiatable(Type type)
        {
            return type != null
                   && !type.IsAbstract
                   && !type.IsGenericType
                   && type.IsClass
                   && type.GetConstructor(Type.EmptyTypes) != null;
        }

        private static Type ResolveType(string managedReferenceFieldTypename)
        {
            if (string.IsNullOrEmpty(managedReferenceFieldTypename))
                return null;

            int space = managedReferenceFieldTypename.IndexOf(' ');
            if (space < 0)
                return null;

            string assemblyName = managedReferenceFieldTypename.Substring(0, space);
            string typeName = managedReferenceFieldTypename.Substring(space + 1);
            return Type.GetType($"{typeName}, {assemblyName}");
        }

        private static string NicifyTypeName(Type type)
        {
            string name = type.Name.Replace("DoTween", string.Empty).Replace("Block", string.Empty);
            return string.IsNullOrEmpty(name) ? type.Name : ObjectNames.NicifyVariableName(name);
        }

        private static float GetChildrenHeight(SerializedProperty property)
        {
            float height = 0f;
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                height += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }

            return height;
        }

        private static void DrawChildren(SerializedProperty property, float x, float width, ref float y)
        {
            SerializedProperty iterator = property.Copy();
            SerializedProperty end = property.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
            {
                float height = EditorGUI.GetPropertyHeight(iterator, true);
                EditorGUI.PropertyField(new Rect(x, y, width, height), iterator, true);
                y += height + EditorGUIUtility.standardVerticalSpacing;
                enterChildren = false;
            }
        }
    }
}
