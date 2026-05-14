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
			.WithTagMapping("!var", typeof(VarRefDto))
			.WithTagMapping("!const", typeof(ConstRefDto))
			.WithTagMapping("!expr", typeof(ExprRefDto))
			.WithTagMapping("!parameter", typeof(ParamRefDto))
			.WithTypeConverter(new VecTypeConverter())
			.WithTypeConverter(new VarTypeConverter())
			.WithTypeConverter(new ConstTypeConverter())
			.WithTypeConverter(new ExprTypeConverter())
			.WithTypeConverter(new ParamTypeConverter())
			.WithNodeDeserializer(new ObjectNodeDeserializer())
			.Build();

		public GameDto Parse(string yaml) =>
			_deserializer.Deserialize<GameDto>(yaml);
	}
}