namespace RecordValueAnalyser.Useful;

using System.Collections;
using System.Text.Json.Serialization;

/// <summary>
///     An immutable dictionary with <em>value</em> equality: two instances are equal when they hold the
///     same key→value entries, irrespective of insertion order. The dictionary counterpart to
///     <see cref="EquatableArray{T}" /> — a bare <see cref="IReadOnlyDictionary{TKey, TValue}" /> record
///     member compares by reference and so breaks the record's value semantics (JSV01). A
///     <c>readonly struct</c> wrapping one <see cref="Dictionary{TKey, TValue}" />; <c>default</c> behaves
///     as empty.
/// </summary>
[JsonConverter(typeof(EquatableDictionaryJsonConverterFactory))]
public readonly struct EquatableDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IEquatable<EquatableDictionary<TKey, TValue>>
	where TKey : notnull
{
	private readonly Dictionary<TKey, TValue>? entries;

	internal EquatableDictionary(Dictionary<TKey, TValue> entries) => this.entries = entries;

	private Dictionary<TKey, TValue> Entries => entries ?? [];

	public int Count => Entries.Count;

	public IEnumerable<TKey> Keys => Entries.Keys;

	public IEnumerable<TValue> Values => Entries.Values;

	public TValue this[TKey key] => Entries[key];

	public bool ContainsKey(TKey key) => Entries.ContainsKey(key);

	public bool TryGetValue(TKey key, out TValue value) => Entries.TryGetValue(key, out value!);

	public bool Equals(EquatableDictionary<TKey, TValue> other)
	{
		if (Count != other.Count) {
			return false;
		}

		var comparer = EqualityComparer<TValue>.Default;
		foreach (var (key, value) in Entries) {
			if (!other.Entries.TryGetValue(key, out var otherValue) || !comparer.Equals(value, otherValue)) {
				return false;
			}
		}

		return true;
	}

	public override bool Equals(object? obj) => obj is EquatableDictionary<TKey, TValue> other && Equals(other);

	public override int GetHashCode()
	{
		// Order-independent: XOR per-entry hashes so equal maps with differing insertion order agree.
		var hash = 0;
		foreach (var (key, value) in Entries) {
			hash ^= HashCode.Combine(key, value);
		}

		return hash;
	}

	public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => Entries.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static bool operator ==(EquatableDictionary<TKey, TValue> left, EquatableDictionary<TKey, TValue> right) => left.Equals(right);

	public static bool operator !=(EquatableDictionary<TKey, TValue> left, EquatableDictionary<TKey, TValue> right) => !left.Equals(right);
}

/// <summary>Explicit copy helpers for <see cref="EquatableDictionary{TKey, TValue}" />.</summary>
public static class EquatableDictionaryFactory
{
	/// <summary>
	///     Explicitly copy a dictionary into an immutable value object.
	/// </summary>
	public static EquatableDictionary<TKey, TValue> CopyOf<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> entries)
		where TKey : notnull
	{
		ArgumentNullException.ThrowIfNull(entries);
		return new(new(entries));
	}
}
