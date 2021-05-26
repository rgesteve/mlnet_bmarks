// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CSharp.RuntimeBinder.Semantics
{
    internal sealed class ExprZeroInit : ExprWithType
    {
        public ExprZeroInit(CType type)
            : base(ExpressionKind.ZeroInit, type)
        {
        }

        public Expr OptionalArgument { get; set; }

        public Expr OptionalConstructorCall { get; set; }

        public bool IsConstructor { get; set; }
    }
}