using System;

namespace Scaffold.MVVM.Binding
{
    internal class RegistrationContext<TSource>
    {
        public RegistrationContext(string path, Type sourceType, BindContext<TSource> context)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (sourceType is null)
            {
                throw new ArgumentNullException(nameof(sourceType));
            }

            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Path = path;
            SourceType = sourceType;
            Context = context;
        }

        public string Path { get; }

        public Type SourceType { get; }

        public BindContext<TSource> Context { get; }
    }
}
