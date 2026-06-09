using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class ResolveExceptionTests
	{
		[Test]
		public void UnregisteredVariableThrowsResolveException()
		{
			var registry = new VariableRegistry();

			var ex = Assert.Throws<ResolveException>(() => registry.Get<int>("missing"));
			Assert.That(ex.Message, Does.Contain("missing"));
		}
	}
}
