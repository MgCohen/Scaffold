using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Scaffold.MVVM.Binding
{
    internal class BindGroups
    {
        private Dictionary<string, BindGroup> groups = new();

        internal void Register(string path, IBindContext context)
        {
            BindingPath bindPath = BindingPath.Create(path);
            Register(bindPath, context);
        }

        private void Register(BindingPath path, IBindContext binding)
        {
            while (path != null)
            {
                AddtoLookup(path, binding);
                path = path.Child;
            }
        }

        private void AddtoLookup(BindingPath path, IBindContext binding)
        {
            BindGroup group = GetGroup(path);
            group.Bind(binding);
        }

        private BindGroup GetGroup(BindingPath path)
        {
            return GetGroup(path.Path);
        }

        internal BindGroup GetGroup(string path)
        {
            if (!groups.TryGetValue(path, out BindGroup group))
            {
                group = new BindGroup();
                groups[path] = group;
            }
            return group;
        }

        internal void Clear()
        {
            groups.Clear();
        }
    }
}
