using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = RecordValueAnalyser.Test.CSharpCodeFixVerifier<RecordValueAnalyser.RecordValueAnalyser, RecordValueAnalyser.RecordValueAnalyserCodeFixProvider>;

namespace RecordValueAnalyser.Test
{
	[TestClass]
	public class RecordValueAnalyserUnitTest
	{
		//No diagnostics expected to show up
		[TestMethod]
		public async Task TestMethod1Async()
		{
			const string test = "";

			await VerifyCS.VerifyAnalyzerAsync(test);
		}

		//Diagnostic and CodeFix both triggered and checked for
		[TestMethod]
		public async Task TestMethod2Async()
		{
			const string test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class {|#0:TypeName|}
        {   
        }
    }";

			const string fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";

			var expected = VerifyCS.Diagnostic("RecordValueAnalyser").WithLocation(0).WithArguments("TypeName");
			await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
		}
	}
}
