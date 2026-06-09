namespace Assembler.Generation.Verification
{
	public interface IGeneratorLogger
	{
		void Log(string message);
	}

	/// <summary>No-op logger, used in place of a null <see cref="IGeneratorLogger"/>.</summary>
	public sealed class NullGeneratorLogger : IGeneratorLogger
	{
		public static readonly NullGeneratorLogger Instance = new();

		private NullGeneratorLogger() { }

		public void Log(string message) { }
	}
}
