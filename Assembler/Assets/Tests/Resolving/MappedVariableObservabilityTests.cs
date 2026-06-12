using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	// An int variable read as a float (or as object) is wrapped in an adapter provider. The adapter must stay
	// observable so a live binding pushes on write rather than silently falling onto the per-frame poll path.
	public class MappedVariableObservabilityTests
	{
		[Test]
		public void IntVariableReadAsFloat_IsObservable_AndForwardsWrites()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("hp", new IntValue(2)));

			var asFloat = registry.Get<float>("hp");

			Assert.IsInstanceOf<IObservableValueProvider>(asFloat, "an int-as-float read must forward observability.");
			Assert.AreEqual(2f, asFloat.Get(TriggerContext.Empty), 1e-4f);

			var pulses = 0;
			((IObservableValueProvider)asFloat).Invalidated += () => pulses++;

			// The underlying int variable is the same instance; writing it must pulse the float view.
			((IWriteValueProvider<int>)registry.Get<int>("hp")).Set(5);

			Assert.AreEqual(1, pulses, "writing the int variable should pulse the float view exactly once.");
			Assert.AreEqual(5f, asFloat.Get(TriggerContext.Empty), 1e-4f, "the float view should reflect the new int value.");
		}

		[Test]
		public void IntVariableReadAsObject_IsObservable_AndForwardsWrites()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("hp", new IntValue(2)));

			var asObject = registry.Get<object>("hp");

			Assert.IsInstanceOf<IObservableValueProvider>(asObject, "a boxed read must forward observability.");

			var pulses = 0;
			((IObservableValueProvider)asObject).Invalidated += () => pulses++;

			((IWriteValueProvider<int>)registry.Get<int>("hp")).Set(7);

			Assert.AreEqual(1, pulses);
			Assert.AreEqual(7, asObject.Get(TriggerContext.Empty));
		}

		[Test]
		public void Unsubscribe_StopsForwardingFromTheInner()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("hp", new IntValue(0)));

			var asFloat = (IObservableValueProvider)registry.Get<float>("hp");
			var pulses = 0;
			void Handler() => pulses++;

			asFloat.Invalidated += Handler;
			((IWriteValueProvider<int>)registry.Get<int>("hp")).Set(1);
			asFloat.Invalidated -= Handler;
			((IWriteValueProvider<int>)registry.Get<int>("hp")).Set(2);

			Assert.AreEqual(1, pulses, "removing the handler must detach it from the underlying variable.");
		}
	}
}
