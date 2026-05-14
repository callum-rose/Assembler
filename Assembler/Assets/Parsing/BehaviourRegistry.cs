using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assembler.Parsing.Info;
using UnityEditor;
using UnityEngine;

namespace Assembler.Parsing
{
	internal record PropDescriptor(string Name, Type Type);

	internal delegate BehaviourInfo BehaviourFactory(
		string id,
		IReadOnlyList<BehaviourDescriptor> listeners,
		Dictionary<string, object>? props,
		IReadOnlyList<VariableInfo> resolvedValues,
		IReadOnlyDictionary<string, object>? parameters);

	internal static class BehaviourRegistry
	{
		internal readonly static IReadOnlyDictionary<string, (BehaviourFactory Factory, PropDescriptor[] Props)> All =
			new Dictionary<string, (BehaviourFactory, PropDescriptor[])>
			{
				["box collider"] = (
					(id, l, props, v, p) => new BoxColliderInfo(id,
						l,
						Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Size"), parameters: p),
						Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsTrigger"), parameters: p)),
					new[]
					{
						new PropDescriptor("Size", typeof(Vector3)), new PropDescriptor("IsTrigger", typeof(bool))
					}),
				["sphere collider"] = (
					(id, l, props, v, p) => new SphereColliderInfo(id,
						l,
						Transformer.Wrap<float>(v, props?.GetValueOrDefault("Radius"), parameters: p),
						Transformer.Wrap<bool>(v, props?.GetValueOrDefault("IsTrigger"), parameters: p)),
					new[]
					{
						new PropDescriptor("Radius", typeof(float)), new PropDescriptor("IsTrigger", typeof(bool))
					}),
				["rigidbody"] = (
					(id, l, props, v, p) => new RigidbodyInfo(id,
						l,
						Transformer.Wrap<bool>(v, props?.GetValueOrDefault("UseGravity"), parameters: p)),
					new[]
					{
						new PropDescriptor("UseGravity", typeof(bool))
					}),
				["velocity"] = (
					(id, l, props, v, p) => new VelocityInfo(id,
						l,
						Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Velocity"), parameters: p)),
					new[]
					{
						new PropDescriptor("Velocity", typeof(Vector3))
					}),
				["translate"] = (
					(id, l, props, v, p) => new TranslateInfo(id,
						l,
						Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Displacement"), parameters: p)),
					new[]
					{
						new PropDescriptor("Displacement", typeof(Vector3))
					}),
				["key hold trigger"] = (
					(id, l, props, v, p) => new KeyHoldTriggerInfo(id,
						l,
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("Key"), parameters: p)),
					new[]
					{
						new PropDescriptor("Key", typeof(string))
					}),
				["collision enter trigger"] = (
					(id, l, props, v, p) => new CollisionEnterTriggerInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect"))),
					new[]
					{
						new PropDescriptor("TagsToDetect", typeof(string[]))
					}),
				["trigger enter trigger"] = (
					(id, l, props, v, p) => new TriggerEnterTriggerInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect"))),
					new[]
					{
						new PropDescriptor("TagsToDetect", typeof(string[]))
					}),
				["vector variable setter"] = (
					(id, l, props, v, p) => new VariableSetterInfo<Vector3>(id,
						l,
						Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
						Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Value"), parameters: p)),
					new[]
					{
						new PropDescriptor("VariableId", typeof(Vector3)), new PropDescriptor("Value", typeof(Vector3))
					}),
				["int variable setter"] = (
					(id, l, props, v, p) => new VariableSetterInfo<int>(id,
						l,
						Transformer.Wrap<int>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
						Transformer.Wrap<int>(v, props?.GetValueOrDefault("Value"), parameters: p)),
					new[]
					{
						new PropDescriptor("VariableId", typeof(int)), new PropDescriptor("Value", typeof(int))
					}),
				["float variable setter"] = (
					(id, l, props, v, p) => new VariableSetterInfo<float>(id,
						l,
						Transformer.Wrap<float>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
						Transformer.Wrap<float>(v, props?.GetValueOrDefault("Value"), parameters: p)),
					new[]
					{
						new PropDescriptor("VariableId", typeof(float)), new PropDescriptor("Value", typeof(float))
					}),
				["bool variable setter"] = (
					(id, l, props, v, p) => new VariableSetterInfo<bool>(id,
						l,
						Transformer.Wrap<bool>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
						Transformer.Wrap<bool>(v, props?.GetValueOrDefault("Value"), parameters: p)),
					new[]
					{
						new PropDescriptor("VariableId", typeof(bool)), new PropDescriptor("Value", typeof(bool))
					}),
				["string variable setter"] = (
					(id, l, props, v, p) => new VariableSetterInfo<string>(id,
						l,
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("Value"), parameters: p)),
					new[]
					{
						new PropDescriptor("VariableId", typeof(string)), new PropDescriptor("Value", typeof(string))
					}),
				["position setter"] = (
					(id, l, props, v, p) => new SetPositionInfo(id,
						l,
						Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Position"), parameters: p)),
					new[]
					{
						new PropDescriptor("Position", typeof(Vector3))
					}),
				["camera"] = (
					(id, l, props, v, p) => new CameraInfo(id,
						l,
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("View"), parameters: p),
						Transformer.Wrap<float>(v, props?.GetValueOrDefault("Size"), parameters: p)),
					new[]
					{
						new PropDescriptor("View", typeof(string)), new PropDescriptor("Size", typeof(float))
					}),
				["condition trigger"] = (
					(id, l, props, v, p) => new ConditionTriggerInfo(id,
						l,
						Transformer.Wrap<bool>(v, props?.GetValueOrDefault("Condition"), parameters: p)),
					new[]
					{
						new PropDescriptor("Condition", typeof(bool))
					}),
				["spawner"] = (
					(id, l, props, v, p) => new SpawnerInfo(id,
						l,
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("TemplateId")),
						Transformer.Wrap<Vector3>(v, props?.GetValueOrDefault("Position"))),
					new[]
					{
						new PropDescriptor("TemplateId", typeof(string)), new PropDescriptor("Position", typeof(Vector3))
					}),
				["destroy"] = (
					(id, l, props, v, p) => new DestroyInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["on start trigger"] = (
					(id, l, props, v, p) => new OnStartTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["key down trigger"] = (
					(id, l, props, v, p) => new KeyDownTriggerInfo(id,
						l,
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("Key"), parameters: p)),
					new[]
					{
						new PropDescriptor("Key", typeof(string))
					}),
				["key up trigger"] = (
					(id, l, props, v, p) => new KeyUpTriggerInfo(id,
						l,
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("Key"), parameters: p)),
					new[]
					{
						new PropDescriptor("Key", typeof(string))
					}),
				["tap trigger"] = (
					(id, l, props, v, p) => new TapTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["double tap trigger"] = (
					(id, l, props, v, p) => new DoubleTapTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["long press trigger"] = (
					(id, l, props, v, p) => new LongPressTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["swipe trigger"] = (
					(id, l, props, v, p) => new SwipeTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["drag trigger"] = (
					(id, l, props, v, p) => new DragTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["pinch trigger"] = (
					(id, l, props, v, p) => new PinchTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["rotate trigger"] = (
					(id, l, props, v, p) => new RotateTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["condition"] = (
					(id, l, props, v, p) => new ConditionInfo(id,
						l,
						Transformer.Wrap<string>(v, props?.GetValueOrDefault("ExpressionId"), parameters: p),
						Transformer.ConvertArgumentList(v, props?.GetValueOrDefault("Arguments"))),
					new[]
					{
						new PropDescriptor("ExpressionId", typeof(string)), new PropDescriptor("Arguments", typeof(object[]))
					}),
				["timer trigger"] = (
					(id, l, props, v, p) => new TimerTriggerInfo(id,
						l,
						Transformer.Wrap<float>(v, props?.GetValueOrDefault("Delay"), parameters: p)),
					new[]
					{
						new PropDescriptor("Delay", typeof(float))
					}),
				["interval trigger"] = (
					(id, l, props, v, p) => new IntervalTriggerInfo(id,
						l,
						Transformer.Wrap<float>(v, props?.GetValueOrDefault("Interval"), parameters: p)),
					new[]
					{
						new PropDescriptor("Interval", typeof(float))
					}),
				["every frame trigger"] = (
					(id, l, props, v, p) => new EveryFrameTriggerInfo(id, l),
					Array.Empty<PropDescriptor>()),
				["trigger exit trigger"] = (
					(id, l, props, v, p) => new TriggerExitTriggerInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect"))),
					new[]
					{
						new PropDescriptor("TagsToDetect", typeof(string[]))
					}),
				["trigger stay trigger"] = (
					(id, l, props, v, p) => new TriggerStayTriggerInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect"))),
					new[]
					{
						new PropDescriptor("TagsToDetect", typeof(string[]))
					}),
				["collision exit trigger"] = (
					(id, l, props, v, p) => new CollisionExitTriggerInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect"))),
					new[]
					{
						new PropDescriptor("TagsToDetect", typeof(string[]))
					}),
				["collision stay trigger"] = (
					(id, l, props, v, p) => new CollisionStayTriggerInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TagsToDetect"))),
					new[]
					{
						new PropDescriptor("TagsToDetect", typeof(string[]))
					}),
				["when all"] = (
					(id, l, props, v, p) => new WhenAllInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TriggerIds"))),
					new[]
					{
						new PropDescriptor("TriggerIds", typeof(string[]))
					}),
				["when any"] = (
					(id, l, props, v, p) => new WhenAnyInfo(id,
						l,
						Transformer.ConvertStringList(props?.GetValueOrDefault("TriggerIds"))),
					new[]
					{
						new PropDescriptor("TriggerIds", typeof(string[]))
					}),
			};

		[MenuItem("Assembler/Find and Log All Concrete Behaviour Info Types")]
		private static void LogAllBehaviourInfoTypes()
		{
			var allTypes = AppDomain.CurrentDomain
				.GetAssemblies()
				.SelectMany(a => a.GetTypes())
				.Where(t => typeof(BehaviourInfo).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

			Debug.Log(new StringBuilder().AppendJoin(", ", allTypes.Select(t => t.Name)));
		}

		[MenuItem("Assembler/Print Behaviour Docs")]
		private static void DebugLogMarkdown()
		{
			Debug.Log(GenerateMarkdown());
		}

		[MenuItem("Assembler/Generate Behaviour Docs")]
		private static void GenerateBehaviourDocs()
		{
			try
			{
				File.WriteAllText("Assets/Docs/Behaviours.md", GenerateMarkdown());
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}
		}

		private static string GenerateMarkdown()
		{
			var sb = new StringBuilder();
			sb.AppendLine("# Behaviours");
			sb.AppendLine();

			foreach (var (type, (_, props)) in All)
			{
				sb.AppendLine($"## `{type}`");

				if (props.Length == 0)
				{
					sb.AppendLine("No properties.");
				}
				else
				{
					sb.AppendLine("| Property | Type |");
					sb.AppendLine("|----------|------|");

					foreach (var prop in props)
						sb.AppendLine($"| {prop.Name} | `{prop.Type}` |");
				}

				sb.AppendLine();
			}

			return sb.ToString();
		}
	}
}