﻿using CommandLine;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.Toolkit.Plugins.Core.Packaging.Metadata;
using Microsoft.Performance.Toolkit.Plugins.Core.Serialization;
using Microsoft.Performance.Toolkit.Plugins.Runtime.Package;
using SystemVersion = System.Version;

namespace Microsoft.Performance.Toolkit.Plugins.Cli.Options
{
    [Verb("pack", HelpText = $"Creates a new {Constants.PluginPackageExtension} package using specified metadata and source directory.")]
    internal class PackOptions
    {
        [Option(
            's',
            "source",
            Required = true,
            HelpText = "The directory containing the plugin binaries.")]
        public string? SourceDirectory { get; set; }

        [Option(
            't',
            "target",
            Required = false,
            HelpText = $"Directory where the {Constants.PluginPackageExtension} file will be created. If not specified, the current directory will be used.")]
        public string? TargetDirectory { get; set; } = Directory.GetCurrentDirectory();

        [Option(
            'm',
            "metadata",
            Required = false,
            HelpText = $"Path to the {PackageConstants.PluginMetadataFileName} file. If not specified, the metadata will be generated from the binaries in the source directory.")]

        public string? MetadataPath { get; set; }

        [Option(
            "id",
            Required = true,
            HelpText = "Id of the packed plugin.")]
        public string? Id { get; set; }

        [Option(
            'v',
            "version",
            Required = true,
            HelpText = "Version of the packed plugin. Must be a valid System.Version string.")]
        public string? Version { get; set; }

        [Option(
            "displayName",
            Required = false,
            HelpText = "Display name of the packed plugin")]
        public string? PluginDisplayName { get; set; }

        [Option(
            "description",
        Required = false,
            HelpText = "Description of the packed plugin")]
        public string? PluginDescription { get; set; }

        public int Run(IMetadataGenerator metadataGenerator)
        {
            if (!Directory.Exists(this.SourceDirectory))
            {
                Console.Error.WriteLine($"The source directory '{this.SourceDirectory}' does not exist.");
                return 1;
            }

            PluginMetadata? metadata = null;
            ISerializer<PluginMetadata> serializer = SerializationUtils.GetJsonSerializerWithDefaultOptions<PluginMetadata>();

            if (!string.IsNullOrEmpty(this.MetadataPath))
            {
                if (!File.Exists(this.MetadataPath))
                {
                    Console.Error.WriteLine($"The metadata file '{this.MetadataPath}' does not exist.");
                    return 1;
                }

                try
                {
                    string path = Path.GetFullPath(this.MetadataPath);
                    using (FileStream stream = File.OpenRead(path))
                    {
                        metadata = serializer.Deserialize(stream);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to read metadata file '{this.MetadataPath}': {ex.Message}");
                    return 1;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(this.Id) || string.IsNullOrEmpty(this.Version))
                {
                    Console.Error.WriteLine("When no metadata file is specified, both id and version must be specified.");
                    return 1;
                }
            }

            if (metadataGenerator.TryCreateMetadata(this.SourceDirectory, out PluginMetadataInit? metadataInit))
            {
                Console.Error.WriteLine("Failed to extract metadata from assembly.");
                return 1;
            }

            //// Override metadata with command line arguments if specified
            //if (!string.IsNullOrEmpty(this.Id))
            //{
            //    metadataInit.Id = this.Id;
            //}

            //if (!string.IsNullOrEmpty(this.Version))
            //{
            //    if (!SystemVersion.TryParse(this.Version, out SystemVersion? version))
            //    {
            //        Console.Error.WriteLine($"The version '{this.Version}' is not a valid System.Version string.");
            //        return 1;
            //    }

            //    metadataInit.Version = version;
            //}

            //if (!string.IsNullOrEmpty(this.PluginDisplayName))
            //{
            //    metadataInit.DisplayName = this.PluginDisplayName;
            //}

            //if (!string.IsNullOrEmpty(this.PluginDescription))
            //{
            //    metadataInit.Description = this.PluginDescription;
            //}

            //if (!MetadataGenerator.TryExtractFromAssembly(this.SourceDirectory, metadataInit))
            //{
            //    Console.Error.WriteLine("Failed to extract metadata from assembly.");
            //    return 1;
            //}

            metadata = metadataInit.ToPluginMetadata();
            string relativePackageFileName = $"{metadata.Identity}{Constants.PluginPackageExtension}";
            string targetFileName = Path.Combine(this.TargetDirectory ?? Environment.CurrentDirectory, relativePackageFileName);

            string tmpPath = Path.GetTempFileName();
            using (var builder = new PluginPackageBuilder(tmpPath, serializer))
            {
                builder.AddMetadata(metadata);
                builder.AddContent(this.SourceDirectory, CancellationToken.None);
            }

            string? targetDirectory = Path.GetDirectoryName(targetFileName);
            Directory.CreateDirectory(targetDirectory);

            File.Delete(targetFileName);
            File.Move(tmpPath, targetFileName);

            Console.WriteLine($"{metadata.Identity} packed to {targetFileName}");

            return 0;
        }
    }
}