using System;
using System.Collections.Generic;

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

        internal void Unregister(string path, IBindContext context)
        {
            if (path is null) { throw new ArgumentNullException(nameof(path)); }
            if (context is null) { throw new ArgumentNullException(nameof(context)); }
            BindingPath bindPath = BindingPath.Create(path);
            while (bindPath != null)
            {
                RemoveFromLookup(bindPath.Path, context);
                bindPath = bindPath.Child;
            }
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

        private void RemoveFromLookup(string path, IBindContext context)
        {
            if (groups.TryGetValue(path, out BindGroup group) == false) { return; }
            group.Unbind(context);
            if (group.IsEmpty)
            {
                groups.Remove(path);
            }
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

