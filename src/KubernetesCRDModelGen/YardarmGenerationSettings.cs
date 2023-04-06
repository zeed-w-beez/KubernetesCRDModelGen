﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using NuGet.Packaging.Core;

namespace KubernetesCRDModelGen
{
    public class YardarmGenerationSettings
    {
        private Stream? _dllOutput;
        private Stream? _pdbOutput;
        private Stream? _xmlDocumentationOutput;

        private readonly List<YardarmExtension> _extensions = new List<YardarmExtension>();
        private readonly List<Action<ILoggingBuilder>> _loggingBuilders = new List<Action<ILoggingBuilder>>();

        public string AssemblyName { get; set; } = "Yardarm.Sdk";
        public string RootNamespace { get; set; } = "Yardarm.Sdk";

        public Version Version { get; set; } = new Version(1, 0, 0);
        public string? VersionSuffix { get; set; }
        public string Author { get; set; } = "anonymous";
        public string Description { get; set; } = "Description";

        public RepositoryMetadata? Repository { get; set; }

        /// <summary>
        /// Base path for generated source files. Files are not persisted to this path,
        /// but it is used within Roslyn to uniquely identify source files.
        /// </summary>
        public string BasePath { get; set; } = AppContext.BaseDirectory;

        /// <summary>
        /// If true, embed generated source code in the symbols PDB file and package. This
        /// enables stepping into the generated SDK when debugging.
        /// </summary>
        public bool EmbedAllSources { get; set; }

        public Stream DllOutput
        {
            get => _dllOutput ??= new MemoryStream();
            set => _dllOutput = value ?? throw new ArgumentNullException(nameof(value));
        }

        public Stream PdbOutput
        {
            get => _pdbOutput ??= new MemoryStream();
            set => _pdbOutput = value ?? throw new ArgumentNullException(nameof(value));
        }

        public Stream XmlDocumentationOutput
        {
            get => _xmlDocumentationOutput ??= new MemoryStream();
            set => _xmlDocumentationOutput = value ?? throw new ArgumentNullException(nameof(value));
        }

        public Stream? ReferenceAssemblyOutput { get; set; }

        /// <summary>
        /// Optional intermediate output path. Typically used to store files useful in incremental builds
        /// and NuGet restores, such as project.assets.json.
        /// </summary>
        public string? IntermediateOutputPath { get; set; }

        /// <summary>
        /// Bypass the restore on generation, assume that a restore has already been done using <see cref="IntermediateOutputPath"/>.
        /// </summary>
        public bool NoRestore { get; set; }

        public ImmutableArray<string> TargetFrameworkMonikers { get; set; } = new[] { "netstandard2.0" }.ToImmutableArray();

        public Stream? NuGetOutput { get; set; }

        public Stream? NuGetSymbolsOutput { get; set; }

        public CSharpCompilationOptions CompilationOptions { get; set; } =
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithDeterministic(true)
                .WithOptimizationLevel(OptimizationLevel.Release)
                .WithNullableContextOptions(NullableContextOptions.Enable)
                .WithOverflowChecks(false)
                .WithPlatform(Platform.AnyCpu)
                .WithConcurrentBuild(true)
                .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default)
                .WithSpecificDiagnosticOptions(new KeyValuePair<string, ReportDiagnostic>[]
                {
                    // Don't warn for binding redirects
                    new("CS1701", ReportDiagnostic.Suppress),
                    new("CS1702", ReportDiagnostic.Suppress)
                });

        public YardarmGenerationSettings()
        {
        }

        public YardarmGenerationSettings(string assemblyName)
        {
            AssemblyName = assemblyName;
            RootNamespace = assemblyName;
        }

        public IServiceProvider BuildServiceProvider(OpenApiDocument? document)
        {
            IServiceCollection services = new ServiceCollection()
                .AddLogging(builder =>
                {
                    foreach (var configuredBuilder in _loggingBuilders)
                    {
                        configuredBuilder(builder);
                    }
                });

            services = _extensions.Aggregate(services, (p, extension) => extension.ConfigureServices(p));

            services.AddYardarm(this, document);

            return services.BuildServiceProvider();
        }

        public YardarmGenerationSettings AddExtension(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            if (!typeof(YardarmExtension).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type.FullName} must inherit from YardarmExtension.");
            }

            var constructor = type.GetConstructor(Type.EmptyTypes);
            if (constructor == null)
            {
                throw new ArgumentException($"Type {type.FullName} must have a default constructor.");
            }

            _extensions.Add((YardarmExtension)constructor.Invoke(null));

            return this;
        }

        public YardarmGenerationSettings AddExtension<T>()
            where T : YardarmExtension =>
            AddExtension(typeof(T));

        public YardarmGenerationSettings AddExtension(Assembly assembly)
        {
            foreach (var type in assembly.GetExportedTypes()
                .Where(p => p.IsClass && !p.IsAbstract && typeof(YardarmExtension).IsAssignableFrom(p)))
            {
                AddExtension(type);
            }

            return this;
        }

        public YardarmGenerationSettings AddLogging(Action<ILoggingBuilder> buildAction)
        {
            if (buildAction == null)
            {
                throw new ArgumentNullException(nameof(buildAction));
            }

            _loggingBuilders.Add(buildAction);

            return this;
        }
    }
}
