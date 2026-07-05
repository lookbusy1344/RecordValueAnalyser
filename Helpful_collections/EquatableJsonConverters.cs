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
