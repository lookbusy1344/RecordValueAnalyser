namespace RecordValueAnalyser.Useful;

using System.Collections;
using System.Text.Json.Serialization;

/// <summary>
///     A <see cref="ReadOnlyMemory{T}" /> with <em>value</em> equality: two instances are equal when their
///     elements are equal in order. <see cref="ReadOnlyMemory{T}" /> is explicitly flagged by the JSV01
///     analyser as never having value semantics — it compares span identity, not contents. A
///     <c>readonly struct</c> wrapping the memory directly, so it never copies the underlying buffer.
/// </summary>
[JsonConverter(typeof(EquatableReadOnlyMemoryJsonConverterFactory))]
public readonly struct EquatableReadOnlyMemory<T>(ReadOnlyMemory<T> memory) : IReadOnlyList<T>, IEquatable<EquatableReadOnlyMemory<T>>
{
	private ReadOnlyMemory<T> Memory { get; } = memory;

	public ReadOnlySpan<T> Span => Memory.Span;

	public int Length => Memory.Length;

	public int Count => Memory.Length;

	public T this[int index] => Memory.Span[index];

	public bool Equals(EquatableReadOnlyMemory<T> other) => Memory.Span.SequenceEqual(other.Memory.Span);

	public override bool Equals(object? obj) => obj is EquatableReadOnlyMemory<T> other && Equals(other);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (var item in Memory.Span) {
			hash.Add(item);
		}

		return hash.ToHashCode();
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (var i = 0; i < Memory.Length; i++) {
			yield return Memory.Span[i];
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static bool operator ==(EquatableReadOnlyMemory<T> left, EquatableReadOnlyMemory<T> right) => left.Equals(right);

	public static bool operator !=(EquatableReadOnlyMemory<T> left, EquatableReadOnlyMemory<T> right) => !left.Equals(right);
}
