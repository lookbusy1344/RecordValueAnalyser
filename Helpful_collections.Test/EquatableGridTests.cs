namespace Helpful_collections.Test;

using RecordValueAnalyser.Useful;

[TestClass]
public class EquatableGridTests
{
	[TestMethod]
	public void Grids_with_same_dimensions_and_content_are_equal()
	{
		var a = EquatableGrid.CopyOf(new[,] { { 1, 2 }, { 3, 4 } });
		var b = EquatableGrid.CopyOf(new[,] { { 1, 2 }, { 3, 4 } });

		Assert.AreEqual(a, b);
		Assert.IsTrue(a == b);
		Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
	}

	[TestMethod]
	public void Grids_with_different_content_are_not_equal()
	{
		var a = EquatableGrid.CopyOf(new[,] { { 1, 2 }, { 3, 4 } });
		var b = EquatableGrid.CopyOf(new[,] { { 1, 2 }, { 3, 9 } });

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Grids_with_different_dimensions_are_not_equal_even_with_same_element_count()
	{
		var a = new EquatableGrid<int>(2, 3);
		var b = new EquatableGrid<int>(3, 2);

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Indexer_reads_and_writes_by_row_and_column()
	{
		var a = new EquatableGrid<int>(2, 2);
		a[0, 1] = 42;

		Assert.AreEqual(42, a[0, 1]);
		Assert.AreEqual(0, a[1, 0]);
	}

	[TestMethod]
	public void Default_instance_behaves_as_empty()
	{
		var a = default(EquatableGrid<int>);

		Assert.AreEqual(0, a.Rows);
		Assert.AreEqual(0, a.Columns);
	}

	[TestMethod]
	public void CopyOf_copies_elements_without_aliasing_the_source()
	{
		var source = new[,] { { 1, 2 }, { 3, 4 } };
		var copy = EquatableGrid.CopyOf(source);

		source[0, 0] = 99;

		Assert.AreEqual(1, copy[0, 0]);
	}
}
