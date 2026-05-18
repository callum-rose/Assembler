using Assembler.Deserialisation.Dtos;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Assembler.Deserialisation
{
	public class GameFileParser
	{
		private readonly IDeserializer _deserializer = new DeserializerBuilder()
			.WithNamingConvention(NullNamingConvention.Instance)
			.WithTagMapping("!vec", typeof(VecDto))
			.WithTagMapping("!colour", typeof(ColourDto))
			.WithTagMapping("!var", typeof(VarRefDto))
			.WithTagMapping("!expr", typeof(ExprRefDto))
			.WithTagMapping("!parameter", typeof(ParamRefDto))
			.WithTagMapping("!asset", typeof(AssetRefDto))
			.WithTagMapping("!output", typeof(OutputRefDto))
			.WithTypeConverter(new VecTypeConverter())
			.WithTypeConverter(new ColourTypeConverter())
			.WithTypeConverter(new VarTypeConverter())
			.WithTypeConverter(new ExprTypeConverter())
			.WithTypeConverter(new ParamTypeConverter())
			.WithTypeConverter(new AssetTypeConverter())
			.WithTypeConverter(new OutputTypeConverter())
			.WithNodeDeserializer(new ObjectNodeDeserializer())
			.Build();

		public GameDto Parse(string yaml) => _deserializer.Deserialize<GameDto>(yaml);
	}
}