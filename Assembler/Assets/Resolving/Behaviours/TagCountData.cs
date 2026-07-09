namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Runtime configuration for the <c>tag count</c> behaviour: the entity tag to count, as a read-only provider
	/// (so it can be a constant, a variable, or an expression). The count is emitted as a trigger output rather
	/// than stored here.
	/// </summary>
	public sealed class TagCountData : TriggerData
	{
		public IValueProvider<string> Tag { get; }

		public TagCountData(string id, IValueProvider<string> tag) : base(id) => Tag = tag;
	}
}
