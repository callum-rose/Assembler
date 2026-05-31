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

		private static Expression BuildBinary(Func<Expression, Expression, Expression> factory, Expression left, Expression right)
		{
			PromoteNumericOperands(ref left, ref right);
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
				throw new Exception($"Expected {type} but got {Current.Type} at line {Current.Line}");
			}

			var token = Current;
			Advance();
			return token;
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
				statements[^1] = Expression.Return(_returnLabel, last, returnType);
			}

			statements.Add(Expression.Label(_returnLabel, Expression.Default(returnType)));

			if (_declaredVariables.Count > 0)
			{
				return Expression.Block(returnType, _declaredVariables, statements);
			}

			return Expression.Block(returnType, statements);
		}

		/// <summary>
		/// Preprocesses early returns by wrapping the subsequent code in an else block.
		/// I couldn't get the compiler to support early returns without this.
		/// </summary>
		private void PreprocessEarlyReturns()
		{
			// Transform: if (cond) { return x; } <more code>
			// Into: if (cond) { return x; } else { <more code> }

			int i = 0;

			while (i < _tokens.Count)
			{
				// Look for: if (...) { ... return ...; }
				if (_tokens[i].Type == TokenType.If)
				{
					int ifStart = i;

					// Skip to the condition
					i++;// Skip 'if'

					if (i >= _tokens.Count || _tokens[i].Type != TokenType.LeftParen)
					{
						i++;
						continue;
					}

					// Skip the condition (find matching right paren)
					int parenDepth = 0;

					while (i < _tokens.Count)
					{
						if (_tokens[i].Type == TokenType.LeftParen)
						{
							parenDepth++;
						}

						if (_tokens[i].Type == TokenType.RightParen)
						{
							parenDepth--;

							if (parenDepth == 0)
							{
								i++;
								break;
							}
						}

						i++;
					}

					if (i >= _tokens.Count || _tokens[i].Type != TokenType.LeftBrace)
					{
						continue;
					}

					// Found the if block, check if it contains a return
					int braceDepth = 0;
					bool hasReturn = false;

					while (i < _tokens.Count)
					{
						if (_tokens[i].Type == TokenType.LeftBrace)
						{
							braceDepth++;
						}

						if (_tokens[i].Type == TokenType.RightBrace)
						{
							braceDepth--;

							if (braceDepth == 0)
							{
								i++;
								break;
							}
						}

						if (_tokens[i].Type == TokenType.Return && braceDepth == 1)
						{
							hasReturn = true;
						}

						i++;
					}

					// Check if there's an else
					if (i < _tokens.Count && _tokens[i].Type == TokenType.Else)
					{
						continue;// Already has else, skip
					}

					// Check if there's more code after this if block
					bool hasMoreCode = false;
					int codeStart = i;

					while (i < _tokens.Count)
					{
						if (_tokens[i].Type != TokenType.EndOfFile)
						{
							hasMoreCode = true;
							break;
						}

						i++;
					}

					// If it has a return and more code, wrap the remaining code in an else block
					if (hasReturn && hasMoreCode)
					{
						// Collect all remaining tokens (except EOF)
						var remainingTokens = new List<Token>();

						for (int j = codeStart; j < _tokens.Count; j++)
						{
							if (_tokens[j].Type != TokenType.EndOfFile)
							{
								remainingTokens.Add(_tokens[j]);
							}
						}

						// Remove them from the token list
						_tokens.RemoveRange(codeStart, _tokens.Count - codeStart);

						// Get line/column from previous token for synthetic tokens
						int line = _tokens[codeStart - 1].Line;
						int column = _tokens[codeStart - 1].Column;

						// Insert: else {
						_tokens.Insert(codeStart, new Token(TokenType.Else, "else", line, column));
						_tokens.Insert(codeStart + 1, new Token(TokenType.LeftBrace, "{", line, column));

						// Add remaining tokens
						_tokens.InsertRange(codeStart + 2, remainingTokens);

						// Add closing brace and EOF
						_tokens.Add(new Token(TokenType.RightBrace, "}", line, column));
						_tokens.Add(new Token(TokenType.EndOfFile, "", line, column));

						// Don't continue processing this if, move to next
						break;
					}

					i = ifStart + 1;// Continue from after the if
				}
				else
				{
					i++;
				}
			}
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
					var paramTypeToken = Current;
					Advance();
					var paramType = GetTypeFromToken(paramTypeToken.Type);
					var paramName = Expect(TokenType.Identifier).Value;
					parameters.Add((paramType, paramName));
				}
				while (Match(TokenType.Comma));
			}

			Expect(TokenType.RightParen);

			// Capture method body tokens
			Expect(TokenType.LeftBrace);
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

			return (name, returnType, parameters, bodyTokens);
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

			var delegateType = GetDelegateType(returnType, parameters.Select(p => p.type).ToArray());
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

		private MethodInfo MakeConcreteGenericMethod(MethodInfo genericMethod, Type instanceType, Type[] argTypes)
		{
			// This method attempts to resolve generic type parameters for extension methods
			// Common case: List<int>.Where(x => ...) where Where is defined as Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
			// Complex case: List<int>.Select(x => x * 2) where Select is defined as Select<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult> selector)
		
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
		
			// Try to infer from lambda return types
			// We need to look at the actual argument expressions to check if they're lambdas
			// Unfortunately we only have argTypes here, but lambdas show up as LambdaExpression type
			// We'll need to pass the actual arguments, not just types
			// For now, use the lambdas we already parsed if argTypes contains LambdaExpression
		
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

		private Type GetDelegateType(Type returnType, Type[] parameterTypes)
		{
			if (returnType == typeof(void))
			{
				return parameterTypes.Length switch
				{
					0 => typeof(Action),
					1 => typeof(Action<>).MakeGenericType(parameterTypes),
					2 => typeof(Action<,>).MakeGenericType(parameterTypes),
					3 => typeof(Action<,,>).MakeGenericType(parameterTypes),
					_ => throw new NotSupportedException()
				};
			}

			var allTypes = parameterTypes.Append(returnType).ToArray();
			return allTypes.Length switch
			{
				1 => typeof(Func<>).MakeGenericType(allTypes),
				2 => typeof(Func<,>).MakeGenericType(allTypes),
				3 => typeof(Func<,,>).MakeGenericType(allTypes),
				4 => typeof(Func<,,,>).MakeGenericType(allTypes),
				_ => throw new NotSupportedException()
			};
		}

		private Expression ParseStatement()
		{
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
			Expect(TokenType.If);
			Expect(TokenType.LeftParen);
			var condition = ParseExpression();
			Expect(TokenType.RightParen);

			var ifTrue = ParseStatementOrBlock();

			if (Match(TokenType.Else))
			{
				var ifFalse = ParseStatementOrBlock();

				// Use Condition for value-returning branches
				if (ifTrue.Type != typeof(void) && ifFalse.Type != typeof(void))
				{
					return Expression.Condition(condition, ifTrue, ifFalse);
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
			var condition = ParseExpression();
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
			var condition = ParseExpression();
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
			Expect(TokenType.Return);

			if (_returnLabel == null)
			{
				throw new Exception("'return' is only valid inside a method body.");
			}

			if (Match(TokenType.Semicolon))
			{
				return Expression.Return(_returnLabel);
			}

			var value = ParseExpression();
			Expect(TokenType.Semicolon);

			return Expression.Return(_returnLabel, value, _returnType);
		}

		private Expression ParseBreak()
		{
			Expect(TokenType.Break);
			Expect(TokenType.Semicolon);

			if (_breakLabels.Count == 0)
			{
				throw new Exception("Break statement outside of loop");
			}

			return Expression.Break(_breakLabels.Peek());
		}

		private Expression ParseContinue()
		{
			Expect(TokenType.Continue);
			Expect(TokenType.Semicolon);

			if (_continueLabels.Count == 0)
			{
				throw new Exception("Continue statement outside of loop");
			}

			return Expression.Continue(_continueLabels.Peek());
		}

		private Expression ParseVariableDeclaration()
		{
			var typeToken = Current;
			Advance();

			var type = GetTypeFromToken(typeToken.Type);

			var name = Expect(TokenType.Identifier).Value;

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
				// Convert initializer if needed
				if (initializer.Type != type)
				{
					initializer = Expression.Convert(initializer, type);
				}
				return Expression.Assign(variable, initializer);
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

			if (Match(TokenType.Question))
			{
				var trueExpr = ParseExpression();
				Expect(TokenType.Colon);
				var falseExpr = ParseExpression();
				return Expression.Condition(expr, trueExpr, falseExpr);
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
			var left = ParseEquality();

			while (Match(TokenType.And))
			{
				var right = ParseEquality();
				left = Expression.AndAlso(left, right);
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
					expr = ParseFunctionCall(expr);
				}
				else if (Match(TokenType.Dot))
				{
					expr = ParseMemberAccess(expr);
				}
				else
				{
					break;
				}
			}

			return expr;
		}

		private Expression ParseMemberAccess(Expression instance)
		{
			var memberName = Expect(TokenType.Identifier).Value;

			// Detect "type sentinel" used for static member access (e.g. UnityEngine.Random.Range(...)).
			// ParsePrimary returns Expression.Constant(type, typeof(Type)) when an identifier resolves to a Type.
			Type? staticTargetType = null;
			if (instance is ConstantExpression ce && ce.Type == typeof(Type) && ce.Value is Type tt)
			{
				staticTargetType = tt;
			}

			// Handle nested type / namespace continuation (e.g. UnityEngine.Random where UnityEngine alone
			// isn't a Type yet). If we have a "namespace sentinel", the previous step stored the partial
			// dotted name in _pendingNamespacePrefix; try to resolve again with the new segment.
			if (staticTargetType == null && instance is ConstantExpression ns && ns.Type == typeof(string) &&
			    ns.Value is string nsPrefix && nsPrefix.StartsWith("__ns:"))
			{
				var combined = nsPrefix.Substring(5) + "." + memberName;
				var resolved = TryResolveType(combined);
				if (resolved != null)
				{
					return Expression.Constant(resolved, typeof(Type));
				}
				// Still not a full type - keep accumulating.
				return Expression.Constant("__ns:" + combined, typeof(string));
			}

			// Static member access on a resolved Type.
			if (staticTargetType != null)
			{
				return ParseStaticMemberAccess(staticTargetType, memberName);
			}

			// Check if it's a method call
			if (Current.Type == TokenType.LeftParen)
			{
				Match(TokenType.LeftParen);
			
				// Find potential methods first to provide type context for lambdas
				MethodInfo? candidateInstanceMethod = null;
				MethodInfo? candidateExtensionMethod = null;
			
				// Try instance method first
				var instanceMethods = instance.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
					.Where(m => m.Name == memberName).ToList();
			
				// Try extension methods
				List<MethodInfo> extensionMethods = new();
				if (_availableMethods.TryGetValue(memberName, out var registeredMethods))
				{
					extensionMethods = registeredMethods
						.Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false))
						.Where(m => m.GetParameters().Length > 0)
						.ToList();
				}
			
				var arguments = new List<Expression>();

				if (Current.Type != TokenType.RightParen)
				{
					do
					{
						// Check if this is a lambda expression
						if (Current.Type == TokenType.Identifier && PeekNextToken()?.Type == TokenType.Arrow)
						{
							// This is a lambda - try to infer its type from method signature
							Type? lambdaParamType = null;
							int argIndex = arguments.Count;
						
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
											lambdaParamType = elementType;
											break;
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
														lambdaParamType = elementType;
														break;
													}
												}
												else
												{
													lambdaParamType = genArgs[0];
													break;
												}
											}
										}
									}
								}
							}
						
							// Try instance methods if no extension method matched
							if (lambdaParamType == null)
							{
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
													lambdaParamType = genArgs[0];
													break;
												}
											}
										}
									}
								}
							}
						
							arguments.Add(ParseLambdaWithType(lambdaParamType));
						}
						else
						{
							arguments.Add(ParseExpression());
						}
					}
					while (Match(TokenType.Comma));
				}

				Expect(TokenType.RightParen);

				// Try to find instance method
				var argTypes = arguments.Select(a => a.Type).ToArray();
				var method = instance.Type.GetMethod(memberName, BindingFlags.Public | BindingFlags.Instance, null, argTypes, null);
			
				bool isExtensionMethod = false;
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
						throw new Exception($"Method '{memberName}' not found on type '{instance.Type.Name}'");
					}
				}

				// Convert arguments if needed (NOW that we have the concrete method)
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

				// For extension methods, add instance as first argument
				if (isExtensionMethod)
				{
					return Expression.Call(null, method, new[] { instance }.Concat(convertedArgs));
				}
			
				return Expression.Call(instance, method, convertedArgs);
			}

			// Check for assignment to member
			if (Match(TokenType.Assign))
			{
				var value = ParseExpression();
				var member = Expression.PropertyOrField(instance, memberName);
				return Expression.Assign(member, value);
			}

			// Property or field access
			return Expression.PropertyOrField(instance, memberName);
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
				throw new Exception($"Function call not supported for expression type: {function.GetType().Name}");
			}

			// Find the best matching overload
			var argTypes = arguments.Select(a => a.Type).ToArray();
			var matchingMethod = FindBestMethodOverload(methodInfos, argTypes);

			if (matchingMethod == null)
			{
				throw new Exception(
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
				var name = _tokens[_position - 1].Value;

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
						return Expression.Invoke(Expression.Constant(localMethod.compiledMethod), arguments);
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
							throw new Exception(
								$"No matching overload found for method '{name}' with argument types: {string.Join(", ", argTypes.Select(t => t.Name))}");
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
					var value = ParseExpression();
					return Expression.Assign(_variables[name], value);
				}

				if (Match(TokenType.PlusAssign))
				{
					return BuildCompoundAssign(Expression.Add, _variables[name], ParseExpression());
				}

				if (Match(TokenType.MinusAssign))
				{
					return BuildCompoundAssign(Expression.Subtract, _variables[name], ParseExpression());
				}

				if (Match(TokenType.MultiplyAssign))
				{
					return BuildCompoundAssign(Expression.Multiply, _variables[name], ParseExpression());
				}

				if (Match(TokenType.DivideAssign))
				{
					return BuildCompoundAssign(Expression.Divide, _variables[name], ParseExpression());
				}

				if (Match(TokenType.Increment))
				{
					return Expression.PreIncrementAssign(_variables[name]);
				}

				if (Match(TokenType.Decrement))
				{
					return Expression.PreDecrementAssign(_variables[name]);
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
					return Expression.Constant("__ns:" + name, typeof(string));
				}

				// Maybe a bare type reference (rare, but harmless to support).
				var bareType = TryResolveType(name);
				if (bareType != null)
				{
					return Expression.Constant(bareType, typeof(Type));
				}

				throw new Exception($"Unknown identifier '{name}' at line {Current.Line}");
			}

			if (Match(TokenType.LeftParen))
			{
				var expr = ParseExpression();
				Expect(TokenType.RightParen);
				return expr;
			}

			throw new Exception($"Unexpected token: {Current.Type} at line {Current.Line}");
		}

		private Expression ParseNewExpression()
		{
			// Parse type name (could be simple name, alias, or fully qualified)
			var typeName = Expect(TokenType.Identifier).Value;
		
			// Handle generic types or nested types with dots
			while (Current.Type == TokenType.Dot)
			{
				Match(TokenType.Dot);
				typeName += "." + Expect(TokenType.Identifier).Value;
			}

			// Try to resolve the type
			Type? type = TryResolveType(typeName);

			if (type == null)
			{
				throw new Exception($"Type '{typeName}' not found. Make sure to register custom types or use fully qualified names.");
			}

			// Expect constructor call
			Expect(TokenType.LeftParen);
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
					throw new Exception($"No matching constructor found for type '{typeName}' with argument types: {string.Join(", ", argTypes.Select(t => t.Name))}");
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

			return Expression.New(constructor, finalArgs);
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
					throw new Exception(
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
					var value = ParseExpression();
					return Expression.Assign(Expression.Property(null, property), value);
				}
				return Expression.Property(null, property);
			}

			var field = targetType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
			if (field != null)
			{
				if (Match(TokenType.Assign))
				{
					var value = ParseExpression();
					return Expression.Assign(Expression.Field(null, field), value);
				}
				return Expression.Field(null, field);
			}

			// Could be a nested type: Foo.Bar where Bar is a nested type of Foo.
			var nested = targetType.GetNestedType(memberName, BindingFlags.Public);
			if (nested != null)
			{
				return Expression.Constant(nested, typeof(Type));
			}

			throw new Exception($"Static member '{memberName}' not found on type '{targetType.FullName}'");
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