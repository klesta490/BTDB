﻿//HintName: TestNamespace.ErrorHandler.g.cs
// <auto-generated/>
#pragma warning disable 612,618
using System;
using System.Runtime.CompilerServices;

namespace TestNamespace;

[CompilerGenerated]
static file class ErrorHandlerRegistration
{
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    extern static global::TestNamespace.ErrorHandler Constr();
    [ModuleInitializer]
    internal static unsafe void Register4BTDB()
    {
        global::BTDB.IOC.IContainer.RegisterFactory(typeof(global::TestNamespace.ErrorHandler), (container, ctx) =>
        {
            return (container2, ctx2) =>
            {
                var res = Constr();
                return res;
            };
        });
    }
}