namespace Assembler.Definitions;

public abstract record ValueOrReference<T>;
public sealed record None<T> : ValueOrReference<T>;
public sealed record Reference<T>(string Ref) : ValueOrReference<T>;
public sealed record Value<T>(T Val) : ValueOrReference<T>;