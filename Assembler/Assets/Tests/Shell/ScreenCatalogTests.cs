using System.Collections.Generic;
using System.Linq;
using Assembler.Shell.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Shell
{
	/// <summary>
	/// The screen catalog's job: turn an id into a prefab and a title, and say plainly when it cannot.
	/// </summary>
	/// <remarks>
	/// The complaints matter more than the lookups. A catalog is edited by hand, and every way of getting it
	/// wrong — a row pointing at nothing, an id entered twice, an id left out — fails at the moment somebody
	/// taps the button that would have opened it, which is the worst possible time to find out.
	/// </remarks>
	public class ScreenCatalogTests
	{
		private readonly List<Object> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var created in _created)
			{
				if (created != null)
				{
					Object.DestroyImmediate(created);
				}
			}

			_created.Clear();
		}

		[Test]
		public void FindsTheRowForAnId()
		{
			var view = Screen();
			var catalog = Catalog(Row(ScreenId.Detail, view, "The Edition"));

			Assert.AreSame(view, catalog.Find(ScreenId.Detail)?.View);
			Assert.AreEqual("The Edition", catalog.TitleOf(ScreenId.Detail));
		}

		[Test]
		public void FallsBackToTheIdWhenNothingNamesIt()
		{
			var catalog = Catalog(Row(ScreenId.Detail, Screen(), string.Empty));

			Assert.AreEqual("Detail", catalog.TitleOf(ScreenId.Detail));
			Assert.AreEqual("Archive", catalog.TitleOf(ScreenId.Archive), "an absent row should still name itself");
		}

		[Test]
		public void ComplainsAboutARowThatNamesNoPrefab()
		{
			var catalog = Catalog(Row(ScreenId.Feed, null, "Front Page"));

			Assert.IsTrue(Complains(catalog, "Feed names no prefab"));
		}

		[Test]
		public void ComplainsAboutADuplicatedId()
		{
			var catalog = Catalog(
				Row(ScreenId.Feed, Screen(), "Front Page"),
				Row(ScreenId.Feed, Screen(), "Front Page Again"));

			Assert.IsTrue(Complains(catalog, "Feed appears 2 times"));
		}

		// The one a full catalog still fails: an id added in code and never given a row is invisible until
		// something pushes it.
		[Test]
		public void ComplainsAboutAnIdWithNoRowAtAll()
		{
			var catalog = Catalog(Row(ScreenId.Feed, Screen(), "Front Page"));

			Assert.IsTrue(Complains(catalog, "Settings has no row"));
		}

		[Test]
		public void IsQuietWhenEveryScreenIsAccountedFor()
		{
			var rows = System.Enum
				.GetValues(typeof(ScreenId))
				.Cast<ScreenId>()
				.Select(id => Row(id, Screen(), id.ToString()))
				.ToArray();

			CollectionAssert.IsEmpty(Catalog(rows).Validate());
		}

		private static bool Complains(ScreenCatalog catalog, string fragment)
		{
			return catalog.Validate().Any(complaint => complaint.Contains(fragment));
		}

		private ScreenCatalog Catalog(params ScreenCatalog.Entry[] entries)
		{
			var catalog = ScriptableObject.CreateInstance<ScreenCatalog>();
			_created.Add(catalog);

			return ShellReflection.Set(catalog, "entries", entries);
		}

		private static ScreenCatalog.Entry Row(ScreenId id, ScreenView? view, string title)
		{
			var entry = new ScreenCatalog.Entry();
			ShellReflection.Set(entry, "id", id);
			ShellReflection.Set(entry, "view", view);
			ShellReflection.Set(entry, "title", title);

			return entry;
		}

		private StubScreen Screen()
		{
			var host = new GameObject("StubScreen");
			_created.Add(host);

			return host.AddComponent<StubScreen>();
		}

		private sealed class StubScreen : ScreenView
		{
		}
	}
}
