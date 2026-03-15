using System;
using System.Text;

namespace Scaffold.MVVM.Binding
{
    public record BindingPath(string Path, BindingPath Child)
    {
        public static BindingPath Create(string path)
        {
            if (path is null) { throw new ArgumentNullException(nameof(path)); }
            if (path.Length == 0) { throw new ArgumentException("Path cannot be empty.", nameof(path)); }
            string[] paths = path.Split(".");
            BindingPath child = null;
            StringBuilder builder = new StringBuilder();
            foreach (var cPath in paths) { child = CreateStep(builder, cPath, child); }
            return child;
        }

        private static BindingPath CreateStep(StringBuilder builder, string cPath, BindingPath prev)
        {
            builder.Append(cPath);
            var currentPath = builder.ToString();
            builder.Append(".");
            return new BindingPath(currentPath, prev);
        }
    }
}


