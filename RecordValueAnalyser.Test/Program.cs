using System.Threading.Tasks;

namespace RecordValueAnalyser.TestClasses;

internal static class Program
{
	internal static async Task MainX()
	{
		var tester = new RecordValueAnalyserUnitTest();

		await tester.ValueTypesOnly();
		await tester.ReadOnlyList();
	}
}
