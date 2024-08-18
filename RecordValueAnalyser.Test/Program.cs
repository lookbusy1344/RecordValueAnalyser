using System.Threading.Tasks;

namespace RecordValueAnalyser.Test.Classes;

internal static class Program
{
	internal static async Task MainX()
	{
		var tester = new RecordValueAnalyserUnitTest();

		await tester.ValueTypesOnly();
		await tester.ReadOnlyList();
	}
}
