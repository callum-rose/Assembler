using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class ConditionalValueProviderTests
	{
		// None of the branches here read context state — constants resolve to plain value providers — so an
		// all-null resolution context is sufficient (mirrors QueryValueProviderTests.ContextWith).
		private static readonly ResolutionContext Context = new(null!, null!, null!, null!, null, null!, null!, null!);

		[Test]
		public void TrueConditionResolvesToThenBranch()
		{
			var source = new ConditionalSource<int>(
				new ConstantSource<bool>(true),
				new ConstantSource<int>(10),
				new ConstantSource<int>(20));

			var provider = source.Resolve(Context);

			Assert.AreEqual(10, provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void FalseConditionResolvesToElseBranch()
		{
			var source = new ConditionalSource<int>(
				new ConstantSource<bool>(false),
				new ConstantSource<int>(10),
				new ConstantSource<int>(20));

			var provider = source.Resolve(Context);

			Assert.AreEqual(20, provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void OnlySelectedBranchIsRead()
		{
			var thenBranch = new RecordingProvider(1);
			var elseBranch = new RecordingProvider(2);
			var provider = new ConditionalValueProvider<int>(new ConstantProvider(true), thenBranch, elseBranch);

			provider.Get(TriggerContext.Empty);

			Assert.AreEqual(1, thenBranch.Reads, "selected branch should be read once");
			Assert.AreEqual(0, elseBranch.Reads, "unselected branch must not be read");
		}

		private sealed class ConstantProvider : IValueProvider<bool>
		{
			private readonly bool _value;

			public ConstantProvider(bool value) => _value = value;

			public bool Get(TriggerContext ctx) => _value;

			object IValueProvider.Get(TriggerContext ctx) => _value;
		}

		private sealed class RecordingProvider : IValueProvider<int>
		{
			private readonly int _value;

			public RecordingProvider(int value) => _value = value;

			public int Reads { get; private set; }

			public int Get(TriggerContext ctx)
			{
				Reads++;
				return _value;
			}

			object IValueProvider.Get(TriggerContext ctx) => Get(ctx);
		}
	}
}
