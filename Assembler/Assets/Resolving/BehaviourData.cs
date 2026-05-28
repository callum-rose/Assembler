namespace Assembler.Resolving
{
	public abstract class BehaviourData
	{
		public string Id { get; }

		protected BehaviourData(string id)
		{
			Id = id;
		}
	}

}
