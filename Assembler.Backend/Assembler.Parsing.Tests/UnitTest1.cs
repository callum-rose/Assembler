namespace Assembler.Parsing.Tests;

public class Tests
{
	[Test]
	public void Test1()
	{
		var yaml = File.ReadAllText("Pong.yaml");
		var gameConfiguration = Deserialiser.Deserialize(yaml);
		Assert.IsNotNull(gameConfiguration);
	}
}