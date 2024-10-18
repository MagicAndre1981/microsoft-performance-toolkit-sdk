// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Performance.SDK.Processing;
using Microsoft.Performance.SDK.Processing.ColumnBuilding;
using Microsoft.Performance.SDK.Runtime.ColumnVariants.TreeNodes;

namespace Microsoft.Performance.SDK.Runtime.ColumnVariants.Registrar;

/// <summary>
///     Implements registering and retrieving column variants.
/// </summary>
public class ColumnVariantsRegistrar
    : IColumnVariantsRegistrar
{
    private readonly Dictionary<IDataColumn, IColumnVariantsTreeNode> columnVariantsTrees = new();
    private readonly Dictionary<IDataColumn, IReadOnlyDictionary<ColumnVariantDescriptor, IDataColumn>> columnVariants = new();

    /// <inheritdoc/>
    public bool TryGetVariantsTreeRoot(IDataColumn baseColumn, out IColumnVariantsTreeNode variantsTreeNodes)
    {
        return this.columnVariantsTrees.TryGetValue(baseColumn, out variantsTreeNodes);
    }

    /// <summary>
    ///     Sets the root of the variants tree for the given column, overwriting any existing value.
    /// </summary>
    /// <param name="column">
    ///     The column for which the variants tree root is being set.
    /// </param>
    /// <param name="variantsTreeNodes">
    ///     The root of the variants tree.
    /// </param>
    internal void SetVariantsTreeRoot(IDataColumn column, IColumnVariantsTreeNode variantsTreeNodes)
    {
        this.columnVariantsTrees[column] = variantsTreeNodes;
    }

    /// <summary>
    ///     Sets the variants for the given base column, overwriting any existing value.
    /// </summary>
    /// <param name="baseColumn">
    ///     The base column for which the variants are being set.
    /// </param>
    /// <param name="variants">
    ///     The variants for the base column.
    /// </param>
    internal void SetVariants(IDataColumn baseColumn,
        IReadOnlyDictionary<ColumnVariantDescriptor, IDataColumn> variants)
    {
        this.columnVariants[baseColumn] = variants;
    }

    public IReadOnlyDictionary<IDataColumn, IReadOnlyDictionary<ColumnVariantDescriptor, IDataColumn>> GetAllVariants()
    {
        return new Dictionary<IDataColumn, IReadOnlyDictionary<ColumnVariantDescriptor, IDataColumn>>(this.columnVariants);
    }
}