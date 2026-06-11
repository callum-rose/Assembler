namespace Assembler.Resolving.Behaviours
{
	public sealed class CursorLockData : BehaviourData
	{
		public IValueProvider<bool> Locked { get; }
		public IValueProvider<bool> Visible { get; }

		public CursorLockData(string id, IValueProvider<bool> locked, IValueProvider<bool> visible) :
			base(id) => (Locked, Visible) = (locked, visible);
	}
}
