// using Assembler.Parsing.Phase1;
// using Assembler.Parsing.Phase1.Dtos;
// using Assembler.Parsing2;
// using Assembler.Parsing2.Info;
//
// namespace Assembler.Parsing.Tests;
//
// public class Tests
// {
// 	[Test]
// 	public void Test1()
// 	{
// 		var yaml = File.ReadAllText("Pong.yaml");
// 		var gameConfiguration = new GameFileParser().Parse(yaml);
// 		Assert.IsNotNull(gameConfiguration);
// 	}
// 	
// 	[Test]
// 	public void Test2()
// 	{
// 		var vecDto = new VecDto { X = "1", Y = new VarRefDto { Id = "test variable" } };
//
// 		var testVariable = new VariableInfo("test variable", 5.0f);
//
// 		var vector2 = vecDto.ToVector2([testVariable]);
//
// 		Assert.That(vector2, Is.EqualTo(new Vector2(1, 5)));
// 	}
//
// 	[Test]
// 	public void Test3()
// 	{
// 		var yaml = File.ReadAllText("Pong.yaml");
// 		var gameDto = new GameFileParser().Parse(yaml);
//
// 		var game = Transformer.Transform(gameDto);
// 		
// 		Assert.IsNotNull(game);
// 	}
// }