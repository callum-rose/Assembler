using YamlDotNet.Serialization;

namespace Assembler.Definitions;

/// <summary>
/// The overall configuration for the game.
/// </summary>
public class GameConfigurationDef
{
	[YamlMember(Alias = "game")]
	public GameInfoDef? Game { get; set; }

	[YamlMember(Alias = "world")]
	public WorldDef? World { get; set; }

	[YamlMember(Alias = "physics")]
	public PhysicsDef? Physics { get; set; }

	[YamlMember(Alias = "constants")]
	public List<ConstantDef>? Constants { get; set; }

	[YamlMember(Alias = "variables")]
	public List<VariableDef>? Variables { get; set; }

	[YamlMember(Alias = "expressions")]
	public List<ExpressionDef>? Expressions { get; set; }

	[YamlMember(Alias = "entities")]
	public List<EntityDef>? Entities { get; set; }
}