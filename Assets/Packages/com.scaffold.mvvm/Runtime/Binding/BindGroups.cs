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
            while (bindPath != null)
            {
                BindGroup group = GetGroup(bindPath.Path);
                group.Bind(context);
                bindPath = bindPath.Child;
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

        internal void Unregister(string path, IBindContext context)
        {
            if (path is null)
{
    throw new ArgumentNullException(nameof(path));
}
            if (context is null)
{
    throw new ArgumentNullException(nameof(context));
}
            BindingPath bindPath = BindingPath.Create(path);
            UnregisterPath(bindPath, context);
        }

        private void UnregisterPath(BindingPath bindPath, IBindContext context)
        {
            while (bindPath != null)
            {
                UnregisterFromGroup(bindPath.Path, context);
                bindPath = bindPath.Child;
            }
        }

        private void UnregisterFromGroup(string path, IBindContext context)
        {
            if (!groups.TryGetValue(path, out BindGroup group))
            {
                return;
            }
            group.Unbind(context);
            if (group.IsEmpty) groups.Remove(path);
        }

        internal void Clear()
        {
            groups.Clear();
        }
    }
}
