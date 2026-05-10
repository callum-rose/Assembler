using System;

namespace Assembler.Parsing.Phase3
{
	public sealed class ExpressionContainer<TReturn>
	{
		private readonly Func<TReturn> _function;

		public ExpressionContainer(Func<TReturn> function) => _function = function;

		public TReturn Invoke() => _function();
	}

	public sealed class ExpressionContainer<TParam, TReturn>
	{
		private readonly Func<TParam, TReturn> _function;

		public ExpressionContainer(Func<TParam, TReturn> function) => _function = function;

		public TReturn Invoke(TParam param) => _function(param);
	}

	public sealed class ExpressionContainer<TParam1, TParam2, TReturn>
	{
		private readonly Func<TParam1, TParam2, TReturn> _function;

		public ExpressionContainer(Func<TParam1, TParam2, TReturn> function) => _function = function;

		public TReturn Invoke(TParam1 param1, TParam2 param2) => _function(param1, param2);
	}

	public sealed class ExpressionContainer<TParam1, TParam2, TParam3, TReturn>
	{
		private readonly Func<TParam1, TParam2, TParam3, TReturn> _function;

		public ExpressionContainer(Func<TParam1, TParam2, TParam3, TReturn> function) => _function = function;

		public TReturn Invoke(TParam1 param1, TParam2 param2, TParam3 param3) => _function(param1, param2, param3);
	}
}