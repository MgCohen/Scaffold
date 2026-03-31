using System;
using System.Collections.Generic;
using System.Text;

namespace Scaffold.MVVM.Binding
{
    public record BindingPath(string Path, BindingPath Child)
    {
        public static BindingPath Create(string path)
        {
            path = BuildValidatedPath(path);
            string[] paths = path.Split(".");
            return BuildPathChain(paths);
        }

        private static BindingPath CreateStep(StringBuilder builder, string cPath, BindingPath prev)
        {
            builder.Append(cPath);
            var currentPath = builder.ToString();
            builder.Append(".");
            return new BindingPath(currentPath, prev);
        }

        private static BindingPath BuildPathChain(IEnumerable<string> paths)
        {
            BindingPath child = null;
            StringBuilder builder = new StringBuilder();
            foreach (string cPath in paths)
{
    child = CreateStep(builder, cPath, child);
}
            return child;
        }

        private static string BuildValidatedPath(string path)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (path.Length == 0)
            {
                throw new ArgumentException("Path cannot be empty.", nameof(path));
            }
            return path;
        }
    }
}






