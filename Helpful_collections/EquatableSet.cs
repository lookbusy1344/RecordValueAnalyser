namespace RecordValueAnalyser.Useful;

using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

/// <summary>
///     A set with <em>value</em> equality: two instances are equal when they hold the same elements,
///     irrespective of insertion order. A bare <see cref="IReadOnlySet{T}" /> or <see cref="HashSet{T}" />
///     record member compares by reference and so breaks the record's value semantics (JSV01). A
///     <c>readonly struct</c> wrapping one <see cref="HashSet{T}" />; <c>default</c> behaves as empty. A
///     collection-expression target so <c>[]</c> / <c>[.. xs]</c> construct it directly.
/// </summary>
[CollectionBuilder(typeof(EquatableSet), nameof(EquatableSet.Create))]
[JsonConverter(typeof(EquatableSetJsonConverterFactory))]
public readonly struct EquatableSet<T> : IReadOnlyCollection<T>, IEquatable<EquatableSet<T>>
{
	private readonly HashSet<T>? entries;

	internal EquatableSet(HashSet<T> entries) => this.entries = entries;

	private HashSet<T> Entries => entries ?? [];

	public int Count => Entries.Count;

	public bool Contains(T item) => Entries.Contains(item);

	public bool Equals(EquatableSet<T> other) => Count == other.Count && Entries.SetEquals(other.Entries);

	public override bool Equals(object? obj) => obj is EquatableSet<T> other && Equals(other);

	public override int GetHashCode()
	{
		var hash = 0;
		foreach (var item in Entries) {
			hash ^= item is null ? 0 : item.GetHashCode();
		}

		return hash;
	}

	public IEnumerator<T> GetEnumerator() => Entries.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static bool operator ==(EquatableSet<T> left, EquatableSet<T> right) => left.Equals(right);

	public static bool operator !=(EquatableSet<T> left, EquatableSet<T> right) => !left.Equals(right);
}

/// <summary>
///     The collection-expression builder and explicit copy helper for <see cref="EquatableSet{T}" />
///     (<c>[]</c> / <c>[.. xs]</c>).
/// </summary>
public static class EquatableSet
{
	public static EquatableSet<T> Create<T>(ReadOnlySpan<T> items)
	{
		var set = new HashSet<T>(items.Length);
		foreach (var item in items) {
			_ = set.Add(item);
		}

		return new(set);
	}

	/// <summary>
	///     Explicitly copy a sequence into a structurally comparable set.
	/// </summary>
	public static EquatableSet<T> CopyOf<T>(IEnumerable<T> items)
	{
		ArgumentNullException.ThrowIfNull(items);
		return new([.. items]);
	}
}
