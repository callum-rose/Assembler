using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Assembler.Compiler.Compiler
{
	public class Parser
	{
		private readonly List<Token> _tokens;
		private int _position;
		private readonly Dictionary<string, ParameterExpression> _variables = new();
		private readonly List<ParameterExpression> _declaredVariables = new();
		private readonly Dictionary<string, List<MethodInfo>> _availableMethods = new();
		private readonly Dictionary<string, (Delegate compiledMethod, Type[] paramTypes, Type returnType)> _localMethods = new();
		private readonly Stack<LabelTarget> _breakLabels = new();
		private readonly Stack<LabelTarget> _continueLabels = new();
		private readonly Dictionary<string, Type> _registeredTypes = new();
		private readonly Stack<ParameterExpression> _lambdaParameters = new();
		private LabelTarget? _returnLabel;
		private Type _returnType = typeof(void);

		// Widening ladder used to mimic C#'s implicit numeric promotion in binary ops.
		private static readonly Dictionary<Type, int> NumericRank = new()
		{
			{ typeof(byte), 1 },
			{ typeof(sbyte), 1 },
			{ typeof(short), 2 },
			{ typeof(ushort), 2 },
			{ typeof(int), 3 },
			{ typeof(uint), 3 },
			{ typeof(long), 4 },
			{ typeof(ulong), 4 },
			{ typeof(float), 5 },
			{ typeof(double), 6 },
			{ typeof(decimal), 7 },
		};

		// Promote the narrower of two differing numeric operands to the wider type so the
		// LINQ Expression factory methods (which require matching operand types) accept them.
		private static void PromoteNumericOperands(ref Expression left, ref Expression right)
		{
			if (left.Type == right.Type)
			{
				return;
			}

			if (!NumericRank.TryGetValue(left.Type, out var leftRank) ||
				!NumericRank.TryGetValue(right.Type, out var rightRank))
			{
				return;
			}

			if (leftRank < rightRank)
			{
				left = Expression.Convert(left, right.Type);
			}
			else if (rightRank < leftRank)
			{
				right = Expression.Convert(right, left.Type);
			}
		}

		// Unity's Vector2/Vector3 arithmetic operators take a float scalar (e.g. Vector3 * float).
		// An integer literal like `2` lexes to int, so `v * 2` would otherwise fail to resolve an
		// operator. Widen a non-float numeric scalar to float whenever the other operand is a vector
		// so the struct's op_Multiply/op_Division (and friends) bind.
		private static void PromoteVectorScalarOperands(ref Expression left, ref Expression right)
		{
			if (IsVectorType(left.Type) && IsNonFloatNumeric(right.Type))
			{
				right = Expression.Convert(right, typeof(float));
			}
			else if (IsVectorType(right.Type) && IsNonFloatNumeric(left.Type))
			{
				left = Expression.Convert(left, typeof(float));
			}
		}

		private static bool IsVectorType(Type type) =>
			type == typeof(UnityEngine.Vector3) || type == typeof(UnityEngine.Vector2);

		private static bool IsNonFloatNumeric(Type type) =>
			NumericRank.ContainsKey(type) && type != typeof(float);

		private static Expression BuildBinary(Func<Expression, Expression, Expression> factory, Expression left, Expression right)
		{
			PromoteNumericOperands(ref left, ref right);
			PromoteVectorScalarOperands(ref left, ref right);
			return factory(left, right);
		}

		// Mirrors C#'s `x op= y` semantics: compute `x op y` with numeric promotion, then
		// narrow the result back to the target's type before assigning.
		private static Expression BuildCompoundAssign(Func<Expression, Expression, Expression> factory, Expression target, Expression value)
		{
			var combined = BuildBinary(factory, target, value);
			if (combined.Type != target.Type)
			{
				combined = Expression.Convert(combined, target.Type);
			}

			return Expression.Assign(target, combined);
		}

		// Coerces a value expression to `target`, mirroring the implicit conversions C# permits at
		// assignment-shaped sites (return, plain `=`, ternary/if-else branches, arguments): identity,
		// reference/boxing upcast and numeric widening along the NumericRank ladder. The conversion is
		// emitted through the Expression factory, but a conversion the factory rejects is translated from
		// its raw ArgumentException/InvalidOperationException into a positioned CompileException, so the
		// error carries the line/column the CompileException contract (and the LLM fix-loop) depends on.
		private Expression Coerce(Expression value, Type target, Token at)
		{
			if (target == typeof(void) || value.Type == target)
			{
				return value;
			}

			try
			{
				return Expression.Convert(value, target);
			}
			catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
			{
				throw Error($"Cannot convert type '{value.Type.Name}' to '{target.Name}'", at);
			}
		}

		// Unifies the two arms of a conditional (ternary `?:` or a value-returning if/else) to a common
		// type, since Expression.Condition requires both arms to share a type. Mirrors C#'s rule that one
		// arm must be implicitly convertible to the other: widens the narrower numeric arm along the
		// NumericRank ladder, otherwise coerces toward whichever arm the other is assignable to.
		private (Expression ifTrue, Expression ifFalse) UnifyConditionalBranches(
			Expression ifTrue, Expression ifFalse, Token at)
		{
			if (ifTrue.Type == ifFalse.Type)
			{
				return (ifTrue, ifFalse);
			}

			if (NumericRank.TryGetValue(ifTrue.Type, out var trueRank) &&
				NumericRank.TryGetValue(ifFalse.Type, out var falseRank))
			{
				return trueRank < falseRank
					? (Coerce(ifTrue, ifFalse.Type, at), ifFalse)
					: (ifTrue, Coerce(ifFalse, ifTrue.Type, at));
			}

			if (ifFalse.Type.IsAssignableFrom(ifTrue.Type))
			{
				return (Coerce(ifTrue, ifFalse.Type, at), ifFalse);
			}

			if (ifTrue.Type.IsAssignableFrom(ifFalse.Type))
			{
				return (ifTrue, Coerce(ifFalse, ifTrue.Type, at));
			}

			throw Error(
				$"Branches of a conditional have incompatible types '{ifTrue.Type.Name}' and '{ifFalse.Type.Name}'",
				at);
		}

		// Validates that a control-flow condition is boolean. The Expression factory (IfThenElse/Condition/
		// Loop) would otherwise throw a position-less ArgumentException ("Argument must be boolean") for
		// e.g. `while (1)`; checking here lets the error carry the condition's line/column and a clear message.
		private Expression RequireBoolean(Expression condition, Token at) =>
			condition.Type == typeof(bool)
				? condition
				: throw Error($"Condition must be boolean, but is '{condition.Type.Name}'", at);

		public Parser(List<Token> tokens)
		{
			_tokens = tokens;
		}

		public void RegisterParameter(ParameterExpression parameter)
		{
			if (parameter.Name != null)
			{
				_variables[parameter.Name] = parameter;
			}
			else
			{
				throw new Exception("Parameter must have a name to be registered.");
			}
		}

		public void RegisterMethod(string name, MethodInfo method)
		{
			if (!_availableMethods.ContainsKey(name))
			{
				_availableMethods[name] = new List<MethodInfo>();
			}

			_availableMethods[name].Add(method);
		}

		public void RegisterType(string name, Type type)
		{
			_registeredTypes[name] = type;
		}

		public void RegisterLocalMethod(string name, Delegate compiledMethod, Type[] paramTypes, Type returnType)
		{
			_localMethods[name] = (compiledMethod, paramTypes, returnType);
		}

		private Token Current => _tokens[_position];

		private void Advance() => _position++;

		private Token? PeekNextToken()
		{
			if (_position + 1 < _tokens.Count)
			{
				return _tokens[_position + 1];
			}
			return null;
		}

		private bool Match(params TokenType[] types)
		{
			if (types.Contains(Current.Type))
			{
				Advance();
				return true;
			}

			return false;
		}

		private Token Expect(TokenType type)
		{
			if (Current.Type != type)
			{
				throw Error($"Expected {type} but got {Current.Type}");
			}

			var token = Current;
			Advance();
			return token;
		}

		// Builds a compile error positioned at the current token. Compile errors signal invalid user
		// source (as opposed to an internal compiler bug, which stays a plain Exception).
		private CompileException Error(string message) => Error(message, Current);

		private static CompileException Error(string message, Token at) => new(message, at.Line, at.Column);

		// Sentinel carried while a dotted identifier prefix (e.g. `UnityEngine.Random`) is still being
		// accumulated and hasn't yet resolved to a Type — the next segment may complete it. It is never a
		// real value: if one survives to a point where a value is required (a call/index target, or the end
		// of a postfix chain), the dotted name was a typo and is reported as an unknown identifier. `Name` is
		// the accumulated dotted path tried so far; `Origin` positions the error at where the name began.
		private sealed class UnresolvedNamespace
		{
			public string Name { get; }
			public Token Origin { get; }

			public UnresolvedNamespace(string name, Token origin) => (Name, Origin) = (name, origin);
		}

		// Wraps an unresolved-namespace sentinel in a Type-carrying constant so it can flow through the
		// expression tree until it either accumulates into a real Type or is rejected at a value site.
		private static Expression UnresolvedNamespaceConstant(string name, Token origin) =>
			Expression.Constant(new UnresolvedNamespace(name, origin), typeof(UnresolvedNamespace));

		// Throws "Unknown identifier" when an expression is an unresolved-namespace sentinel that reached a
		// position where a value is required — its dotted name never resolved to a registered type. A no-op
		// for every other expression.
		private static void RejectUnresolvedNamespace(Expression expr)
		{
			if (expr is ConstantExpression { Value: UnresolvedNamespace ns })
			{
				throw Error($"Unknown identifier '{ns.Name}'", ns.Origin);
			}
		}

		public Expression ParseMethodBody(Dictionary<string, Type> parameters)
		{
			return ParseMethodBody(parameters, typeof(void));
		}

		public Expression ParseMethodBody(Dictionary<string, Type> parameters, Type returnType)
		{
			_returnType = returnType;
			_returnLabel = Expression.Label(returnType, "methodReturn");

			foreach (var param in parameters.Where(p => !_variables.ContainsKey(p.Key)))
			{
				var paramExpr = Expression.Parameter(param.Value, param.Key);
				_variables[param.Key] = paramExpr;
			}

			var statements = new List<Expression>();

			while (Current.Type != TokenType.EndOfFile)
			{
				if (IsMethodDefinition())
				{
					var methodDef = ParseMethodDefinition();
					CompileLocalMethod(methodDef.name, methodDef.returnType, methodDef.parameters, methodDef.bodyTokens);
				}
				else
				{
					statements.Add(ParseStatement());
				}
			}

			if (returnType == typeof(void))
			{
				statements.Add(Expression.Label(_returnLabel));

				if (_declaredVariables.Count > 0)
				{
					return Expression.Block(_declaredVariables, statements);
				}

				return statements.Count == 1 ? statements[0] : Expression.Block(statements);
			}

			// If the last statement is a value of the return type, treat it as an implicit return.
			if (statements.Count > 0 && statements[^1].Type != typeof(void))
			{
				var last = statements[^1];
				statements[^1] = Expression.Return(_returnLabel, Coerce(last, returnType, Current), returnType);
			}

			statements.Add(Expression.Label(_returnLabel, Expression.Default(returnType)));

			if (_declaredVariables.Count > 0)
			{
				return Expression.Block(returnType, _declaredVariables, statements);
			}

			return Expression.Block(returnType, statements);
		}

		private bool IsMethodDefinition()
		{
			var savedPosition = _position;

			// Check for return type
			if (!IsTypeToken(Current.Type) && Current.Type != TokenType.Void)
			{
				_position = savedPosition;
				return false;
			}

			Advance();

			// Check for identifier
			if (Current.Type != TokenType.Identifier)
			{
				_position = savedPosition;
				return false;
			}

			Advance();

			// Check for opening parenthesis
			var isMethod = Current.Type == TokenType.LeftParen;
			_position = savedPosition;
			return isMethod;
		}

		private bool IsTypeToken(TokenType type)
		{
			return type == TokenType.Int || type == TokenType.String_ || type == TokenType.Bool ||
				   type == TokenType.Float || type == TokenType.Double || type == TokenType.Var;
		}

		private (string name, Type returnType, List<(Type type, string name)> parameters, List<Token> bodyTokens)
			ParseMethodDefinition()
		{
			// Parse return type
			var returnTypeToken = Current;
			Advance();
			var returnType = GetTypeFromToken(returnTypeToken.Type);

			// Parse method name
			var name = Expect(TokenType.Identifier).Value;

			// Parse parameters
			Expect(TokenType.LeftParen);
			var parameters = new List<(Type type, string name)>();

			if (Current.Type != TokenType.RightParen)
			{
				do
				{
					var paramType = ParseSignatureType();
					var paramName = Expect(TokenType.Identifier).Value;
					parameters.Add((paramType, paramName));
				}
				while (Match(TokenType.Comma));
			}

			Expect(TokenType.RightParen);

			// Capture method body tokens
			var openBrace = Expect(TokenType.LeftBrace);
			var bodyTokens = new List<Token>();
			int braceCount = 1;

			while (braceCount > 0 && Current.Type != TokenType.EndOfFile)
			{
				if (Current.Type == TokenType.LeftBrace)
				{
					braceCount++;
				}

				if (Current.Type == TokenType.RightBrace)
				{
					braceCount--;
				}

				if (braceCount > 0)
				{
					bodyTokens.Add(Current);
				}

				Advance();
			}

			// Reaching EOF with the brace still open means the body was never closed. Without this the
			// truncated body parses on and fails later with an unrelated, mispositioned error.
			if (braceCount > 0)
			{
				throw Error($"Unbalanced braces in body of local method '{name}': missing '}}' before end of input.",
					openBrace);
			}

			return (name, returnType, parameters, bodyTokens);
		}

		// Parses a type in a local-method signature: a built-in keyword (`int`/`float`/…) or a resolvable
		// (possibly dotted) registered type name such as `UnityEngine.Vector3`. Emits a positioned compile
		// error rather than the bare Exception GetTypeFromToken would throw for an unregistered name.
		private Type ParseSignatureType()
		{
			if (IsTypeToken(Current.Type) || Current.Type == TokenType.Void)
			{
				var keyword = Current;
				Advance();
				return GetTypeFromToken(keyword.Type);
			}

			var nameToken = Expect(TokenType.Identifier);
			var typeName = nameToken.Value;

			while (Current.Type == TokenType.Dot)
			{
				Match(TokenType.Dot);
				typeName += "." + Expect(TokenType.Identifier).Value;
			}

			return TryResolveType(typeName)
				?? throw Error(
					$"Type '{typeName}' not found. Make sure to register custom types or use fully qualified names.",
					nameToken);
		}

		private void CompileLocalMethod(string name,
			Type returnType,
			List<(Type type, string name)> parameters,
			List<Token> bodyTokens)
		{
			// Add EOF token
			bodyTokens.Add(new Token(TokenType.EndOfFile, "", 0, 0));

			// Create new parser for method body
			var methodParser = new Parser(bodyTokens);

			// Copy registered methods
			foreach (var methodGroup in _availableMethods)
			{
				foreach (var method in methodGroup.Value)
				{
					methodParser.RegisterMethod(methodGroup.Key, method);
				}
			}

			// Copy registered types
			foreach (var type in _registeredTypes)
			{
				methodParser.RegisterType(type.Key, type.Value);
			}

			// Copy already compiled local methods
			foreach (var localMethod in _localMethods)
			{
				methodParser._localMethods[localMethod.Key] = localMethod.Value;
			}

			// Create parameter expressions and register them
			var paramExprs = parameters.Select(p => Expression.Parameter(p.type, p.name)).ToArray();

			foreach (var paramExpr in paramExprs)
			{
				methodParser.RegisterParameter(paramExpr);
			}

			var paramDict = new Dictionary<string, Type>();
			var body = methodParser.ParseMethodBody(paramDict, returnType);

			// Handle void return
			if (returnType == typeof(void))
			{
				body = Expression.Block(body, Expression.Empty());
			}

			var delegateType = DelegateTypeHelper.GetDelegateType(returnType, parameters.Select(p => p.type).ToArray());
			var lambda = Expression.Lambda(delegateType, body, paramExprs);
			var compiled = lambda.Compile();

			_localMethods[name] = (compiled, parameters.Select(p => p.type).ToArray(), returnType);
		}

		private Type GetTypeFromToken(TokenType tokenType)
		{
			return tokenType switch
			{
				TokenType.Int => typeof(int),
				TokenType.String_ => typeof(string),
				TokenType.Bool => typeof(bool),
				TokenType.Float => typeof(float),
				TokenType.Double => typeof(double),
				TokenType.Void => typeof(void),
				TokenType.Var => typeof(object),
				_ => throw new Exception($"Unknown type token: {tokenType}")
			};
		}

		private MethodInfo? FindBestMethodOverload(List<MethodInfo> methods, Type[] argTypes)
		{
			// First, try to find an exact match
			foreach (var method in methods)
			{
				var parameters = method.GetParameters();

				if (parameters.Length != argTypes.Length)
				{
					continue;
				}

				bool exactMatch = !argTypes.Where((t, i) => parameters[i].ParameterType != t).Any();

				if (exactMatch)
				{
					return method;
				}
			}

			// If no exact match, try to find a compatible one (with conversions)
			foreach (var method in methods)
			{
				var parameters = method.GetParameters();

				if (parameters.Length == argTypes.Length)
				{
					bool compatible = true;

					for (int i = 0; i < argTypes.Length; i++)
					{
						if (!IsCompatibleType(argTypes[i], parameters[i].ParameterType))
						{
							compatible = false;
							break;
						}
					}

					if (compatible)
					{
						return method;
					}
				}
			}

			return null;
		}

		private MethodInfo MakeConcreteGenericMethodWithArgs(MethodInfo genericMethod, Type instanceType, List<Expression> arguments)
		{
			// This version has access to the actual argument expressions, so we can inspect lambda return types
			var genericArgs = genericMethod.GetGenericArguments();
			var typeMap = new Dictionary<Type, Type>();

			// Try to infer generic types from the instance type (the 'this' parameter)
			var firstParam = genericMethod.GetParameters()[0];
			if (firstParam.ParameterType.IsGenericParameter)
			{
				// Simple case: this T source
				typeMap[firstParam.ParameterType] = instanceType;
			}
			else if (firstParam.ParameterType.IsGenericType)
			{
				// Complex case: this IEnumerable<T> source
				var paramGenericDef = firstParam.ParameterType.GetGenericTypeDefinition();

				// Find matching interface on instance type
				Type? matchingInterface = null;
				if (instanceType.IsGenericType && instanceType.GetGenericTypeDefinition() == paramGenericDef)
				{
					matchingInterface = instanceType;
				}
				else
				{
					foreach (var iface in instanceType.GetInterfaces())
					{
						if (iface.IsGenericType && iface.GetGenericTypeDefinition() == paramGenericDef)
						{
							matchingInterface = iface;
							break;
						}
					}
				}

				if (matchingInterface != null)
				{
					var paramGenericArgs = firstParam.ParameterType.GetGenericArguments();
					var ifaceGenericArgs = matchingInterface.GetGenericArguments();

					for (int i = 0; i < paramGenericArgs.Length && i < ifaceGenericArgs.Length; i++)
					{
						if (paramGenericArgs[i].IsGenericParameter)
						{
							typeMap[paramGenericArgs[i]] = ifaceGenericArgs[i];
						}
					}
				}
			}

			// Try to infer from lambda return types in arguments
			var methodParams = genericMethod.GetParameters();
			for (int i = 0; i < arguments.Count && i + 1 < methodParams.Length; i++)
			{
				var paramType = methodParams[i + 1].ParameterType; // +1 to skip 'this' parameter
				var arg = arguments[i];

				// Check if this is a lambda and the parameter type is a delegate with generic args
				if (arg is LambdaExpression lambdaExpr && paramType.IsGenericType && !paramType.IsGenericTypeDefinition)
				{
					var paramGenericArgs = paramType.GetGenericArguments();
					// For Func<TSource, TResult>, last generic arg is the return type
					if (paramType.GetGenericTypeDefinition().Name.StartsWith("Func`") && paramGenericArgs.Length > 0)
					{
						var returnTypeGenericParam = paramGenericArgs[paramGenericArgs.Length - 1];
						if (returnTypeGenericParam.IsGenericParameter && !typeMap.ContainsKey(returnTypeGenericParam))
						{
							// Infer from lambda's return type
							typeMap[returnTypeGenericParam] = lambdaExpr.Body.Type;
						}
					}
				}
			}

			// Build the concrete type arguments array
			var concreteTypes = new Type[genericArgs.Length];
			for (int i = 0; i < genericArgs.Length; i++)
			{
				if (typeMap.TryGetValue(genericArgs[i], out var mappedType))
				{
					concreteTypes[i] = mappedType;
				}
				else
				{
					// Couldn't infer - this might fail, but let's try with object as fallback
					concreteTypes[i] = typeof(object);
				}
			}

			return genericMethod.MakeGenericMethod(concreteTypes);
		}

		private bool IsCompatibleType(Type from, Type to)
		{
			if (from == to)
			{
				return true;
			}

			// Check if conversion is possible
			if (to.IsAssignableFrom(from))
			{
				return true;
			}

			// Handle lambda expressions (which are represented as LambdaExpression in the AST but compile to delegates)
			if (to.IsSubclassOf(typeof(Delegate)) || to == typeof(Delegate))
			{
				// Lambda expressions are compatible with any delegate type
				if (from.IsSubclassOf(typeof(System.Linq.Expressions.LambdaExpression)) ||
					from == typeof(System.Linq.Expressions.LambdaExpression))
				{
					return true;
				}

				// For delegate-to-delegate, check signature compatibility
				if (from.IsSubclassOf(typeof(Delegate)) || from == typeof(Delegate))
				{
					var fromInvoke = from.GetMethod("Invoke");
					var toInvoke = to.GetMethod("Invoke");

					if (fromInvoke != null && toInvoke != null)
					{
						var fromParams = fromInvoke.GetParameters();
						var toParams = toInvoke.GetParameters();

						// Must have same parameter count
						if (fromParams.Length != toParams.Length)
						{
							return false;
						}

						// Check parameter types match
						for (int i = 0; i < fromParams.Length; i++)
						{
							if (fromParams[i].ParameterType != toParams[i].ParameterType)
							{
								return false;
							}
						}

						// Check return types match
						if (fromInvoke.ReturnType != toInvoke.ReturnType)
						{
							return false;
						}

						return true;
					}
				}
			}

			// Numeric conversions
			if (IsNumericType(from) && IsNumericType(to))
			{
				return true;
			}

			return false;
		}

		private bool IsNumericType(Type type)
		{
			return type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte) ||
				   type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte) ||
				   type == typeof(float) || type == typeof(double) || type == typeof(decimal);
		}

		private Expression ParseStatement()
		{
			// A registered/resolvable type name used in declaration position (e.g. `Vector3 dir = ...;`)
			// looks like a bare identifier to the lexer, so disambiguate it from an expression statement
			// with lookahead before falling through to the keyword cases below.
			if (Current.Type == TokenType.Identifier && IsTypedDeclaration())
			{
				return ParseVariableDeclaration();
			}

			return Current.Type switch
			{
				TokenType.If => ParseIf(),
				TokenType.For => ParseFor(),
				TokenType.While => ParseWhile(),
				TokenType.Return => ParseReturn(),
				TokenType.Break => ParseBreak(),
				TokenType.Continue => ParseContinue(),
				TokenType.Var
					or TokenType.Int
					or TokenType.String_
					or TokenType.Bool
					or TokenType.Float
					or TokenType.Double => ParseVariableDeclaration(),
				TokenType.LeftBrace => ParseBlock(),
				_ => ParseExpressionStatement()
			};
		}

		// Looks ahead (without consuming tokens) to decide whether the current identifier begins a typed
		// local declaration: a resolvable type name (possibly dotted, e.g. `UnityEngine.Vector3`) followed
		// by a variable name and then `=` or `;`. Requiring the type to resolve keeps expression statements
		// like `foo.bar = x;` from being misread as declarations.
		private bool IsTypedDeclaration()
		{
			var savedPosition = _position;
			try
			{
				if (Current.Type != TokenType.Identifier)
				{
					return false;
				}

				var typeName = Current.Value;
				Advance();

				while (Current.Type == TokenType.Dot)
				{
					Advance();
					if (Current.Type != TokenType.Identifier)
					{
						return false;
					}
					typeName += "." + Current.Value;
					Advance();
				}

				if (Current.Type != TokenType.Identifier)
				{
					return false;
				}
				Advance();

				if (Current.Type != TokenType.Assign && Current.Type != TokenType.Semicolon)
				{
					return false;
				}

				return TryResolveType(typeName) != null;
			}
			finally
			{
				_position = savedPosition;
			}
		}

		private Expression ParseBlock()
		{
			Expect(TokenType.LeftBrace);

			var statements = new List<Expression>();

			while (Current.Type != TokenType.RightBrace && Current.Type != TokenType.EndOfFile)
			{
				statements.Add(ParseStatement());
			}

			Expect(TokenType.RightBrace);

			return Expression.Block(statements);
		}

		private Expression ParseIf()
		{
			var ifToken = Expect(TokenType.If);
			Expect(TokenType.LeftParen);
			var conditionToken = Current;
			var condition = RequireBoolean(ParseExpression(), conditionToken);
			Expect(TokenType.RightParen);

			var ifTrue = ParseStatementOrBlock();

			if (Match(TokenType.Else))
			{
				var ifFalse = ParseStatementOrBlock();

				// Use Condition for value-returning branches, unifying their types so the factory accepts them.
				if (ifTrue.Type != typeof(void) && ifFalse.Type != typeof(void))
				{
					var (unifiedTrue, unifiedFalse) = UnifyConditionalBranches(ifTrue, ifFalse, ifToken);
					return Expression.Condition(condition, unifiedTrue, unifiedFalse);
				}

				return Expression.IfThenElse(condition, ifTrue, ifFalse);
			}

			// If the if-branch returns a value but there's no else, we still need to handle it
			// This is valid when the if returns but code continues after
			return Expression.IfThen(condition, ifTrue);
		}

		private Expression ParseStatementOrBlock()
		{
			if (Current.Type == TokenType.LeftBrace)
			{
				Expect(TokenType.LeftBrace);
				var statements = new List<Expression>();
				var blockVariables = new List<ParameterExpression>();

				// Save current variable count to track new ones
				var initialVarCount = _declaredVariables.Count;

				while (Current.Type != TokenType.RightBrace && Current.Type != TokenType.EndOfFile)
				{
					statements.Add(ParseStatement());
				}

				Expect(TokenType.RightBrace);

				// Collect variables declared in this block
				for (int i = initialVarCount; i < _declaredVariables.Count; i++)
				{
					blockVariables.Add(_declaredVariables[i]);
				}

				// Check if last statement is a return (non-void expression)
				if (statements.Count > 0)
				{
					var lastStmt = statements[^1];

					// If it's a non-void expression, use it as the block result
					if (lastStmt.Type != typeof(void))
					{
						if (blockVariables.Count > 0)
						{
							return Expression.Block(lastStmt.Type, blockVariables, statements);
						}

						return statements.Count == 1 ? lastStmt : Expression.Block(lastStmt.Type, statements);
					}
				}

				if (blockVariables.Count > 0)
				{
					return Expression.Block(blockVariables, statements);
				}

				return statements.Count > 0 ? Expression.Block(statements) : Expression.Empty();
			}

			return ParseStatement();
		}

		private Expression ParseFor()
		{
			Expect(TokenType.For);
			Expect(TokenType.LeftParen);

			var initializer = ParseStatement();
			var conditionToken = Current;
			var condition = RequireBoolean(ParseExpression(), conditionToken);
			Expect(TokenType.Semicolon);
			var increment = ParseExpression();

			Expect(TokenType.RightParen);

			var breakLabel = Expression.Label("break");
			var continueLabel = Expression.Label("continue");

			_breakLabels.Push(breakLabel);
			_continueLabels.Push(continueLabel);

			var body = ParseStatement();

			_breakLabels.Pop();
			_continueLabels.Pop();

			return Expression.Block(
				initializer,
				Expression.Loop(
					Expression.IfThenElse(
						condition,
						Expression.Block(
							body,
							Expression.Label(continueLabel),
							increment
						),
						Expression.Break(breakLabel)
					),
					breakLabel
				)
			);
		}

		private Expression ParseWhile()
		{
			Expect(TokenType.While);
			Expect(TokenType.LeftParen);
			var conditionToken = Current;
			var condition = RequireBoolean(ParseExpression(), conditionToken);
			Expect(TokenType.RightParen);

			var breakLabel = Expression.Label("break");
			var continueLabel = Expression.Label("continue");

			_breakLabels.Push(breakLabel);
			_continueLabels.Push(continueLabel);

			var body = ParseStatement();

			_breakLabels.Pop();
			_continueLabels.Pop();

			return Expression.Loop(
				Expression.Block(
					Expression.Label(continueLabel),
					Expression.IfThenElse(
						condition,
						body,
						Expression.Break(breakLabel)
					)
				),
				breakLabel
			);
		}

		private Expression ParseReturn()
		{
			var returnToken = Expect(TokenType.Return);

			if (_returnLabel == null)
			{
				throw Error("'return' is only valid inside a method body.", returnToken);
			}

			if (Match(TokenType.Semicolon))
			{
				return Expression.Return(_returnLabel);
			}

			var valueToken = Current;
			var value = ParseExpression();
			Expect(TokenType.Semicolon);

			return Expression.Return(_returnLabel, Coerce(value, _returnType, valueToken), _returnType);
		}

		private Expression ParseBreak()
		{
			var breakToken = Expect(TokenType.Break);
			Expect(TokenType.Semicolon);

			if (_breakLabels.Count == 0)
			{
				throw Error("'break' is only valid inside a loop", breakToken);
			}

			return Expression.Break(_breakLabels.Peek());
		}

		private Expression ParseContinue()
		{
			var continueToken = Expect(TokenType.Continue);
			Expect(TokenType.Semicolon);

			if (_continueLabels.Count == 0)
			{
				throw Error("'continue' is only valid inside a loop", continueToken);
			}

			return Expression.Continue(_continueLabels.Peek());
		}

		// Parses the declared type of a local: a built-in keyword (`var`/`int`/`float`/…) or a resolvable
		// type name (possibly dotted, e.g. `UnityEngine.Vector3`). `var` resolves to object here and is
		// inferred from the initializer by the caller.
		private Type ParseDeclarationType()
		{
			if (Current.Type is TokenType.Var
				or TokenType.Int
				or TokenType.String_
				or TokenType.Bool
				or TokenType.Float
				or TokenType.Double)
			{
				var typeToken = Current;
				Advance();
				return GetTypeFromToken(typeToken.Type);
			}

			var typeNameToken = Expect(TokenType.Identifier);
			var typeName = typeNameToken.Value;

			while (Current.Type == TokenType.Dot)
			{
				Match(TokenType.Dot);
				typeName += "." + Expect(TokenType.Identifier).Value;
			}

			return TryResolveType(typeName)
				?? throw Error($"Type '{typeName}' not found. Make sure to register custom types or use fully qualified names.",
					typeNameToken);
		}

		private Expression ParseVariableDeclaration()
		{
			var type = ParseDeclarationType();

			var nameToken = Expect(TokenType.Identifier);
			var name = nameToken.Value;

			Expression? initializer = null;

			if (Match(TokenType.Assign))
			{
				initializer = ParseExpression();

				// If type is object (var keyword), infer from initializer
				if (type == typeof(object) && initializer != null)
				{
					type = initializer.Type;
				}
			}

			Expect(TokenType.Semicolon);

			var variable = Expression.Variable(type, name);
			_variables[name] = variable;
			_declaredVariables.Add(variable);

			if (initializer != null)
			{
				return Expression.Assign(variable, Coerce(initializer, type, nameToken));
			}

			return Expression.Empty();
		}

		private Expression ParseExpressionStatement()
		{
			var expr = ParseExpression();
			Expect(TokenType.Semicolon);
			return expr;
		}

		private Expression ParseExpression()
		{
			return ParseTernary();
		}

		private Expression ParseTernary()
		{
			var expr = ParseLogicalOr();

			if (Current.Type == TokenType.Question)
			{
				var questionToken = Expect(TokenType.Question);
				var trueExpr = ParseExpression();
				Expect(TokenType.Colon);
				var falseExpr = ParseExpression();
				var (ifTrue, ifFalse) = UnifyConditionalBranches(trueExpr, falseExpr, questionToken);
				return Expression.Condition(expr, ifTrue, ifFalse);
			}

			return expr;
		}

		private Expression ParseLogicalOr()
		{
			var left = ParseLogicalAnd();

			while (Match(TokenType.Or))
			{
				var right = ParseLogicalAnd();
				left = Expression.OrElse(left, right);
			}

			return left;
		}

		private Expression ParseLogicalAnd()
		{
			var left = ParseXor();

			while (Match(TokenType.And))
			{
				var right = ParseXor();
				left = Expression.AndAlso(left, right);
			}

			return left;
		}

		// `^` sits between `&&` and `==` in C#'s precedence ladder. For bool operands it is
		// logical XOR; for integral operands it is bitwise XOR. Expression.ExclusiveOr resolves
		// both (and any struct op_ExclusiveOr) via the same factory.
		private Expression ParseXor()
		{
			var left = ParseEquality();

			while (Match(TokenType.Xor))
			{
				left = BuildBinary(Expression.ExclusiveOr, left, ParseEquality());
			}

			return left;
		}

		private Expression ParseEquality()
		{
			var left = ParseComparison();

			while (true)
			{
				if (Match(TokenType.Equal))
				{
					left = BuildBinary(Expression.Equal, left, ParseComparison());
				}
				else if (Match(TokenType.NotEqual))
				{
					left = BuildBinary(Expression.NotEqual, left, ParseComparison());
				}
				else
				{
					break;
				}
			}

			return left;
		}

		private Expression ParseComparison()
		{
			var left = ParseAdditive();

			while (true)
			{
				if (Match(TokenType.LessThan))
				{
					left = BuildBinary(Expression.LessThan, left, ParseAdditive());
				}
				else if (Match(TokenType.GreaterThan))
				{
					left = BuildBinary(Expression.GreaterThan, left, ParseAdditive());
				}
				else if (Match(TokenType.LessThanOrEqual))
				{
					left = BuildBinary(Expression.LessThanOrEqual, left, ParseAdditive());
				}
				else if (Match(TokenType.GreaterThanOrEqual))
				{
					left = BuildBinary(Expression.GreaterThanOrEqual, left, ParseAdditive());
				}
				else
				{
					break;
				}
			}

			return left;
		}

		private Expression ParseAdditive()
		{
			var left = ParseMultiplicative();

			while (true)
			{
				if (Match(TokenType.Plus))
				{
					var right = ParseMultiplicative();
					if (left.Type == typeof(string) || right.Type == typeof(string))
					{
						var concat = typeof(string).GetMethod("Concat", new[] { typeof(string), typeof(string) });
						var l = left.Type == typeof(string) ? left : Expression.Call(left, "ToString", Type.EmptyTypes);
						var r = right.Type == typeof(string) ? right : Expression.Call(right, "ToString", Type.EmptyTypes);
						left = Expression.Call(concat, l, r);
					}
					else
					{
						left = BuildBinary(Expression.Add, left, right);
					}
				}
				else if (Match(TokenType.Minus))
				{
					left = BuildBinary(Expression.Subtract, left, ParseMultiplicative());
				}
				else
				{
					break;
				}
			}

			return left;
		}

		private Expression ParseMultiplicative()
		{
			var left = ParseUnary();

			while (true)
			{
				if (Match(TokenType.Multiply))
				{
					left = BuildBinary(Expression.Multiply, left, ParseUnary());
				}
				else if (Match(TokenType.Divide))
				{
					left = BuildBinary(Expression.Divide, left, ParseUnary());
				}
				else if (Match(TokenType.Modulo))
				{
					left = BuildBinary(Expression.Modulo, left, ParseUnary());
				}
				else
				{
					break;
				}
			}

			return left;
		}

		private Expression ParseUnary()
		{
			if (Match(TokenType.Not))
			{
				return Expression.Not(ParseUnary());
			}

			if (Match(TokenType.Minus))
			{
				return Expression.Negate(ParseUnary());
			}

			// Handle cast: (type)expression
			if (Current.Type == TokenType.LeftParen)
			{
				var savedPos = _position;
				Match(TokenType.LeftParen);

				// Check if this is a type cast
				if (Current.Type == TokenType.Int || Current.Type == TokenType.Float ||
					Current.Type == TokenType.Double || Current.Type == TokenType.Bool ||
					Current.Type == TokenType.String_)
				{
					var castType = GetTypeFromToken(Current.Type);
					Advance();
					Expect(TokenType.RightParen);
					var expr = ParseUnary();
					return Expression.Convert(expr, castType);
				}

				// Not a cast, restore position
				_position = savedPos;
			}

			return ParsePostfix();
		}

		private Expression ParsePostfix()
		{
			var expr = ParsePrimary();

			while (true)
			{
				if (Match(TokenType.LeftParen))
				{
					// `name(...)` where `name` is a still-unresolved dotted prefix (a member access keeps
					// accumulating instead) means the name was a typo — report it before the call site.
					RejectUnresolvedNamespace(expr);
					expr = ParseFunctionCall(expr);
				}
				else if (Match(TokenType.Dot))
				{
					expr = ParseMemberAccess(expr);
				}
				else if (Match(TokenType.LeftBracket))
				{
					RejectUnresolvedNamespace(expr);
					expr = ParseIndexAccess(expr);
				}
				else
				{
					break;
				}
			}

			// A sentinel that reaches here (e.g. a bare `Mthf.Foo`) never resolved to a type — report it
			// rather than letting the typed constant leak into the surrounding expression as a value.
			RejectUnresolvedNamespace(expr);
			return expr;
		}

		// Parses an element/indexer access `instance[i]` (caller has already consumed '['). Supports
		// arrays (including multi-dimensional `a[i, j]`) and indexer properties (`this[...]` such as
		// List<T>, Dictionary<K,V>, string). The built access is an assignable IndexExpression, so the
		// assignment / compound-assignment / increment cases mirror the variable-target handling in
		// ParsePrimary, letting `a[i] = x`, `a[i] += x`, and `a[i]++` work as statements.
		private Expression ParseIndexAccess(Expression instance)
		{
			var indices = new List<Expression> { ParseExpression() };

			while (Match(TokenType.Comma))
			{
				indices.Add(ParseExpression());
			}

			Expect(TokenType.RightBracket);

			var indexAccess = BuildIndexAccess(instance, indices);

			if (Match(TokenType.Assign))
			{
				var valueToken = Current;
				var value = ParseExpression();
				return Expression.Assign(indexAccess, Coerce(value, indexAccess.Type, valueToken));
			}

			if (Match(TokenType.PlusAssign))
			{
				return BuildCompoundAssign(Expression.Add, indexAccess, ParseExpression());
			}

			if (Match(TokenType.MinusAssign))
			{
				return BuildCompoundAssign(Expression.Subtract, indexAccess, ParseExpression());
			}

			if (Match(TokenType.MultiplyAssign))
			{
				return BuildCompoundAssign(Expression.Multiply, indexAccess, ParseExpression());
			}

			if (Match(TokenType.DivideAssign))
			{
				return BuildCompoundAssign(Expression.Divide, indexAccess, ParseExpression());
			}

			if (Match(TokenType.Increment))
			{
				return Expression.PreIncrementAssign(indexAccess);
			}

			if (Match(TokenType.Decrement))
			{
				return Expression.PreDecrementAssign(indexAccess);
			}

			return indexAccess;
		}

		// Builds the element/indexer access for `instance[indices...]`: an array access for array types,
		// otherwise a matching indexer property. The result is an assignable IndexExpression so callers
		// can use it both as a value and as an assignment target.
		private Expression BuildIndexAccess(Expression instance, List<Expression> indices)
		{
			if (instance.Type.IsArray)
			{
				// Array indices must be int; widen narrower integral index expressions to match.
				var arrayIndices = indices
					.Select(i => i.Type == typeof(int) ? i : Expression.Convert(i, typeof(int)))
					.ToArray();

				return Expression.ArrayAccess(instance, arrayIndices);
			}

			var argTypes = indices.Select(a => a.Type).ToArray();
			var indexer = FindIndexer(instance.Type, argTypes)
				?? throw Error(
					$"No indexer on type '{instance.Type.Name}' accepting argument types: " +
					$"{string.Join(", ", argTypes.Select(t => t.Name))}");

			var indexParams = indexer.GetIndexParameters();
			var convertedArgs = new Expression[indices.Count];
			for (int i = 0; i < indices.Count; i++)
			{
				convertedArgs[i] = indices[i].Type != indexParams[i].ParameterType
					? Expression.Convert(indices[i], indexParams[i].ParameterType)
					: indices[i];
			}

			return Expression.Property(instance, indexer, convertedArgs);
		}

		// Finds an indexer property (`this[...]`) on the type whose index parameters match the given
		// argument types — exact match preferred, then a conversion-compatible one. Matches on the
		// index-parameter shape rather than the property name because an indexer can be declared with a
		// non-default name via IndexerNameAttribute; GetProperties already surfaces inherited indexers.
		private PropertyInfo? FindIndexer(Type type, Type[] argTypes)
		{
			var indexers = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.GetIndexParameters().Length == argTypes.Length)
				.ToList();

			foreach (var indexer in indexers)
			{
				var indexParams = indexer.GetIndexParameters();
				if (!argTypes.Where((t, i) => indexParams[i].ParameterType != t).Any())
				{
					return indexer;
				}
			}

			foreach (var indexer in indexers)
			{
				var indexParams = indexer.GetIndexParameters();
				if (Enumerable.Range(0, argTypes.Length).All(i => IsCompatibleType(argTypes[i], indexParams[i].ParameterType)))
				{
					return indexer;
				}
			}

			return null;
		}

		private Expression ParseMemberAccess(Expression instance)
		{
			var memberToken = Expect(TokenType.Identifier);
			var memberName = memberToken.Value;

			// Static member / namespace-sentinel access (e.g. UnityEngine.Random.Range(...)).
			var staticAccess = ResolveStaticTypeContext(instance, memberName);
			if (staticAccess != null)
			{
				return staticAccess;
			}

			// Method call
			if (Current.Type == TokenType.LeftParen)
			{
				Match(TokenType.LeftParen);

				// Gather candidate methods up front to provide type context for lambda arguments.
				var instanceMethods = instance.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
					.Where(m => m.Name == memberName).ToList();

				List<MethodInfo> extensionMethods = new();
				if (_availableMethods.TryGetValue(memberName, out var registeredMethods))
				{
					extensionMethods = registeredMethods
						.Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
						.Where(m => m.GetParameters().Length > 0)
						.ToList();
				}

				var arguments = ParseCallArguments(instance, instanceMethods, extensionMethods);

				Expect(TokenType.RightParen);

				var method = FindAndValidateMethod(instance, memberName, memberToken, arguments, out var isExtensionMethod);
				var convertedArgs = ConvertArguments(method, arguments, isExtensionMethod);

				return isExtensionMethod
					? Expression.Call(null, method, new[] { instance }.Concat(convertedArgs))
					: Expression.Call(instance, method, convertedArgs);
			}

			// Assignment to member
			if (Match(TokenType.Assign))
			{
				var valueToken = Current;
				var value = ParseExpression();
				var member = Expression.PropertyOrField(instance, memberName);
				return Expression.Assign(member, Coerce(value, member.Type, valueToken));
			}

			// Property or field access
			return Expression.PropertyOrField(instance, memberName);
		}

		// Resolves static member / namespace-sentinel access. Returns the resulting expression when the
		// instance is a Type or namespace sentinel, or null when it is an ordinary instance to be handled
		// by the caller.
		private Expression? ResolveStaticTypeContext(Expression instance, string memberName)
		{
			// Detect "type sentinel" used for static member access (e.g. UnityEngine.Random.Range(...)).
			// ParsePrimary returns Expression.Constant(type, typeof(Type)) when an identifier resolves to a Type.
			Type? staticTargetType = null;
			if (instance is ConstantExpression ce && ce.Type == typeof(Type) && ce.Value is Type tt)
			{
				staticTargetType = tt;
			}

			// Handle nested type / namespace continuation (e.g. UnityEngine.Random where UnityEngine alone
			// isn't a Type yet). If we have a "namespace sentinel", the previous step stored the partial
			// dotted name; try to resolve again with the new segment appended.
			if (staticTargetType == null && instance is ConstantExpression { Value: UnresolvedNamespace ns })
			{
				var combined = ns.Name + "." + memberName;
				var resolved = TryResolveType(combined);
				if (resolved != null)
				{
					return Expression.Constant(resolved, typeof(Type));
				}
				// Still not a full type - keep accumulating, preserving the original start position.
				return UnresolvedNamespaceConstant(combined, ns.Origin);
			}

			// Static member access on a resolved Type.
			return staticTargetType != null ? ParseStaticMemberAccess(staticTargetType, memberName) : null;
		}

		// Parses the comma-separated argument list of a method call (caller has already consumed '('),
		// inferring lambda parameter types from the candidate method signatures.
		private List<Expression> ParseCallArguments(Expression instance, List<MethodInfo> instanceMethods,
			List<MethodInfo> extensionMethods)
		{
			var arguments = new List<Expression>();

			if (Current.Type != TokenType.RightParen)
			{
				do
				{
					// Check if this is a lambda expression
					if (Current.Type == TokenType.Identifier && PeekNextToken()?.Type == TokenType.Arrow)
					{
						var lambdaParamType =
							InferLambdaParameterType(instance, instanceMethods, extensionMethods, arguments.Count);
						arguments.Add(ParseLambdaWithType(lambdaParamType));
					}
					else
					{
						arguments.Add(ParseExpression());
					}
				}
				while (Match(TokenType.Comma));
			}

			return arguments;
		}

		// Infers the parameter type for a lambda argument at argIndex from extension- then instance-method
		// signatures (e.g. List<int>.Where(x => ...) infers x as int). Returns null when it can't be inferred.
		private Type? InferLambdaParameterType(Expression instance, List<MethodInfo> instanceMethods,
			List<MethodInfo> extensionMethods, int argIndex)
		{
			// Try to infer from extension methods first
			foreach (var extMethod in extensionMethods)
			{
				var extParams = extMethod.GetParameters();
				if (extParams.Length > argIndex + 1) // +1 because first param is 'this'
				{
					var paramType = extParams[argIndex + 1].ParameterType;

					// Handle generic types
					if (paramType.IsGenericParameter)
					{
						// This is a generic parameter like TFunc - try to find the concrete type
						// For List<int>.Where(x => ...), the TSource should be int
						var elementType = GetEnumerableElementType(instance.Type);
						if (elementType != null)
						{
							return elementType;
						}
					}
					else if (paramType.IsGenericType)
					{
						var genDef = paramType.GetGenericTypeDefinition();
						if (genDef == typeof(Func<,>) || genDef == typeof(Func<,,>))
						{
							var genArgs = paramType.GetGenericArguments();
							// First generic argument is the parameter type for the lambda
							if (genArgs.Length >= 1)
							{
								if (genArgs[0].IsGenericParameter)
								{
									// Resolve generic parameter from instance type
									var elementType = GetEnumerableElementType(instance.Type);
									if (elementType != null)
									{
										return elementType;
									}
								}
								else
								{
									return genArgs[0];
								}
							}
						}
					}
				}
			}

			// Try instance methods if no extension method matched
			foreach (var instMethod in instanceMethods)
			{
				var instParams = instMethod.GetParameters();
				if (instParams.Length > argIndex)
				{
					var paramType = instParams[argIndex].ParameterType;
					if (paramType.IsGenericType)
					{
						var genDef = paramType.GetGenericTypeDefinition();
						if (genDef == typeof(Func<,>) || genDef == typeof(Func<,,>))
						{
							var genArgs = paramType.GetGenericArguments();
							if (genArgs.Length >= 1)
							{
								return genArgs[0];
							}
						}
					}
				}
			}

			return null;
		}

		// Resolves the target method for a call: instance method first, then extension method (made concrete
		// if it's a generic definition). Throws when no overload matches.
		private MethodInfo FindAndValidateMethod(Expression instance, string memberName, Token memberToken,
			List<Expression> arguments, out bool isExtensionMethod)
		{
			isExtensionMethod = false;

			// Try to find instance method
			var argTypes = arguments.Select(a => a.Type).ToArray();
			var method = instance.Type.GetMethod(memberName, BindingFlags.Public | BindingFlags.Instance, null, argTypes, null);

			if (method == null)
			{
				// Try to find extension method - use better matching that considers lambda parameter counts
				method = FindBestExtensionMethod(instance.Type, memberName, arguments);

				if (method != null)
				{
					isExtensionMethod = true;

					// If it's a generic method definition, make it concrete
					if (method.IsGenericMethodDefinition)
					{
						method = MakeConcreteGenericMethodWithArgs(method, instance.Type, arguments);
					}
				}
				else
				{
					throw Error($"Method '{memberName}' not found on type '{instance.Type.Name}'", memberToken);
				}
			}

			return method;
		}

		// Converts call arguments to their target parameter types now that the concrete method is known,
		// re-creating lambdas with the correct delegate type and inserting numeric/reference conversions.
		private List<Expression> ConvertArguments(MethodInfo method, List<Expression> arguments, bool isExtensionMethod)
		{
			var parameters = method.GetParameters();
			var convertedArgs = new List<Expression>();

			// For extension methods, the first parameter is the instance (this parameter)
			int startIndex = isExtensionMethod ? 1 : 0;

			for (int i = 0; i < arguments.Count; i++)
			{
				var targetParamType = parameters[i + startIndex].ParameterType;
				var arg = arguments[i];

				// Special handling for lambda expressions
				if (arg is LambdaExpression lambdaExpr)
				{
					// Re-create the lambda with the correct delegate type
					if (targetParamType.IsSubclassOf(typeof(Delegate)) || targetParamType == typeof(Delegate))
					{
						// Validate parameter count matches
						var invokeMethod = targetParamType.GetMethod("Invoke");
						if (invokeMethod != null && invokeMethod.GetParameters().Length == lambdaExpr.Parameters.Count)
						{
							var lambdaBody = lambdaExpr.Body;

							// Check if return type needs conversion
							if (invokeMethod.ReturnType != lambdaBody.Type)
							{
								if (invokeMethod.ReturnType.IsAssignableFrom(lambdaBody.Type))
								{
									lambdaBody = Expression.Convert(lambdaBody, invokeMethod.ReturnType);
								}
								else if (IsNumericType(lambdaBody.Type) && IsNumericType(invokeMethod.ReturnType))
								{
									lambdaBody = Expression.Convert(lambdaBody, invokeMethod.ReturnType);
								}
							}

							convertedArgs.Add(Expression.Lambda(targetParamType, lambdaBody, lambdaExpr.Parameters));
						}
						else
						{
							// Can't convert - parameter count mismatch
							convertedArgs.Add(arg);
						}
					}
					else
					{
						convertedArgs.Add(arg);
					}
				}
				else if (arg.Type != targetParamType)
				{
					convertedArgs.Add(Expression.Convert(arg, targetParamType));
				}
				else
				{
					convertedArgs.Add(arg);
				}
			}

			return convertedArgs;
		}

		private MethodInfo? FindBestExtensionMethod(Type instanceType, string methodName, List<Expression> arguments)
		{
			// Look through all registered methods to find extension methods
			if (!_availableMethods.TryGetValue(methodName, out var methods))
			{
				return null;
			}

			List<MethodInfo> candidates = new();

			foreach (var method in methods)
			{
				if (!method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
				{
					continue;
				}

				var parameters = method.GetParameters();
				if (parameters.Length == 0)
				{
					continue;
				}

				// Check parameter count matches (including 'this' parameter)
				if (parameters.Length - 1 != arguments.Count)
				{
					continue;
				}

				// First parameter should be compatible with instance type
				var firstParamType = parameters[0].ParameterType;
				bool instanceMatches = false;

				// Handle generic parameter (like TSource in generic method definitions)
				if (firstParamType.IsGenericParameter)
				{
					// Can't check directly - will be resolved later
					instanceMatches = true;
				}
				else if (firstParamType.IsGenericType && instanceType.IsGenericType)
				{
					// Both are generic types - check if they're compatible
					var firstParamGenericDef = firstParamType.GetGenericTypeDefinition();
					var firstParamGenArgs = firstParamType.GetGenericArguments();

					// Check if instance type matches or implements the interface
					if (instanceType.GetGenericTypeDefinition() == firstParamGenericDef)
					{
						// Direct match - check generic arguments
						var instanceGenArgs = instanceType.GetGenericArguments();

						if (firstParamGenArgs.Length == instanceGenArgs.Length)
						{
							bool allMatch = true;
							for (int i = 0; i < firstParamGenArgs.Length; i++)
							{
								// Allow match if parameter is a generic parameter OR types match exactly
								if (!firstParamGenArgs[i].IsGenericParameter && firstParamGenArgs[i] != instanceGenArgs[i])
								{
									allMatch = false;
									break;
								}
							}
							instanceMatches = allMatch;
						}
					}

					if (!instanceMatches)
					{
						// Check interfaces
						foreach (var iface in instanceType.GetInterfaces())
						{
							if (iface.IsGenericType && iface.GetGenericTypeDefinition() == firstParamGenericDef)
							{
								// Check generic arguments match
								var ifaceGenArgs = iface.GetGenericArguments();

								if (firstParamGenArgs.Length == ifaceGenArgs.Length)
								{
									bool allMatch = true;
									for (int i = 0; i < firstParamGenArgs.Length; i++)
									{
										// Allow match if parameter is a generic parameter OR types match exactly
										if (!firstParamGenArgs[i].IsGenericParameter && firstParamGenArgs[i] != ifaceGenArgs[i])
										{
											allMatch = false;
											break;
										}
									}
									if (allMatch)
									{
										instanceMatches = true;
										break;
									}
								}
							}
						}
					}
				}
				else if (IsCompatibleType(instanceType, firstParamType))
				{
					instanceMatches = true;
				}

				if (!instanceMatches)
				{
					continue;
				}

				// Check if arguments are compatible - with special handling for lambdas
				bool argsMatch = true;
				for (int i = 0; i < arguments.Count; i++)
				{
					var arg = arguments[i];
					var paramType = parameters[i + 1].ParameterType;

					// For lambda expressions, check parameter count compatibility
					if (arg is LambdaExpression lambdaExpr)
					{
						// If paramType is a generic type like Func<T, bool> or Func<T, int, bool>
						if (paramType.IsGenericType && !paramType.IsGenericTypeDefinition)
						{
							var genDef = paramType.GetGenericTypeDefinition();
							// Check if it's a Func or Action
							if (genDef.Name.StartsWith("Func`") || genDef.Name.StartsWith("Action`"))
							{
								// The number after ` tells us the parameter count
								// Func<T, TResult> has 2 generic args (1 param + return type)
								// Func<T1, T2, TResult> has 3 generic args (2 params + return type)
								var genArgs = paramType.GetGenericArguments();
								int expectedParamCount = genDef.Name.StartsWith("Func`") ? genArgs.Length - 1 : genArgs.Length;

								if (expectedParamCount != lambdaExpr.Parameters.Count)
								{
									argsMatch = false;
									break;
								}
							}
						}
						else if (paramType.IsSubclassOf(typeof(Delegate)) || paramType == typeof(Delegate))
						{
							// Non-generic delegate or fully constructed delegate - check invoke method
							var invokeMethod = paramType.GetMethod("Invoke");
							if (invokeMethod != null)
							{
								var delegateParams = invokeMethod.GetParameters();
								if (delegateParams.Length != lambdaExpr.Parameters.Count)
								{
									argsMatch = false;
									break;
								}
							}
						}
						// If paramType is a generic parameter or delegate-ish, consider it compatible
						// Lambda is compatible with delegate types
						continue;
					}

					// For non-lambda arguments, check type compatibility only for non-generic types
					if (!paramType.IsGenericParameter && !IsCompatibleType(arg.Type, paramType))
					{
						argsMatch = false;
						break;
					}
				}

				if (argsMatch)
				{
					candidates.Add(method);
				}
			}

			// Return the first candidate (or we could rank them further)
			return candidates.FirstOrDefault();
		}

		private MethodInfo? FindExtensionMethod(Type instanceType, string methodName, Type[] argTypes)
		{
			// Look through all registered methods to find extension methods
			if (!_availableMethods.TryGetValue(methodName, out var methods))
			{
				return null;
			}

			foreach (var method in methods)
			{
				if (!method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
				{
					continue;
				}

				var parameters = method.GetParameters();
				if (parameters.Length == 0)
				{
					continue;
				}

				// First parameter should be compatible with instance type
				// For generic extension methods, we need special handling
				var firstParamType = parameters[0].ParameterType;
				bool instanceMatches = false;

				if (IsCompatibleType(instanceType, firstParamType))
				{
					instanceMatches = true;
				}
				else if (firstParamType.IsGenericType)
				{
					// Check if instance type implements the generic interface
					var genericTypeDef = firstParamType.GetGenericTypeDefinition();
					foreach (var iface in instanceType.GetInterfaces())
					{
						if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericTypeDef)
						{
							instanceMatches = true;
							break;
						}
					}
				}

				if (!instanceMatches)
				{
					continue;
				}

				// Check remaining parameters match argument types
				if (parameters.Length - 1 != argTypes.Length)
				{
					continue;
				}

				bool match = true;
				for (int i = 0; i < argTypes.Length; i++)
				{
					var paramType = parameters[i + 1].ParameterType;
					var argType = argTypes[i];

					// Special handling for lambda expressions matching delegate parameters
					if (argType.IsSubclassOf(typeof(System.Linq.Expressions.LambdaExpression)) ||
						argType == typeof(System.Linq.Expressions.LambdaExpression))
					{
						// Lambda can match any delegate type, but we need to check parameter count
						if (paramType.IsSubclassOf(typeof(Delegate)) || paramType == typeof(Delegate))
						{
							// This is a delegate parameter - lambda is potentially compatible
							// We'll validate the actual signature later when we have the concrete method
							continue;
						}
					}

					if (!IsCompatibleType(argType, paramType))
					{
						match = false;
						break;
					}
				}

				if (match)
				{
					return method;
				}
			}

			return null;
		}

		private Expression ParseFunctionCall(Expression function)
		{
			var arguments = new List<Expression>();

			if (Current.Type != TokenType.RightParen)
			{
				do
				{
					arguments.Add(ParseExpression());
				}
				while (Match(TokenType.Comma));
			}

			Expect(TokenType.RightParen);

			if (function is not ParameterExpression paramExpr ||
				!_availableMethods.TryGetValue(paramExpr.Name ?? "", out var methodInfos))
			{
				throw Error($"Function call not supported for expression type: {function.GetType().Name}");
			}

			// Find the best matching overload
			var argTypes = arguments.Select(a => a.Type).ToArray();
			var matchingMethod = FindBestMethodOverload(methodInfos, argTypes);

			if (matchingMethod == null)
			{
				throw Error(
					$"No matching overload found for method '{paramExpr.Name}' with argument types: {string.Join(", ", argTypes.Select(t => t.Name))}");
			}

			// Convert arguments if needed
			var convertedArgs = new List<Expression>();
			var parameters = matchingMethod.GetParameters();

			for (int i = 0; i < arguments.Count; i++)
			{
				if (arguments[i].Type != parameters[i].ParameterType)
				{
					convertedArgs.Add(Expression.Convert(arguments[i], parameters[i].ParameterType));
				}
				else
				{
					convertedArgs.Add(arguments[i]);
				}
			}

			return Expression.Call(null, matchingMethod, convertedArgs);
		}

		private Expression ParsePrimary()
		{
			// Handle 'new' keyword for object construction
			if (Match(TokenType.New))
			{
				return ParseNewExpression();
			}

			if (Match(TokenType.Number))
			{
				var value = _tokens[_position - 1].Value;

				// Numeric literal suffixes: 2f, 2F (float), 2d, 2D (double), 2m, 2M (decimal),
				// 2L (long), 2u (uint), 2ul / 2uL (ulong). The lexer emits these as a separate
				// Identifier token immediately after the number.
				if (Current.Type == TokenType.Identifier)
				{
					var suffix = Current.Value;
					switch (suffix)
					{
						case "f":
						case "F":
							Advance();
							return Expression.Constant(float.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
						case "d":
						case "D":
							Advance();
							return Expression.Constant(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
						case "m":
						case "M":
							Advance();
							return Expression.Constant(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
						case "l":
						case "L":
							Advance();
							return Expression.Constant(long.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
						case "u":
						case "U":
							Advance();
							return Expression.Constant(uint.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
						case "ul":
						case "uL":
						case "Ul":
						case "UL":
							Advance();
							return Expression.Constant(ulong.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
					}
				}

				if (value.Contains('.'))
				{
					return Expression.Constant(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
				}

				return Expression.Constant(int.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
			}

			if (Match(TokenType.String))
			{
				return Expression.Constant(_tokens[_position - 1].Value);
			}

			if (Match(TokenType.BooleanLiteral))
			{
				return Expression.Constant(_tokens[_position - 1].Value == "true", typeof(bool));
			}

			if (Match(TokenType.Identifier))
			{
				var nameToken = _tokens[_position - 1];
				var name = nameToken.Value;

				// Resolves the target of an assignment / increment. Guards the variable lookup so
				// assigning to an undeclared name reports the same friendly "Unknown identifier"
				// message used for reads, rather than throwing a raw KeyNotFoundException.
				Expression AssignTarget()
				{
					if (_variables.TryGetValue(name, out var target))
					{
						return target;
					}

					throw Error($"Unknown identifier '{name}'", nameToken);
				}

				// Check for lambda expression: identifier => expression
				if (Current.Type == TokenType.Arrow)
				{
					Match(TokenType.Arrow);

					// Create lambda parameter with inferred or default type
					var lambdaParam = Expression.Parameter(typeof(object), name);
					_lambdaParameters.Push(lambdaParam);
					_variables[name] = lambdaParam;

					var lambdaBody = ParseExpression();

					_lambdaParameters.Pop();
					_variables.Remove(name);

					return Expression.Lambda(lambdaBody, lambdaParam);
				}

				if (Current.Type == TokenType.LeftParen)
				{
					if (_localMethods.TryGetValue(name, out var localMethod))
					{
						Match(TokenType.LeftParen);
						var arguments = new List<Expression>();

						if (Current.Type != TokenType.RightParen)
						{
							do
							{
								arguments.Add(ParseExpression());
							}
							while (Match(TokenType.Comma));
						}

						Expect(TokenType.RightParen);

						var convertedLocalArgs = new List<Expression>();
						for (int i = 0; i < arguments.Count; i++)
						{
							var targetType = i < localMethod.paramTypes.Length ? localMethod.paramTypes[i] : arguments[i].Type;
							convertedLocalArgs.Add(arguments[i].Type != targetType
								? Expression.Convert(arguments[i], targetType)
								: arguments[i]);
						}

						return Expression.Invoke(Expression.Constant(localMethod.compiledMethod), convertedLocalArgs);
					}

					if (_availableMethods.TryGetValue(name, out var methodInfos))
					{
						Match(TokenType.LeftParen);
						var arguments = new List<Expression>();

						if (Current.Type != TokenType.RightParen)
						{
							do
							{
								arguments.Add(ParseExpression());
							}
							while (Match(TokenType.Comma));
						}

						Expect(TokenType.RightParen);

						// Find the best matching overload
						var argTypes = arguments.Select(a => a.Type).ToArray();
						var matchingMethod = FindBestMethodOverload(methodInfos, argTypes);

						if (matchingMethod == null)
						{
							throw Error(
								$"No matching overload found for method '{name}' with argument types: {string.Join(", ", argTypes.Select(t => t.Name))}",
								nameToken);
						}

						// Convert arguments if needed
						var convertedArgs = new List<Expression>();
						var parameters = matchingMethod.GetParameters();

						for (int i = 0; i < arguments.Count; i++)
						{
							if (arguments[i].Type != parameters[i].ParameterType)
							{
								convertedArgs.Add(Expression.Convert(arguments[i], parameters[i].ParameterType));
							}
							else
							{
								convertedArgs.Add(arguments[i]);
							}
						}

						return Expression.Call(null, matchingMethod, convertedArgs);
					}
				}

				if (Match(TokenType.Assign))
				{
					var valueToken = Current;
					var value = ParseExpression();
					var target = AssignTarget();
					return Expression.Assign(target, Coerce(value, target.Type, valueToken));
				}

				if (Match(TokenType.PlusAssign))
				{
					return BuildCompoundAssign(Expression.Add, AssignTarget(), ParseExpression());
				}

				if (Match(TokenType.MinusAssign))
				{
					return BuildCompoundAssign(Expression.Subtract, AssignTarget(), ParseExpression());
				}

				if (Match(TokenType.MultiplyAssign))
				{
					return BuildCompoundAssign(Expression.Multiply, AssignTarget(), ParseExpression());
				}

				if (Match(TokenType.DivideAssign))
				{
					return BuildCompoundAssign(Expression.Divide, AssignTarget(), ParseExpression());
				}

				if (Match(TokenType.Increment))
				{
					return Expression.PreIncrementAssign(AssignTarget());
				}

				if (Match(TokenType.Decrement))
				{
					return Expression.PreDecrementAssign(AssignTarget());
				}

				if (_variables.TryGetValue(name, out var variable))
				{
					return variable;
				}

				// Not a known variable. If followed by a dot, it might be a namespace/type prefix
				// (e.g. UnityEngine.Random.Range(...)). Try to resolve the dotted name as a Type;
				// if not yet a full type, return a "namespace sentinel" so ParseMemberAccess can
				// keep accumulating segments.
				if (Current.Type == TokenType.Dot)
				{
					var asType = TryResolveType(name);
					if (asType != null)
					{
						return Expression.Constant(asType, typeof(Type));
					}
					return UnresolvedNamespaceConstant(name, nameToken);
				}

				// Maybe a bare type reference (rare, but harmless to support).
				var bareType = TryResolveType(name);
				if (bareType != null)
				{
					return Expression.Constant(bareType, typeof(Type));
				}

				throw Error($"Unknown identifier '{name}'", nameToken);
			}

			if (Match(TokenType.LeftParen))
			{
				var expr = ParseExpression();
				Expect(TokenType.RightParen);
				return expr;
			}

			throw Error($"Unexpected token: {Current.Type}");
		}

		private Expression ParseNewExpression()
		{
			// Parse the type being constructed: a simple/aliased/qualified name, optionally with generic
			// arguments (`new List<int>(...)`, `new Dictionary<int, string>()`). Keep the leading token for
			// error positioning.
			var typeNameToken = Current;
			var type = ParseTypeReference();

			// Constructor arguments are optional when a collection/dictionary initializer follows, mirroring
			// C#'s `new List<int> { 1, 2, 3 }`. Otherwise an explicit constructor call is required.
			var arguments = new List<Expression>();
			if (Match(TokenType.LeftParen))
			{
				if (Current.Type != TokenType.RightParen)
				{
					do
					{
						arguments.Add(ParseExpression());
					}
					while (Match(TokenType.Comma));
				}

				Expect(TokenType.RightParen);
			}
			else if (Current.Type != TokenType.LeftBrace)
			{
				throw Error("Expected '(' or '{' after type in 'new' expression", typeNameToken);
			}

			// Find matching constructor
			var argTypes = arguments.Select(a => a.Type).ToArray();
			var constructor = type.GetConstructor(argTypes);

			if (constructor == null)
			{
				// Try to find compatible constructor
				foreach (var ctor in type.GetConstructors())
				{
					var parameters = ctor.GetParameters();
					if (parameters.Length != argTypes.Length)
					{
						continue;
					}

					bool compatible = true;
					for (int i = 0; i < argTypes.Length; i++)
					{
						if (!IsCompatibleType(argTypes[i], parameters[i].ParameterType))
						{
							compatible = false;
							break;
						}
					}

					if (compatible)
					{
						constructor = ctor;
						break;
					}
				}

				if (constructor == null)
				{
					throw Error($"No matching constructor found for type '{type.Name}' with argument types: {string.Join(", ", argTypes.Select(t => t.Name))}",
						typeNameToken);
				}
			}

			// Always convert arguments to match the resolved constructor's parameter types.
			var ctorParams = constructor.GetParameters();
			var finalArgs = new List<Expression>(arguments.Count);
			for (int i = 0; i < arguments.Count; i++)
			{
				if (arguments[i].Type != ctorParams[i].ParameterType)
				{
					finalArgs.Add(Expression.Convert(arguments[i], ctorParams[i].ParameterType));
				}
				else
				{
					finalArgs.Add(arguments[i]);
				}
			}

			var newExpr = Expression.New(constructor, finalArgs);

			// A trailing `{ ... }` is a collection / dictionary initializer over the just-constructed object.
			if (Current.Type == TokenType.LeftBrace)
			{
				return ParseCollectionInitializer(newExpr);
			}

			return newExpr;
		}

		// Parses a collection/dictionary initializer (caller verified the next token is '{'). Each entry is
		// desugared to an `Add` call: a bare element `{ x }` → `Add(x)` (List<T> etc.), a nested brace group
		// `{ k, v }` → `Add(k, v)` (Dictionary<K,V> etc.). Builds an Expression.ListInit so the whole
		// `new T { ... }` is a single value expression usable inline. An empty `{ }` is just the construction.
		private Expression ParseCollectionInitializer(NewExpression newExpr)
		{
			Expect(TokenType.LeftBrace);

			var initializers = new List<ElementInit>();
			while (Current.Type != TokenType.RightBrace && Current.Type != TokenType.EndOfFile)
			{
				var elementArgs = ParseInitializerElement();
				var argTypes = elementArgs.Select(a => a.Type).ToArray();

				var addMethod = FindAddMethod(newExpr.Type, argTypes)
					?? throw Error(
						$"No 'Add' method on type '{newExpr.Type.Name}' accepting argument types: " +
						$"{string.Join(", ", argTypes.Select(t => t.Name))}");

				var addParams = addMethod.GetParameters();
				var convertedArgs = new Expression[elementArgs.Count];
				for (int i = 0; i < elementArgs.Count; i++)
				{
					convertedArgs[i] = elementArgs[i].Type != addParams[i].ParameterType
						? Expression.Convert(elementArgs[i], addParams[i].ParameterType)
						: elementArgs[i];
				}

				initializers.Add(Expression.ElementInit(addMethod, convertedArgs));

				// Allow (and stop on) a trailing comma before the closing brace.
				if (!Match(TokenType.Comma))
				{
					break;
				}
			}

			Expect(TokenType.RightBrace);

			// Expression.ListInit requires at least one initializer; an empty initializer is just the `new`.
			return initializers.Count == 0 ? newExpr : Expression.ListInit(newExpr, initializers);
		}

		// Parses a single collection-initializer entry into the argument list for its `Add` call. A nested
		// brace group `{ a, b, ... }` (e.g. a dictionary key/value pair) yields multiple arguments; anything
		// else is a single-expression element.
		private List<Expression> ParseInitializerElement()
		{
			if (Match(TokenType.LeftBrace))
			{
				var args = new List<Expression> { ParseExpression() };
				while (Match(TokenType.Comma))
				{
					args.Add(ParseExpression());
				}

				Expect(TokenType.RightBrace);
				return args;
			}

			return new List<Expression> { ParseExpression() };
		}

		// Finds a public instance `Add` overload matching the given argument types — exact match preferred,
		// then a conversion-compatible one — mirroring FindIndexer's resolution. Used to desugar collection
		// and dictionary initializers.
		private MethodInfo? FindAddMethod(Type type, Type[] argTypes)
		{
			var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
				.Where(m => m.Name == "Add" && m.GetParameters().Length == argTypes.Length)
				.ToList();

			foreach (var method in candidates)
			{
				var parameters = method.GetParameters();
				if (!argTypes.Where((t, i) => parameters[i].ParameterType != t).Any())
				{
					return method;
				}
			}

			foreach (var method in candidates)
			{
				var parameters = method.GetParameters();
				if (Enumerable.Range(0, argTypes.Length).All(i => IsCompatibleType(argTypes[i], parameters[i].ParameterType)))
				{
					return method;
				}
			}

			return null;
		}

		// Parses a type reference in a `new` / generic-argument position: a primitive keyword (`int`,
		// `float`, …), or a simple/aliased/dotted identifier, optionally followed by generic arguments
		// (`List<int>`, `Dictionary<int, List<string>>`). Returns the resolved (closed) Type.
		private Type ParseTypeReference()
		{
			if (Match(TokenType.Int))
			{
				return typeof(int);
			}

			if (Match(TokenType.Float))
			{
				return typeof(float);
			}

			if (Match(TokenType.Double))
			{
				return typeof(double);
			}

			if (Match(TokenType.Bool))
			{
				return typeof(bool);
			}

			if (Match(TokenType.String_))
			{
				return typeof(string);
			}

			var nameToken = Expect(TokenType.Identifier);
			var typeName = nameToken.Value;

			// Nested types / namespace-qualified names with dots.
			while (Current.Type == TokenType.Dot)
			{
				Match(TokenType.Dot);
				typeName += "." + Expect(TokenType.Identifier).Value;
			}

			if (Match(TokenType.LessThan))
			{
				var typeArgs = new List<Type> { ParseTypeReference() };
				while (Match(TokenType.Comma))
				{
					typeArgs.Add(ParseTypeReference());
				}

				Expect(TokenType.GreaterThan);
				return ResolveGenericType(typeName, typeArgs.ToArray(), nameToken);
			}

			return TryResolveType(typeName)
				?? throw Error(
					$"Type '{typeName}' not found. Make sure to register custom types or use fully qualified names.",
					nameToken);
		}

		// Resolves a closed generic type from a name and its type arguments, e.g. ("List", [int]) →
		// List<int>. Looks up the open generic definition by arity, then constructs it with the arguments.
		private Type ResolveGenericType(string typeName, Type[] typeArgs, Token at)
		{
			var definition = ResolveGenericTypeDefinition(typeName, typeArgs.Length)
				?? throw Error(
					$"Generic type '{typeName}' with {typeArgs.Length} type argument(s) not found. " +
					"Make sure to register custom types or use fully qualified names.",
					at);

			try
			{
				return definition.MakeGenericType(typeArgs);
			}
			catch (Exception e)
			{
				throw Error($"Cannot construct '{typeName}' with the given type arguments: {e.Message}", at);
			}
		}

		// Finds the open generic type definition (e.g. List`1, Dictionary`2) for a simple/dotted name and
		// arity. Tries registered types and direct resolution first, then the System.Collections.Generic
		// namespace (covering List/Dictionary/HashSet/Queue/Stack/…), then a full assembly scan as a
		// last resort. Returns null if nothing matches.
		private Type? ResolveGenericTypeDefinition(string typeName, int arity)
		{
			var mangled = typeName + "`" + arity;

			if (_registeredTypes.TryGetValue(mangled, out var registered))
			{
				return registered;
			}

			var direct = TryResolveType(mangled) ?? Type.GetType(mangled);
			if (direct != null)
			{
				return direct;
			}

			if (!typeName.Contains('.'))
			{
				var sys = Type.GetType("System.Collections.Generic." + mangled);
				if (sys != null)
				{
					return sys;
				}
			}

			var simpleName = typeName.Contains('.') ? typeName[(typeName.LastIndexOf('.') + 1)..] : typeName;
			var simpleMangled = simpleName + "`" + arity;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;
				try
				{
					types = assembly.GetTypes();
				}
				catch
				{
					// Some assemblies fail to fully load their types under reflection; skip them.
					continue;
				}

				var match = types.FirstOrDefault(t => t.IsGenericTypeDefinition && t.Name == simpleMangled);
				if (match != null)
				{
					return match;
				}
			}

			return null;
		}

		private Type? TryResolveType(string typeName)
		{
			if (_registeredTypes.TryGetValue(typeName, out var type))
			{
				return type;
			}

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				var t = assembly.GetType(typeName);
				if (t != null)
				{
					return t;
				}
			}

			return null;
		}

		private Expression ParseStaticMemberAccess(Type targetType, string memberName)
		{
			// Static method call
			if (Current.Type == TokenType.LeftParen)
			{
				Match(TokenType.LeftParen);

				var arguments = new List<Expression>();
				if (Current.Type != TokenType.RightParen)
				{
					do
					{
						arguments.Add(ParseExpression());
					}
					while (Match(TokenType.Comma));
				}
				Expect(TokenType.RightParen);

				var argTypes = arguments.Select(a => a.Type).ToArray();

				// Try exact-signature lookup first
				var method = targetType.GetMethod(
					memberName,
					BindingFlags.Public | BindingFlags.Static,
					null,
					argTypes,
					null);

				// Fall back to overload resolution by name + arg count
				if (method == null)
				{
					var candidates = targetType
						.GetMethods(BindingFlags.Public | BindingFlags.Static)
						.Where(m => m.Name == memberName)
						.ToList();

					method = FindBestMethodOverload(candidates, argTypes);
				}

				if (method == null)
				{
					throw Error(
						$"No matching static method '{memberName}' on type '{targetType.FullName}' " +
						$"with argument types: {string.Join(", ", argTypes.Select(t => t.Name))}");
				}

				var parameters = method.GetParameters();
				var convertedArgs = new List<Expression>();
				for (int i = 0; i < arguments.Count; i++)
				{
					if (arguments[i].Type != parameters[i].ParameterType)
					{
						convertedArgs.Add(Expression.Convert(arguments[i], parameters[i].ParameterType));
					}
					else
					{
						convertedArgs.Add(arguments[i]);
					}
				}

				return Expression.Call(null, method, convertedArgs);
			}

			// Static property / field access (and possible assignment)
			var property = targetType.GetProperty(memberName, BindingFlags.Public | BindingFlags.Static);
			if (property != null)
			{
				if (Match(TokenType.Assign))
				{
					var valueToken = Current;
					var value = ParseExpression();
					var staticProperty = Expression.Property(null, property);
					return Expression.Assign(staticProperty, Coerce(value, staticProperty.Type, valueToken));
				}
				return Expression.Property(null, property);
			}

			var field = targetType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
			if (field != null)
			{
				if (Match(TokenType.Assign))
				{
					var valueToken = Current;
					var value = ParseExpression();
					var staticField = Expression.Field(null, field);
					return Expression.Assign(staticField, Coerce(value, staticField.Type, valueToken));
				}
				return Expression.Field(null, field);
			}

			// Could be a nested type: Foo.Bar where Bar is a nested type of Foo.
			var nested = targetType.GetNestedType(memberName, BindingFlags.Public);
			if (nested != null)
			{
				return Expression.Constant(nested, typeof(Type));
			}

			throw Error($"Static member '{memberName}' not found on type '{targetType.FullName}'");
		}

		private Expression ParseLambdaWithType(Type? parameterType)
		{
			// Parse: identifier => expression
			var paramName = Expect(TokenType.Identifier).Value;
			Expect(TokenType.Arrow);

			// Use inferred type or default to object
			var actualType = parameterType ?? typeof(object);
			var lambdaParam = Expression.Parameter(actualType, paramName);

			_lambdaParameters.Push(lambdaParam);
			_variables[paramName] = lambdaParam;

			var lambdaBody = ParseExpression();

			_lambdaParameters.Pop();
			_variables.Remove(paramName);

			return Expression.Lambda(lambdaBody, lambdaParam);
		}

		private Type? GetEnumerableElementType(Type type)
		{
			// Check if it's IEnumerable<T>
			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
			{
				return type.GetGenericArguments()[0];
			}

			// Check if it implements IEnumerable<T>
			foreach (var interfaceType in type.GetInterfaces())
			{
				if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				{
					return interfaceType.GetGenericArguments()[0];
				}
			}

			return null;
		}
	}
}
