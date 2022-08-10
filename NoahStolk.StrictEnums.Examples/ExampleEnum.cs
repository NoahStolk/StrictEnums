namespace NoahStolk.StrictEnums.Examples;

public partial class ExampleEnum : IStrictEnum<int, ExampleEnum>
{
	public static readonly ExampleEnum Value1 = new(1);
	public static readonly ExampleEnum Value2 = new(2);
	public static readonly ExampleEnum Value3 = new(3);
	public static readonly ExampleEnum Value4 = new(4);

	// Not relying on "enum" itself makes it possible to declare methods directly rather than via an extension method.
	public int GetValueMultiplied(int multiplier)
	{
		return Value * multiplier;
	}
}
