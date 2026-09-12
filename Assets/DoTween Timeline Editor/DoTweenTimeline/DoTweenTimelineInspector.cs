using System;
using UnityEngine;
using UnityEditor;
using Hitwicket.DoTweenTimeline;

namespace Hitwicket.Editor.DoTweenTimeline
{
    [CustomEditor(typeof(Hitwicket.DoTweenTimeline.DoTweenTimeline))]
    public class DoTweenTimelineInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            GUILayout.Space(4);
            if (GUILayout.Button("Add Block"))
                ShowAddBlockMenu();

            GUILayout.Space(10);
            if (GUILayout.Button("Open Timeline Editor", GUILayout.Height(30)))
            {
                DoTweenTimelineWindow.ShowWindow((Hitwicket.DoTweenTimeline.DoTweenTimeline)target);
            }
        }

        private void ShowAddBlockMenu()
        {
            var menu = new GenericMenu();
            foreach (Type type in TypeCache.GetTypesDerivedFrom<DoTweenBlock>())
            {
                if (type.IsAbstract || type.IsGenericType || type.GetConstructor(Type.EmptyTypes) == null)
                    continue;

                string label = type.Name.Replace("DoTween", string.Empty).Replace("Block", string.Empty);
                menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(label)), false, () => AddBlock(type));
            }

            menu.ShowAsContext();
        }

        private void AddBlock(Type type)
        {
            serializedObject.Update();
            SerializedProperty blocks = serializedObject.FindProperty("blocks");
            blocks.arraySize++;
            blocks.GetArrayElementAtIndex(blocks.arraySize - 1).managedReferenceValue =
                Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
