using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Spawners;
using Assembler.Extensions;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Building
{
	public class GameEntityFactory : IEntitySpawner
	{
		private const string SpawnedIdPrefix = "$spawn$";

		private readonly VariableRegistry _variables;
		private readonly CompiledExpressionsRegistry _expressions;
		private readonly BehaviourRegistry _behaviourRegistry;
		private readonly AssetRegistry _assets;
		private readonly EntityTransformRegistry _entityTransforms;
		private readonly ExclusiveGroupRegistry _exclusiveGroups;
		private readonly IGameClock _clock;
		private readonly IReadOnlyDictionary<string, EntityInfo> _templates;
		private readonly TransformContext _parseContext;
		private readonly Transform _root;

		private int _spawnCounter;

		public GameEntityFactory(VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			BehaviourRegistry behaviourRegistry,
			AssetRegistry assets,
			EntityTransformRegistry entityTransforms,
			ExclusiveGroupRegistry exclusiveGroups,
			IGameClock clock,
			IReadOnlyDictionary<string, EntityInfo> templates,
			TransformContext parseContext,
			Transform root)
		{
			_variables = variables;
			_expressions = expressions;
			_behaviourRegistry = behaviourRegistry;
			_assets = assets;
			_entityTransforms = entityTransforms;
			_exclusiveGroups = exclusiveGroups;
			_clock = clock;
			_templates = templates;
			_parseContext = parseContext;
			_root = root;
		}

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo) => Create(entityInfo, _root);

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo, Transform? parent)
		{
			var scope = EntityVariableScope.Create(entityInfo.Variables);

			var initialPositionContext = new ResolutionContext(_variables, _expressions, _assets, scope, _entityTransforms, _clock);

			var gameObject = new GameObject(entityInfo.Id)
			{
				transform =
				{
					position = entityInfo.InitialPosition.Resolve(initialPositionContext).Get(),
					rotation = entityInfo.InitialRotation.Resolve(initialPositionContext).Get().FromEuler()
				}
			};

			if (parent != null)
			{
				gameObject.transform.SetParent(parent, worldPositionStays: false);
			}

			_entityTransforms.Register(entityInfo.Id, gameObject.transform);

			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Tags = entityInfo.Tags.ToArray();
			gameEntity.VariableScope = scope;

			var behaviours = new List<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour, IReadOnlyList<string> BehaviourTags)>();
			var initialisations = new List<InitialiseBehaviourEvent>();

			var buildContext = new BehaviourBuildContext(
				new ResolutionContext(_variables, _expressions, _assets, scope, _entityTransforms, _clock),
				this,
				_exclusiveGroups,
				_clock);

			foreach (var behaviourInfo in entityInfo.Behaviours)
			{
				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject, behaviourInfo, buildContext);

				gameBehaviour.Tags = behaviourInfo.Tags.ToArray();

				behaviours.Add((new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour, behaviourInfo.Tags));
				initialisations.Add(initialise);
			}

			foreach (var child in entityInfo.Children)
			{
				var childId = child.AbsoluteId ?? $"{entityInfo.Id}/{child.IdSuffix}";

				EntityInfo childTemplate;

				if (child.TemplateRefId != null)
				{
					if (!_templates.TryGetValue(child.TemplateRefId, out childTemplate!))
					{
						throw new InvalidOperationException(
							$"Child '{childId}' references unknown template id '{child.TemplateRefId}'");
					}
				}
				else
				{
					childTemplate = NullEntityInfo.Instance;
				}

				var resolvedChild = TemplateInstantiator.Instantiate(
					childTemplate,
					childId,
					_parseContext,
					child.InitialPosition,
					child.InitialRotation,
					child.Parameters,
					child.Tags,
					child.Behaviours,
					child.Variables,
					child.Children);

				var childResult = Create(resolvedChild, gameObject.transform);

				behaviours.AddRange(childResult.Behaviours);
				initialisations.AddRange(childResult.Initialisations);
			}

			return new EntityBuildResult(behaviours, initialisations);
		}

		public void Spawn(string templateId, Vector3 position, Vector3 rotation, IReadOnlyDictionary<string, object> parameters)
		{
			if (!_templates.TryGetValue(templateId, out var template))
			{
				throw new InvalidOperationException($"No template registered with id '{templateId}'");
			}

			var newId = $"{SpawnedIdPrefix}{templateId}_{_spawnCounter++}";

			var entity = TemplateInstantiator.Instantiate(
				template,
				newId,
				_parseContext,
				new ConstantSource<Vector3>(position),
				new ConstantSource<Vector3>(rotation),
				parameters: new Dictionary<string, AssemblerValue>(),
				runtimeParameters: parameters);

			var result = Create(entity);
			_behaviourRegistry.Register(result);

			foreach (var init in result.Initialisations)
			{
				init(_behaviourRegistry);
			}
		}
	}
}
