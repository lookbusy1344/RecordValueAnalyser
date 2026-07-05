namespace Helpful_collections.Test;

using RecordValueAnalyser.Useful;

[TestClass]
public class EquatableArrayTests
{
	[TestMethod]
	public void Equal_arrays_with_same_elements_in_order_are_equal()
	{
		EquatableArray<int> a = [1, 2, 3];
		EquatableArray<int> b = [1, 2, 3];

		Assert.AreEqual(a, b);
		Assert.IsTrue(a == b);
		Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
	}

	[TestMethod]
	public void Arrays_with_different_order_are_not_equal()
	{
		EquatableArray<int> a = [1, 2, 3];
		EquatableArray<int> b = [3, 2, 1];

		Assert.AreNotEqual(a, b);
		Assert.IsTrue(a != b);
	}

	[TestMethod]
	public void Arrays_with_different_lengths_are_not_equal()
	{
		EquatableArray<int> a = [1, 2, 3];
		EquatableArray<int> b = [1, 2];

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Default_instance_behaves_as_empty()
	{
		var a = default(EquatableArray<int>);
		EquatableArray<int> b = [];

		Assert.AreEqual(0, a.Count);
		Assert.AreEqual(a, b);
	}

	[TestMethod]
	public void Indexer_returns_expected_element()
	{
		EquatableArray<string> a = ["x", "y", "z"];

		Assert.AreEqual("y", a[1]);
	}

	[TestMethod]
	public void CopyOf_enumerable_copies_elements_without_aliasing_the_source()
	{
		var source = new List<int> { 1, 2, 3 };
		var copy = EquatableArray.CopyOf(source);

		source.Add(4);

		Assert.AreEqual(3, copy.Count);
	}

	[TestMethod]
	public void Enumeration_yields_elements_in_order()
	{
		EquatableArray<int> a = [1, 2, 3];
		int[] expected = [1, 2, 3];

		CollectionAssert.AreEqual(expected, a.ToList());
	}
}
