# Helpful collections

Drop-in replacements for the collection types that most commonly trigger [JSV01](../README.md), so a record member can keep proper value semantics instead of needing a hand-written `Equals`/`GetHashCode` override. All types are `readonly struct`s that wrap an existing BCL collection directly, so none of them allocate beyond what the wrapped collection already costs.

Namespace: `RecordValueAnalyser.Useful`. Project: `Helpful_collections.csproj`. Tests: `../Helpful_collections.Test`.

## Types

- **`EquatableArray<T>`** — a value-semantics replacement for `IReadOnlyList<T>`/arrays. Wraps an `ImmutableArray<T>`. Equality compares elements in order. Supports collection expressions (`[]`, `[.. xs]`) via `[CollectionBuilder]`.
- **`EquatableDictionary<TKey, TValue>`** — a value-semantics replacement for `IReadOnlyDictionary<TKey, TValue>`. Wraps a `Dictionary<TKey, TValue>`. Equality compares key/value entries independent of insertion order.
- **`EquatableSet<T>`** — a value-semantics replacement for `IReadOnlySet<T>`/`HashSet<T>`. Wraps a `HashSet<T>`. Equality compares elements independent of insertion order. Supports collection expressions via `[CollectionBuilder]`.
- **`EquatableReadOnlyMemory<T>`** — a value-semantics replacement for `ReadOnlyMemory<T>`, which the analyser explicitly flags as never having value semantics (it compares span identity, not contents). Wraps the memory directly — no copy of the underlying buffer.
- **`EquatableArraySegment<T>`** — the same fix for `ArraySegment<T>`, also explicitly flagged by the analyser. Wraps the segment directly.
- **`EquatableGrid<T>`** — a value-semantics replacement for `T[,]`, which has no structural equality at all and isn't a type the analyser recurses into. Backed by a single flat `T[]` sized `rows * columns`, so it costs one allocation regardless of dimensions, rather than an array-of-arrays.
- **`EquatableJsonConverters.cs`** — internal `System.Text.Json` converters required by the types above; each struct is annotated with `[JsonConverter(typeof(...Factory))]`, so including this file is what makes them serialise as plain JSON arrays/objects instead of STJ reflecting over the struct's fields. Not called directly — it wires itself in automatically, and delegates to the element/value type's own `JsonTypeInfo`, so it works with source-generated (reflection-free) `JsonSerializerContext` setups too.

## Example

```csharp
public record Order(int Id, EquatableArray<int> LineItemIds, EquatableDictionary<string, decimal> Prices);

var a = new Order(1, [1, 2, 3], EquatableDictionaryFactory.CopyOf(prices));
var b = new Order(1, [1, 2, 3], EquatableDictionaryFactory.CopyOf(prices));

a == b; // true — compares contents, not references
```

## A note on mutability

`EquatableReadOnlyMemory<T>` and `EquatableArraySegment<T>` compare the contents of the underlying buffer at the time of comparison — like the types they replace, they don't defensively copy, so mutating the backing array after construction is still visible through the wrapper. `EquatableGrid<T>` is intentionally mutable in the same way, via its `[row, column]` setter. `EquatableArray<T>`, `EquatableDictionary<TKey, TValue>`, and `EquatableSet<T>` copy on construction (`CopyOf`) and expose no mutators, so they stay genuinely immutable.
