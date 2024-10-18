// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Performance.SDK.Processing.ColumnBuilding;
using Microsoft.Performance.SDK.Runtime.ColumnVariants.TreeNodes;

namespace Microsoft.Performance.SDK.Runtime.ColumnBuilding.Builders.CallbackInvokers;

/// <summary>
///     Represents an object that can invoke a callback to build a column variant.
///     Implementations of this interface will typically invoke an <see cref="Action{T}"/>
///     for a <see cref="ColumnBuilder"/> and return the final root <see cref="IColumnVariantsTreeNode"/>
///     that was built.
/// </summary>
internal interface IBuilderCallbackInvoker
{
    /// <summary>
    ///     Attempts to get the built column variant.
    /// </summary>
    /// <param name="builtVariantsTreeNode">
    ///     The built column variant, if successful.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the column variant was successfully built; <c>false</c> otherwise.
    /// </returns>
    bool TryGet(out IColumnVariantsTreeNode builtVariantsTreeNode);
}