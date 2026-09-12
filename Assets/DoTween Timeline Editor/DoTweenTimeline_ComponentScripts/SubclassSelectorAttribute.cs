using System;
using UnityEngine;

namespace Hitwicket.DoTweenTimeline
{
    /// <summary>
    /// Marks a [SerializeReference] field so the inspector can pick a concrete subclass.
    /// Unity-native stand-in for Odin / SerializeReferenceExtensions SubclassSelector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class SubclassSelectorAttribute : PropertyAttribute
    {
    }
}
