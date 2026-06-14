namespace Assembler.Behaviours.Spawners
{
	/// <summary>
	/// The despawn counterpart to <see cref="IEntitySpawner"/>: where the spawner creates an entity from a
	/// template, the sink disposes one. <c>DestroyBehaviour</c> routes through this rather than calling Unity's
	/// <c>Destroy</c> directly so the factory can transparently return a pooled entity's shell to its pool
	/// instead of destroying it. A non-pooled entity (one not produced by <c>Spawn</c>) is really destroyed.
	/// </summary>
	public interface IEntitySink
	{
		void Despawn(GameEntity entity);
	}

	/// <summary>Marker for a behaviour that needs the <see cref="IEntitySink"/> injected by the build pipeline,
	/// mirroring <see cref="INeedsSpawner"/>.</summary>
	public interface INeedsEntitySink
	{
		IEntitySink Sink { get; set; }
	}
}
