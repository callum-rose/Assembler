using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using UnityEditor;
using UnityEngine;
using SphereGizmoInfo = Assembler.Parsing.Info.Behaviours.SphereGizmoInfo;
using CubeGizmoInfo = Assembler.Parsing.Info.Behaviours.CubeGizmoInfo;

namespace Assembler.Parsing
{
	internal record PropDescriptor(string Name, Type Type);

	internal delegate BehaviourInfo BehaviourFactory(
		string id,
		IReadOnlyList<ListenerInfo> listeners,
		Dictionary<string, object>? props,
		IReadOnlyList<ValueInfo> resolvedValues,
		IReadOnlyDictionary<string, object>? parameters);

	internal static class BehaviourRegistry
	{
		internal readonly static IReadOnlyDictionary<string, (BehaviourFactory Factory, PropDescriptor[] Props)> All =
			new Dictionary<string, (BehaviourFactory, PropDescriptor[])>
			{
				["box collider"] = (BoxColliderInfo.Create, new[]
				{
					new PropDescriptor("Size", typeof(Vector3)), new PropDescriptor("IsTrigger", typeof(bool))
				}),
				["sphere collider"] = (SphereColliderInfo.Create, new[]
				{
					new PropDescriptor("Radius", typeof(float)), new PropDescriptor("IsTrigger", typeof(bool))
				}),
				["rigidbody"] = (RigidbodyInfo.Create, new[]
				{
					new PropDescriptor("UseGravity", typeof(bool))
				}),
				["velocity"] = (VelocityInfo.Create, new[]
				{
					new PropDescriptor("Velocity", typeof(Vector3))
				}),
				["translate"] = (TranslateInfo.Create, new[]
				{
					new PropDescriptor("Displacement", typeof(Vector3))
				}),
				["key hold trigger"] = (KeyHoldTriggerInfo.Create, new[]
				{
					new PropDescriptor("Key", typeof(string))
				}),
				["key down trigger"] = (KeyDownTriggerInfo.Create, new[]
				{
					new PropDescriptor("Key", typeof(string))
				}),
				["key up trigger"] = (KeyUpTriggerInfo.Create, new[]
				{
					new PropDescriptor("Key", typeof(string))
				}),
				["tap trigger"] = (TapTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["double tap trigger"] = (DoubleTapTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["long press trigger"] = (LongPressTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["swipe trigger"] = (SwipeTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["drag trigger"] = (DragTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["pinch trigger"] = (PinchTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["rotate trigger"] = (RotateTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["condition"] = (ConditionInfo.Create, new[]
				{
					new PropDescriptor("ExpressionId", typeof(string)), new PropDescriptor("Arguments", typeof(object[]))
				}),
				["timer trigger"] = (TimerTriggerInfo.Create, new[]
				{
					new PropDescriptor("Delay", typeof(float))
				}),
				["on start trigger"] = (OnStartTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["interval trigger"] = (IntervalTriggerInfo.Create, new[]
				{
					new PropDescriptor("Interval", typeof(float))
				}),
				["every frame trigger"] = (EveryFrameTriggerInfo.Create, Array.Empty<PropDescriptor>()),
				["collision enter trigger"] = (CollisionEnterTriggerInfo.Create, new[]
				{
					new PropDescriptor("TagsToDetect", typeof(string[]))
				}),
				["trigger enter trigger"] = (TriggerEnterTriggerInfo.Create, new[]
				{
					new PropDescriptor("TagsToDetect", typeof(string[]))
				}),
				["trigger exit trigger"] = (TriggerExitTriggerInfo.Create, new[]
				{
					new PropDescriptor("TagsToDetect", typeof(string[]))
				}),
				["trigger stay trigger"] = (TriggerStayTriggerInfo.Create, new[]
				{
					new PropDescriptor("TagsToDetect", typeof(string[]))
				}),
				["collision exit trigger"] = (CollisionExitTriggerInfo.Create, new[]
				{
					new PropDescriptor("TagsToDetect", typeof(string[]))
				}),
				["collision stay trigger"] = (CollisionStayTriggerInfo.Create, new[]
				{
					new PropDescriptor("TagsToDetect", typeof(string[]))
				}),
				["when all"] = (WhenAllInfo.Create, new[]
				{
					new PropDescriptor("TriggerIds", typeof(string[]))
				}),
				["when any"] = (WhenAnyInfo.Create, new[]
				{
					new PropDescriptor("TriggerIds", typeof(string[]))
				}),
				["spawner"] = (SpawnerInfo.Create, new[]
				{
					new PropDescriptor("TemplateId", typeof(string)),
					new PropDescriptor("Position", typeof(Vector3)),
					new PropDescriptor("Rotation", typeof(Vector3)),
					new PropDescriptor("Parameters", typeof(Dictionary<string, object>))
				}),
				["destroy"] = (DestroyInfo.Create, Array.Empty<PropDescriptor>()),
				["position setter"] = (SetPositionInfo.Create, new[]
				{
					new PropDescriptor("Position", typeof(Vector3))
				}),
				["camera"] = (CameraInfo.Create, new[]
				{
					new PropDescriptor("View", typeof(string)), new PropDescriptor("Size", typeof(float))
				}),
				["condition trigger"] = (ConditionTriggerInfo.Create, new[]
				{
					new PropDescriptor("Condition", typeof(bool))
				}),
				["vector variable setter"] = (VariableSetterInfo<Vector3>.Create, new[]
				{
					new PropDescriptor("VariableId", typeof(Vector3)), new PropDescriptor("Value", typeof(Vector3))
				}),
				["int variable setter"] = (VariableSetterInfo<int>.Create, new[]
				{
					new PropDescriptor("VariableId", typeof(int)), new PropDescriptor("Value", typeof(int))
				}),
				["float variable setter"] = (VariableSetterInfo<float>.Create, new[]
				{
					new PropDescriptor("VariableId", typeof(float)), new PropDescriptor("Value", typeof(float))
				}),
				["bool variable setter"] = (VariableSetterInfo<bool>.Create, new[]
				{
					new PropDescriptor("VariableId", typeof(bool)), new PropDescriptor("Value", typeof(bool))
				}),
				["string variable setter"] = (VariableSetterInfo<string>.Create, new[]
				{
					new PropDescriptor("VariableId", typeof(string)), new PropDescriptor("Value", typeof(string))
				}),
				["sprite"] = (SpriteInfo.Create, new[]
				{
					new PropDescriptor("Sprite", typeof(Sprite)), new PropDescriptor("Size", typeof(Vector2))
				}),
				["audio source"] = (AudioSourceInfo.Create, new[]
				{
					new PropDescriptor("Clip", typeof(AudioClip)), new PropDescriptor("PlayOnStart", typeof(bool)),
					new PropDescriptor("Loop", typeof(bool))
				}),
				["sphere gizmo"] = (SphereGizmoInfo.Create, new[]
				{
					new PropDescriptor("Radius", typeof(float)), new PropDescriptor("IsWire", typeof(bool)),
					new PropDescriptor("Colour", typeof(Color))
				}),
				["cube gizmo"] = (CubeGizmoInfo.Create, new[]
				{
					new PropDescriptor("Size", typeof(Vector3)), new PropDescriptor("IsWire", typeof(bool)),
					new PropDescriptor("Colour", typeof(Color))
				}),
				["for each trigger"] = (ForEachTriggerInfo.Create, new[]
				{
					new PropDescriptor("Entities", typeof(object))
				})
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