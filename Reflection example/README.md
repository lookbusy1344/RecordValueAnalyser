# Reflection based prototype

This is some earlier work on using reflection at runtime to search for problematic records. Use the Roslyn code analyser in preference to this.

Call the method once at the start of your `main()` method. The code is DEBUG mode only, and is not compiled in RELEASE mode. In `main` add:

```
JS_Tools.ValueEquality.CheckAssembly();

```

It will throw a `ValueEqualityException` if any problems are detected.
