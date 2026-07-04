// -----------------------------------------------------------------------------
//  Kenetic Trade Recorder - Polyfills.cs
//
//  PURPOSE:    Provides the IsExternalInit shim required by C# 9+ records and
//              init-only setters. Both compile targets used by this solution -
//              netstandard2.0 (Shared, Core) and net48 (AddOn, which is what
//              NinjaTrader 8 actually loads) - consume init-only members but do
//              NOT ship System.Runtime.CompilerServices.IsExternalInit (it first
//              appears in .NET 5).
//
//  WHY DUPLICATED:  This identical file is compiled into EVERY project that
//              defines or consumes init-only members. The type is 'internal',
//              so each assembly needs its own copy - an internal copy in one
//              assembly is not visible to another. Do NOT "DRY this up" into a
//              single shared file; removing a project's copy re-breaks that
//              project's build with CS0518. The '!NET5_0_OR_GREATER' guard
//              excludes it on modern .NET (e.g. the net8 test/shim build) where
//              the framework already provides the type, avoiding any clash.
// -----------------------------------------------------------------------------
#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>Compiler-only marker enabling init-only setters where the target framework lacks it.</summary>
    internal static class IsExternalInit { }
}
#endif
