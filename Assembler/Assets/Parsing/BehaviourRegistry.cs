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
		IReadOnlyDictionary<string, AssemblerValue> props,
		IReadOnlyList<ValueInfo> resolvedValues,
		IReadOnlyDictionary<string, AssemblerValue> parameters);

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
				["deferred trigger"] = (DeferredTriggerInfo.Create, new[]
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
					new PropDescriptor("Parameters", typeof(Dictionary<string, AssemblerValue>))
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

				// --- List operations ---
				["vector list add"] = (ListAddInfo<Vector3>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Vector3>)), new PropDescriptor("Value", typeof(Vector3))
				}),
				["vector list remove at"] = (ListRemoveAtInfo<Vector3>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Vector3>)), new PropDescriptor("Index", typeof(int))
				}),
				["vector list set at"] = (ListSetAtInfo<Vector3>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Vector3>)),
					new PropDescriptor("Index", typeof(int)),
					new PropDescriptor("Value", typeof(Vector3))
				}),
				["vector list clear"] = (ListClearInfo<Vector3>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Vector3>))
				}),

				["int list add"] = (ListAddInfo<int>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<int>)), new PropDescriptor("Value", typeof(int))
				}),
				["int list remove at"] = (ListRemoveAtInfo<int>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<int>)), new PropDescriptor("Index", typeof(int))
				}),
				["int list set at"] = (ListSetAtInfo<int>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<int>)),
					new PropDescriptor("Index", typeof(int)),
					new PropDescriptor("Value", typeof(int))
				}),
				["int list clear"] = (ListClearInfo<int>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<int>))
				}),

				["float list add"] = (ListAddInfo<float>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<float>)), new PropDescriptor("Value", typeof(float))
				}),
				["float list remove at"] = (ListRemoveAtInfo<float>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<float>)), new PropDescriptor("Index", typeof(int))
				}),
				["float list set at"] = (ListSetAtInfo<float>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<float>)),
					new PropDescriptor("Index", typeof(int)),
					new PropDescriptor("Value", typeof(float))
				}),
				["float list clear"] = (ListClearInfo<float>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<float>))
				}),

				["bool list add"] = (ListAddInfo<bool>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<bool>)), new PropDescriptor("Value", typeof(bool))
				}),
				["bool list remove at"] = (ListRemoveAtInfo<bool>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<bool>)), new PropDescriptor("Index", typeof(int))
				}),
				["bool list set at"] = (ListSetAtInfo<bool>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<bool>)),
					new PropDescriptor("Index", typeof(int)),
					new PropDescriptor("Value", typeof(bool))
				}),
				["bool list clear"] = (ListClearInfo<bool>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<bool>))
				}),

				["string list add"] = (ListAddInfo<string>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<string>)), new PropDescriptor("Value", typeof(string))
				}),
				["string list remove at"] = (ListRemoveAtInfo<string>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<string>)), new PropDescriptor("Index", typeof(int))
				}),
				["string list set at"] = (ListSetAtInfo<string>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<string>)),
					new PropDescriptor("Index", typeof(int)),
					new PropDescriptor("Value", typeof(string))
				}),
				["string list clear"] = (ListClearInfo<string>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<string>))
				}),

				["colour list add"] = (ListAddInfo<Color>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Color>)), new PropDescriptor("Value", typeof(Color))
				}),
				["colour list remove at"] = (ListRemoveAtInfo<Color>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Color>)), new PropDescriptor("Index", typeof(int))
				}),
				["colour list set at"] = (ListSetAtInfo<Color>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Color>)),
					new PropDescriptor("Index", typeof(int)),
					new PropDescriptor("Value", typeof(Color))
				}),
				["colour list clear"] = (ListClearInfo<Color>.Create, new[]
				{
					new PropDescriptor("List", typeof(IList<Color>))
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
				["text label"] = (TextLabelInfo.Create, new[]
				{
					new PropDescriptor("Text", typeof(string)),
					new PropDescriptor("Label", typeof(string)),
					new PropDescriptor("FontSize", typeof(int)),
					new PropDescriptor("Rect", typeof(Dictionary<string, object>))
				}),
				["progress bar"] = (ProgressBarInfo.Create, new[]
				{
					new PropDescriptor("Value", typeof(float)),
					new PropDescriptor("Rect", typeof(Dictionary<string, object>))
				}),
				["ui image"] = (UIImageInfo.Create, new[]
				{
					new PropDescriptor("Colour", typeof(Color)),
					new PropDescriptor("Rect", typeof(Dictionary<string, object>))
				}),
				["ui button"] = (UIButtonInfo.Create, new[]
				{
					new PropDescriptor("Label", typeof(string)),
					new PropDescriptor("Rect", typeof(Dictionary<string, object>))
				}),
				["ui toggle"] = (UIToggleInfo.Create, new[]
				{
					new PropDescriptor("InitialValue", typeof(bool)),
					new PropDescriptor("Label", typeof(string)),
					new PropDescriptor("Rect", typeof(Dictionary<string, object>))
				}),
				["ui slider"] = (UISliderInfo.Create, new[]
				{
					new PropDescriptor("InitialValue", typeof(float)),
					new PropDescriptor("MinValue", typeof(float)),
					new PropDescriptor("MaxValue", typeof(float)),
					new PropDescriptor("Rect", typeof(Dictionary<string, object>))
				}),
				["ui input field"] = (UIInputFieldInfo.Create, new[]
				{
					new PropDescriptor("Rect", typeof(Dictionary<string, object>))
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