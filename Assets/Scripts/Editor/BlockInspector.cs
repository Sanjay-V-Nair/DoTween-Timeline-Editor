using System;
using System.Collections.Generic;
using UnityEditor;

namespace DoTweenTimeline.Editor {
    /// <summary>Optional custom inspector for one TweenBlock type. Blocks without one use the default field drawing.</summary>
    public abstract class BlockDrawer {
        public abstract void OnGUI(SerializedProperty block);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class CustomBlockDrawerAttribute : Attribute {
        public readonly Type BlockType;
        public CustomBlockDrawerAttribute(Type blockType) => BlockType = blockType;
    }

    /// <summary>Single entry point for drawing a block's fields (window details panel, inspector).</summary>
    public static class BlockInspector {
        private static Dictionary<Type, BlockDrawer> _drawers;

        public static void Draw(SerializedProperty block) {
            if (block == null || block.propertyType != SerializedPropertyType.ManagedReference || block.managedReferenceValue == null) return;

            var drawer = FindDrawer(block.managedReferenceValue.GetType());
            if (drawer != null) drawer.OnGUI(block);
            else DrawDefault(block);
        }

        public static void DrawDefault(SerializedProperty block) {
            var iterator = block.Copy();
            var end = block.GetEndProperty();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end)) {
                EditorGUILayout.PropertyField(iterator, true);
                enterChildren = false;
            }
        }

        private static BlockDrawer FindDrawer(Type blockType) {
            _drawers ??= BuildRegistry();
            for (var t = blockType; t != null; t = t.BaseType) {
                if (_drawers.TryGetValue(t, out var drawer)) return drawer;
            }
            return null;
        }

        private static Dictionary<Type, BlockDrawer> BuildRegistry() {
            var map = new Dictionary<Type, BlockDrawer>();
            foreach (var drawerType in TypeCache.GetTypesWithAttribute<CustomBlockDrawerAttribute>()) {
                if (drawerType.IsAbstract || !typeof(BlockDrawer).IsAssignableFrom(drawerType)) continue;
                var attr = (CustomBlockDrawerAttribute)Attribute.GetCustomAttribute(drawerType, typeof(CustomBlockDrawerAttribute));
                map[attr.BlockType] = (BlockDrawer)Activator.CreateInstance(drawerType);
            }
            return map;
        }
    }
}
