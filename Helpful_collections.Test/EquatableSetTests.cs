namespace Helpful_collections.Test;

using RecordValueAnalyser.Useful;

[TestClass]
public class EquatableSetTests
{
	[TestMethod]
	public void Sets_with_same_elements_are_equal_regardless_of_insertion_order()
	{
		EquatableSet<int> a = [1, 2, 3];
		EquatableSet<int> b = [3, 2, 1];

		Assert.AreEqual(a, b);
		Assert.IsTrue(a == b);
		Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
	}

	[TestMethod]
	public void Sets_with_different_elements_are_not_equal()
	{
		EquatableSet<int> a = [1, 2, 3];
		EquatableSet<int> b = [1, 2, 4];

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Sets_with_different_counts_are_not_equal()
	{
		EquatableSet<int> a = [1, 2];
		EquatableSet<int> b = [1, 2, 3];

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Duplicate_elements_are_deduplicated()
	{
		EquatableSet<int> a = [1, 1, 2];

		Assert.AreEqual(2, a.Count);
	}

	[TestMethod]
	public void Default_instance_behaves_as_empty()
	{
		var a = default(EquatableSet<int>);
		EquatableSet<int> b = [];

		Assert.AreEqual(0, a.Count);
		Assert.AreEqual(a, b);
	}

	[TestMethod]
	public void Contains_reports_membership()
	{
		EquatableSet<int> a = [1, 2, 3];

		Assert.IsTrue(a.Contains(2));
		Assert.IsFalse(a.Contains(4));
	}

	[TestMethod]
	public void CopyOf_copies_elements_without_aliasing_the_source()
	{
		var source = new HashSet<int> { 1, 2 };
		var copy = EquatableSet.CopyOf(source);

		_ = source.Add(3);

		Assert.AreEqual(2, copy.Count);
	}
}
