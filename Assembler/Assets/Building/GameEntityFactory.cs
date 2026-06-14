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
	public class GameEntityFactory : IEntitySpawner, IEntitySink
	{
		private const string SpawnedIdPrefix = "$spawn$";

		// Recycled shells keyed by template id. Populated lazily by Despawn; drained by Spawn. Empty until
		// something is despawned, so games that never destroy keep the pre-pooling behaviour and pay nothing.
		private readonly EntityPool _pool = new();

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
		private readonly LivePropertyUpdater _liveProperties;
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
			LivePropertyUpdater liveProperties,
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
			_liveProperties = liveProperties;
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

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo, Transform? parent, int? siblingIndex = null) =>
			Build(entityInfo, parent, siblingIndex, templateId: null, reuseShell: null);

		/// <summary>
		/// The single entity builder behind both fresh creation and pooled reuse. <paramref name="reuseShell"/>
		/// is a recycled GameObject to rebuild onto — a pooled respawn, or (recursively) a pooled tree's existing
		/// child — and is <c>null</c> for a fresh build. <paramref name="templateId"/> is stamped on the root
		/// entity only on a pooled spawn; it gates the entity's return to the pool on despawn and is left
		/// <c>null</c> on children (they pool as part of the root, never independently).
		///
		/// Reuse keeps every component alive and only re-applies state: the scope and resolved providers are
		/// rebuilt each spawn (a disposed scope cannot be reused, and resolved <c>Data</c> bakes in this spawn's
		/// parameters), the existing behaviour components are re-initialised in place, and their transient runtime
		/// state is reset via <c>OnReuse</c> after initialisation (see <see cref="Spawn"/>).
		/// </summary>
		private EntityBuildResult Build(ConcreteEntityInfo entityInfo, Transform? parent, int? siblingIndex,
			string? templateId, GameObject? reuseShell)
		{
			var scope = EntityVariableScope.Create(entityInfo.Variables);

			var context = _baseContext with { Scope = scope };

			var position = entityInfo.InitialPosition.Resolve(context).ValueOr(Vector3.zero);
			var rotation = entityInfo.InitialRotation.Resolve(context).ValueOr(Vector3.zero).FromEuler();

			var reused = reuseShell != null;
			var gameObject = reuseShell ?? new GameObject(entityInfo.Id);

			// Configure while inactive so behaviours' Awake (which creates their Unity sub-components) and the
			// entity's query self-register fire against fully-set Tags/Query/Id. A reused shell is already inactive.
			gameObject.SetActive(false);

			if (reused)
			{
				gameObject.name = entityInfo.Id;
			}

			if (parent != null)
			{
				gameObject.transform.SetParent(parent, worldPositionStays: false);
			}

			// Setting local pos/rot under the parent matches the fresh path's "stamp the unparented transform,
			// then SetParent(worldPositionStays:false)" — both leave the entity at these values local to its parent.
			gameObject.transform.localPosition = position;
			gameObject.transform.localRotation = rotation;

			// Pin sibling order to the descriptor's child order so layout groups (which arrange by sibling index)
			// are deterministic regardless of when children are instantiated.
			if (parent != null && siblingIndex.HasValue)
			{
				gameObject.transform.SetSiblingIndex(siblingIndex.Value);
			}

			_entityTransforms.Register(entityInfo.Id, gameObject.transform);

			var gameEntity = reused ? gameObject.GetComponent<GameEntity>() : gameObject.AddComponent<GameEntity>();
			gameEntity.Id = entityInfo.Id;
			gameEntity.Tags = entityInfo.Tags.ToArray();
			gameEntity.VariableScope = scope;
			gameEntity.Query = _entityQuery;
			gameEntity.TemplateId = templateId;

			// On teardown the entity self-evicts from every runtime index it was registered in. The deregistrations
			// hang off one event rather than separate registry refs on the entity; each captures the id it needs.
			// Re-subscribed every life because Recycle clears them on despawn.
			var entityId = entityInfo.Id;
			gameEntity.Destroying += () => _entityQuery.Unregister(entityId);
			gameEntity.Destroying += () => _entityTransforms.Unregister(entityId);
			gameEntity.Destroying += () => _behaviourRegistry.DeregisterEntity(entityId);

			// Activate now that GameEntity is configured, then explicitly register it into the query index. The
			// self-register lives in Activate (not GameEntity.Awake) because Awake runs only once per component
			// lifetime: a reused shell keeps its GameEntity, so Awake would not re-fire and the entity would
			// silently never re-register. Behaviour sub-components are created in their OnInitialise (the init pass
			// below), not Awake — Awake does not run in edit mode (the sandbox validator), and OnInitialise is the
			// only build step guaranteed to run in both edit and play mode.
			gameObject.SetActive(true);
			gameEntity.Activate();

			var behaviours = new List<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour, IReadOnlyList<string> BehaviourTags)>();
			var initialisations = new List<InitialiseBehaviourEvent>();

			var buildContext = new BehaviourBuildContext(
				context,
				this,
				this,
				_exclusiveGroups,
				_controls,
				_controlsAsset,
				_clock,
				_uiPrefabs,
				_entityQuery,
				_sight,
				_nav,
				_liveProperties);

			// A reused shell carries its behaviour components in their original add order, which is the template's
			// behaviour-list order — identical across instances of a template id — so index i lines up with
			// entityInfo.Behaviours[i]. Pass each existing component so the factory re-uses it instead of adding a
			// duplicate (a second component has its own null sub-component field, so its OnInitialise would create
			// a second Rigidbody/SpriteRenderer/collider alongside the original).
			var existingBehaviours = reused ? gameObject.GetComponents<GameBehaviour>() : null;

			var behaviourIndex = 0;

			foreach (var behaviourInfo in entityInfo.Behaviours)
			{
				var existing = existingBehaviours?[behaviourIndex];

				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject, behaviourInfo, buildContext, existing);

				gameBehaviour.SetEntity(gameEntity);
				gameBehaviour.Tags = behaviourInfo.Tags.ToArray();

				behaviours.Add((new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour, behaviourInfo.Tags));
				initialisations.Add(initialise);
				behaviourIndex++;
			}

			// A reused tree already carries its child entity GameObjects (told apart from behaviour-spawned helper
			// GameObjects like "Sprite" by their GameEntity component), so rebuild onto them in descriptor order
			// rather than creating duplicates.
			var existingChildren = reused ? ExistingChildEntities(gameObject.transform) : null;

			var childSiblingIndex = 0;
			var childIndex = 0;

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

				var childShell = existingChildren?[childIndex].gameObject;

				var childResult = Build(resolvedChild, gameObject.transform, childSiblingIndex++,
					templateId: null, reuseShell: childShell);

				behaviours.AddRange(childResult.Behaviours);
				initialisations.AddRange(childResult.Initialisations);
				childIndex++;
			}

			return new EntityBuildResult(behaviours, initialisations);
		}

		// The entity children of a pooled shell, in descriptor (sibling) order. Filters out behaviour-spawned
		// helper GameObjects (Sprite/VoxelMesh/Primitive children), which carry no GameEntity; the entity children
		// were pinned to sibling indices 0..n-1 on the previous build, so transform child order matches.
		private static IReadOnlyList<GameEntity> ExistingChildEntities(Transform parent)
		{
			var children = new List<GameEntity>();

			for (var i = 0; i < parent.childCount; i++)
			{
				if (parent.GetChild(i).TryGetComponent<GameEntity>(out var childEntity))
				{
					children.Add(childEntity);
				}
			}

			return children;
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

			// A fresh spawn id each life (even on reuse) keeps listener descriptors consistent with today and
			// avoids a recycled shell answering to its previous life's id.
			var newId = $"{SpawnedIdPrefix}{templateId}_{_spawnCounter++}";

			var entity = TemplateInstantiator.Instantiate(
				template,
				newId,
				_parseContext,
				new ConstantSource<Vector3>(position),
				new ConstantSource<Vector3>(rotation),
				parameters: new Dictionary<string, AssemblerValue>(),
				runtimeParameters: parameters);

			var reused = _pool.TryRent(templateId, out var shell);

			var result = Build(entity, _root, siblingIndex: null, templateId: templateId, reuseShell: reused ? shell : null);
			_behaviourRegistry.Register(result);

			foreach (var init in result.Initialisations)
			{
				init(_behaviourRegistry);
			}

			// Reset transient state and re-arm Start-style logic AFTER initialisation, so each behaviour sees this
			// spawn's Data. Only on reuse: a fresh component's Awake/Initialise/Start already run clean.
			if (reused)
			{
				foreach (var (_, behaviour, _) in result.Behaviours)
				{
					behaviour.OnReuse();
				}
			}
		}

		/// <summary>
		/// The despawn seam (<see cref="IEntitySink"/>) that <c>DestroyBehaviour</c> routes through. A non-pooled
		/// entity — anything not produced by <see cref="Spawn"/>, so <c>TemplateId</c> is null: hand-authored,
		/// placement, game-controller entities — is really destroyed, exactly as before pooling existed. A pooled
		/// entity's whole tree is torn down from the runtime indexes and its live bindings cleared (so the dormant
		/// shell neither ticks nor stacks duplicate bindings next life), then the shell is parked inactive under
		/// the game root and returned to its template's pool for the next <see cref="Spawn"/> to rebuild.
		/// </summary>
		public void Despawn(GameEntity entity)
		{
			if (entity == null)
			{
				return;
			}

			var gameObject = entity.gameObject;

			if (entity.TemplateId is not { } templateId)
			{
				UnityEngine.Object.Destroy(gameObject);
				return;
			}

			// Clear live-property bindings across the tree first: their teardown unsubscribes from game-global
			// variables and disposes expression providers before Recycle disposes the entity scopes those read.
			foreach (var bindings in gameObject.GetComponentsInChildren<LivePropertyBindings>(includeInactive: true))
			{
				bindings.ResetBindings();
			}

			// Recycle (not destroy) every entity in the tree — root and children — so each deregisters from the
			// query/transform/behaviour indexes and disposes its scope, while the GameObjects survive for reuse.
			foreach (var treeEntity in gameObject.GetComponentsInChildren<GameEntity>(includeInactive: true))
			{
				treeEntity.Recycle();
			}

			gameObject.transform.SetParent(_root, worldPositionStays: false);
			gameObject.SetActive(false);
			_pool.Return(templateId, gameObject);
		}
	}
}
