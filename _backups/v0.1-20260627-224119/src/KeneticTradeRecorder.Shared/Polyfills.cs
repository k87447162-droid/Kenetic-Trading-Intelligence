// -----------------------------------------------------------------------------
//  KeneticTradeRecorder.Shared - Polyfills.cs
//
//  PURPOSE:    Provides the IsExternalInit shim required by C# 9+ records and
//              init-only setters when targeting netstandard2.0. NinjaTrader 8
//              runs on .NET Framework 4.8, which consumes netstandard2.0 but
//              does not ship this type. Declared 'internal' so it never clashes
//              with the framework-provided type when this assembly is referenced
//              from a modern (.NET 6/8) test or tooling project.
// -----------------------------------------------------------------------------
#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Compiler-only marker enabling init-only setters on netstandard2.0.</summary>
    internal static class IsExternalInit { }
}
#endif
