using System;

namespace Scaffold.GraphFlow.PackageGenerator
{
    static class RunnerStem
    {
        internal static string FromTypeName(string runnerTypeName)
        {
            const string suffix = "Runner";
            if (runnerTypeName.EndsWith(suffix, StringComparison.Ordinal) && runnerTypeName.Length > suffix.Length)
            {
                return runnerTypeName.Substring(0, runnerTypeName.Length - suffix.Length);
            }

            return runnerTypeName;
        }
    }
}
