namespace NoahStolk.StrictEnums;

public interface IStrictEnum<TValue, TSelf>
	where TValue : struct
	where TSelf : IStrictEnum<TValue, TSelf>
{
	public TValue Value { get; }

	public static abstract TSelf ConvertFrom(TValue value);
}
