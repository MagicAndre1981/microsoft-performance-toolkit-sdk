// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK.Processing.ColumnBuilding;
using Microsoft.Performance.SDK.Runtime.ColumnBuilding.Processors;
using Microsoft.Performance.SDK.Runtime.ColumnVariants.TreeNodes;

namespace Microsoft.Performance.SDK.Runtime.ColumnBuilding.Builders.CallbackInvokers;

/// <summary>
///     Responsible for invoking a callback that builds a single mode of a modal column variant.
/// </summary>
internal readonly struct ModeBuilderCallbackInvoker
    : IBuilderCallbackInvoker
{
    private readonly Func<ToggleableColumnBuilder, ColumnBuilder> callback;
    private readonly IDataColumn baseColumn;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ModeBuilderCallbackInvoker"/>
    /// </summary>
    /// <param name="callback">
    ///     The callback that builds the mode.
    /// </param>
    /// <param name="baseColumn">
    ///     The base column that the mode is being built for.
    /// </param>
    public ModeBuilderCallbackInvoker(
        Func<ToggleableColumnBuilder, ColumnBuilder> callback,
        IDataColumn baseColumn)
    {
        this.callback = callback;
        this.baseColumn = baseColumn;
    }

    public bool TryGet(out IColumnVariantsTreeNode builtVariantsTreeNode)
    {
        if (this.callback == null)
        {
            builtVariantsTreeNode = NullColumnVariantsTreeNode.Instance;
            return false;
        }

        var processor = new BuiltColumnVariantReflector();
        var builder = new EmptyColumnBuilder(processor, baseColumn);

        this.callback(builder).Commit();
        builtVariantsTreeNode = processor.Output;
        return true;
    }
}