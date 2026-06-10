using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Behaviours.Spawners;
using Assembler.Behaviours.UI;
using Assembler.Extensions;
using Assembler.Parsing;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assembler.Building
{
	public class GameEntityFactory : IEntitySpawner
	{
		private const string SpawnedIdPrefix = "$spawn$";

		private readonly VariableRegistry _variables;
		private readonly CompiledExpressionsRegistry _expressions;
		private readonly BehaviourRegistry _behaviourRegistry;
		private readonly AssetRegistry _assets;
		private readonly StringTableRegistry _strings;
		private readonly EntityTransformRegistry _entityTransforms;
		private readonly EntityQueryService _entityQuery;
		private readonly LineOfSightService _sight;
		private readonly NavGridService _nav;
		private readonly ExclusiveGroupRegistry _exclusiveGroups;
		private readonly IGameClock _clock;
		private readonly IReadOnlyDictionary<string, EntityInfo> _templates;
		private readonly TransformContext _parseContext;
		private readonly Transform _root;
		private readonly ControlsInfo _controls;
		private readonly InputActionAsset _controlsAsset;
		private readonly UiPrefabLibrary _uiPrefabs;

		// Registries/clock/query service are fixed for the factory's lifetime, so build a base context once
		// and derive the per-entity context with `with { Scope = scope }`. The base Scope is a placeholder
		// that every derived context overrides.
		private readonly ResolutionContext _baseContext;

		private int _spawnCounter;

		public GameEntityFactory(VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			BehaviourRegistry behaviourRegistry,
			AssetRegistry assets,
			StringTableRegistry strings,
			EntityTransformRegistry entityTransforms,
			EntityQueryService entityQuery,
			LineOfSightService sight,
			NavGridService nav,
			ExclusiveGroupRegistry exclusiveGroups,
			IGameClock clock,
			IReadOnlyDictionary<string, EntityInfo> templates,
			TransformContext parseContext,
			Transform root,
			ControlsInfo controls,
			InputActionAsset controlsAsset,
			UiPrefabLibrary uiPrefabs)
		{
			_variables = variables;
			_expressions = expressions;
			_behaviourRegistry = behaviourRegistry;
			_assets = assets;
			_strings = strings;
			_entityTransforms = entityTransforms;
			_entityQuery = entityQuery;
			_sight = sight;
			_nav = nav;
			_exclusiveGroups = exclusiveGroups;
			_clock = clock;
			_templates = templates;
			_parseContext = parseContext;
			_root = root;
			_controls = controls;
			_controlsAsset = controlsAsset;
			_uiPrefabs = uiPrefabs;

			_baseContext = new ResolutionContext(_variables, _expressions, _assets, _strings,
				EntityVariableScope.Create(Enumerable.Empty<ValueInfo>()), _entityTransforms, _entityQuery, _clock);
		}

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo) => Create(entityInfo, _root);

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo, Transform? parent, int? siblingIndex = null)
		{
			var scope = EntityVariableScope.Create(entityInfo.Variables);

			var context = _baseContext with { Scope = scope };

			var gameObject = new GameObject(entityInfo.Id)
			{
				transform =
				{
					position = entityInfo.InitialPosition.Resolve(context).Get(),
					rotation = entityInfo.InitialRotation.Resolve(context).Get().FromEuler()
				}
			};

			// Configure GameEntity while inactive so its Awake (which self-registers into the query index) runs
			// only once Tags/Query are set, when we activate below.
			gameObject.SetActive(false);

			if (parent != null)
			{
				gameObject.transform.SetParent(parent, worldPositionStays: false);

				// Pin sibling order to the descriptor's child order so layout groups (which arrange by
				// sibling index) are deterministic regardless of when children are instantiated.
				if (siblingIndex.HasValue)
				{
					gameObject.transform.SetSiblingIndex(siblingIndex.Value);
				}
			}

			_entityTransforms.Register(entityInfo.Id, gameObject.transform);

			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Id = entityInfo.Id;
			gameEntity.Tags = entityInfo.Tags.ToArray();
			gameEntity.VariableScope = scope;
			gameEntity.Query = _entityQuery;

			// Activate now that GameEntity is configured: its Awake self-registers into the query index.
			gameObject.SetActive(true);

			var behaviours = new List<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour, IReadOnlyList<string> BehaviourTags)>();
			var initialisations = new List<InitialiseBehaviourEvent>();

			var buildContext = new BehaviourBuildContext(
				context,
				this,
				_exclusiveGroups,
				_controls,
				_controlsAsset,
				_clock,
				_uiPrefabs,
				_entityQuery,
				_sight,
				_nav);

			foreach (var behaviourInfo in entityInfo.Behaviours)
			{
				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject, behaviourInfo, buildContext);

				gameBehaviour.Tags = behaviourInfo.Tags.ToArray();

				behaviours.Add((new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour, behaviourInfo.Tags));
				initialisations.Add(initialise);
			}

			var childSiblingIndex = 0;

			foreach (var child in entityInfo.Children)
			{
				var childId = $"{entityInfo.Id}/{child.IdSuffix}";

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

				var childResult = Create(resolvedChild, gameObject.transform, childSiblingIndex++);

				behaviours.AddRange(childResult.Behaviours);
				initialisations.AddRange(childResult.Initialisations);
			}

			return new EntityBuildResult(behaviours, initialisations);
		}

		/// <summary>
		/// Expands a <see cref="PlacementInfo"/> into one independent <see cref="ConcreteEntityInfo"/> per
		/// position its <c>At</c> source resolves to. Positions resolve here (build time) because an
		/// expression-sourced list needs the live variable/expression registries. Each instance is stamped
		/// at its absolute position via the same template-instantiation path as <see cref="Spawn"/>, sharing
		/// the placement's rotation, parameters and tags; ids are <c>&lt;placementId&gt;_&lt;i&gt;</c>. An
		/// empty/absent position list creates nothing. Returns the infos so the caller drives Create +
		/// initialisation uniformly with hand-authored entities.
		/// </summary>
		public IReadOnlyList<ConcreteEntityInfo> ExpandPlacement(PlacementInfo placement)
		{
			using var scope = EntityVariableScope.Create(Array.Empty<ValueInfo>());

			var resolutionContext = new ResolutionContext(_variables, _expressions, _assets, _strings, scope,
				_entityTransforms, _entityQuery, _clock);

			var positions = placement.Positions.Resolve(resolutionContext).Get();

			if (positions is null || positions.Count == 0)
			{
				return Array.Empty<ConcreteEntityInfo>();
			}

			if (!_templates.TryGetValue(placement.TemplateId, out var template))
			{
				throw new InvalidOperationException(
					$"Placement '{placement.Id}' references unknown template id '{placement.TemplateId}'");
			}

			var instances = new ConcreteEntityInfo[positions.Count];

			for (var i = 0; i < positions.Count; i++)
			{
				instances[i] = TemplateInstantiator.Instantiate(
					template,
					$"{placement.Id}_{i}",
					_parseContext,
					new ConstantSource<Vector3>(positions[i]),
					placement.Rotation,
					placement.Parameters,
					placement.Tags);
			}

			return instances;
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
