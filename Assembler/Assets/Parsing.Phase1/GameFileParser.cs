using Parsing.Phase1.Dtos;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Parsing.Phase1
{
	public class GameFileParser
	{
		private readonly IDeserializer _deserializer = new DeserializerBuilder()
			.WithNamingConvention(NullNamingConvention.Instance)
			.WithTagMapping("!vec", typeof(VecDto))
			.WithTagMapping("!var", typeof(VarRefDto))
			.WithTagMapping("!const", typeof(ConstRefDto))
			.WithTypeConverter(new VecTypeConverter())
			.WithTypeConverter(new VarTypeConverter())
			.WithTypeConverter(new ConstTypeConverter())
			.WithNodeDeserializer(new ObjectNodeDeserializer())
			.Build();

		public GameDto Parse(string yaml) =>
			_deserializer.Deserialize<GameDto>(yaml);
	}
}