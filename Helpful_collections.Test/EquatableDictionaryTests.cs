namespace Helpful_collections.Test;

using RecordValueAnalyser.Useful;

[TestClass]
public class EquatableDictionaryTests
{
	[TestMethod]
	public void Dictionaries_with_same_entries_are_equal_regardless_of_insertion_order()
	{
		var a = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });
		var b = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 });

		Assert.AreEqual(a, b);
		Assert.IsTrue(a == b);
		Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
	}

	[TestMethod]
	public void Dictionaries_with_different_values_are_not_equal()
	{
		var a = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["a"] = 1 });
		var b = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["a"] = 2 });

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Dictionaries_with_different_counts_are_not_equal()
	{
		var a = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["a"] = 1 });
		var b = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 });

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Default_instance_behaves_as_empty()
	{
		var a = default(EquatableDictionary<string, int>);

		Assert.AreEqual(0, a.Count);
		Assert.IsFalse(a.ContainsKey("missing"));
	}

	[TestMethod]
	public void Indexer_and_TryGetValue_return_expected_values()
	{
		var a = EquatableDictionaryFactory.CopyOf(new Dictionary<string, int> { ["a"] = 1 });

		Assert.AreEqual(1, a["a"]);
		Assert.IsTrue(a.TryGetValue("a", out var value));
		Assert.AreEqual(1, value);
		Assert.IsFalse(a.TryGetValue("missing", out _));
	}

	[TestMethod]
	public void CopyOf_copies_entries_without_aliasing_the_source()
	{
		var source = new Dictionary<string, int> { ["a"] = 1 };
		var copy = EquatableDictionaryFactory.CopyOf(source);

		source["b"] = 2;

		Assert.AreEqual(1, copy.Count);
	}
}
