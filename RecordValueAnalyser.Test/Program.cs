using System.Threading.Tasks;

namespace RecordValueAnalyser.Test;

#pragma warning disable RCS1046 // Asynchronous method name should end with 'Async'.

internal static class Program
{
	internal static async Task MainX()
	{
		var tester = new RecordValueAnalyserUnitTest();

		await tester.ValueTypesOnlyTest();
		await tester.ReadOnlyListTest();
	}
}
