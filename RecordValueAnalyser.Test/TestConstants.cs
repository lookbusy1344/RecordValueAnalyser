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
}
