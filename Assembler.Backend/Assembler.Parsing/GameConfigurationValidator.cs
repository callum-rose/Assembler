using Assembler.Definitions;

namespace Assembler.Parsing;

/// <summary>
/// Validates a GameConfigurationDef to ensure all references are valid.
/// Provides detailed error messages to help locate issues in the YAML.
/// </summary>
public class GameConfigurationValidator
{
	private readonly GameConfigurationDef _config;
	private readonly List<string> _errors = new();

	public GameConfigurationValidator(GameConfigurationDef config)
	{
		_config = config;
	}

	/// <summary>
	/// Validates the entire configuration and returns error messages.
	/// Returns empty list if validation passes.
	/// </summary>
	public List<string> Validate()
	{
		_errors.Clear();

		ValidateConstants();
		ValidateVariables();
		ValidateExpressions();
		ValidateEntities();

		return _errors;
	}

	/// <summary>
	/// Validates that all constants are well-formed.
	/// </summary>
	private void ValidateConstants()
	{
		if (_config.Constants is null) return;

		var constantIds = new HashSet<string>();
		foreach (var constant in _config.Constants)
		{
			if (string.IsNullOrEmpty(constant.Id))
			{
				_errors.Add("Constant with empty 'id' found");
			}
			else if (!constantIds.Add(constant.Id))
			{
				_errors.Add($"Duplicate constant ID: '{constant.Id}'");
			}

			if (constant.Value is null)
			{
				_errors.Add($"Constant '{constant.Id}' has null value");
			}
		}
	}

	/// <summary>
	/// Validates that variable initial values reference valid constants or other variables.
	/// </summary>
	private void ValidateVariables()
	{
		if (_config.Variables is null) return;

		var constantIds = GetConstantIds();
		var variableIds = new HashSet<string>();
		var orderedVariables = _config.Variables.ToList();

		foreach (var variable in orderedVariables)
		{
			if (string.IsNullOrEmpty(variable.Id))
			{
				_errors.Add("Variable with empty 'id' found");
				continue;
			}

			if (!variableIds.Add(variable.Id))
			{
				_errors.Add($"Duplicate variable ID: '{variable.Id}'");
			}

			if (variable.InitialValue is Reference reference)
			{
				var refName = reference.Ref;
				if (!constantIds.Contains(refName) && !variableIds.Contains(refName))
				{
					_errors.Add($"Variable '{variable.Id}' references unknown constant or variable: '{refName}'");
				}
			}
		}
	}

	/// <summary>
	/// Validates that all expressions reference valid arguments and have valid types.
	/// </summary>
	private void ValidateExpressions()
	{
		if (_config.Expressions is null) return;

		var constantIds = GetConstantIds();
		var variableIds = GetVariableIds();
		var expressionIds = new HashSet<string>();

		foreach (var expression in _config.Expressions)
		{
			if (string.IsNullOrEmpty(expression.Id))
			{
				_errors.Add("Expression with empty 'id' found");
				continue;
			}

			if (!expressionIds.Add(expression.Id))
			{
				_errors.Add($"Duplicate expression ID: '{expression.Id}'");
			}

			if (string.IsNullOrEmpty(expression.Type))
			{
				_errors.Add($"Expression '{expression.Id}' has empty 'type'");
			}

			if (string.IsNullOrEmpty(expression.Expression))
			{
				_errors.Add($"Expression '{expression.Id}' has empty expression string");
			}

			if (expression.Arguments is not null)
			{
				foreach (var arg in expression.Arguments)
				{
					if (arg is Reference reference)
					{
						var refName = reference.Ref;
						if (!constantIds.Contains(refName) && !variableIds.Contains(refName))
						{
							_errors.Add($"Expression '{expression.Id}' references unknown constant or variable: '{refName}'");
						}
					}
				}
			}
		}
	}

	/// <summary>
	/// Validates entity references and behaviour integrity.
	/// </summary>
	private void ValidateEntities()
	{
		if (_config.Entities is null) return;

		var constantIds = GetConstantIds();
		var variableIds = GetVariableIds();
		var entityIds = new HashSet<string>();
		var entityBehaviours = new Dictionary<string, HashSet<string>>();

		// First pass: collect and validate entity and behaviour IDs
		foreach (var entity in _config.Entities)
		{
			if (string.IsNullOrEmpty(entity.Id))
			{
				_errors.Add("Entity with empty 'id' found");
				continue;
			}

			if (!entityIds.Add(entity.Id))
			{
				_errors.Add($"Duplicate entity ID: '{entity.Id}'");
			}

			var behaviourIds = new HashSet<string>();
			if (entity.Behaviours is not null)
			{
				foreach (var behaviour in entity.Behaviours)
				{
					if (!string.IsNullOrEmpty(behaviour.Id))
					{
						if (!behaviourIds.Add(behaviour.Id))
						{
							_errors.Add($"Entity '{entity.Id}' has duplicate behaviour ID: '{behaviour.Id}'");
						}
					}

					// Validate behaviour structure
					ValidateBehaviour(behaviour, entity.Id, variableIds);
				}
			}
			entityBehaviours[entity.Id] = behaviourIds;

			// Validate position/rotation references
			if (!string.IsNullOrEmpty(entity.Position) && !constantIds.Contains(entity.Position))
			{
				_errors.Add($"Entity '{entity.Id}' references unknown constant for position: '{entity.Position}'");
			}

			if (!string.IsNullOrEmpty(entity.Rotation) && !constantIds.Contains(entity.Rotation))
			{
				_errors.Add($"Entity '{entity.Id}' references unknown constant for rotation: '{entity.Rotation}'");
			}
		}

		// Second pass: validate behaviour listener references
		foreach (var entity in _config.Entities)
		{
			if (string.IsNullOrEmpty(entity.Id) || entity.Behaviours is null)
				continue;

			foreach (var behaviour in entity.Behaviours)
			{
				var listeners = ExtractListeners(behaviour);
				if (listeners != null)
				{
					foreach (var listener in listeners)
					{
						ValidateListener(listener, entityIds, entityBehaviours, entity.Id);
					}
				}
			}
		}
	}

	/// <summary>
	/// Validates behaviour structure and required properties.
	/// </summary>
	private void ValidateBehaviour(BehaviourDef behaviour, string entityId, HashSet<string> variableIds)
	{
		if (string.IsNullOrEmpty(behaviour.Type))
		{
			_errors.Add($"Behaviour in entity '{entityId}' has empty 'type'");
			return;
		}

		var props = behaviour.Properties;
		var behaviourDesc = $"behaviour '{behaviour.Id ?? "unknown"}' in entity '{entityId}' ({behaviour.Type})";

		// Validate required properties
		switch (behaviour.Type)
		{
			case "key hold trigger":
				if (props is null || !props.ContainsKey("key"))
					_errors.Add($"{behaviourDesc} must have 'key' property");
				break;

			case "translate":
				if (props is null || !props.ContainsKey("displacement"))
					_errors.Add($"{behaviourDesc} must have 'displacement' property");
				break;

			case "vector variable setter":
			case "int variable setter":
				if (props is null || !props.ContainsKey("variable to set ref"))
					_errors.Add($"{behaviourDesc} must have 'variable to set ref' property");
				if (props is null || !props.ContainsKey("expression ref"))
					_errors.Add($"{behaviourDesc} must have 'expression ref' property");
				break;
		}

		// Validate variable references in properties
		if (props is not null)
		{
			if (behaviour.Type != "position variable setter" &&
			    props.TryGetValue("variable to set ref", out var varRef) && varRef is string varRefStr && !string.IsNullOrEmpty(varRefStr))
			{
				if (!variableIds.Contains(varRefStr))
					_errors.Add($"{behaviourDesc} references unknown variable: '{varRefStr}'");
			}

			if (props.TryGetValue("velocity variable ref", out var velRef) && velRef is string velRefStr && !string.IsNullOrEmpty(velRefStr))
			{
				if (!variableIds.Contains(velRefStr))
					_errors.Add($"{behaviourDesc} references unknown variable: '{velRefStr}'");
			}
		}
	}

	/// <summary>
	/// Validates that a listener references valid entity and behaviour.
	/// </summary>
	private void ValidateListener(ListenerDef listener, HashSet<string> entityIds,
		Dictionary<string, HashSet<string>> entityBehaviours, string fromEntity)
	{
		if (string.IsNullOrEmpty(listener.Entity))
		{
			_errors.Add($"Listener in entity '{fromEntity}' has empty 'entity ref'");
			return;
		}

		if (!entityIds.Contains(listener.Entity))
		{
			_errors.Add($"Listener in entity '{fromEntity}' references unknown entity: '{listener.Entity}'");
			return;
		}

		if (string.IsNullOrEmpty(listener.Behaviour))
		{
			_errors.Add($"Listener in entity '{fromEntity}' has empty 'behaviour ref'");
			return;
		}

		if (!entityBehaviours.TryGetValue(listener.Entity, out var behaviours) ||
			!behaviours.Contains(listener.Behaviour))
		{
			_errors.Add($"Listener in entity '{fromEntity}' references unknown behaviour '{listener.Behaviour}' in entity '{listener.Entity}'");
		}
	}

	/// <summary>
	/// Extracts listeners from behaviour properties if they exist.
	/// </summary>
	private IReadOnlyList<ListenerDef>? ExtractListeners(BehaviourDef behaviour)
	{
		if (behaviour.Properties is null || !behaviour.Properties.TryGetValue("listeners", out var listeners))
			return null;

		return listeners as IReadOnlyList<ListenerDef>;
	}

	/// <summary>
	/// Helper to get all constant IDs.
	/// </summary>
	private HashSet<string> GetConstantIds()
	{
		var ids = new HashSet<string>();
		if (_config.Constants is not null)
		{
			foreach (var constant in _config.Constants)
			{
				if (!string.IsNullOrEmpty(constant.Id))
				{
					ids.Add(constant.Id);
				}
			}
		}
		return ids;
	}

	/// <summary>
	/// Helper to get all variable IDs.
	/// </summary>
	private HashSet<string> GetVariableIds()
	{
		var ids = new HashSet<string>();
		if (_config.Variables is not null)
		{
			foreach (var variable in _config.Variables)
			{
				if (!string.IsNullOrEmpty(variable.Id))
				{
					ids.Add(variable.Id);
				}
			}
		}
		return ids;
	}
}

