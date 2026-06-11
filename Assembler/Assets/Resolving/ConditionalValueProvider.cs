namespace Assembler.Resolving
{
	// Live wrapper backing the !if tag: every read evaluates the condition and returns the matching branch's
	// value. Only the selected branch is read each time (matching C# ternary short-circuiting), so an
	// otherwise-costly or invalid branch is never evaluated when it isn't chosen. Read-only — it resolves into
	// a writable slot only via AsWritable, which throws, since a conditional has no single thing to write to.
	public sealed class ConditionalValueProvider<T> : IValueProvider<T>
	{
		private readonly IValueProvider<bool> _condition;
		private readonly IValueProvider<T> _then;
		private readonly IValueProvider<T> _else;

		public ConditionalValueProvider(
			IValueProvider<bool> condition,
			IValueProvider<T> then,
			IValueProvider<T> @else)
		{
			_condition = condition;
			_then = then;
			_else = @else;
		}

		public T Get(TriggerContext ctx) => _condition.Get(ctx) ? _then.Get(ctx) : _else.Get(ctx);

		object IValueProvider.Get(TriggerContext ctx) => Get(ctx)!;
	}
}
