namespace RecordValueAnalyser.Useful;

using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

/// <summary>
///     An immutable array with <em>value</em> equality: two instances are equal when their elements are
///     equal in order. This is the piece a bare <see cref="IReadOnlyList{T}" /> record member is missing —
///     a list field falls back to reference equality, silently breaking the value semantics a
///     <c>record</c> advertises (the JSV01 defect class). Backed by <see cref="ImmutableArray{T}" />, a
///     <c>readonly struct</c> so it never allocates beyond the array it wraps, and a collection-expression
///     target so <c>[]</c> / <c>[.. xs]</c> construct it directly.
/// </summary>
[CollectionBuilder(typeof(EquatableArray), nameof(EquatableArray.Create))]
[JsonConverter(typeof(EquatableArrayJsonConverterFactory))]
public readonly struct EquatableArray<T>(ImmutableArray<T> items) : IReadOnlyList<T>, IEquatable<EquatableArray<T>>
{
	private ImmutableArray<T> Items { get => field.IsDefault ? [] : field; } = items;

	public int Count => Items.Length;

	public T this[int index] => Items[index];

	public bool Equals(EquatableArray<T> other) => Items.AsSpan().SequenceEqual(other.Items.AsSpan());

	public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (var item in Items) {
			hash.Add(item);
		}

		return hash.ToHashCode();
	}

	public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)Items).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

	public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}

/// <summary>
///     The collection-expression builder and explicit copy helpers for <see cref="EquatableArray{T}" />
///     (<c>[]</c> / <c>[.. xs]</c>).
/// </summary>
public static class EquatableArray
{
	public static EquatableArray<T> Create<T>(ReadOnlySpan<T> items) => new([.. items]);

	/// <summary>
	///     Explicitly copy a sequence into a structurally comparable immutable array.
	/// </summary>
	public static EquatableArray<T> CopyOf<T>(IEnumerable<T> items)
	{
		ArgumentNullException.ThrowIfNull(items);
		return new([.. items]);
	}

	/// <summary>Wrap an already immutable array without another copy.</summary>
	public static EquatableArray<T> CopyOf<T>(ImmutableArray<T> items) => new(items);
}
