using System.Collections.Generic;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class ValueProviderTests
	{
		[Test]
		public void Set_RaisesChangedWithPreviousAndCurrent()
		{
			var provider = new ValueProvider<int>(1);
			var events = new List<(int previous, int current)>();
			provider.Changed += (previous, current) => events.Add((previous, current));

			provider.Set(2);
			provider.Set(5);

			CollectionAssert.AreEqual(
				new[] { (1, 2), (2, 5) },
				events);
		}

		[Test]
		public void Set_ToEqualValue_DoesNotRaiseChanged()
		{
			var provider = new ValueProvider<string>("hello");
			var raised = false;
			provider.Changed += (_, _) => raised = true;

			provider.Set("hello");

			Assert.IsFalse(raised);
			Assert.AreEqual("hello", provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void Changed_IsNotRaisedAfterUnsubscribe()
		{
			var provider = new ValueProvider<int>(0);
			var count = 0;
			void Handler(int previous, int current) => count++;

			provider.Changed += Handler;
			provider.Set(1);
			provider.Changed -= Handler;
			provider.Set(2);

			Assert.AreEqual(1, count);
		}
	}
}
