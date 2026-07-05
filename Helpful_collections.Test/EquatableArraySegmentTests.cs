namespace Helpful_collections.Test;

using RecordValueAnalyser.Useful;

[TestClass]
public class EquatableArraySegmentTests
{
	[TestMethod]
	public void Equal_content_is_equal_even_from_different_backing_arrays()
	{
		var a = new EquatableArraySegment<int>(new ArraySegment<int>([1, 2, 3, 4], 1, 2));
		var b = new EquatableArraySegment<int>(new ArraySegment<int>([0, 2, 3, 9], 1, 2));

		Assert.AreEqual(a, b);
		Assert.IsTrue(a == b);
		Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
	}

	[TestMethod]
	public void Different_content_is_not_equal()
	{
		var a = new EquatableArraySegment<int>(new ArraySegment<int>([1, 2, 3], 0, 2));
		var b = new EquatableArraySegment<int>(new ArraySegment<int>([1, 2, 3], 1, 2));

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Different_lengths_are_not_equal()
	{
		var a = new EquatableArraySegment<int>(new ArraySegment<int>([1, 2, 3], 0, 2));
		var b = new EquatableArraySegment<int>(new ArraySegment<int>([1, 2, 3], 0, 3));

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Default_instance_behaves_as_empty()
	{
		var a = default(EquatableArraySegment<int>);
		var b = new EquatableArraySegment<int>(default);

		Assert.AreEqual(0, a.Count);
		Assert.AreEqual(a, b);
	}

	[TestMethod]
	public void Indexer_returns_expected_element_relative_to_the_segment()
	{
		var a = new EquatableArraySegment<int>(new ArraySegment<int>([10, 20, 30, 40], 1, 2));

		Assert.AreEqual(20, a[0]);
		Assert.AreEqual(30, a[1]);
	}
}
