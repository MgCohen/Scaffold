using System;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow
{
    public interface IGraphMiddleware
    {
        ValueTask InvokeAsync(MiddlewareContext context, Func<ValueTask> next);
    }
}
