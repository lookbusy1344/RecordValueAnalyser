# Framework Design Guidelines — Modern .NET Supplement

Platform and language capabilities that postdate or qualify parts of Cwalina and Abrams, *Framework Design Guidelines* (4th ed.). Not book text. Read it beside [Framework Design Guidelines — Essentials](Framework_Design_Guidelines_Essentials.md); the book's scenario-first design, naming, compatibility, exception, and extensibility principles remain the foundation.

This supplement reuses the Essentials' strength vocabulary: **DO** and **DO NOT** are strong recommendations; **CONSIDER** and **AVOID** are weaker defaults. These markers express editorial recommendations derived from the linked current documentation, not labels from the book.

## Nullability is part of API design

- **DO** enable nullable reference type analysis for new libraries and annotate the whole public surface deliberately.
- `T` declares a reference expected non-null; `T?` declares null part of the contract. The annotation is not a distinct CLR type, so changes usually affect compilation analysis, not runtime identity.
- **DO** use `System.Diagnostics.CodeAnalysis` attributes (`NotNullWhen`, etc.) when input/output/Boolean relationships cannot be expressed by `?` alone.
- Annotations improve caller analysis; they do not replace validation at a public boundary. **DO** keep the two in agreement.
- **DO** audit defaults: zero-initialised structs and arrays can place null in storage annotated non-nullable.

Source: [Nullable reference types](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/null-safety/nullable-reference-types).

## Date and time types that express the domain

The book uses `DateTime` at midnight for a whole date and `TimeSpan` for a time of day. On modern targets:

- **DO** use `DateOnly` for a calendar date with no time or offset (birthday, business date).
- **DO** use `TimeOnly` for a time of day. **DO** keep `TimeSpan` for elapsed time or duration.
- **DO** use `DateTimeOffset` for an exact instant with an offset.
- **DO** use `DateTime` only when the domain combines date and time without an exact offset, or for compatibility.
- `DateOnly`/`TimeOnly` require .NET 6+; they are unavailable on .NET Framework.

Source: [How to use `DateOnly` and `TimeOnly`](https://learn.microsoft.com/en-us/dotnet/standard/datetime/how-to-use-dateonly-timeonly).

## `required` and `init` are contract choices

- `init` allows object-initializer syntax with post-construction immutability, but still exposes initialization order and initializer semantics.
- `required` makes initialization a compile-time obligation. **DO** analyse adding it to a shipped member as a source-breaking contract change.
- A `required` reference member can still be set to `null`; nullable analysis warns, it does not guarantee.
- `SetsRequiredMembers` asserts a constructor satisfies required members; the compiler does not verify the assertion.
- **DO** prefer constructors when invariants need atomic validation or partially initialised states complicate the contract.

Source: [`required` modifier](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/required).

## Records and value semantics

**CONSIDER** a record when value equality is part of the domain, not merely convenient:

- Record class: reference storage where same-component instances should compare equal.
- `readonly record struct`: when all struct criteria hold and the generated equality, `GetHashCode`, `==`/`!=`, printing, and copying are the intended contract. **DO** use `readonly`; a plain `record struct` is mutable.
- A regular struct without explicit equality falls back to `ValueType.Equals`, which may use reflection. **DO** avoid that by implementing `IEquatable<T>`, `Equals`, `GetHashCode`, and the operators, as the Essentials already requires.
- **DO** keep a hand-written struct when equality needs domain-specific behaviour or the generated surface is inappropriate. **DO NOT** pick a record as a presumed performance win; measure hot paths.
- **DO** treat positional parameter names and order as durable API; they shape the constructor, properties, and deconstruction.
- **DO** prefer an explicit nominal type when invariants, identity, inheritance evolution, or surface control matter more than concise syntax.

Source: [Records](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/types/records).

## Default interface members are a narrow evolution tool

Default interface members can let existing implementations continue working when an interface gains a function member, but they do not make interface evolution generally safe:

- **DO** continue treating an interface's required abstract member set as fixed after release.
- **CONSIDER** a default interface member when new behaviour has a coherent implementation expressible entirely through the existing contract.
- **DO NOT** use a default interface member to introduce a new required capability or assume every target framework and consumer language can use it.
- **DO** analyse source, binary, runtime, and behavioural compatibility across every supported target and language before adding any interface member.
- **CONSIDER** a new derived interface or an abstract base class when the new concept cannot safely default.

Sources: [C# interface specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/interfaces), [default interface method versioning tutorial](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/interface-implementation/default-interface-methods-versions), and [breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes).

## Keep `Task<T>` as the async default

The 4th edition includes `ValueTask<T>`, but compression can make the choices look equivalent:

- **DO** prefer `Task`/`Task<T>` for public async APIs; they are easier to consume and compose.
- **CONSIDER** `ValueTask<T>` only when synchronous completion is common *and* the method is on a critical path where the allocation saving is worth the consumption constraints.
- `ConfigureAwait(false)` follows the consuming app model and synchronization-context needs; it is not API shape and does not alter the task.

Source: [Async return types](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-return-types) and [`ConfigureAwait` FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/).

## Runtime serialization is legacy compatibility work

The book's runtime-serialization guidance targets .NET Framework AppDomain and remoting scenarios. It is not the default model for modern libraries:

- **AVOID** `[Serializable]`, `ISerializable`, and serialization constructors on new general-purpose public types unless a supported legacy boundary explicitly requires them.
- **DO NOT** use `BinaryFormatter`; it is insecure and cannot be made secure. Starting with .NET 9, its in-box implementation always throws.
- **DO** choose an explicit serialization contract and format appropriate to the application boundary rather than coupling a general-purpose domain type to runtime serialization.
- **DO** treat persisted formats as compatibility contracts and test backward and forward compatibility independently of CLR API compatibility.
- **DO** retain legacy exception serialization only while a supported .NET Framework AppDomain or remoting scenario requires it; document that constraint.

Sources: [`BinaryFormatter` security guide](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide), [`BinaryFormatter` migration guide](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-migration-guide/), and [.NET 9 removal](https://learn.microsoft.com/en-us/dotnet/core/compatibility/serialization/9.0/binaryformatter-removal).

## Make compatibility checks executable

**DO** enforce the book's compatibility taxonomy in the release process:

- **DO** compare the candidate against a supported baseline with package validation or `Microsoft.DotNet.ApiCompat.Tool`.
- A clean binary comparison is necessary but insufficient: behavioral, source, serialization, reflection, and recompile changes still need review.
- **DO** keep compatible target-framework assemblies at the same API surface or a documented superset.
- **DO** minimise dependencies exposed through public signatures. **DO** review dependency changes as compatibility changes.

Sources: [NuGet package compatibility rules](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/nuget-package-compatibility-rules), [Package validation](https://learn.microsoft.com/en-us/dotnet/fundamentals/package-validation/overview), and [Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes).

## Separate API policy from implementation style

Appendix A records the BCL team's house style at publication. Treat it as one coherent style, not a compatibility contract:

- **DO** let repository formatting and analyser configuration enforce implementation style.
- **DO** keep public API naming and contract rules under explicit review even when formatting is automated.
- **DO NOT** promote historical style preferences into API requirements unless they affect generated source, source compatibility, or consumer experience.

API design choices constrain consumers for years; most implementation-style choices can be changed mechanically inside the library.
