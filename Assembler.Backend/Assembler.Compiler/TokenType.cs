public enum TokenType
{
	Identifier, Number, String, Void,
	If, Else, For, While, Foreach, Switch, Case, Default, Return, Break, Continue,
	Var, Int, String_, Bool, Float, Double, New,
	Plus, Minus, Multiply, Divide, Modulo,
	Equal, NotEqual, LessThan, GreaterThan, LessThanOrEqual, GreaterThanOrEqual,
	And, Or, Not,
	Assign, Semicolon, Comma, Dot,
	LeftParen, RightParen, LeftBrace, RightBrace, LeftBracket, RightBracket,
	Question, Colon, Arrow,
	PlusAssign, MinusAssign, MultiplyAssign, DivideAssign, Increment, Decrement,
	EndOfFile
}