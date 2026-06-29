# Framework Design Guidelines — Essentials

A distilled reference to Cwalina & Abrams, *Framework Design Guidelines* (4th ed.).
Rule strength and direction: **DO** and **DO NOT** are strong positive and negative rules; **CONSIDER** and **AVOID** are weaker positive and negative defaults. These are strong defaults, not substitutes for understanding the trade-off. When a compressed bullet combines rules of different strengths, each rule repeats its own marker; unmarked sentences explain rationale, scope, or consequences rather than introduce another rule.
Each chapter merges the core rules with the finer points. API design (ch. 2, 5) and error handling (ch. 7) carry extra depth.

This document distinguishes the book's guidance from editorial material. Text marked **Editorial synthesis** connects ideas across chapters; the quick-reference sheets are derived decision aids rather than additional book rules. For post-book guidance, see [Modern .NET Supplement](Framework_Design_Guidelines_Modern_Supplement.md).

## Contents

- [1. Introduction](#1-introduction)
- [2. Framework Design Fundamentals](#2-framework-design-fundamentals--api-design--core-chapter)
- [3. Naming Guidelines](#3-naming-guidelines)
- [4. Type Design Guidelines](#4-type-design-guidelines)
- [5. Member Design](#5-member-design--api-design--core-chapter)
- [6. Designing for Extensibility](#6-designing-for-extensibility)
- [7. Exceptions](#7-exceptions--error-handling--core-chapter)
- [8. Usage Guidelines](#8-usage-guidelines-using-common-types-in-apis)
- [9. Common Design Patterns](#9-common-design-patterns)
- [Appendix A — C# Coding Style Conventions](#appendix-a--c-coding-style-conventions)
- [Appendix B — Obsolete Guidance](#appendix-b--obsolete-guidance-know-what-changed)
- [Appendix C — Minimal API Specification](#appendix-c--minimal-api-specification)
- [Appendix D — Breaking Changes](#appendix-d--breaking-changes-compatibility-taxonomy)
- [Quick-Reference Cheat-Sheets](#quick-reference-cheat-sheets)

---

## 1. Introduction

*Source: chapter 1. The “Guiding principles” subsection is editorial synthesis across chapters 1, 2, 5, and 6.*

- Frameworks are read and used far more than they are written. Optimise for the **consumer**, not the author.
- Public API is a long-term compatibility commitment: a breaking change can affect any customer. Design deliberately, review hard, ship slowly.
- Three pillars run through the whole book: **simple things stay simple**, **consistency** with the platform, and **scenario-driven** design.

### Guiding principles (editorial synthesis)

When a specific guideline doesn't cover your case, fall back to these — they're what the rules are derived from:

- **Frameworks must be designed from scenarios.** Start from the code you want the consumer to write; derive the object model from it, not the reverse.
- **Design the "pit of success."** Make the correct, secure, performant path the *easiest* one to fall into, and the wrong path require deliberate effort. Users should succeed without reading docs and struggle to misuse the API.
- **Be self-documenting.** Names, types, and shapes should make intent obvious at the call site. Strong typing turns runtime mistakes into compile-time errors.
- **Be consistent** — internally, with .NET, and with frameworks your users combine with yours. Consistency lets knowledge transfer instead of being relearned.
- **Layer for both audiences** — productivity (high-level) and power (low-level) — and keep low-level complexity out of mainline namespaces.
- **Power vs. simplicity is a false tension.** A well-layered framework delivers both; when genuinely forced to choose for the mainline scenario, favour simplicity.
- **Every public element is a lifetime commitment.** Virtual members, protected members, and abstractions are contracts you maintain forever; add them only when proven.

---

## 2. Framework Design Fundamentals  *(API design — core chapter)*

*Source: chapter 2, especially §§2.1–2.2.4.*

The thesis of the book: a good API is *discovered*, not *read about*. The design process, not the cleverness of the object model, is what produces that.

**Design for the full audience**
- **DO** design frameworks that are both powerful *and* easy to use — these are not in tension if you layer correctly.
- **DO** explicitly design for a broad range of developers: different styles, skill levels, and requirements. The "opportunistic" developer (copy-paste, IntelliSense-driven) and the "systematic" developer (reads specs, wants control) both have to succeed.
- **DO** design for the broad variety of CLR languages — what reads naturally in C# may be awkward in F# or VB.

**Scenario-driven design (the central practice)**
- **DO** make the API design specification the centre of any feature with a public surface.
- **DO** define the top usage scenarios for each major feature area, at an abstraction level matching real end-user use cases.
- **DO** design APIs by *writing the sample code first* for the main scenarios, then defining the object model to support that code. The samples are the spec.
- **DO** write those samples in at least two different language families (e.g. C# and F#); **CONSIDER** also a dynamic language (PowerShell, IronPython) to stress-test.
- **DO NOT** rely solely on standard design methodologies (UML, classic OO decomposition) for the public layer — they optimise the implementation model, not the usage model.
- **DO** run usability studies on the main scenarios. Watching real developers fail is the fastest way to find bad names and bad shapes.

**Low barrier to entry**
- **DO NOT** require users to instantiate more than one type, perform extensive initialization, or touch advanced members for the most basic scenarios.
- **DO NOT** put members intended for advanced scenarios on types intended for mainline use — they clutter IntelliSense and confuse the common case.
- **DO** provide simple overloads with few, primitive parameters. **DO** provide good defaults for every property and parameter via convenience overloads where possible.
- **DO** ensure each main feature-area namespace contains only common-scenario types. **DO** push advanced types into subnamespaces.

**Self-documenting and consistent**
- **DO** ensure APIs are usable in basic scenarios *without* reading reference docs; if a developer must read docs to make a hello-world work, the shape is wrong.
- **DO NOT** be afraid of verbose identifiers when they make the API self-documenting. **DO** make naming a first-class topic in spec reviews. **CONSIDER** involving technical writers early.
- **DO** communicate incorrect usage through exceptions (see ch. 7). **DO** provide strongly typed APIs wherever possible to push errors to compile time.
- **DO** stay consistent with .NET and any other frameworks your users are likely to combine with yours. Consistency lets users transfer knowledge instead of relearning.
- **DO** still ship great documentation. **DO** provide code samples for the most important APIs; self-documenting is the floor, not a substitute.

**Layering**
- **CONSIDER** a layered framework: high-level APIs optimised for productivity, low-level APIs optimised for power and expressiveness.
- **AVOID** many abstractions in mainline-scenario APIs — each abstraction is a concept the user must learn before being productive.
- **AVOID** mixing very complex low-level APIs into the same namespace as high-level ones.
- **DO** keep layers of a single feature area well integrated, so a developer can start in one layer and drop to another without rewriting.

---

## 3. Naming Guidelines

*Source: chapter 3, §§3.1–3.8.*

**Casing & characters**
- **DO** use PascalCasing for namespaces, types, members, and generic type parameters. **DO** use camelCasing for parameters.
- **DO** capitalise both letters of a two-letter acronym (`IOStream`), but only the first of a 3+-letter acronym (`XmlReader`). **DO NOT** capitalise *any* acronym letters at the start of a camelCased identifier (`ioStream`, not `IOStream`).
- **DO NOT** split closed-form compound words (`Endpoint`, `Callback`, `Filename` — not `EndPoint`/`CallBack`/`FileName`).
- **DO NOT** use underscores, hyphens, other non-alphanumerics, or Hungarian notation. **DO** use ASCII only. **DO NOT** assume case sensitivity — names must not differ by case alone.

**Word choice**
- **DO** favour readability over brevity: `CanScrollHorizontally` beats `ScrollableX`.
- **DO NOT** use unapproved abbreviations/contractions or obscure acronyms.
- For an identifier with no semantic meaning beyond its type, **DO** use the CLR type name (`GetInt32`) or a common word (`value`, `item`) — not a language keyword.

**Namespaces**
- **DO** use `Company.Technology[.Feature][.Subnamespace]`, PascalCased, periods between components.
- **DO** use a stable, version-independent product name at the second level; **DO NOT** base namespaces on org charts (teams get renamed).
- **DO NOT** name a namespace the same as a type within it; **CONSIDER** plural namespace names where natural.
- **DO NOT** introduce overly general type names (`Element`, `Node`, `Log`, `Message`) or names that conflict with core/technology namespace types.
- **DO** name assemblies/packages for broad functionality, independently of namespace factoring. **CONSIDER** `<Company>.<Component>`.

**Types**
- **DO** name classes/structs with nouns or noun phrases; interfaces with adjective phrases (occasionally nouns), prefixed `I`.
- **DO NOT** prefix class names with `C`. **CONSIDER** ending a derived class with the base-class name. **DO** make a standard implementation/interface pair differ only by the `I` (`Component`/`IComponent`).
- **DO** name generic type parameters descriptively and prefix descriptive names with `T` (`TKey`, `TSession`). **CONSIDER** bare `T` for a single, self-explanatory parameter. **CONSIDER** encoding constraints in the name.
- **DO** follow the standard suffix conventions for derived BCL types (`*Stream`, `*Collection`, `*Dictionary`, `*Exception`, `*EventArgs`, `*Attribute`).
- **DO** reserve the best, shortest names for the most commonly used types.

**Enums**
- **DO** use a singular name for a simple enum and a plural name for a `[Flags]` enum; **DO NOT** use `Enum`, `Flag`, or `Flags` suffixes, or value-name prefixes (`adXxx`, `rtfXxx`).

**Members**
- **DO** name methods with verbs or verb phrases. **DO** name properties with nouns, noun phrases, or adjectives. **DO NOT** define a property matching the name of a `Get` method.
- **DO** name Boolean properties affirmatively (`CanSeek`, `IsEnabled`) and collection properties with a plural (not `XList`/`XCollection`).
- **DO** name events with a verb and a tense distinction (`Closing`/`Closed`). **DO NOT** use `Before`/`After` pre/post prefixes. **DO** end event-handler type names with `EventHandler`, end argument-class names with `EventArgs`, and use `(sender, e)` for handler parameters.
- **DO** use PascalCase for public/protected field names. **DO NOT** prefix them. **DO NOT** expose localizable resources directly as public or protected members.
- **DO** use `left`/`right` for binary operator parameters and `value` for unary operator parameters when they have no domain meaning. **DO NOT** use abbreviations or numeric indices for them.

**Versioning a name**
- **DO** keep the new name similar to the old. **DO** prefer a *suffix* over a prefix. **DO** use a numeric suffix where no meaningful alternative exists (`X509Certificate2`). **DO NOT** use the `Ex` suffix. **DO** use a `64` suffix for a 64-bit-integer variant of an existing 32-bit API.

---

## 4. Type Design Guidelines

*Source: chapter 4, §§4.1–4.11.*

**DO** make each type a coherent set of related members, not a grab-bag. **DO** use namespaces to organise types into feature-area hierarchies. **AVOID** very deep hierarchies, too many namespaces, and mixing advanced types with common ones. **DO NOT** define a type with no namespace.

**Class vs. interface vs. struct**
- **DO** favour classes over interfaces. **DO** use an abstract class to decouple a contract from its implementations; abstract classes can safely add members more readily than interfaces.
- **DO** define an interface when a common API must span value types. **CONSIDER** an interface for multiple-inheritance-like mixing or to add a contract to types that already have a base class. **AVOID** marker (empty) interfaces; use an attribute unless compile-time checking is essential.
- **DO NOT** add members to an already-shipped interface — existing implementations can break. Default interface members can help with narrow cases but are not a general-purpose interface-evolution strategy; see the [Modern .NET Supplement](Framework_Design_Guidelines_Modern_Supplement.md#default-interface-members-are-a-narrow-evolution-tool).
- **DO** ship at least one concrete implementation *and* at least one consuming API for every interface and abstract class you define — it's the only way to know the abstraction is usable.
- **DO NOT** give abstract types public or protected-internal constructors; **DO** give them a protected or internal constructor.
- **Modern supplement:** when value equality is part of the domain, **CONSIDER** a record class for reference storage or a `readonly record struct` when all struct criteria also hold. **CONSIDER** a `readonly record struct` over a hand-written struct when its generated equality, operators, printing, and copying contract are all appropriate; see the [Modern .NET Supplement](Framework_Design_Guidelines_Modern_Supplement.md#records-and-value-semantics).

**Static classes** — **DO** use them sparingly. **DO NOT** treat one as a miscellaneous bucket. **DO** declare the class `static` (or abstract and sealed with a private constructor where the language lacks direct support). **DO NOT** define instance members.

**Structs** (§4.2) — **AVOID** defining a struct unless *all* hold: it logically represents a single value, its instance size is less than 24 bytes, it is immutable, and it will not be boxed frequently. The source also records an annotator's more conservative ≈16-byte rule of thumb, but that is not the normative threshold.
- **DO NOT** define mutable value types. **DO NOT** provide a default constructor for a struct. **DO** ensure the all-zero state is valid.
- **DO** apply `readonly` to immutable structs and to non-mutating methods of mutable structs. **DO** implement `IEquatable<T>`.
- **DO NOT** explicitly extend `ValueType`/`Enum`, or define a `ref struct` outside specialised low-level perf code.

**Enums**
- **DO** prefer an enum over `static const` sets to strongly type the values. **CONSIDER** `Int32` as the underlying type unless you need interop sizing, more than 32 flags, or a smaller footprint.
- **DO NOT** use enums for open sets, reserved/future values, or sentinels. **AVOID** single-value public enums. **DO** give simple enums a zero value.
- For flag enums, **DO** use a plural name, powers-of-two values, and `[Flags]`. **DO NOT** apply `[Flags]` to a simple enum. **DO** name zero `None` and make it mean “all flags are cleared.” **CONSIDER** predefined combined values. **AVOID** invalid combinations.
- **CONSIDER** adding enum values despite the compatibility risk. Additions are safer for input-only and flag enums than for values returned to callers, whose exhaustive switches may break.

**Nested types** — **DO** use one when member-accessibility coupling to the outer type is genuinely wanted. **AVOID** publicly exposed nested types. **DO NOT** use them as a grouping mechanism, when the type is likely to be referenced or instantiated externally, or as an interface member.

**Assemblies** — **DO** apply `CLSCompliant(true)`, `AssemblyVersion`, and informational attributes to any assembly with public types. **CONSIDER** `ComVisible(false)`, `AssemblyFileVersion`, and `AssemblyCopyright`. **CONSIDER** file-version format `V.S.B.R` (major.servicing.build.revision).

**Strongly typed strings** — **CONSIDER** one when a base class supports a fixed set of string inputs but derived classes may support more. **DO** implement it as an immutable `struct` with a string constructor that accepts null. **DO** make `ToString()` return the underlying string representation, using an empty string for the zero/null state. **DO** override equality operators. **CONSIDER** exposing the raw string through a get-only property. **DO** expose known values through static get-only properties. **AVOID** overloads that differ only between `String` and the wrapper, unless the `String` overload shipped previously.

---

## 5. Member Design  *(API design — core chapter)*

*Source: chapter 5, §§5.1–5.9.*

This is where most of the day-to-day API surface lives. The members are what the consumer actually calls; their shape decides whether the API feels obvious or hostile.

**Overloads**
- **DO NOT** arbitrarily vary parameter names across overloads. **AVOID** inconsistent parameter ordering. A parameter representing the same input should keep the same name and position.
- **DO** make only the longest overload virtual (when extensibility is needed); shorter overloads call through to it, so subclasses have one place to override.
- **DO NOT** overload solely on `ref`/`out`/`in`; **DO NOT** give same-position parameters similar types but different semantics; **DO NOT** mix a generic parameter in one overload against a concrete type in another.
- **DO** allow `null` for optional arguments. **DO** use descriptive parameter names so a short overload documents the default exposed by the long one.

**Default parameters**
- **CONSIDER** default parameters on the longest overload, but **DO** also provide a no-default simple overload when ≥2 are defaulted.
- **DO NOT** use default parameters on virtual or interface methods (except `CancellationToken`) — the default is baked into the call site, not the override.
- **DO NOT** change a default value once shipped; call sites already compiled in the old one. **DO NOT** create overloads whose required parameters and defaults make invocation ambiguous. **AVOID** multiple overloads that use defaults. **DO** move defaults onto the new longer overload when extending a method.

**Explicit interface members**
- **AVOID** explicit implementation without a strong reason: members become less discoverable, and interface casts can box value types.
- **CONSIDER** it for infrastructure-only members, to adapt an interface member's type, or—sparingly—to expose a better public name. **DO NOT** treat it as a security boundary.
- If derived classes must customise the behaviour, **DO** route the explicit member through an equivalent protected virtual member, conventionally suffixed `Core`.

**Properties vs. methods**
- **CONSIDER** a property when the member is a logical attribute backed by in-process state and the getter is cheap.
- **DO** use a *method* instead when: the work is orders-of-magnitude slower than a field read, it has an observable side effect, it returns a different value each call, it returns a copy, it returns an array, or it's a conversion (`ToXxx`).
- **DO** create get-only properties when callers shouldn't change the value; **DO NOT** provide set-only properties or a setter broader than its getter.
- **DO** allow properties to be set in any order, even into a temporarily invalid state. **DO** preserve the old value if a setter throws. **AVOID** throwing from getters.
- **CONSIDER** change-notification events for high-level/designer APIs and when a property can change through external forces; the overhead is rarely justified for low-level APIs.

**Indexers** — **CONSIDER** them for collection-like types or internal-array access. **AVOID** more than one parameter or parameter types other than `Int32`/`Int64`/`String`/`Range`/`Index`/enum, except for dictionary-like types. **DO** name them `Item` unless an obviously better name exists. **DO NOT** define more than one indexer family, non-default indexed properties, or an indexer alongside semantically equivalent methods. **DO** return the declaring type from a `Range` indexer.

**Constructors**
- **CONSIDER** providing a simple/default constructor; **CONSIDER** a static factory instead when "new" doesn't fit the semantics (see ch. 9).
- **DO** keep constructors to minimal work. **DO** use constructor parameters as shortcuts for setting main properties and give them the same names. **DO** throw from an instance constructor when appropriate.
- **AVOID** calling virtual members from a constructor; a subclass override runs before its fields are initialised. **DO** make static constructors private. **DO NOT** throw from static constructors. **CONSIDER** inline static-field initialisation instead of a static constructor. **AVOID** explicit struct default constructors.

**Events**
- **DO** say “raise,” not “fire” or “trigger.” **DO** use `EventHandler<T>` instead of hand-rolled delegates. **CONSIDER** a subclass of `EventArgs` for event data unless the event can never carry data.
- **DO** raise each event from a `protected virtual On<Event>(<Args> e)` method taking exactly one parameter, so subclasses can intercept. (Applies to non-static events on unsealed classes.)
- **DO NOT** pass a null sender for a non-static event. **DO** pass null for a static event. **DO NOT** pass null event data. **CONSIDER** cancellable pre-events.

**Fields** — **DO NOT** expose public/protected *instance* fields. **DO** surface state through properties. **DO** use `const` for true constants. **CONSIDER** `public static readonly` fields for predefined instances. **DO NOT** assign a mutable instance to a public or protected `readonly` field; the reference is read-only, but the object is not.

**Extension methods** — **AVOID** defining them frivolously, especially on types you don't own.
- **CONSIDER** them for helpers across every implementation of an interface, or to avoid an unwanted dependency on the type.
- **DO** throw `ArgumentNullException` when the `this` argument is null; **AVOID** extending `System.Object` or defining two with the same signature (even across namespaces).
- **DO NOT** put them in the extended type's namespace unless extending an interface or applying a generic constraint. **CONSIDER** naming the holder type for its purpose (`Routing`, not `FooExtensions`).

**Operators & conversions** — **AVOID** operator overloads except on types that should feel like primitives (e.g. `Decimal`).
- **DO** overload operators symmetrically and provide named-method equivalents. **DO NOT** define surprising or “cute” operator semantics. At least one operand must be the defining type.
- **DO NOT** provide an implicit conversion that is lossy, costly, or can throw. **DO** throw `InvalidCastException` when a cast would be lossy and the operator's contract does not permit loss. **DO NOT** provide conversions outside the type's domain or ones users would not expect.
- **DO** define comparison operators only on `IComparable<T>` types, return `bool`, and keep their semantics consistent with `IComparable<T>`. **DO** provide the complete natural set; for example, provide `<=` when defining `<` on an `IEquatable<T>` type.

**Parameters**
- **DO** use the least-derived parameter type that provides the needed functionality (accept `IEnumerable<T>`, not `List<T>`).
- **DO NOT** add reserved parameters for hypothetical future expansion; add an overload when the need becomes real.
- **DO** validate all arguments on public/protected/explicitly-implemented members. **DO** use `ArgumentNullException` for null and `ArgumentException` or a subtype for invalid values. **DO** validate enum parameters, but **DO NOT** use `Enum.IsDefined` for range checks; it is expensive and changes behaviour when the enum gains values. **DO** account for mutable arguments changing after validation.
- **DO** place `out` parameters after by-value and `ref` parameters. **DO** keep parameter names consistent across overrides and interface implementations.
- **DO** prefer an enum over two or more Boolean parameters; calls such as `Foo(true, false)` are unreadable. **CONSIDER** Boolean constructor parameters only for genuinely two-state values used to initialise Boolean properties.
- **AVOID** `ref`/`out` except for patterns that require them, such as Try. **DO NOT** pass reference types by `ref`/`in` or value types by `in`. **DO NOT** publicly expose pointers, pointer arrays, or multidimensional arrays. **DO** provide a CLS-compliant alternative for any pointer-taking member.

**`params`** — **CONSIDER** it when callers pass small inline arrays. **DO** order parameters to enable it. **AVOID** it when the caller commonly already has an array. **DO NOT** use it if the member mutates the array. Account for `null` being passed as the array. **DO NOT** use `varargs`/ellipsis.

**Tuples** — **DO** prefer descriptive named types for discoverability and evolution. When using tuples, **DO** name their elements with PascalCase and keep them to three fields or fewer. **DO NOT** use tuples as method parameters or define extension methods over them. **CONSIDER** adding `Deconstruct` to a type chosen instead of a tuple.

---

## 6. Designing for Extensibility

*Source: chapter 6, §§6.1–6.3.*

Extensibility is a *contract*. Every virtual member, protected member, and abstraction is a promise you must design, test, and maintain forever — and **DO** treat protected members as public for security, documentation, and compatibility analysis.

**Choosing a mechanism (cheapest first)**
- Unsealed class with no extra virtuals → protected members → events → virtual members → abstract types. Prefer the cheapest that meets the need.
- **CONSIDER** an unsealed class with no added virtual/protected members — inexpensive, much-appreciated extensibility.
- **DO NOT** make a member virtual without a concrete reason and awareness of the full cost. **AVOID** making *public* members virtual. **CONSIDER** a protected virtual called from a nonvirtual public member (Template Method, ch. 9), so validation stays in one place.
- **DO** prefer protected over public accessibility for virtual members.

**Callbacks, events, delegates**
- **CONSIDER** events for customisation by developers who don't want to learn your object model; they're familiar and designer-integrated. **CONSIDER** plain callbacks for code injection, but **AVOID** them in performance-sensitive APIs.
- **DO** use `Func<…>`/`Action<…>`/`Expression<…>` instead of custom delegate types where possible. **DO** measure the cost of `Expression` versus `Func`/`Action`.
- **DO** understand that invoking a delegate runs arbitrary user code — with security, correctness, and compatibility consequences.

**Abstractions**
- **DO NOT** ship an abstraction until it is validated by several concrete implementations *and* consuming APIs. **CONSIDER** shipping reference/conformance tests so implementers can self-check.
- **DO** choose abstract class versus interface deliberately (see ch. 4). **CONSIDER** making base classes abstract even with no abstract members and placing them in a separate namespace. **AVOID** a `Base` suffix on public base classes.

**Sealing** — **DO NOT** seal a class without a good reason; **DO NOT** declare protected/virtual members on a sealed type; **CONSIDER** sealing the overrides you provide.

---

## 7. Exceptions  *(error handling — core chapter)*

*Source: chapter 7, §§7.1–7.5.*

The exception model makes failures propagate by default instead of relying on callers to inspect return codes. It also carries failure information outside the return type, which is essential for constrained signatures such as constructors, properties, events, virtual overrides, and operator overloads. Get this chapter wrong and every consumer inherits the unreliability.

One of the biggest misconceptions about exceptions is that they are for “exceptional conditions”. The reality is that they are intended for communicating **error conditions**. From a framework design perspective, there is no such thing as an exceptional condition. One man’s exceptional condition is another man’s chronic condition.

**Throw, don't return codes**
- **DO** report execution failures by throwing exceptions. **DO NOT** return error codes, including for performance reasons. Error codes are easy to ignore; exceptions propagate by default.
- .NET APIs use exceptions pervasively. Returning error codes introduces a second, parallel failure protocol that consumers must handle alongside exceptions; it is inconsistent with the platform and non-idiomatic. Use a deliberate Tester-Doer or Try API only for expected, common failure cases.
- **CONSIDER** `Environment.FailFast` instead of throwing when continuing would corrupt process state; **AVOID** APIs that can cause an unrecoverable system failure.

**Use exceptions for error conditions, not normal control flow**
- **DO NOT** use exceptions for normal flow. For failures common in normal operation, offer a **Tester-Doer** (`if (d.ContainsKey(k)) …`) or the **Try pattern** so callers avoid the throw entirely.
- **CONSIDER** the performance cost: throw rates above ~100/second will noticeably hurt most apps. The cost is in the throw, not the try.

**The exception contract**
- **DO** document every exception a public member throws from a *contract violation* (vs. a system failure) and treat it as part of the API contract; changes require compatibility analysis.
- **DO NOT** have a public member that throws-or-not depending on an option, or that returns exceptions as a return value/`out` parameter.
- **CONSIDER** exception builder/helper methods to keep throw sites consistent and the message logic in one place.
- **DO NOT** throw from exception filter blocks; **AVOID** explicitly throwing from `finally` (an implicit throw from a called method is fine).

**Choosing what to throw**
- **DO** throw the most specific (most derived) exception that fits.
- **DO** use `ArgumentException` or a subtype for bad arguments, set `ParamName`, and use `value` for a setter's implicit parameter. **DO** use `InvalidOperationException` for a bad object state, `OperationCanceledException` for caller-initiated aborts, `FormatException` for malformed parse input or bad format specifiers, and `NotSupportedException` or `PlatformNotSupportedException` for unsupported operations or environments.
- **DO NOT** throw `Exception`, `SystemException`, or `ApplicationException`; they are too broad to catch meaningfully. **DO NOT** derive from `ApplicationException`.
- **DO NOT** let public APIs surface `NullReferenceException`, `AccessViolationException`, or `IndexOutOfRangeException` (validate first and throw an `ArgumentException`). **DO NOT** explicitly throw `StackOverflowException`, `OutOfMemoryException`, `COMException`, `SEHException`, or `ExecutionEngineException`; **DO NOT** catch `StackOverflowException`.

**Messages**
- **DO** provide a rich, meaningful message targeted at the *developer*, grammatically correct, each sentence ending in a period.
- **AVOID** question/exclamation marks; **DO NOT** disclose security-sensitive information; **CONSIDER** localising messages if your component serves developers in multiple languages.

**Custom exception types**
- **DO NOT** create a custom type to communicate a usage error or one that would not be handled differently from an existing framework exception; throw the existing one. **DO NOT** invent an exception merely to stamp the feature's name on it.
- **DO** create a new type only for a unique program-error condition a caller can handle differently. **DO** use the `Exception` suffix and derive from `System.Exception` or a suitable common exception. **AVOID** deep exception hierarchies. **DO** provide `()`, `(string)`, and `(string, Exception)` constructors. **Modern qualification:** runtime serialization and its constructor apply only when targeting legacy AppDomain or remoting boundaries; modern libraries generally do not need them. See the [Modern .NET Supplement](Framework_Design_Guidelines_Modern_Supplement.md#runtime-serialization-is-legacy-compatibility-work).

**Catching, rethrowing, wrapping**
- **DO NOT** swallow or over-catch errors; exceptions should usually propagate. In framework code, **DO NOT** catch `Exception`/`SystemException` unless rethrowing. In application code, **AVOID** broad catches except in top-level handlers. **CONSIDER** catching a specific exception only when you understand why it was thrown and can respond programmatically.
- **DO** use try-finally for cleanup; it is far more common than try-catch in good exception code. **DO** rethrow with a bare `throw;` to preserve the stack, or use `ExceptionDispatchInfo` when rethrowing across contexts. **DO NOT** exclude special exceptions when transferring. **DO NOT** handle non-CLS exceptions with a parameterless catch.
- **CONSIDER** wrapping a lower-layer exception in a more appropriate one when that layer is an implementation detail. **DO** set `InnerException`. **AVOID** catching and wrapping non-specific exceptions.

**The Try pattern**
- **CONSIDER** the Tester-Doer or Try pattern for members that would otherwise throw in common scenarios.
- **DO** use a `Try` prefix and a Boolean return. **DO** “return” the value via an `out` parameter. **DO** write `default(T)` to `out` on `false`. **AVOID** writing to `out` when throwing.
- **DO** pick *one* reason a Try method returns `false` and throw for every other failure. **DO** provide an exception-throwing counterpart for each Try member. **DO** make `static bool TryParse(string, out T)` return `false`, rather than throw, on null input.

---

## 8. Usage Guidelines (using common types in APIs)

*Source: chapter 8, §§8.1–8.13.*

**Arrays vs. collections** (§§8.1, 8.3.3)
- **DO** prefer collections over arrays in public APIs, and remember that arrays are mutable even when holding immutable items. **AVOID** array properties generally. **DO NOT** use an array property when its getter must create a new array on every call; a direct-return property can be appropriate for an array-transport type whose purpose requires exposing the array.
- **CONSIDER** jagged arrays over multidimensional arrays. **DO** use byte arrays for byte data. **CONSIDER** arrays for low-level APIs where minimising allocation and maximising performance matter.

**Collection types in signatures**
- **DO NOT** use weakly typed collections, `ArrayList`/`List<T>`, or `Hashtable`/`Dictionary<,>` in public APIs (they expose implementation and prevent validation).
- **DO NOT** expose `IEnumerator<T>` except from `GetEnumerator`, or combine the enumerable and enumerator roles on one public type; the same applies to their non-generic counterparts.
- **DO** accept the least-specialised type that works (usually `IEnumerable<T>`); **AVOID** `ICollection<T>` just to read `Count`.
- **DO** return `Collection<T>` (or a subclass) for read/write collections, `ReadOnlyCollection<T>` for read-only; **DO NOT** provide settable collection properties or return null (return an empty collection/array).
- **DO NOT** return snapshot collections from properties. **DO** use an explicit snapshot collection or a live `IEnumerable<T>` for volatile data.
- When designing a collection type, **DO** implement `IEnumerable<T>`. **CONSIDER** inheriting from `Collection<T>`, `ReadOnlyCollection<T>`, or `KeyedCollection<,>`. **DO NOT** inherit from legacy `CollectionBase`. **DO** use the `Collection` or `Dictionary` suffix as appropriate. **CONSIDER** prefixing the name with the item type or `ReadOnly`. **AVOID** implementation-implying suffixes such as `LinkedList` or `Hashtable`.

**`Equals`, `GetHashCode`, equality operators**
- **DO** override `GetHashCode` whenever overriding `Equals`. **DO** ensure equal objects return equal, well-distributed hashes. **DO** keep an instance's hash stable over its lifetime. **AVOID** throwing from `Equals` or `GetHashCode`.
- **DO** implement `IEquatable<T>` and override `Equals` on value types. **CONSIDER** implementing `IEquatable<T>` when overriding `Equals` on reference types. **DO NOT** implement value equality on *mutable* reference types.
- **DO NOT** overload only one of `==` and `!=`. **DO** keep both operators consistent with `Equals` in semantics and performance. **DO NOT** throw from equality operators. **CONSIDER** overloading `<`, `>`, `<=`, and `>=` when implementing `IComparable<T>`.

**`ToString`** — **DO** override it when a useful human-readable string exists. **DO** keep it short, side-effect-free, and culture-aware. **DO NOT** return null or an empty string. **AVOID** throwing. **DO** prefer a friendly name to an unreadable ID. **DO** offer `IFormattable` or `ToString(format)` for culture-sensitive output. **CONSIDER** making the output round-trip through the type's parse method.

**`ICloneable`** — **DO NOT** implement or use it; its deep-versus-shallow contract is ambiguous. **CONSIDER** a documented `Clone`, stating whether it is deep or shallow, only when cloning is genuinely required.

**Standard framework types**
- **DO** use `DateTimeOffset` for an exact instant. **DO** use `DateTime` for zone-agnostic, unknown-zone, or whole-date (00:00:00) values, and `TimeSpan` for time-of-day. **AVOID** `DateTimeKind` when `DateTimeOffset` fits. See the modern supplement for `DateOnly` and `TimeOnly`.
- **CONSIDER** `Nullable<T>` for optional values. **DO NOT** use it unless a nullable reference would be appropriate in the analogous reference-type API. **AVOID** `Nullable<bool>` as a general tri-state and **AVOID** `System.DBNull`.
- **DO** use `System.Uri`, rather than a string, for URL/URI data. **CONSIDER** string overloads only for the most common members. **DO NOT** generate them mechanically. **DO** delegate to the `Uri` overload when available.
- **DO** use `XmlReader`, `IXPathNavigable`, or `XNode` for XML I/O. **DO NOT** use `XmlNode` or `XmlDocument` in public APIs. **DO NOT** subclass `XmlDocument`.

**Custom attributes** — **DO** use the `…Attribute` suffix and apply `AttributeUsage`. **DO** seal attribute classes where possible. **DO** represent required arguments with constructor parameters and get-only properties. **DO** represent optional arguments with settable properties. **DO NOT** use constructor parameters for optional arguments. **AVOID** overloading attribute constructors.

**Serialization** — **AVOID** serialization attributes or interfaces on public types in a general-purpose library. Legacy .NET Framework types that must cross AppDomain or remoting boundaries, including exception types used there, are the principal exception. When supporting serialization, **DO** analyse backward and forward compatibility. When implementing legacy `ISerializable`, **DO** make its constructor protected (private on sealed types) and **DO** implement its members explicitly. **Modern qualification:** this mechanism is obsolete for most modern libraries; see Appendix B and the [Modern .NET Supplement](Framework_Design_Guidelines_Modern_Supplement.md#runtime-serialization-is-legacy-compatibility-work).

---

## 9. Common Design Patterns

*Source: chapter 9, §§9.1–9.13.*

**Aggregate components** — high-level facades modelling *physical objects* (`MessageQueue`, `Process`), not system tasks, supporting **Create-Set-Call**.
- **DO** give aggregate components a default or simple constructor. **DO** provide properties corresponding to constructor parameters. **DO** use events, rather than virtuals or delegate callbacks, for extensibility.
- **DO** make them usable after trivial initialisation (a clear exception if a required step is skipped).
- **DO NOT** require users to instantiate multiple objects, inherit, override, implement interfaces, or configure anything in common scenarios.
- **CONSIDER** automatic mode changes (factored types have *no* modes), Visual Studio designer integration, separate assemblies for aggregate vs. factored types, and exposing the internal factored types.

**Async (Task-Based Async Pattern)** (§§9.2.2–9.2.5)
- **DO** implement new async APIs with TAP. **DO** use the `Async` suffix, prefer `Task`/`Task<T>`, and accept a defaulted `CancellationToken` named `cancellationToken`. **CONSIDER** placing the token last for alignment with the synchronous signature. **CONSIDER** `ValueTask<T>` only when the method commonly completes synchronously *and* is on a critical path where avoiding allocation justifies its tighter usage constraints. **AVOID** public `async void`; it follows the event-based pattern and is appropriate mainly for event handlers.
- **DO** convert async signatures to remove `ref` and `out`. **AVOID** `in`, and **DO NOT** use it on virtual or abstract async methods. **DO NOT** return a null or `Created`-state task. **DO NOT** return `Task` from a long-running *synchronous* method. **CONSIDER** a synchronous variant and cancellation for blocking synchronous methods.
- **DO** throw usage-error exceptions directly before returning a task: validate in a public non-`async` wrapper, then call a non-public async core. **DO** surface execution errors through the returned task.
- When honouring cancellation, **DO** throw `OperationCanceledException`, normally via `cancellationToken.ThrowIfCancellationRequested()`; an early successful return incorrectly leaves the task in `RanToCompletion`.
- **DO** `await` rather than call `.Wait()` or read `.Result`. **DO** call async variants from async implementations. **DO** use `ConfigureAwait(false)` except where the app model depends on the synchronization context. **DO NOT** operate on a `ValueTask` more than once or save it; only await or return it.
- For `IAsyncEnumerable<T>`, **DO** use the `Async` suffix and `[EnumeratorCancellation]` on its token. **DO** use `WithCancellation` and `ConfigureAwait` on `await foreach` as appropriate. **DO NOT** expose `IAsyncEnumerator<T>` except from `GetAsyncEnumerator`. **DO NOT** combine enumerable and enumerator roles on one public type.

**Dispose pattern**
- **DO** implement `IDisposable` on types owning disposable or unmanaged resources. **CONSIDER** it on base classes whose subtypes are likely to own them. **DO** implement `Dispose()` as `Dispose(true)` followed by `GC.SuppressFinalize(this)`, with cleanup centralised in `protected virtual void Dispose(bool disposing)`.
- **DO** make `Dispose(bool)` idempotent. **DO NOT** make `Dispose()` virtual or declare overloads other than `Dispose()` and `Dispose(bool)`. **DO** throw `ObjectDisposedException` from members that cannot be used after disposal. **AVOID** throwing from `Dispose(bool)` except on process corruption. **CONSIDER** a `Close()` alias where that is the domain term.
- Finalizers: **DO NOT** make public types finalizable. Put unmanaged-resource finalization in a private/internal holder, preferably an existing `SafeHandle`. **DO** apply the basic dispose pattern to every finalizable holder. **DO NOT** touch other finalizable objects or let exceptions escape in its finalizer path. **DO NOT** remove a finalizer from an unsealed public type.
- Async disposal: **DO** implement `DisposeAsync()` as awaiting `DisposeAsyncCore()`, then calling `Dispose(false)` and `GC.SuppressFinalize(this)`, in that order, except on sealed types. **DO NOT** declare other `DisposeAsync` overloads. **CONSIDER** returning a disposable instead of paired begin/end methods.

**Factories** — **DO** prefer constructors because they are generally more usable, consistent, and convenient. **DO** use a factory when the caller cannot know which concrete type to construct or for conversions such as `Parse` and `Decode`. **CONSIDER** a factory when creation needs more control than a constructor provides or a named method is needed to explain the operation. **DO** implement factory operations as methods returning the instance, rather than properties or `out` parameters. **CONSIDER** names of the form `Create<Type>` and `<Type>Factory`.

**LINQ support** — **DO** implement `IEnumerable<T>` for basic LINQ. **CONSIDER** `ICollection<T>` to speed query operators and direct Query Pattern support. **DO** defer execution of query operators. **CONSIDER** `IQueryable<T>` only when query-expression inspection is needed, and **DO NOT** implement it without understanding the performance implications. **DO** throw `NotSupportedException` for operations the source cannot support. **DO** represent an ordered sequence as a distinct type exposing `ThenBy` or `IOrderedEnumerable<T>`. **DO** place query extension methods in a `…Linq` subnamespace and use `Expression<Func<…>>` when the query must be inspected.

**Variance** — **DO** mark generic parameters `out` when used only for output and `in` when used only for input, where the language supports it. **DO NOT** narrow variance in a later version or declare an array-returning member covariant. **CONSIDER** a sub-interface to enable variance. **CONSIDER** the Simulated Covariance Pattern, using an ideally abstract non-generic root type, when one representation across all instantiations is needed.

**Template Method** — **AVOID** public virtual members. **CONSIDER** a public nonvirtual member calling a `protected virtual <Name>Core` member. **DO** validate arguments and state in the nonvirtual member. **DO** perform only validation unique to the derived type in the `Core` member.

**Spans, Memory, and buffers**
- **CONSIDER** spans for buffers. **DO** prefer `ReadOnlySpan<T>` over `Span<T>`. **DO** use `Memory<T>`/`ReadOnlyMemory<T>` in async methods or when storing a buffer reference; a Span cannot cross an await or live on the heap.
- **AVOID** returning a Span unless its lifetime is clear. **DO** document ownership for any returned Span that did not originate with the caller. **AVOID** overloading across Span/ReadOnlySpan or Span/Memory. **CONSIDER** array-, string-, and array-returning alternatives, and **DO** perform normal validation in them.
- **DO** prefer `source` for the input buffer and `destination` for the output buffer. **DO** position the source first and the destination immediately after all sources. **DO** report written counts via `out`; **CONSIDER** names such as `bytesWritten`, `charsWritten`, or `valuesWritten`.
- **Try-Write pattern**: **DO** use a `Try` prefix and Boolean return. **DO** return `false` if and only if the destination is too small and throw for every other error. **DO** report the written count via `out`. **DO** provide a way to compute a sufficient size for potentially large results. **CONSIDER** a size-computation API and a throwing alternative for smaller results.
- **OperationStatus pattern**: **CONSIDER** it for partial or streaming results. **DO** report consumed and written counts via `out` parameters. **DO** include an optional `isFinalBlock` Boolean, defaulting to true, when the last block needs special handling.

**Misc patterns** — **DO** represent timeouts with a `TimeSpan` parameter and throw `TimeoutException`; **DO NOT** return timeout error codes. For XAML, **CONSIDER** a parameterless constructor and **DO** provide a markup extension for immutable types. **AVOID** new type converters unless the conversion is natural and intuitive. **CONSIDER** `ContentPropertyAttribute` for the primary property. For optional capabilities, **CONSIDER** the Optional Feature Pattern; **DO** expose a Boolean `IsXSupported` property and base virtual members that throw `NotSupportedException`.

**WPF dependency properties** — **DO** use them when WPF styling, binding, animation, resources, or inheritance requires them. **DO** pair the CLR wrapper with a `public static readonly <Name>Property`. **DO** keep wrapper accessors limited to `GetValue` and `SetValue`. **DO** put defaults, validation, change notification, and coercion in registration metadata. **DO NOT** store secrets in dependency properties.

---

# Appendix A — C# Coding Style Conventions

House style for *implementation* code; the FDG naming rules above still govern the public surface.

*Source: Appendix A, §§A.1–A.4. These are the book's BCL house-style conventions, not universal C# requirements.*

- **Braces** (§A.1.1) — **DO** use Allman style: opening brace on its own line aligned with the statement and closing brace on its own line aligned with the opener. **AVOID** omitting braces, with a **CONSIDER** exception for a single-line argument-validation preamble. **DO NOT** use braceless `using`. **AVOID** braceless `await using`, except to simulate stacked `await using` statements with `ConfigureAwait` in a fresh scope.
- **Spacing** — **DO** use one space inside same-line braces, after commas, and between arguments. **DO NOT** put spaces inside parentheses or square brackets, or between a member name and `(`. **DO** put a space between a flow-control keyword and `(` and around binary operators. **DO NOT** put spaces around unary operators.
- **Indentation** — **DO** use four spaces and **DO NOT** use tabs. **DO** indent block contents, `case` blocks, continued lines, and chopped argument lists one level. **DO** outdent `goto` labels one level. **DO** place one argument or parameter per line when chopping.
- **Blank lines** — **DO** add one before control-flow statements, after a closing brace unless the next line is also `}`, and between logical paragraphs where it improves readability.
- **Modifiers** — **DO** specify visibility explicitly and first, followed by `static`, `extern`, the slot modifier (`new`/`virtual`/`abstract`/`sealed`/`override`), `readonly`, and `unsafe`/`volatile`, with `async` last. **DO** use `protected internal` and `private protected`, not their reversed forms.
- **Language usage** — **DO** use language keywords (`string`, `int`) over BCL names (`String`, `Int32`). **DO NOT** use `var` except for `new`, an `as` cast, or a hard cast. **DO** use `nameof(...)`, `readonly` fields where possible, auto-properties, object/collection initializers, `if`…`throw` validation, and trailing commas in enums and multiline initializers. **CONSIDER** expression-bodied members when the implementation is unlikely to change. **DO NOT** use `this.` unless required. **DO** keep source ASCII and use `\uXXXX` escapes for non-ASCII.
- **Naming (implementation)** — **DO** use PascalCase for namespaces, types, members, and const locals/fields. **DO** use camelCase for locals, parameters, and private/internal fields. **DO** prefix private/internal fields with `_` for instance, `s_` for static, and `t_` for thread-static fields. **DO NOT** use Hungarian notation.
- **Comments** — **DO NOT** add comments for obvious code. **AVOID** `/* */`, end-of-line comments, and writing “I”; prefer `//` even across lines.
- **File organisation** — **DO NOT** place more than one public type in a file except for generic-arity variants or nesting. **DO** name the file for its type and partial files as `Type.Aspect.cs`. **DO** mirror namespaces in the directory tree. **DO** place `using` directives outside the namespace, alphabetically with `System.*` first. **DO** group members in a consistent order: constants, fields, constructors, properties, methods, events, then nested types.

---

# Appendix B — Obsolete Guidance (know what changed)

*Source: Appendix B.*

Rules once normative, now discouraged — useful when reading older code or advice:

- **Code Access Security**, security transparency, link demands, remoting-oriented exception serialization, and formatter-based serialization are legacy mechanisms.
- **Non-generic collections** (`ArrayList`, `Hashtable`, `CollectionBase`) and weakly typed APIs — superseded by generics.
- **`ICloneable`**, **`System.DBNull`**, **`ApplicationException`**, and **`varargs`** — actively discouraged today.
- **Classic Async Pattern** (`BeginX`/`EndX` + `IAsyncResult`) and **Event-Based Async Pattern** (`XAsync` + `XCompleted` event) — superseded by TAP for new API design.
- **Editorial synthesis:** re-evaluate older guidance against current async, spans, nullable-reference-type, and default-interface-member capabilities before applying it.

---

# Appendix C — Minimal API Specification

*Editorial synthesis based on the scenario-driven process in chapter 2 and the API-specification example in source Appendix C.*

For every significant public feature, keep a short design artefact containing:

1. Main scenarios expressed as consumer code, including a second language family where relevant.
2. Goals and explicit non-goals.
3. The proposed public API surface and its observable behaviour.
4. Open questions and decisions, including compatibility, platform, and failure semantics.

Treat the scenario code as the primary specification: revise the object model until the common call sites are clear and economical.

---

# Appendix D — Breaking Changes (compatibility taxonomy)

*Source: Appendix D.*

Every library change can break a caller. Classify it on the source's four dimensions:

- **Runtime** — an existing binary observes new behaviour or fails (`TypeLoadException`, `MissingMethodException`, changed dispatch or results).
- **Compilation** — existing source no longer compiles against the new library.
- **Recompile** — recompiling succeeds but changes the program's behaviour or binding.
- **Reflection** — metadata consumers observe a unique break not already covered above.

The appendix marks whether the .NET BCL team would normally *accept* a change; accepted does not mean non-breaking. Audience matters: a small library may accept risks that a foundational library cannot.

**Usually unacceptable** — renaming, removing, recasing, or moving public APIs without forwarding; sealing an unsealed type; changing `class`↔`struct` or `struct`→`ref struct`; adding abstract/interface members or a base interface; changing signatures/defaults/`const` values; moving members down or to a base interface; removing ordinary members or a finalizer from an unsealed type.

**Often accepted, with explicit risks** — adding ordinary members can produce hiding warnings or overload ambiguity; adding an override can change behaviour only after callers recompile; moving a type with `[TypeForwardedTo]` affects reflection and cannot safely be undone; removing an override reverts behaviour to the base implementation; unsealing a type and adding `readonly` to a struct are generally acceptable.

Adding a namespace that conflicts with a type is a compilation break. Adding the first overload can break `default`/`null` calls and reflection lookup. Treat all additions as compatibility analysis, not automatically safe changes.

**Rule of thumb** — classify every public change on all four dimensions. Default to additive evolution on widely consumed libraries, but inspect name and overload collisions; prefer `[Obsolete]` plus a replacement over mutating an existing contract.

---

# Quick-Reference Cheat-Sheets

**Editorial synthesis.** Compact decision aids distilled from the chapters above; these are supplementary and not a chapter of the book.

### Class vs. record vs. struct vs. interface

| Choose | When |
| --- | --- |
| **Class** (default) | The normal choice. Reference semantics, can evolve by adding members, supports inheritance. |
| **Record class** | Reference storage with generated value equality is part of the domain contract. Review every generated member as public API. |
| **Abstract class** | You need a contract *and* the freedom to add members later, or to share implementation. Prefer over an interface for a contract you own and expect to grow. |
| **Interface** | A contract must span value types, simulate multiple inheritance, or be added to types that already have a base class. Treat its abstract member set as fixed after release. |
| **Readonly record struct** | All struct criteria hold and generated value equality, operators, printing, and copying are the intended public contract. |
| **Struct** | All of: logically one value, less than 24 bytes, immutable, rarely boxed. Use a hand-written struct when generated record members are inappropriate; implement equality explicitly. |

### Property vs. method

Use a **property** for a logical attribute backed by cheap in-memory state. Use a **method** when any of these hold:

- the call is far slower than a field access, or does I/O;
- it has an observable side effect;
- it returns a different value on each call;
- it returns a copy, or returns an array;
- it's a conversion (`ToXxx`), or call order matters.

### Exception type selector

| Situation | Throw |
| --- | --- |
| Null argument (not allowed) | `ArgumentNullException` |
| Argument out of valid range | `ArgumentOutOfRangeException` |
| Other invalid argument | `ArgumentException` (set `ParamName`) |
| Object in wrong state for the call | `InvalidOperationException` |
| Caller-initiated cancel/abort | `OperationCanceledException` |
| Bad parse input / bad format specifier | `FormatException` |
| Operation legitimately unsupported | `NotSupportedException` |
| Unsupported in this environment | `PlatformNotSupportedException` |
| Used after disposal | `ObjectDisposedException` |
| Timeout elapsed | `TimeoutException` |
| **Do not throw directly** | `Exception`, `SystemException`, `ApplicationException`, `NullReferenceException`, `IndexOutOfRangeException`, `AccessViolationException`, `StackOverflowException`, `OutOfMemoryException` |
| **Do not derive from** | `ApplicationException`; derive custom exceptions from `Exception` or a suitable common subtype |

### Collection types in public signatures

| Role | Use |
| --- | --- |
| Input parameter | `IEnumerable<T>` (least-specialised that works) |
| Read/write property or return | `Collection<T>` (or a subclass) |
| Read-only property or return | `ReadOnlyCollection<T>` (or `IEnumerable<T>`) |
| Keyed lookup with unique keys | `KeyedCollection<TKey,TItem>` |
| **Never expose** | `ArrayList`, `List<T>`, `Hashtable`, `Dictionary<,>`, weakly typed collections |
| **Usually avoid** | Array properties; never use one when every getter must allocate a new array |

### Reserved / conventional names

Match these exactly — tooling and developer muscle-memory depend on them:

- `value` — implicit property-setter parameter; also the conventional name for a unary-operator operand and a generic single value.
- `sender`, `e` — the two event-handler parameters.
- `cancellationToken` — the TAP cancellation parameter (defaulted; conventionally last).
- `source` / `destination` — input vs. output buffer (source first).
- `left` / `right` — binary-operator operands with no domain meaning.
- `item`, `index`, `count`, `format`, `provider` — conventional names; don't repurpose them.
- Count-reporting `out` parameters: `bytesWritten` / `charsWritten` / `valuesWritten`, and for `OperationStatus`, `bytesConsumed` / `charsConsumed`.

### Standard term pairs (use the established opposite)

Pick the conventional partner rather than inventing one:

`Add`/`Remove` · `Insert`/`Delete` (or `Remove`) · `Create`/`Destroy` · `Begin`/`End` · `Get`/`Set` · `Open`/`Close` · `Read`/`Write` · `Lock`/`Unlock` · `Acquire`/`Release` · `Push`/`Pop` · `Enqueue`/`Dequeue` · `Increment`/`Decrement` · `Old`/`New` · `Source`/`Destination` · `First`/`Last` · `Min`/`Max` · `Next`/`Previous` · `Up`/`Down` · `Show`/`Hide` · `Start`/`Stop`.

Avoid mixing antonym sets (`Insert`/`Remove`, `Add`/`Delete`) and avoid synonyms across an API (settle on one of `Remove`/`Delete`, one of `Sentinel`/`Limit`, etc.).

### Standard suffixes

`*Attribute` · `*Exception` · `*EventArgs` · `*EventHandler` · `*Collection` · `*Dictionary` · `*Stream` · `*Async` (TAP methods) · `Try*` (Try / Try-Write pattern, Boolean return). Avoid implementation-implying suffixes (`*LinkedList`, `*Hashtable`) on abstractions, and the `Ex` / `Base` suffixes on public types.
