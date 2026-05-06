using YamlDotNet.Serialization;

namespace Assembler.Definitions;

/// <summary>
/// Base type for all behaviour-specific property definitions.
/// Each behaviour type has its own sealed record subclass with required/optional properties.
/// </summary>
public abstract record BehaviourProperties;

/// <summary>Box collider with optional size and trigger flag.</summary>
public sealed record BoxColliderProperties(
	[property: YamlMember(Alias = "size")] ValueOrReference? Size = null,
	[property: YamlMember(Alias = "is trigger")] ValueOrReference? IsTrigger = null
) : BehaviourProperties;

/// <summary>Sphere collider with optional size.</summary>
public sealed record SphereColliderProperties(
	[property: YamlMember(Alias = "size")] ValueOrReference? Size = null
) : BehaviourProperties;

/// <summary>Key hold trigger that listens for key input.</summary>
public sealed record KeyHoldTriggerProperties(
	[property: YamlMember(Alias = "key")] ValueOrReference Key,
	[property: YamlMember(Alias = "listeners")] IReadOnlyList<ListenerDef>? Listeners = null
) : BehaviourProperties;

/// <summary>Translate behaviour that moves an entity by a displacement.</summary>
public sealed record TranslateProperties(
	[property: YamlMember(Alias = "displacement")] ValueOrReference Displacement
) : BehaviourProperties;

/// <summary>Rigidbody physics component settings.</summary>
public sealed record RigidbodyProperties(
	[property: YamlMember(Alias = "use gravity")] ValueOrReference? UseGravity = null
) : BehaviourProperties;

/// <summary>Velocity component that applies velocity to an entity.</summary>
public sealed record VelocityProperties(
	[property: YamlMember(Alias = "velocity variable ref")] string? VelocityVariableRef = null
) : BehaviourProperties;

/// <summary>Collision enter trigger that fires on collision start.</summary>
public sealed record CollisionEnterTriggerProperties(
	[property: YamlMember(Alias = "tags to detect")] List<string>? TagsToDetect = null,
	[property: YamlMember(Alias = "listeners")] IReadOnlyList<ListenerDef>? Listeners = null
) : BehaviourProperties;

/// <summary>Trigger enter trigger that fires when trigger is entered.</summary>
public sealed record TriggerEnterTriggerProperties(
	[property: YamlMember(Alias = "tags to detect")] List<string>? TagsToDetect = null,
	[property: YamlMember(Alias = "listeners")] IReadOnlyList<ListenerDef>? Listeners = null
) : BehaviourProperties;

/// <summary>Condition trigger that fires when an expression evaluates to true.</summary>
public sealed record ConditionTriggerProperties(
	[property: YamlMember(Alias = "condition expression ref")] string? ConditionExpressionRef = null
) : BehaviourProperties;

/// <summary>Vector variable setter that assigns an expression result to a vector variable.</summary>
public sealed record VectorVariableSetterProperties(
	[property: YamlMember(Alias = "variable to set ref")] string? VariableToSetRef = null,
	[property: YamlMember(Alias = "expression ref")] string? ExpressionRef = null
) : BehaviourProperties;

/// <summary>Int variable setter that assigns an expression result to an int variable.</summary>
public sealed record IntVariableSetterProperties(
	[property: YamlMember(Alias = "variable to set ref")] string? VariableToSetRef = null,
	[property: YamlMember(Alias = "expression ref")] string? ExpressionRef = null
) : BehaviourProperties;

/// <summary>Camera component with view type and size.</summary>
public sealed record CameraProperties(
	[property: YamlMember(Alias = "view")] ValueOrReference? View = null,
	[property: YamlMember(Alias = "size")] ValueOrReference? Size = null
) : BehaviourProperties;

/// <summary>Empty properties for behaviours without required configuration.</summary>
public sealed record EmptyBehaviourProperties : BehaviourProperties;

