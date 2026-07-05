namespace RecordValueAnalyser.Useful;

using System.Text.Json.Serialization;

/// <summary>
///     A replacement for <c>T[,]</c> with <em>value</em> equality: two instances are equal when their
///     dimensions and elements match. A rectangular array has no structural equality at all, and the JSV01
///     analyser does not recurse into it as a collection. Backed by a single flat <c>T[]</c>, so it costs
///     one allocation regardless of row/column count, rather than an array-of-arrays.
/// </summary>
[JsonConverter(typeof(EquatableGridJsonConverterFactory))]
public readonly struct EquatableGrid<T> : IEquatable<EquatableGrid<T>>
{
	private readonly T[]? items;

	public EquatableGrid(int rows, int columns)
	{
		Rows = rows;
		Columns = columns;
		items = rows > 0 && columns > 0 ? new T[rows * columns] : [];
	}

	internal EquatableGrid(int rows, int columns, T[] items)
	{
		Rows = rows;
		Columns = columns;
		this.items = items;
	}

	public int Rows { get; }

	public int Columns { get; }

	private T[] Items => items ?? [];

	public T this[int row, int column]
	{
		get => Items[(row * Columns) + column];
		set => Items[(row * Columns) + column] = value;
	}

	public bool Equals(EquatableGrid<T> other) =>
		Rows == other.Rows && Columns == other.Columns && Items.AsSpan().SequenceEqual(other.Items.AsSpan());

	public override bool Equals(object? obj) => obj is EquatableGrid<T> other && Equals(other);

	public override int GetHashCode()
	{
		var hash = new HashCode();
		hash.Add(Rows);
		hash.Add(Columns);
		foreach (var item in Items) {
			hash.Add(item);
		}

		return hash.ToHashCode();
	}

	public static bool operator ==(EquatableGrid<T> left, EquatableGrid<T> right) => left.Equals(right);

	public static bool operator !=(EquatableGrid<T> left, EquatableGrid<T> right) => !left.Equals(right);
}

/// <summary>Explicit copy helper for <see cref="EquatableGrid{T}" />.</summary>
public static class EquatableGrid
{
	/// <summary>
	///     Explicitly copy a rectangular array into a structurally comparable grid.
	/// </summary>
	public static EquatableGrid<T> CopyOf<T>(T[,] source)
	{
		ArgumentNullException.ThrowIfNull(source);

		var rows = source.GetLength(0);
		var columns = source.GetLength(1);
		var items = new T[rows * columns];

		for (var row = 0; row < rows; row++) {
			for (var column = 0; column < columns; column++) {
				items[(row * columns) + column] = source[row, column];
			}
		}

		return new(rows, columns, items);
	}
}
