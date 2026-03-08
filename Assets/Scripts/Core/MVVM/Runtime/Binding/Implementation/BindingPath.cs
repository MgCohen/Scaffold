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
            
            foreach(var cPath in paths)
            {
                builder.Append(cPath);
                var currentPath = builder.ToString();
                child = new BindingPath(currentPath, child);
                builder.Append(".");
            }

            return child;
        }
    }
}
