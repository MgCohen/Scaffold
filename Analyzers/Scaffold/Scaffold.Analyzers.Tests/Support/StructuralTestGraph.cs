using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Scaffold.Analyzers.Tests;

internal sealed class StructuralTestGraph
{
    private readonly Dictionary<string, StructuralAssemblyNodeBuilder> assemblies = new(StringComparer.OrdinalIgnoreCase);
    private readonly string rootAssemblyName;

    private StructuralTestGraph(string rootAssemblyName)
    {
        this.rootAssemblyName = rootAssemblyName;
    }

    public static StructuralTestGraph Create(string rootAssemblyName)
    {
        if (string.IsNullOrWhiteSpace(rootAssemblyName))
        {
            throw new ArgumentException("Root assembly name is required.", nameof(rootAssemblyName));
        }

        var graph = new StructuralTestGraph(rootAssemblyName);
        graph.Assembly(rootAssemblyName);
        return graph;
    }

    public StructuralAssemblyNodeBuilder Assembly(string assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            throw new ArgumentException("Assembly name is required.", nameof(assemblyName));
        }

        if (!assemblies.TryGetValue(assemblyName, out var node))
        {
            node = new StructuralAssemblyNodeBuilder(this, assemblyName);
            assemblies[assemblyName] = node;
        }

        return node;
    }

    public StructuralTestGraph Build()
    {
        var builtAssemblies = assemblies.Values.Select(node => node.BuildNode()).ToImmutableArray();
        if (!builtAssemblies.Any(node => string.Equals(node.Name, rootAssemblyName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Root assembly '{rootAssemblyName}' is not declared.");
        }

        return new StructuralTestGraph(rootAssemblyName)
        {
            Model = new StructuralTestGraphModel(rootAssemblyName, builtAssemblies)
        };
    }

    internal StructuralTestGraphModel Model { get; private set; } = new(string.Empty, ImmutableArray<StructuralAssemblyNode>.Empty);

    internal sealed class StructuralAssemblyNodeBuilder
    {
        private readonly StructuralTestGraph owner;
        private readonly HashSet<string> references = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<StructuralSourceFile> sourceFiles = new();

        internal StructuralAssemblyNodeBuilder(StructuralTestGraph owner, string assemblyName)
        {
            this.owner = owner;
            Name = assemblyName;
        }

        public string Name { get; }

        public StructuralAssemblyNodeBuilder References(params string[] assemblyNames)
        {
            foreach (var assemblyName in assemblyNames ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(assemblyName))
                {
                    references.Add(assemblyName);
                }
            }

            return this;
        }

        public StructuralAssemblyNodeBuilder WithSource(string path, string source)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Source path is required.", nameof(path));
            }

            sourceFiles.Add(new StructuralSourceFile(path.Replace('\\', '/'), source ?? string.Empty));
            return this;
        }

        public StructuralAssemblyNodeBuilder WithSources(params StructuralSourceFile[] files)
        {
            foreach (var file in files ?? Array.Empty<StructuralSourceFile>())
            {
                sourceFiles.Add(new StructuralSourceFile(file.Path.Replace('\\', '/'), file.Content ?? string.Empty));
            }

            return this;
        }

        public StructuralAssemblyNodeBuilder Assembly(string assemblyName) => owner.Assembly(assemblyName);

        public StructuralTestGraph Build() => owner.Build();

        internal StructuralAssemblyNode BuildNode()
        {
            if (sourceFiles.Count == 0)
            {
                throw new InvalidOperationException($"Assembly '{Name}' must declare at least one source file.");
            }

            return new StructuralAssemblyNode(Name, references.ToImmutableArray(), sourceFiles.ToImmutableArray());
        }
    }
}

internal sealed record StructuralTestGraphModel(string RootAssemblyName, ImmutableArray<StructuralAssemblyNode> Assemblies);

internal sealed record StructuralAssemblyNode(
    string Name,
    ImmutableArray<string> References,
    ImmutableArray<StructuralSourceFile> SourceFiles);

internal sealed record StructuralSourceFile(string Path, string Content);
