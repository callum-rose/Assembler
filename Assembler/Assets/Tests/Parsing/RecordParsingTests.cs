using System.Linq;
using Assembler.Deserialisation;
using Assembler.Libraries;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class RecordParsingTests
	{
		private const string ItemSchema = @"
Records:
  Item:
    templateId: { Type: string }
    count:      { Type: int,   Default: 1 }
    durability: { Type: float, Default: 1.0 }
";

		private static GameInfo Transform(string yaml) => Transformer.Transform(new GameFileParser().Parse(yaml));

		[Test]
		public void RecordsSection_ParsesFieldTypesAndDefaults()
		{
			var info = Transform(ItemSchema + @"
Constants:
  marker: 0
");

			var schema = info.ParseContext.RecordSchemas.Get("Item");

			Assert.AreEqual(3, schema.Fields.Count);

			var templateId = schema.Fields.Single(f => f.Name == "templateId");
			Assert.AreEqual(typeof(string), templateId.ClrType);

			var count = schema.Fields.Single(f => f.Name == "count");
			Assert.AreEqual(typeof(int), count.ClrType);
			Assert.AreEqual(1, count.Default);

			var durability = schema.Fields.Single(f => f.Name == "durability");
			Assert.AreEqual(typeof(float), durability.ClrType);
		}

		[Test]
		public void RecordLiteral_FillsUnsetFieldsWithDefaults()
		{
			var info = Transform(ItemSchema + @"
Constants:
  potion: !record { Type: Item, templateId: potion }
");

			var value = info.Variables.Single(v => v.Id == "potion").Value;
			Assert.IsInstanceOf<RecordValue>(value);

			var record = RecordValues.ToRecord((RecordValue)value);
			Assert.AreEqual("potion", RecordMath.GetString(record, "templateId"));
			Assert.AreEqual(1, RecordMath.GetInt(record, "count"));
			Assert.AreEqual(1f, RecordMath.GetFloat(record, "durability"));
		}

		[Test]
		public void RecordLiteral_OverridesDefaultsWhenProvided()
		{
			var info = Transform(ItemSchema + @"
Constants:
  potion: !record { Type: Item, templateId: potion, count: 5 }
");

			var record = RecordValues.ToRecord((RecordValue)info.Variables.Single(v => v.Id == "potion").Value);
			Assert.AreEqual(5, RecordMath.GetInt(record, "count"));
		}

		[Test]
		public void RecordLiteral_UnknownFieldThrows()
		{
			var ex = Assert.Throws<ParsingException>(() => Transform(ItemSchema + @"
Constants:
  bad: !record { Type: Item, bogus: 5 }
"));

			StringAssert.Contains("bogus", ex.Message);
		}

		[Test]
		public void RecordLiteral_TypeMismatchThrows()
		{
			Assert.Throws<ParsingException>(() => Transform(ItemSchema + @"
Constants:
  bad: !record { Type: Item, count: ""not an int"" }
"));
		}

		[Test]
		public void EmptyRecordList_ProducesTypedListOfRecord()
		{
			var info = Transform(ItemSchema + @"
Variables:
  inventory: !record []
");

			var value = info.Variables.Single(v => v.Id == "inventory").Value;
			Assert.IsInstanceOf<TypedListValue>(value);

			var typed = (TypedListValue)value;
			Assert.AreEqual(typeof(Record), typed.ElementType);
			Assert.AreEqual(0, typed.Items.Count);
		}

		[Test]
		public void RecordList_ElementsAreCompletedWithDefaults()
		{
			var info = Transform(ItemSchema + @"
Variables:
  inventory: !record [ { Type: Item, templateId: coin, count: 3 }, { Type: Item, templateId: key } ]
");

			var typed = (TypedListValue)info.Variables.Single(v => v.Id == "inventory").Value;
			Assert.AreEqual(2, typed.Items.Count);

			var coin = RecordValues.ToRecord((RecordValue)typed.Items[0]);
			Assert.AreEqual(3, RecordMath.GetInt(coin, "count"));

			var key = RecordValues.ToRecord((RecordValue)typed.Items[1]);
			Assert.AreEqual("key", RecordMath.GetString(key, "templateId"));
			Assert.AreEqual(1, RecordMath.GetInt(key, "count"));
		}
	}
}
