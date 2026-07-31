using Spike.CompilerHarness.Cases.Adversarial;

namespace Spike.CompilerHarness.Cases
{
	/// <summary>
	/// Assembles the whole corpus into one flat, ordered list. Order is stable and meaningful: the ported
	/// suites come first in their original file order, then the four adversarial families. A case's index
	/// is what <c>CompilerSpikeRunner._startIndex</c> takes, so keep this order fixed once a device run has
	/// started — reordering invalidates any index the user noted to resume past a crash.
	/// </summary>
	public static class AllCases
	{
		public static void Register(SpikeCaseList list)
		{
			// Ported corpus: 161 cases, a one-to-one port of Assets/Tests/Compiler/.
			ArithmeticAndOperatorCases.Register(list);   // 38
			NumericPromotionCases.Register(list);        // 29
			IndexerAndCollectionCases.Register(list);    // 25
			MethodAndTypeRegistrationCases.Register(list); // 21
			LinqCases.Register(list);                    // 15
			ControlFlowCases.Register(list);             // 11
			ErrorReportingCases.Register(list);          // 11
			ScopingRegressionCases.Register(list);       // 10
			PositionListCases.Register(list);            // 1

			// Adversarial families, ordered by how likely they are to find something.
			ValueTypeGenericCases.Register(list);
			HighArityDelegateCases.Register(list);
			BoxingAndPromotionCases.Register(list);
			NestingAndClosureCases.Register(list);
		}
	}
}
