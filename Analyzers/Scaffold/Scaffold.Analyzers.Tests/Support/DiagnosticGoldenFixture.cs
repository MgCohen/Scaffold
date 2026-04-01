using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Scaffold.Analyzers.Tests;

/// <summary>
/// Compares analyzer output to a checked-in baseline (<c>TestData/Golden/*.golden.txt</c>).
/// Optional <c>TestData/Golden/{name}.options.txt</c>: lines <c>key=value</c> for analyzer config.
/// Line <c>__include_unity_engine_reference__=true</c> enables UnityEngine.CoreModule stub reference.
/// </summary>
internal static class DiagnosticGoldenFixture
{
    private const string GoldenSubfolder = "Golden";

    /// <summary>
    /// Formats diagnostics in a stable, reviewable way (sorted by line, column, id, message).
    /// </summary>
    public static string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics, string? diagnosticIdFilter = null)
    {
        IEnumerable<Diagnostic> query = diagnostics;
        if (!string.IsNullOrEmpty(diagnosticIdFilter))
        {
            query = query.Where(d => d.Id == diagnosticIdFilter);
        }

        var lines = query
            .Select(FormatOne)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Runs the analyzer on the golden snippet and returns the formatted baseline text (no file I/O).
    /// </summary>
    public static async Task<string> ComputeGoldenActualAsync(
        string goldenBaseName,
        DiagnosticAnalyzer analyzer,
        string syntheticFilePath,
        string? diagnosticIdFilter = null)
    {
        var options = LoadOptionsIfPresent(goldenBaseName, out var includeUnityRef);
        var source = AnalyzerTestHarness.LoadTestDataText(Path.Combine(GoldenSubfolder, goldenBaseName + ".cs"));
        var diagnostics = await AnalyzerTestHarness.GetDiagnosticsAsync(
            source,
            syntheticFilePath,
            analyzer,
            options,
            includeUnityRef);

        return FormatDiagnostics(diagnostics, diagnosticIdFilter).TrimEnd();
    }

    public static async Task AssertMatchesGoldenAsync(
        string goldenBaseName,
        DiagnosticAnalyzer analyzer,
        string syntheticFilePath,
        string? diagnosticIdFilter = null)
    {
        var actual = await ComputeGoldenActualAsync(goldenBaseName, analyzer, syntheticFilePath, diagnosticIdFilter);
        var expectedPath = Path.Combine(
            Path.GetDirectoryName(typeof(DiagnosticGoldenFixture).Assembly.Location) ?? string.Empty,
            "TestData",
            GoldenSubfolder,
            goldenBaseName + ".golden.txt");
        if (!File.Exists(expectedPath))
        {
            throw new FileNotFoundException("Golden baseline missing: " + expectedPath, expectedPath);
        }

        var expected = NormalizeNewlines(File.ReadAllText(expectedPath)).TrimEnd();
        Assert.Equal(expected, actual);
    }

    private static string NormalizeNewlines(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static string FormatOne(Diagnostic d)
    {
        var message = NormalizeNewlines(d.GetMessage()).Replace("\n", " ").Trim();
        var severity = d.Severity.ToString().ToLowerInvariant();
        if (!d.Location.IsInSource)
        {
            return $"{d.Id}|0|0|{severity}|{message}";
        }

        var span = d.Location.GetLineSpan();
        var line = span.StartLinePosition.Line + 1;
        var col = span.StartLinePosition.Character + 1;
        return $"{d.Id}|{line}|{col}|{severity}|{message}";
    }

    private static Dictionary<string, string>? LoadOptionsIfPresent(string goldenBaseName, out bool includeUnityRef)
    {
        includeUnityRef = false;
        var baseDir = Path.GetDirectoryName(typeof(DiagnosticGoldenFixture).Assembly.Location) ?? string.Empty;
        var path = Path.Combine(baseDir, "TestData", GoldenSubfolder, goldenBaseName + ".options.txt");
        if (!File.Exists(path))
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (string.Equals(key, "__include_unity_engine_reference__", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                includeUnityRef = true;
                continue;
            }

            map[key] = value;
        }

        if (map.Count == 0 && !includeUnityRef)
        {
            return null;
        }

        return map;
    }
}
