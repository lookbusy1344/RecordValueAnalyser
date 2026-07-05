namespace RecordValueAnalyser.Useful;

using System.Collections;
using System.Text.Json.Serialization;

/// <summary>
///     An <see cref="ArraySegment{T}" /> with <em>value</em> equality: two instances are equal when their
///     elements are equal in order. <see cref="ArraySegment{T}" /> is explicitly flagged by the JSV01
///     analyser as never having value semantics — it compares backing-array identity, not contents. A
///     <c>readonly struct</c> wrapping the segment directly, so it never copies the underlying array.
/// </summary>
[JsonConverter(typeof(EquatableArraySegmentJsonConverterFactory))]
public readonly struct EquatableArraySegment<T>(ArraySegment<T> segment) : IReadOnlyList<T>, IEquatable<EquatableArraySegment<T>>
{
	private ArraySegment<T> Segment { get; } = segment;

	public ReadOnlySpan<T> Span => Segment.AsSpan();

	public int Count => Segment.Count;

	public T this[int index] => Segment[index];

	public bool Equals(EquatableArraySegment<T> other) => Segment.AsSpan().SequenceEqual(other.Segment.AsSpan());

	public override bool Equals(object? obj) => obj is EquatableArraySegment<T> other && Equals(other);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		foreach (var item in Segment.AsSpan()) {
			hash.Add(item);
		}

		return hash.ToHashCode();
	}

	public IEnumerator<T> GetEnumerator()
	{
		for (var i = 0; i < Segment.Count; i++) {
			yield return Segment[i];
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public static bool operator ==(EquatableArraySegment<T> left, EquatableArraySegment<T> right) => left.Equals(right);

	public static bool operator !=(EquatableArraySegment<T> left, EquatableArraySegment<T> right) => !left.Equals(right);
}
