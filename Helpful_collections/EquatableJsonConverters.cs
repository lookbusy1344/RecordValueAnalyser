namespace RecordValueAnalyser.Useful;

using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

/// <summary>
///     Serialises <see cref="EquatableArray{T}" /> as a plain JSON array. Each element is (de)serialised
///     through the ambient <see cref="JsonSerializerOptions" /> — i.e. the source-generated
///     <see cref="EnrolmentJsonContext" /> — so the wrapper stays reflection-free: it adds no metadata of
///     its own, it just borrows the element's generated <see cref="JsonTypeInfo{T}" />.
/// </summary>
internal sealed class EquatableArrayJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EquatableArray<>);

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(
			typeof(EquatableArrayJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}

internal sealed class EquatableArrayJsonConverter<T> : JsonConverter<EquatableArray<T>>
{
	public override EquatableArray<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartArray) {
			throw new JsonException($"Expected start of array for {typeToConvert}.");
		}

		var elementInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
		var builder = ImmutableArray.CreateBuilder<T>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
			builder.Add(JsonSerializer.Deserialize(ref reader, elementInfo)!);
		}

		return EquatableArray.CopyOf(builder.ToImmutable());
	}

	public override void Write(Utf8JsonWriter writer, EquatableArray<T> value, JsonSerializerOptions options)
	{
		var elementInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
		writer.WriteStartArray();
		foreach (var item in value) {
			JsonSerializer.Serialize(writer, item, elementInfo);
		}

		writer.WriteEndArray();
	}
}

/// <summary>
///     Serialises <see cref="EquatableDictionary{TKey, TValue}" /> as a plain JSON object by delegating to
///     the source-generated <see cref="Dictionary{TKey, TValue}" /> contract, so the wrapper inherits the
///     context's key-naming policy unchanged.
/// </summary>
internal sealed class EquatableDictionaryJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EquatableDictionary<,>);

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(
			typeof(EquatableDictionaryJsonConverter<,>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}

internal sealed class EquatableDictionaryJsonConverter<TKey, TValue> : JsonConverter<EquatableDictionary<TKey, TValue>>
	where TKey : notnull
{
	public override EquatableDictionary<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var info = (JsonTypeInfo<Dictionary<TKey, TValue>>)options.GetTypeInfo(typeof(Dictionary<TKey, TValue>));
		// The deserialized dictionary is freshly allocated and unaliased, so hand it straight to the
		// ownership constructor rather than copying it again through the public CopyOf factory.
		var entries = JsonSerializer.Deserialize(ref reader, info)
					  ?? throw new JsonException("Expected a JSON object.");
		return new(entries);
	}

	public override void Write(Utf8JsonWriter writer, EquatableDictionary<TKey, TValue> value, JsonSerializerOptions options)
	{
		var info = (JsonTypeInfo<Dictionary<TKey, TValue>>)options.GetTypeInfo(typeof(Dictionary<TKey, TValue>));
		JsonSerializer.Serialize(writer, new(value), info);
	}
}

/// <summary>Serialises <see cref="EquatableReadOnlyMemory{T}" /> as a plain JSON array.</summary>
internal sealed class EquatableReadOnlyMemoryJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EquatableReadOnlyMemory<>);

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(
			typeof(EquatableReadOnlyMemoryJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}

internal sealed class EquatableReadOnlyMemoryJsonConverter<T> : JsonConverter<EquatableReadOnlyMemory<T>>
{
	public override EquatableReadOnlyMemory<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var arrayInfo = (JsonTypeInfo<T[]>)options.GetTypeInfo(typeof(T[]));
		var items = JsonSerializer.Deserialize(ref reader, arrayInfo) ?? [];
		return new(items);
	}

	public override void Write(Utf8JsonWriter writer, EquatableReadOnlyMemory<T> value, JsonSerializerOptions options)
	{
		var elementInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
		writer.WriteStartArray();
		foreach (var item in value.Span) {
			JsonSerializer.Serialize(writer, item, elementInfo);
		}

		writer.WriteEndArray();
	}
}

/// <summary>Serialises <see cref="EquatableArraySegment{T}" /> as a plain JSON array.</summary>
internal sealed class EquatableArraySegmentJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EquatableArraySegment<>);

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(
			typeof(EquatableArraySegmentJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}

internal sealed class EquatableArraySegmentJsonConverter<T> : JsonConverter<EquatableArraySegment<T>>
{
	public override EquatableArraySegment<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var arrayInfo = (JsonTypeInfo<T[]>)options.GetTypeInfo(typeof(T[]));
		var items = JsonSerializer.Deserialize(ref reader, arrayInfo) ?? [];
		return new(items);
	}

	public override void Write(Utf8JsonWriter writer, EquatableArraySegment<T> value, JsonSerializerOptions options)
	{
		var elementInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
		writer.WriteStartArray();
		foreach (var item in value.Span) {
			JsonSerializer.Serialize(writer, item, elementInfo);
		}

		writer.WriteEndArray();
	}
}

/// <summary>Serialises <see cref="EquatableSet{T}" /> as a plain JSON array.</summary>
internal sealed class EquatableSetJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EquatableSet<>);

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(
			typeof(EquatableSetJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}

internal sealed class EquatableSetJsonConverter<T> : JsonConverter<EquatableSet<T>>
{
	public override EquatableSet<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var arrayInfo = (JsonTypeInfo<T[]>)options.GetTypeInfo(typeof(T[]));
		var items = JsonSerializer.Deserialize(ref reader, arrayInfo)
					?? throw new JsonException($"Expected an array for {typeToConvert}.");
		return EquatableSet.CopyOf(items);
	}

	public override void Write(Utf8JsonWriter writer, EquatableSet<T> value, JsonSerializerOptions options)
	{
		var elementInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
		writer.WriteStartArray();
		foreach (var item in value) {
			JsonSerializer.Serialize(writer, item, elementInfo);
		}

		writer.WriteEndArray();
	}
}

/// <summary>Serialises <see cref="EquatableGrid{T}" /> as a JSON array of row arrays.</summary>
internal sealed class EquatableGridJsonConverterFactory : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert) =>
		typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(EquatableGrid<>);

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
		(JsonConverter)Activator.CreateInstance(
			typeof(EquatableGridJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}

internal sealed class EquatableGridJsonConverter<T> : JsonConverter<EquatableGrid<T>>
{
	public override EquatableGrid<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		var rowInfo = (JsonTypeInfo<T[]>)options.GetTypeInfo(typeof(T[]));

		if (reader.TokenType != JsonTokenType.StartArray) {
			throw new JsonException($"Expected start of array for {typeToConvert}.");
		}

		var rows = new List<T[]>();
		while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
			rows.Add(JsonSerializer.Deserialize(ref reader, rowInfo) ?? []);
		}

		var rowCount = rows.Count;
		var columnCount = rowCount > 0 ? rows[0].Length : 0;
		var flat = new T[rowCount * columnCount];
		for (var row = 0; row < rowCount; row++) {
			rows[row].AsSpan().CopyTo(flat.AsSpan(row * columnCount, columnCount));
		}

		return new(rowCount, columnCount, flat);
	}

	public override void Write(Utf8JsonWriter writer, EquatableGrid<T> value, JsonSerializerOptions options)
	{
		var elementInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
		writer.WriteStartArray();
		for (var row = 0; row < value.Rows; row++) {
			writer.WriteStartArray();
			for (var column = 0; column < value.Columns; column++) {
				JsonSerializer.Serialize(writer, value[row, column], elementInfo);
			}

			writer.WriteEndArray();
		}

		writer.WriteEndArray();
	}
}
