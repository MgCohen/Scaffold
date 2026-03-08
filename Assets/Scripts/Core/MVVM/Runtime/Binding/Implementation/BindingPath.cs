using System.Text;

namespace Scaffold.MVVM.Binding
{
    public record BindingPath(string Path, BindingPath Child)
    {
        public static BindingPath Create(string path)
        {
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
