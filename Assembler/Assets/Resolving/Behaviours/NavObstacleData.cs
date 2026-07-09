namespace Assembler.Resolving.Behaviours
{
	public sealed class NavObstacleData : BehaviourData
	{
		public IValueProvider<bool> Blocked { get; }

		public NavObstacleData(string id, IValueProvider<bool> blocked) :
			base(id) => Blocked = blocked;
	}
}
