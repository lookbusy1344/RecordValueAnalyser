namespace Helpful_collections.Test;

using System.Text.Json;
using RecordValueAnalyser.Useful;

[TestClass]
public class EquatableJsonConverterTests
{
	[TestMethod]
	public void EquatableArray_round_trips_through_json()
	{
		EquatableArray<int> original = [1, 2, 3];

		var json = JsonSerializer.Serialize(original);
		var roundTripped = JsonSerializer.Deserialize<EquatableArray<int>>(json);

		Assert.AreEqual("[1,2,3]", json);
		Assert.IsTrue(original == roundTripped);
	}

	[TestMethod]
	public void EquatableDictionary_round_trips_through_json()
	{
		var original = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

		var json = JsonSerializer.Serialize(original);
		var roundTripped = JsonSerializer.Deserialize<EquatableDictionary<string, int>>(json);

		Assert.IsTrue(original == roundTripped);
	}

	[TestMethod]
	public void EquatableReadOnlyMemory_round_trips_through_json()
	{
		var original = new EquatableReadOnlyMemory<int>(new[] { 1, 2, 3 });

		var json = JsonSerializer.Serialize(original);
		var roundTripped = JsonSerializer.Deserialize<EquatableReadOnlyMemory<int>>(json);

		Assert.AreEqual("[1,2,3]", json);
		Assert.IsTrue(original == roundTripped);
	}

	[TestMethod]
	public void EquatableArraySegment_round_trips_through_json()
	{
		var original = new EquatableArraySegment<int>(new ArraySegment<int>([1, 2, 3, 4], 1, 2));

		var json = JsonSerializer.Serialize(original);
		var roundTripped = JsonSerializer.Deserialize<EquatableArraySegment<int>>(json);

		Assert.AreEqual("[2,3]", json);
		Assert.IsTrue(original == roundTripped);
	}

	[TestMethod]
	public void EquatableSet_round_trips_through_json()
	{
		EquatableSet<int> original = [1, 2, 3];

		var json = JsonSerializer.Serialize(original);
		var roundTripped = JsonSerializer.Deserialize<EquatableSet<int>>(json);

		Assert.IsTrue(original == roundTripped);
	}

	[TestMethod]
	public void EquatableGrid_round_trips_through_json()
	{
		var original = EquatableGrid.CopyOf(new[,] { { 1, 2 }, { 3, 4 } });

		var json = JsonSerializer.Serialize(original);
		var roundTripped = JsonSerializer.Deserialize<EquatableGrid<int>>(json);

		Assert.AreEqual("[[1,2],[3,4]]", json);
		Assert.AreEqual(original, roundTripped);
	}
}
