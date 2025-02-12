namespace RecordValueAnalyser.Test.Classes;

using System.Threading.Tasks;

internal static class Program
{
	internal static async Task MainX()
	{
		var tester = new RecordValueAnalyserUnitTest();

		await tester.ValueTypesOnly();
		await tester.ReadOnlyList();
	}
}
