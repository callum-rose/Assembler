namespace Assembler.Parsing.Phase2.Info
{
	public record BehaviourDescriptor
	{
		public BehaviourDescriptor(string EntityId, string BehaviourId)
		{
			this.EntityId = EntityId;
			this.BehaviourId = BehaviourId;
		}

		public string EntityId { get; init; }
		public string BehaviourId { get; init; }

		public void Deconstruct(out string EntityId, out string BehaviourId)
		{
			EntityId = this.EntityId;
			BehaviourId = this.BehaviourId;
		}
	}
}