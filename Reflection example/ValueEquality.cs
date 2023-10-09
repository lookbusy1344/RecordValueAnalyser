#if DEBUG
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
#endif

namespace JS_Tools;

#if DEBUG

// warnings related to AOT compile, that doesnt support reflection. But this doesnt matter for Debug builds
// and code is turned off in Release build

#pragma warning disable IDE0079, IL2070, IL2026, IL3050

/// <summary>
/// Debug-only class to check that all record for value semantics
/// </summary>
public static class ValueEquality
{
	private static readonly StringBuilder results = new();
	private static readonly Type[] objecttypearray = new Type[] { typeof(object) };
	private static bool runflag;

	/// <summary>
	/// Check all marked classes in this assembly for value semantics
	/// </summary>
	public static void CheckAssembly(params Type[] ignoretypes)
	{
		ArgumentNullException.ThrowIfNull(ignoretypes);
		if (runflag) throw new InvalidOperationException("ValueEquality.CheckAssembly() has already been run");

		var assembly = Assembly.GetExecutingAssembly();
		var alltypes = assembly.GetTypes();

		// identify all record structs and classes, and check them
		foreach (var type in alltypes.Where(t => IsRecordClass(t) || IsRecordStruct(t)))
			if (ignoretypes.Length == 0 || !ignoretypes.Contains(type))
				CheckValueEquality(type, type);

		runflag = true;
		if (results.Length > 0)
		{
			_ = results.Insert(0, "The following records lack value semantics:\r\n");
			throw new ValueEqualityException(results.ToString());
		}
	}

	/// <summary>
	/// Check this type, including any nested structs, for value semantics
	/// </summary>
	private static void CheckValueEquality(Type outertype, Type type)
	{
		// if the type has a manually written Equals(T) method, we can assume it has value semantics
		// Normally records have an automatically generated Equals(T) method, but this can be overridden
		if (HasManualEquals(type)) return;

		var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		foreach (var field in fields)
		{
			// a primitive type or string is fine
			// if the field type itself is marked with the custom attribute, we can assume it is checked there, and not here!
			if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType == typeof(string))
				continue;

			// is this a nested record? If so it will be checked separately
			if (IsRecordClass(field.FieldType) || IsRecordStruct(field.FieldType))
				continue;

			// if the type has IEquatable<T> or Equals(T) overloads we can assume it has value semantics
			if (HasEqualityImplementation(field.FieldType) || HasEqualsMethod(field.FieldType))
				continue;

			if (field.FieldType.IsValueType)
			{
				// this is a struct without custom equality behaviour, so recurse into fields
				CheckValueEquality(outertype, field.FieldType);
			}
			else
			{
				// otherwise, this is a class and we have a problem
				string msg;
				if (outertype == type)
					msg = $"{type.FullName} - {field.FieldType.Name} {field.Name}";
				else
					msg = $"{outertype.FullName}::{type.FullName} - {field.FieldType.Name} {field.Name}";

				_ = results.AppendLine(msg);
				Debug.WriteLine($"{msg} - lacks value semantics");
			}
		}
	}

	/// <summary>
	/// Does type have Equals(T t) or overridden Equals(object o)
	/// </summary>
	private static bool HasEqualsMethod(Type type)
	{
		// check for Equals(object) override in this type (not inherited)
		var equalsMethodObj = type.GetMethod("Equals", objecttypearray);
		if (equalsMethodObj?.DeclaringType == type) return true;

		// check for Equals(T) in this type (not inherited)
		var equalsMethod = type.GetMethod("Equals", new Type[] { type });
		return equalsMethod?.DeclaringType == type;
	}

	/// <summary>
	/// Does this type implement IEquatable<T>?
	/// </summary>
	/// <param name="type"></param>
	private static bool HasEqualityImplementation(Type type) =>
		typeof(IEquatable<>).MakeGenericType(type).IsAssignableFrom(type);

	/// <summary>
	/// Check if the type is a record class, by looking for the non-virtual Deconstruct method
	/// </summary>
	private static bool IsRecordStruct(Type type)
	{
		if (!type.IsValueType) return false;

		// Get all public instance methods on the current type
		var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

		// Loop through all methods
		foreach (var method in methods)
		{
			if (method.IsVirtual || method.DeclaringType != type) continue;

			if (method.Name == "Deconstruct")
			{
				// Check if the method has the CompilerGenerated attribute
				var compilergenerated = method.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false);
				if (compilergenerated.Length > 0) return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Check if the type is a record struct, by looking for the generated Clone method
	/// </summary>
	private static bool IsRecordClass(Type type)
	{
		if (!type.IsClass) return false;

		// Get all public instance methods on the current type
		var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

		// Loop through all methods
		foreach (var method in methods)
		{
			// Check if the declaring type is the current type, and if its virtual
			if (!method.IsVirtual || method.DeclaringType != type) continue;

			if (method.Name == "<Clone>$")
			{
				// Check if the method has the CompilerGenerated attribute
				var attributes = method.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false);
				if (attributes.Length > 0) return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Does this record have a manually written Equals(T)?
	/// </summary>
	private static bool HasManualEquals(Type type)
	{
		if (!type.IsClass && !type.IsValueType) return false;

		// Get all public instance methods on the current type
		var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);

		// Loop through all methods
		foreach (var method in methods)
		{
			// Check if the declaring type is the current type, and if its virtual
			if (method.DeclaringType != type) continue;

			if (method.Name == "Equals")
			{
				// check if it has one parameter of the same type
				var parameters = method.GetParameters();
				if (parameters.Length != 1) continue;

				var paramtype = parameters[0].ParameterType;
				if (paramtype == typeof(object)) continue;

				if (type.IsValueType && paramtype.IsValueType)
				{
					// For value types check if Equals(T) or Equals(Nullable<T>)
					var nullableType = typeof(Nullable<>).MakeGenericType(type);
					if (paramtype != type && !nullableType.IsAssignableFrom(paramtype)) continue;
				}
				else
				{
					// for references just check Equals(T)
					if (paramtype != type) continue;
				}

				// Check if the method has the CompilerGenerated attribute
				var attributes = method.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false);
				return attributes.Length == 0;
			}
		}

		return false;
	}
}
#else
public static class ValueEquality
{
	/// <summary>
	/// Dummy method for release builds
	/// </summary>
	public static void CheckAssembly() { }
}
#endif

public class ValueEqualityException : Exception
{
	public ValueEqualityException(string message) : base(message) { }

	public ValueEqualityException() { }

	public ValueEqualityException(string message, Exception innerException) : base(message, innerException) { }
}
