namespace Assembler.Parsing.Info
{
	public abstract record AssetInfo(string Id, string Path);
	
	public record SpriteAssetInfo(string Id, string Path) : AssetInfo(Id, Path);
}