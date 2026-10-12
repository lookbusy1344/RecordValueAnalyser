namespace RecordValueAnalyser.Test;

internal static class TestConstants
{
	internal const string General = @"
using System;
using System.Collections.Generic;

namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
";

	// Stub for the real InlineArrayAttribute (required for testing .NET 8 inline arrays).
	// https://github.com/dotnet/runtime/issues/61135
	// https://learn.microsoft.com/en-us/dotnet/api/system.runtime.compilerservices.inlinearrayattribute?view=net-8.0
	internal const string InlineArrayAttribute = @"
		namespace System.Runtime.CompilerServices {
		    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
		    public sealed class InlineArrayAttribute : Attribute {
		        public InlineArrayAttribute (int length) { Length = length; }
		        public int Length { get; }
		    } }
		";

	// Stubs for the C# 15 union runtime types (shipped in .NET 11, absent from the test reference assemblies).
	// https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/unions.md
	internal const string UnionTypes = @"
		namespace System.Runtime.CompilerServices {
		    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
		    public class UnionAttribute : Attribute { }
		    public interface IUnion { object Value { get; } }
		}
		";
}
