﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Performance.SDK;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK.Runtime;
using Microsoft.Performance.SDK.Runtime.NetCoreApp.Discovery;
using Microsoft.Performance.SDK.Runtime.NetCoreApp.Plugins;
using Microsoft.Performance.Toolkit.Plugins.Cli.Manifest;
using Microsoft.Performance.Toolkit.Plugins.Core;
using Microsoft.Performance.Toolkit.Plugins.Core.Metadata;
using NuGet.Versioning;
using LicenseInfo = Microsoft.Performance.Toolkit.Plugins.Core.Metadata.LicenseInfo;
using ProcessingSourceInfo = Microsoft.Performance.Toolkit.Plugins.Core.Metadata.ProcessingSourceInfo;
using ProjectInfo = Microsoft.Performance.Toolkit.Plugins.Core.Metadata.ProjectInfo;

namespace Microsoft.Performance.Toolkit.Plugins.Cli.Processing
{
    /// <summary>
    ///     Processes a plugin's artifacts.
    /// </summary>
    internal class PluginArtifactsProcessor
        : IPluginArtifactsProcessor
    {
        private readonly IManifestFileValidator manifestValidator;
        private readonly IManifestFileReader manifestReader;
        private readonly ILogger<PluginArtifactsProcessor> logger;
        private static readonly string? sdkAssemblyName = SdkAssembly.Assembly.GetName().Name;

        public PluginArtifactsProcessor(
            IManifestFileValidator manifestValidator,
            IManifestFileReader manifestReader,
            ILogger<PluginArtifactsProcessor> logger)
        {
            this.manifestValidator = manifestValidator;
            this.manifestReader = manifestReader;
            this.logger = logger;
        }

        /// <inheritdoc />
        public bool TryProcess(PluginArtifacts artifacts, [NotNullWhen(true)] out ProcessedPluginResult? processedPlugin)
        {
            processedPlugin = null;
            if (!TryProcessSourceDir(artifacts.SourceDirectoryFullPath, out ProcessedPluginSourceDirectory? processedDir))
            {
                this.logger.LogError($"Failed to process source directory {artifacts.SourceDirectoryFullPath}.");
                return false;
            }

            string manifestFilePath = artifacts.ManifestFileFullPath;

            try
            {
                if (!this.manifestValidator.IsValid(manifestFilePath, out List<string> validationMessages))
                {
                    string errors = string.Join(Environment.NewLine, validationMessages);
                    this.logger.LogWarning($"Manifest file failed some json schema format validation checks: \n{errors}");
                    this.logger.LogWarning("Continuing with packing process but it is recommended to fix the validation errors and repack before publishing the plugin.");
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Failed to validate manifest file {manifestFilePath} due to an exception {ex.Message}.");
                return false;
            }

            if (!this.manifestReader.TryRead(manifestFilePath, out PluginManifest? manifest))
            {
                this.logger.LogError($"Failed to read manifest file {manifestFilePath}.");
                return false;
            }

            var versionChecker = new TrackingVersionChecker();
            using PluginsLoader pluginsLoader = new(
                new IsolationAssemblyLoader(),
                x => new SandboxPreloadValidator(x, versionChecker),
                Logger.Create<PluginsLoader>());

            bool loadSuccess = pluginsLoader.TryLoadPlugin(processedDir.FullPath, out ErrorInfo errorInfo);
            if (!loadSuccess)
            {
                // TODO: Check error codes and throw more specific exceptions
                this.logger.LogError($"Failed to load plugin from {processedDir.FullPath}: {errorInfo.Message}");
            }

            if (versionChecker.CheckedVersions.Count == 0)
            {
                this.logger.LogError($"Invalid plugin: {sdkAssemblyName} is not referenced anywhere in the plugin.");
                return false;
            }

            if (versionChecker.CheckedVersions.Count > 1)
            {
                this.logger.LogError($"Mutiple versions of {sdkAssemblyName} are referenced in the plugin: " +
                    $"{string.Join(", ", versionChecker.CheckedVersions.Keys)}. Only one version is allowed.");
                return false;
            }

            (SemanticVersion? pluginSDKversion, bool isVersionSupported) = versionChecker.CheckedVersions.Single();
            if (!isVersionSupported)
            {
                SemanticVersion cliSDKVersion = SdkAssembly.Assembly.GetSemanticVersion();
                if (pluginSDKversion.Major != cliSDKVersion.Major)
                {
                    this.logger.LogError($"Plugin is built against SDK version {pluginSDKversion} but the sdk used in the CLI is {cliSDKVersion}. " +
                        "The major version of the SDK used in the CLI must match the major version of the SDK used to build the plugin. " +
                        "Please use the CLI that targets the same major version of the SDK as the plugin.");
                }

                if (pluginSDKversion.Minor > cliSDKVersion.Minor)
                {
                    this.logger.LogError($"Plugin is built against SDK version {pluginSDKversion} but the sdk used in the CLI is {cliSDKVersion}. " +
                        "The minor version of the SDK used in the CLI must be greater than or equal to the minor version of the SDK used to build the plugin. " +
                        $"If your plugin does NOT use any features from SDK version {pluginSDKversion}, consider downgrading the plugin to use version {cliSDKVersion} or lower. " +
                        "Using the lowest minor version required will maximize compatibility of your plugin. " +
                        $"If your plugin does use features from SDK version {pluginSDKversion}, please update your CLI to a version that targets SDK version {pluginSDKversion} or later.");
                }
            }

            if (!loadSuccess || !isVersionSupported)
            {
                return false;
            }

            PluginMetadata metadata = GenerateMetadata(processedDir, manifest, pluginSDKversion);

            if (!metadata.Identity.HasValidId(out string error))
            {
                this.logger.LogError($"The plugin id {metadata.Identity.Id} is not valid.");
                this.logger.LogError(error);

                return false;
            }

            if (!TryGenerateContentsMetadata(pluginsLoader, out PluginContentsMetadata? contentsMetadata))
            {
                this.logger.LogError($"Failed to generate contents metadata for plugin.");
                return false;
            }

            processedPlugin = new ProcessedPluginResult(artifacts.SourceDirectoryFullPath, processedDir.AllContentFilePaths, metadata, contentsMetadata!);
            return true;
        }

        private bool TryProcessSourceDir(
           string sourceDir,
           [NotNullWhen(true)] out ProcessedPluginSourceDirectory? processedDir)
        {
            processedDir = null;
            string? manifestFilePath = null;
            long totalSize = 0;
            int dllCount = 0;
            var filesToPack = new List<string>();

            foreach (string file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);
                if (Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    dllCount++;
                    if (Path.GetFileNameWithoutExtension(file).Equals(sdkAssemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        this.logger.LogError($"{sdkAssemblyName} should not present in the directory.");
                        return false;
                    }
                }
                else if (fileName.Equals(Constants.BundledManifestName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                filesToPack.Add(Path.GetRelativePath(sourceDir, file));
                totalSize += new FileInfo(file).Length;
            }

            if (dllCount == 0)
            {
                this.logger.LogError($"Directory does not contain any DLLs: {sourceDir}.");
                return false;
            }

            processedDir = new ProcessedPluginSourceDirectory(
                sourceDir,
                filesToPack,
                manifestFilePath,
                totalSize);

            return true;
        }

        private PluginMetadata GenerateMetadata(ProcessedPluginSourceDirectory processedDir, PluginManifest manifest, SemanticVersion sdkVersion)
        {
            this.logger.LogTrace($"Generating metadata for plugin {manifest.Identity.Id}-{manifest.Identity.Version}");

            PluginIdentity identity = new(manifest.Identity.Id, manifest.Identity.Version);
            ulong installedSize = (ulong)processedDir.PluginSize;
            IEnumerable<PluginOwnerInfo> owners = manifest.Owners.Select(
                o => new PluginOwnerInfo(o.Name, o.Address, o.EmailAddresses.ToArray(), o.PhoneNumbers.ToArray()));

            PluginMetadata metadata = new(
                identity,
                installedSize,
                manifest.DisplayName,
                manifest.Description,
                sdkVersion,
                manifest.ProjectUrl, owners);

            return metadata;
        }

        private bool TryGenerateContentsMetadata(PluginsLoader pluginsLoader, out PluginContentsMetadata? contentsMetadata)
        {
            HashSet<ProcessingSourceMetadata> processingSourcesMetadata = new();

            foreach (ProcessingSourceReference psr in pluginsLoader.LoadedProcessingSources)
            {
                if (!TryCreateProcessingSourceMetadata(psr, out var metadata))
                {
                    contentsMetadata = null;
                    return false;
                }

                processingSourcesMetadata.Add(metadata);
            }

            // TODO: #294 Figure out how to extract description of a datacooker.
            var dataCookers = pluginsLoader.Extensions.SourceDataCookers
                .Concat(pluginsLoader.Extensions.CompositeDataCookers)
                .Select(x => new DataCookerMetadata(x.DataCookerId, null, x.SourceParserId))
                .ToList();

            var tables = pluginsLoader.Extensions.TablesById.Values
                .Select(x => new TableMetadata(
                    x.TableDescriptor.Guid,
                    x.TableDescriptor.Name,
                    x.TableDescriptor.Description,
                    x.TableDescriptor.Category,
                    x.TableDescriptor.IsMetadataTable))
                .ToList();

            contentsMetadata = new(processingSourcesMetadata, dataCookers, tables);
            return true;
        }

        private bool TryCreateProcessingSourceMetadata(ProcessingSourceReference psr, [NotNullWhen(true)] out ProcessingSourceMetadata? metadata)
        {
            // Tables
            IEnumerable<TableMetadata> dataTables = psr.Instance.DataTables.Select(x => new TableMetadata(
                x.Guid,
                x.Name,
                x.Description,
                x.Category,
                false));

            IEnumerable<TableMetadata> metadataTables = psr.Instance.MetadataTables.Select(x => new TableMetadata(
                x.Guid,
                x.Name,
                x.Description,
                x.Category,
                true));

            // Data Sources
            var dataSourcesMetadata = new List<DataSourceMetadata>();
            foreach (DataSourceAttribute? ds in psr.DataSources)
            {
                if (ds is FileDataSourceAttribute fds)
                {
                    dataSourcesMetadata.Add(new DataSourceMetadata(fds.FileExtension, fds.Description));
                }
                else if (ds is DirectoryDataSourceAttribute)
                {
                    dataSourcesMetadata.Add(new DataSourceMetadata(DataSourceNameConstants.DirectoryDataSourceName, ds.Description));
                }
                else if (ds is ExtensionlessFileDataSourceAttribute)
                {
                    dataSourcesMetadata.Add(new DataSourceMetadata(DataSourceNameConstants.ExtensionlessDataSourceName, ds.Description));
                }
                else
                {
                    Debug.Assert(false, $"Unknown DataSourceAttribute type: {ds.GetType()}");
                }
            }

            if (!TryGetAboutInfo(psr, out ProcessingSourceInfo? aboutInfo))
            {
                metadata = null;
                return false;
            }

            metadata = new(
                version: Version.Parse(psr.Version),
                name: psr.Name,
                description: psr.Description,
                guid: psr.Instance.TryGetGuid(),
                aboutInfo: aboutInfo,
                availableTables: dataTables.Concat(metadataTables),
                supportedDataSources: dataSourcesMetadata);

            return true;
        }

        private bool TryGetAboutInfo(ProcessingSourceReference psr, [NotNullWhen(true)] out ProcessingSourceInfo? processingSourceInfo)
        {
            processingSourceInfo = null;

            // Check if the GetAboutInfo method is overridden
            MethodInfo? methodInfo = psr.Instance.GetType().GetMethod(nameof(ProcessingSource.GetAboutInfo));
            if (methodInfo == null || methodInfo.DeclaringType == typeof(ProcessingSource))
            {
                this.logger.LogError($"Unable to generate processing source info: {psr.Name} does not override the virtual method {nameof(ProcessingSource)}.{nameof(ProcessingSource.GetAboutInfo)}.");
                return false;
            }

            var aboutInfo = psr.Instance.GetAboutInfo();

            if (aboutInfo == null)
            {
                this.logger.LogError($"Unable to generate processing source info: {psr.Name}'s {nameof(IProcessingSource.GetAboutInfo)} returned null.");
                return false;
            }

            List<string> errors = new();

            if (!TryGetOwners(psr, aboutInfo, out Core.Metadata.ContactInfo[]? owners, out string? ownersError))
            {
                errors.Add(ownersError);
            }

            if (!TryGetProjectInfo(psr, aboutInfo, out ProjectInfo? projectInfo, out string? projectInfoError))
            {
                errors.Add(projectInfoError);
            }

            if (!TryGetLicenseInfo(psr, aboutInfo, out LicenseInfo? licenseInfo, out string? licenseInfoError))
            {
                errors.Add(licenseInfoError);
            }

            if (errors.Any())
            {
                this.logger.LogError($"Unable to generate processing source info. The following error{ (errors.Count == 1 ? " was" : "s were") } encountered:{string.Join("", errors.Select(e => $"\n\t - {e}"))}");
                return false;
            }

            processingSourceInfo = new ProcessingSourceInfo(owners, projectInfo, licenseInfo, aboutInfo.CopyrightNotice, aboutInfo.AdditionalInformation);
            return true;
        }

        private bool TryGetOwners(
            ProcessingSourceReference processSource,
            Microsoft.Performance.SDK.Processing.ProcessingSourceInfo info,
            [NotNullWhen(true)] out Core.Metadata.ContactInfo[]? owners,
            [NotNullWhen(false)] out string? error)
        {
            error = null;
            owners = null;

            if (info.Owners == null)
            {
                error = $"The {nameof(ProcessingSourceInfo.Owners)} property of {processSource.Name}'s {nameof(ProcessingSourceInfo)} is null.";
                return false;
            }

            owners = info.Owners
                .Select(o => new Core.Metadata.ContactInfo(o.Name, o.Address, o.EmailAddresses ?? [], o.PhoneNumbers ?? []))
                .ToArray();

            if (owners.Any(o => o.EmailAddresses.Any(string.IsNullOrWhiteSpace)))
            {
                error = $"One or more of the {nameof(ProcessingSourceInfo.Owners)}'s {nameof(Core.Metadata.ContactInfo.EmailAddresses)} on {processSource.Name}'s {nameof(ProcessingSourceInfo)} is null or whitespace.";
                return false;
            }

            if (owners.Any(o => o.PhoneNumbers.Any(string.IsNullOrWhiteSpace)))
            {
                error = $"One or more of the {nameof(ProcessingSourceInfo.Owners)}'s {nameof(Core.Metadata.ContactInfo.PhoneNumbers)} on {processSource.Name}'s {nameof(ProcessingSourceInfo)} is null or whitespace.";
                return false;
            }

            return true;
        }

        private bool TryGetProjectInfo(
            ProcessingSourceReference processSource,
            Microsoft.Performance.SDK.Processing.ProcessingSourceInfo processingSourceInfo,
            out ProjectInfo? projectInfo,
            [NotNullWhen(false)] out string? error)
        {
            error = null;
            projectInfo = null;

            if (processingSourceInfo.ProjectInfo == null)
            {
                // This is an optional field
                projectInfo = null;
                return true;
            }

            if (processingSourceInfo.ProjectInfo.Uri == null)
            {
                error = $"The {nameof(ProcessingSourceInfo.ProjectInfo)}.{nameof(ProcessingSourceInfo.ProjectInfo.Uri)} property of {processSource.Name}'s {nameof(ProcessingSourceInfo)} is null.";
                return false;
            }

            if (!Uri.TryCreate(processingSourceInfo.ProjectInfo.Uri, UriKind.Absolute, out Uri? uri))
            {
                error = $"{processingSourceInfo.ProjectInfo.Uri} is not an absolute URI.";
                return false;
            }

            projectInfo = new ProjectInfo(uri);
            return true;
        }
        
        private bool TryGetLicenseInfo(
            ProcessingSourceReference processSource,
            Microsoft.Performance.SDK.Processing.ProcessingSourceInfo processingSourceInfo,
            out LicenseInfo? licenseInfo,
            [NotNullWhen(false)] out string? error)
        {
            error = null;
            licenseInfo = null;

            if (processingSourceInfo.LicenseInfo == null)
            {
                // This is an optional field
                licenseInfo = null;
                return true;
            }

            if (processingSourceInfo.LicenseInfo.Uri == null)
            {
                error = $"The {nameof(ProcessingSourceInfo.LicenseInfo)}.{nameof(ProcessingSourceInfo.LicenseInfo.Uri)} property of {processSource.Name}'s {nameof(ProcessingSourceInfo)} is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(processingSourceInfo.LicenseInfo.Name))
            {
                error = $"The {nameof(ProcessingSourceInfo.LicenseInfo)}.{nameof(ProcessingSourceInfo.LicenseInfo.Name)} property of {processSource.Name}'s {nameof(ProcessingSourceInfo)} is null or whitespace.";
                return false;
            }

            if (!Uri.TryCreate(processingSourceInfo.LicenseInfo.Uri, UriKind.Absolute, out Uri? uri))
            {
                error = $"{processingSourceInfo.LicenseInfo.Uri} is not an absolute URI.";
                return false;
            }

            licenseInfo = new LicenseInfo(processingSourceInfo.LicenseInfo.Name, uri, processingSourceInfo.LicenseInfo.Text);
            return true;
        }

        private record ProcessedPluginSourceDirectory(
            string FullPath,
            IReadOnlyList<string> AllContentFilePaths,
            string? ManifestFilePath,
            long PluginSize);
    }
}
