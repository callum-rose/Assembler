namespace Assembler.Parsing.Phase2.Parsing.Phase2.Info
{
	public abstract record ValueInfo;
	public record ConstantValueInfo(object Value) : ValueInfo;
	public record VariableRefValueInfo(string VariableId) : ValueInfo;

	public abstract record ValueInfo<T>;
	public record ConstantValueInfo<T>(T Value) : ValueInfo<T>;
	public record VariableRefValueInfo<T>(string VariableId) : ValueInfo<T>;
}