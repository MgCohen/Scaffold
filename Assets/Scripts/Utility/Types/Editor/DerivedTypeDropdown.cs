using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Types.Editor
{
    public class DerivedTypeDropdown
    {
        private readonly List<Type> subTypes;
        private readonly string[] subTypeNames;
        private int selectedIndex = -1;

        public Type SelectedType => subTypes.ElementAtOrDefault(Math.Max(0, selectedIndex));

        public DerivedTypeDropdown(Type targetType, Type currentType = null)
        {
            subTypes = new List<Type>();
            if (!targetType.IsAbstract)
            {
                subTypes.Add(targetType);
            }

            IEnumerable<Type> foundClasses = TypeCache.GetTypesDerivedFrom(targetType);
            var validTypes = foundClasses.Where(t => !t.ContainsGenericParameters);
            subTypes.AddRange(validTypes.ToList());
            subTypeNames = subTypes.Select(x => x.Name).ToArray();
            selectedIndex = subTypes.IndexOf(currentType);
        }

        public void RefreshSelection(Type currentType)
        {
            selectedIndex = subTypes.IndexOf(currentType);
        }

        public bool ChangeCheck(Rect position)
        {
            int oldIndex = selectedIndex;
            int newIndex = EditorGUI.Popup(position, Math.Max(0, selectedIndex), subTypeNames);
            selectedIndex = newIndex;
            return oldIndex != newIndex;
        }

        public object CreateInstance(object oldValue = null)
        {
            return Activator.CreateInstance(SelectedType);
        }
    }
}
