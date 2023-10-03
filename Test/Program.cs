namespace AnalyserTest;

#pragma warning disable IDE0079, IDE0022, CA1051

internal static class Program
{
	private static void Main()
	{
	}
}

public record class A(F FooFail, G BarFail, string SPass, StructA SaFail);

public record class B(int IPass, IReadOnlyList<int> JFail, int? NullableIntPass, StructB BaPass);

public record struct AS(F FooFail, G GShouldFail, H HShouldPass, string SPass, Inner InnPass, object OFail);

public record struct Tup1(int IPass, (int a, int b) TupPass, DateTime? DtPass);

public record struct Tup2(int IPass, (int a, int[] b, object o) TupFail);

public record struct Tup3(int IPass, (bool, int) TupPass);

public record class RecFields(int IPass, string SPass, object OFail)
{
	public IList<string>? FieldFail;
	public int[]? PropertyFail { get; set; }

	public int FieldPass;
	public string? PropertyPass { get; set; }

	//public virtual bool Equals(RecFields? other) => true;

	public override int GetHashCode() => 1;
}

public record Inner(int IPass, string JPass, DateTime DtPass);

public record class HasEqualsRecordClass(IReadOnlyList<int> NumsPass)
{
	public virtual bool Equals(HasEqualsRecordClass? other) => other != null && NumsPass.SequenceEqual(other.NumsPass);

	public override int GetHashCode() => NumsPass.GetHashCode();
}

public record struct HasEqualsRecordStruct(IReadOnlyList<int> NumsPass)
{
	public readonly bool Equals(HasEqualsRecordStruct other) => NumsPass.SequenceEqual(other.NumsPass);

	public override readonly int GetHashCode() => NumsPass.GetHashCode();
}

public class F { public int[]? n; } // class, and also contains a refence!

public class G { public int i; }    // doesnt have Equals(T), so should always cause warning in record

public class H //: IEquatable<H>      // unlike G, this should pass equality test
{
	public int i;

	public bool Equals(H? other) => other != null && other.i == this.i;

	//public override bool Equals(object? obj) => Equals(obj as H);

	public override int GetHashCode() => i.GetHashCode();
}

public struct StructA { public int[] Numbers; } // a record containing this should fail, unless it has IEquatable

public struct StructB { public int A; public string S; } // a record containg this is fine
