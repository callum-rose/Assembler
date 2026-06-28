using Assembler.Deserialisation.Dtos;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization.NodeDeserializers;

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
			.WithTagMapping("!entity", typeof(EntityRefDto))
			.WithTagMapping("!query", typeof(EntityQueryRefDto))
			.WithTagMapping("!rigidbody", typeof(RigidbodyRefDto))
			.WithTagMapping("!clock", typeof(ClockRefDto))
			.WithTagMapping("!gameover", typeof(GameOverListenerDto))
			.WithTagMapping("!text", typeof(TextRefDto))
			.WithTagMapping("!record", typeof(RecordLiteralDto))
			// The scalar-element tags have no DTO/converter — ObjectNodeDeserializer parses them inline
			// (both the `!int 5` scalar and `!int [ … ]` typed-list forms). They still need a tag mapping
			// or YamlDotNet rejects the tag as unresolved before that deserializer runs; mapping to object
			// keeps the node object-typed so it reaches ObjectNodeDeserializer rather than a type converter.
			.WithTagMapping("!int", typeof(object))
			.WithTagMapping("!float", typeof(object))
			.WithTagMapping("!bool", typeof(object))
			.WithTagMapping("!string", typeof(object))
			.WithTypeConverter(new VecTypeConverter())
			.WithTypeConverter(new ColourTypeConverter())
			.WithTypeConverter(new VarTypeConverter())
			.WithTypeConverter(new ExprTypeConverter())
			.WithTypeConverter(new ParamTypeConverter())
			.WithTypeConverter(new AssetTypeConverter())
			.WithTypeConverter(new OutputTypeConverter())
			.WithTypeConverter(new EntityTypeConverter())
			.WithTypeConverter(new EntityQueryTypeConverter())
			.WithTypeConverter(new RigidbodyTypeConverter())
			.WithTypeConverter(new ClockTypeConverter())
			.WithTypeConverter(new GameOverListenerTypeConverter())
			.WithTypeConverter(new ListenerTypeConverter())
			.WithTypeConverter(new TextTypeConverter())
			.WithTypeConverter(new RecordTypeConverter())
			.WithTypeConverter(new BindingTypeConverter())
			.WithNodeDeserializer(
				new ObjectNodeDeserializer(),
				where => where.Before<TypeConverterNodeDeserializer>())
			.Build();

		public GameDto Parse(string yaml) => _deserializer.Deserialize<GameDto>(yaml);
	}
}
