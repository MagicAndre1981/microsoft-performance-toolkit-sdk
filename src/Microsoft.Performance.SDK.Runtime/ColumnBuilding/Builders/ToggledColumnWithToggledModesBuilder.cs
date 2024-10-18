// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK.Runtime.ColumnBuilding.Builders.CallbackInvokers;
using Microsoft.Performance.SDK.Runtime.ColumnBuilding.Processors;
using Microsoft.Performance.SDK.Runtime.ColumnVariants.TreeNodes;

namespace Microsoft.Performance.SDK.Runtime.ColumnBuilding.Builders;

/// <summary>
///     A column variants builder with at least one hierarchical toggle, where the final toggle
///     is a toggle for a set of modes.
/// </summary>
internal sealed class ToggledColumnWithToggledModesBuilder
    : ToggledColumnBuilder
{
    private readonly string modesToggleText;
    private readonly ModesBuilderCallbackInvoker modesBuilderCallbackActionInvoker;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToggledColumnWithToggledModesBuilder"/>
    /// </summary>
    /// <param name="toggles">
    ///     The toggles that have been so far added to the column.
    /// </param>
    /// <param name="baseColumn">
    ///     The base <see cref="IDataColumn" /> that is being built upon.
    /// </param>
    /// <param name="processor">
    ///     The <see cref="IColumnVariantsProcessor" /> to invoke once the column variants are built.
    /// </param>
    /// <param name="modesBuilderCallbackActionInvoker">
    ///     The callback invoker that will build the modes for the final toggle.
    /// </param>
    /// <param name="modesToggleText">
    ///     The text to display for the final toggle for the modes.
    /// </param>
    public ToggledColumnWithToggledModesBuilder(
        IReadOnlyCollection<AddedToggle> toggles,
        IDataColumn baseColumn,
        IColumnVariantsProcessor processor,
        ModesBuilderCallbackInvoker modesBuilderCallbackActionInvoker,
        string modesToggleText)
        : base(toggles, baseColumn, processor)
    {
        this.modesBuilderCallbackActionInvoker = modesBuilderCallbackActionInvoker;
        this.modesToggleText = modesToggleText;
    }

    protected override IColumnVariantsTreeNode GetRootVariant()
    {
        if (this.modesBuilderCallbackActionInvoker.TryGet(out var builtModesVariant))
        {
            return new ModesToggleColumnVariantsTreeNode(this.modesToggleText, builtModesVariant);
        }
        else
        {
            return NullColumnVariantsTreeNode.Instance;
        }
    }
}