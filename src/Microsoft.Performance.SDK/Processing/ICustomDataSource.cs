// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Performance.SDK.Processing
{
    /// <summary>
    ///     Enumerates the different sources of serialized table configuration
    ///     data.
    /// </summary>
    public enum SerializationSource
    {
        /// <summary>
        ///     The tables configurations are pre-built and persisted.
        /// </summary>
        PrebuiltTableConfiguration,
    }

    /// <summary>
    ///     This interface is used to expose the tables associated
    ///     with processing a given data source.
    /// </summary>
    public interface ICustomDataSource
    {
        /// <summary>
        ///     Gets the collection of tables exposed by this data source.
        ///     These are the data tables for exposing the processed data.
        /// </summary>
        IEnumerable<TableDescriptor> DataTables { get; }

        /// <summary>
        ///     Gets the collection of tables that are considered to contain
        ///     metadata about the data being processed.
        /// </summary>
        IEnumerable<TableDescriptor> MetadataTables { get; }

        /// <summary>
        ///     Gets the options that are supported by this data source.
        /// </summary>
        IEnumerable<Option> CommandLineOptions { get; }

        /// <summary>
        ///     Gets information about this Custom Data Source. This information
        ///     is displayed in the About Box for this data source.
        /// </summary>
        /// <returns>
        ///     The data source information.
        /// </returns>
        CustomDataSourceInfo GetAboutInfo();

        /// <summary>
        ///     Sets the application environment for the <see cref="ICustomDataSource"/>.
        /// </summary>
        /// <param name="applicationEnvironment">
        ///     The application environment.
        /// </param>
        void SetApplicationEnvironment(IApplicationEnvironment applicationEnvironment);

        /// <summary>
        ///     Called to provide the data source an application-appropriate logging mechanism.
        /// </summary>
        /// <param name="logger">
        ///     Used to log information.
        /// </param>
        void SetLogger(ILogger logger);

        /// <summary>
        ///     Creates a new processor for processing the specified data source.
        /// </summary>
        /// <param name="dataSource">
        ///     The data source to process.
        /// </param>
        /// <param name="processorEnvironment">
        ///     The environment for this specific processor instance.
        /// </param>
        /// <param name="options">
        ///     The command line options to pass to the processor.
        /// </param>
        /// <returns>
        ///     The created <see cref="ICustomDataProcessor"/>.
        /// </returns>
        ICustomDataProcessor CreateProcessor(
            IDataSource dataSource,
            IProcessorEnvironment processorEnvironment,
            ProcessorOptions options);

        /// <summary>
        ///     Creates a new processor for processing the specified data sources.
        /// </summary>
        /// <param name="dataSources">
        ///     The data sources to process.
        /// </param>
        /// <param name="processorEnvironment">
        ///     The environment for this specific processor instance.
        /// </param>
        /// <param name="options">
        ///     The command line options to pass to the processor.
        /// </param>
        /// <returns>
        ///     The created <see cref="ICustomDataProcessor"/>.
        /// </returns>
        ICustomDataProcessor CreateProcessor(
            IEnumerable<IDataSource> dataSources,
            IProcessorEnvironment processorEnvironment,
            ProcessorOptions options);

        /// <summary>
        ///     Retrieves a stream for serializing data. This method may return
        ///     <c>null</c>.
        ///     <para />
        ///     Source: PrebuiltTableConfiguration => TableConfigurations[]
        /// </summary>
        /// <param name="source">
        ///     Identifies the stream source.
        /// </param>
        /// <returns>
        ///     Serialization stream.
        /// </returns>
        Stream GetSerializationStream(SerializationSource source);

        /// <summary>
        ///     Returns a value indicating whether the given Data Source can
        ///     be opened by this instance.
        /// </summary>
        /// <param name="dataSource">
        ///     The Data Source of interest.
        /// </param>
        /// <returns>
        ///     <c>true</c> if this instance can actually process the given Data Source;
        ///     <c>false</c> otherwise.
        /// </returns>
        bool IsDataSourceSupported(IDataSource dataSource);

        /// <summary>
        ///     Provides a method for this Custom Data Source to do any
        ///     special cleanup operations for processors created by
        ///     this instance, if applicable.
        ///     <para />
        ///     It is guaranteed that the <paramref name="processor"/>
        ///     passed to this method was created by this instance.
        /// </summary>
        /// <param name="processor">
        ///     The processor to dispose.
        /// </param>
        void DisposeProcessor(ICustomDataProcessor processor);
    }
}
