namespace Helpful_collections.Test;

using RecordValueAnalyser.Useful;

[TestClass]
public class EquatableReadOnlyMemoryTests
{
	[TestMethod]
	public void Equal_content_is_equal_even_from_different_backing_arrays()
	{
		var a = new EquatableReadOnlyMemory<int>(new[] { 1, 2, 3 });
		var b = new EquatableReadOnlyMemory<int>(new[] { 1, 2, 3 });

		Assert.AreEqual(a, b);
		Assert.IsTrue(a == b);
		Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
	}

	[TestMethod]
	public void Different_content_is_not_equal()
	{
		var a = new EquatableReadOnlyMemory<int>(new[] { 1, 2, 3 });
		var b = new EquatableReadOnlyMemory<int>(new[] { 1, 2, 4 });

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Different_lengths_are_not_equal()
	{
		var a = new EquatableReadOnlyMemory<int>(new[] { 1, 2, 3 });
		var b = new EquatableReadOnlyMemory<int>(new[] { 1, 2 });

		Assert.AreNotEqual(a, b);
	}

	[TestMethod]
	public void Default_instance_behaves_as_empty()
	{
		var a = default(EquatableReadOnlyMemory<int>);
		var b = new EquatableReadOnlyMemory<int>(ReadOnlyMemory<int>.Empty);

		Assert.AreEqual(0, a.Length);
		Assert.AreEqual(a, b);
	}

	[TestMethod]
	public void Span_exposes_underlying_content()
	{
		var a = new EquatableReadOnlyMemory<int>(new[] { 1, 2, 3 });

		Assert.AreEqual(3, a.Span.Length);
		Assert.AreEqual(2, a.Span[1]);
	}
}
