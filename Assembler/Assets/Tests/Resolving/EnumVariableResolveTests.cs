using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class EnumVariableResolveTests
	{
		[Test]
		public void EnumBoundVariableResolvesToATypedEnumProvider()
		{
			// A `camera view: orthographic` constant is stored as a plain string. Reading it as an enum
			// (the use site is ValueReferenceSource<CameraProjection>) must parse it once and hand back a
			// genuine ValueProvider<CameraProjection> — a real enum value, not a per-read string mapping.
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("camera view", new StringValue("orthographic")));

			var provider = registry.Get<CameraProjection>("camera view");

			Assert.IsInstanceOf<ValueProvider<CameraProjection>>(provider);
			Assert.AreEqual(CameraProjection.Orthographic, provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void EnumBoundVariableParsesCaseAndSpaceInsensitively()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("ease", new StringValue("Out Back")));

			var provider = registry.Get<Easing>("ease");

			Assert.AreEqual(Easing.OutBack, provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void EnumBoundVariableWithUnknownValueThrows()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("camera view", new StringValue("isometric")));

			Assert.Throws<ParsingException>(() => registry.Get<CameraProjection>("camera view"));
		}
	}
}
