using System;
using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Libraries;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class RecordResolvingTests
	{
		private static RecordValue Item(string templateId, int count, float durability = 1f) =>
			new("Item", new Dictionary<string, AssemblerValue>
			{
				["templateId"] = new StringValue(templateId),
				["count"] = new IntValue(count),
				["durability"] = new FloatValue(durability)
			});

		private static TypedListValue Inventory(params RecordValue[] items) =>
			new(typeof(Record), items);

		// ---- VariableRegistry ----------------------------------------------------

		[Test]
		public void VariableRegistry_BuildsRecordListProviderWithFieldsAndDefaults()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("inv", Inventory(Item("coin", 3), Item("key", 1))));

			var records = registry.Get<List<Record>>("inv").Get();

			Assert.AreEqual(2, records.Count);
			Assert.AreEqual("coin", RecordMath.GetString(records[0], "templateId"));
			Assert.AreEqual(3, RecordMath.GetInt(records[0], "count"));
			Assert.AreEqual(1f, RecordMath.GetFloat(records[0], "durability"));
			Assert.AreEqual("key", RecordMath.GetString(records[1], "templateId"));
		}

		[Test]
		public void Record_MutationViaSetInt_IsReflectedInTheList()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("inv", Inventory(Item("potion", 2))));

			var records = registry.Get<List<Record>>("inv").Get();

			// Fetch an instance, mutate it in place, and confirm the list still holds the same instance.
			RecordMath.SetInt(records[0], "count", RecordMath.GetInt(records[0], "count") - 1);

			Assert.AreEqual(1, RecordMath.GetInt(registry.Get<List<Record>>("inv").Get()[0], "count"));
		}

		[Test]
		public void RecordConstant_ResolvesToRecordProvider()
		{
			var registry = new VariableRegistry();
			registry.Register(new ValueInfo("coin kind", Item("coin", 1)));

			var record = registry.Get<Record>("coin kind").Get();

			Assert.AreEqual("coin", RecordMath.GetString(record, "templateId"));
		}

		// ---- compiler integration (Step-6 expressions) ---------------------------

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Expr(string id, string returnType, string body,
			IReadOnlyList<(string type, string name)>? args = null) =>
			new(id, args ?? Array.Empty<(string, string)>(), returnType,
				Array.Empty<string>(), Array.Empty<string>(), body);

		private static List<Record> SampleInventory() => new()
		{
			RecordValues.ToRecord(Item("potion", 2)),
			RecordValues.ToRecord(Item("potion", 3)),
			RecordValues.ToRecord(Item("key", 1))
		};

		[Test]
		public void Expression_CountsItemsOfAKind()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("potion count", "int",
					"return inv.Where(r => GetString(r,\"templateId\")==\"potion\").Sum(r => GetInt(r,\"count\"));",
					new[] { ("record list", "inv") })
			});

			var func = (Func<List<Record>, int>)registry.GetCompiled("potion count").@delegate;

			Assert.AreEqual(5, func(SampleInventory()));
		}

		[Test]
		public void Expression_IndexerStyleCountMatchesAccessorStyle()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("potion count", "int",
					"return inv.Where(r => (string)r[\"templateId\"]==\"potion\").Sum(r => (int)r[\"count\"]);",
					new[] { ("record list", "inv") })
			});

			var func = (Func<List<Record>, int>)registry.GetCompiled("potion count").@delegate;

			Assert.AreEqual(5, func(SampleInventory()));
		}

		[Test]
		public void Expression_HasAtLeastN()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("has keys", "bool",
					"return inv.Where(r => GetString(r,\"templateId\")==\"key\").Sum(r => GetInt(r,\"count\")) >= n;",
					new[] { ("record list", "inv"), ("int", "n") })
			});

			var func = (Func<List<Record>, int, bool>)registry.GetCompiled("has keys").@delegate;

			Assert.IsTrue(func(SampleInventory(), 1));
			Assert.IsFalse(func(SampleInventory(), 2));
		}

		[Test]
		public void Expression_ConsumesOneInPlaceAndReturnsSameList()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[]
			{
				Expr("consume potion", "record list",
					"var item = inv.First(r => GetString(r,\"templateId\")==\"potion\");" +
					"item[\"count\"] = (int)item[\"count\"] - 1;" +
					"return inv;",
					new[] { ("record list", "inv") })
			});

			var func = (Func<List<Record>, List<Record>>)registry.GetCompiled("consume potion").@delegate;

			var inventory = SampleInventory();
			var result = func(inventory);

			Assert.AreSame(inventory, result);
			Assert.AreEqual(1, RecordMath.GetInt(inventory[0], "count"));
		}
	}
}
