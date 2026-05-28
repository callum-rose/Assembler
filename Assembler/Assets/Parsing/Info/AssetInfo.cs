namespace Assembler.Parsing.Info
{
	public abstract record AssetInfo(string Id, string Source, string Path);

	public record SpriteAssetInfo(string Id, string Source, string Path) : AssetInfo(Id, Source, Path);

	public record AudioClipAssetInfo(string Id, string Source, string Path) : AssetInfo(Id, Source, Path);

	public record MeshAssetInfo(string Id, string Source, string Path) : AssetInfo(Id, Source, Path);
}
