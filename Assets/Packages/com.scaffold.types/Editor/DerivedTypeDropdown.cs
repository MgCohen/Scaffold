using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Scaffold.Types.Editor
{
    public class DerivedTypeDropdown
    {
        public DerivedTypeDropdown(Type targetType, Type currentType = null)
        {
            subTypes = new List<Type>();
            if (!targetType.IsAbstract)
            {
                subTypes.Add(targetType);
            }

            IEnumerable<Type> foundClasses = TypeCache.GetTypesDerivedFrom(targetType);
            var validTypes = foundClasses.Where(t => !t.ContainsGenericParameters);
            var validTypesList = validTypes.ToList();
            subTypes.AddRange(validTypesList);
            subTypeNames = subTypes.Select(x => x.Name).ToArray();
            selectedIndex = subTypes.IndexOf(currentType);
        }

        public Type SelectedType
        {
            get
            {
                var safeIndex = Math.Max(0, selectedIndex);
                return subTypes.ElementAtOrDefault(safeIndex);
            }
        }

        private readonly List<Type> subTypes;
        private readonly string[] subTypeNames;
        private int selectedIndex = -1;

        public void RefreshSelection(Type currentType)
        {
            selectedIndex = subTypes.IndexOf(currentType);
        }

        public bool ChangeCheck(Rect position)
        {
            int oldIndex = selectedIndex;
            var safeIndex = Math.Max(0, selectedIndex);
            int newIndex = EditorGUI.Popup(position, safeIndex, subTypeNames);
            selectedIndex = newIndex;
            return oldIndex != newIndex;
        }

        public object CreateInstance(object oldValue = null)
        {
            return Activator.CreateInstance(SelectedType);
        }
    }
}


