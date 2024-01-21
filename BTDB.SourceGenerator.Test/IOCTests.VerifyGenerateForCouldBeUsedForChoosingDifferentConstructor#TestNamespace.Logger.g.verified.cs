﻿//HintName: TestNamespace.Logger.g.cs
// <auto-generated/>
using System;
using System.Runtime.CompilerServices;

namespace TestNamespace;

static file class LoggerRegistration
{
    [ModuleInitializer]
    internal static unsafe void Register4BTDB()
    {
        global::BTDB.IOC.IContainer.RegisterFactory(typeof(global::TestNamespace.Logger), (container, ctx) =>
        {
            var f0 = container.CreateFactory(ctx, typeof(int), "a");
            if (f0 == null) throw new global::System.ArgumentException("Cannot resolve int a parameter of TestNamespace.Logger");
            return (container2, ctx2) =>
            {
                var res = new global::TestNamespace.Logger((int)(f0(container2, ctx2)));
                return res;
            };
        });
    }
}
