// CivOne tests
//
// Rule 4 of docs/cursed_wonders.md: "Every cursed wonder's civilopedia entry carries one
// quiet warning sentence, so the curse is retrospectively fair."
//
// It was 4 of 12. The four wonders written AFTER the design doc (The Internet, The Portal,
// Stonehenge, Nanobot Factory) carry their warning on page 1. Manhattan and the Lighthouse
// carried one on page 2, which is unreachable with "Civipedia Text" off — Civilopedia.cs
// remaps page 2 back to page-1 text and PageCount is 2, so there is no way to turn to it.
// The remaining six — Pyramids, Shakespeare's Theatre, Newton's College, the Great Wall,
// the Cure for Cancer and Angkor Wat — said nothing at all.
//
// PAGE ONE IS THE ASSERTION. A warning nobody can turn to is the same as no warning, and
// that is exactly how two of these failed while looking fine in the source.
//
// The concept page added alongside this deliberately names none of them: the whole point of
// rule 4 is that the player is surprised, then finds the line that told them. A roster in
// the reference book replaces that with a lookup table.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CivOne;
using CivOne.Concepts;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class CursedWonderFlavourTests
	{
		// The roster, each with a phrase from the warning that has to be readable on page 1.
		// Pinned deliberately: this is prose, so a reword should be a decision somebody makes
		// on purpose and reflects here, not something that silently drifts to nothing.
		private static readonly (string wonder, string phrase)[] Warnings =
		{
			("Pyramids",             "They are aligned to something"),
			("GreatWall",            "tells those outside it exactly where"),
			("Lighthouse",           "answered from below"),
			("Oracle",               "asked only what you would want answered"),
			("ShakespearesTheatre",  "will perform anything that is put in front"),
			("IsaacNewtonsCollege",  "Most of what he wrote was not"),
			("CureForCancer",        "The trials were stopped early"),
			("ManhattanProject",     "wakes something that was sleeping"),
			("Stonehenge",           "how to keep them shut"),
			("ThePortal",            "expect a great many things"),
			("TheInternet",          "mostly constructive"),
			("NanobotFactory",       "mathematically proven to hold"),
		};

		private static BaseWonder Build(string typeName)
		{
			Sim.EnsureRuntime();
			return (BaseWonder)Reflect.GetWonders().Single(w => w.GetType().Name == typeName);
		}

		private static string Page(BaseWonder w, byte page) =>
			string.Join(" ", w.GetPageText(page));

		[Theory]
		[MemberData(nameof(Roster))]
		public void EveryCursedWonderWarnsOnPageOne(string wonder, string phrase)
		{
			BaseWonder w = Build(wonder);

			Assert.Contains(phrase, Page(w, 1));
		}

		public static IEnumerable<object[]> Roster =>
			Warnings.Select(x => new object[] { x.wonder, x.phrase });

		// The table has to keep up with the code. Every wonder City.cs rolls a curse for must
		// appear above, so adding a thirteenth cursed wonder fails here until its foreshadow
		// is written.
		//
		// Covers the ten rolled inline in City.cs. The Portal rolls inside Game.OpenPortal and
		// Gozira wakes on the first detonation in BaseUnit.cs rather than at the build, so
		// neither is reachable by this scan — both are pinned in the table by hand instead.
		[Fact]
		public void TheTableCoversEveryWonderCityRollsACurseFor()
		{
			string src = File.ReadAllText(Path.Combine(Sim.RepoRoot(), "src", "City.cs"));
			var blocks = Regex.Split(src, @"wonder is Wonders\.").Skip(1).ToArray();
			Assert.NotEmpty(blocks);

			var rolled = new List<string>();
			foreach (string block in blocks)
			{
				string name = Regex.Match(block, @"^\w+").Value;
				int next = block.IndexOf("wonder is Wonders.");
				string body = next < 0 ? block : block.Substring(0, next);
				if (body.Contains("Settings.Instance.CursedWonders")) rolled.Add(name);
			}
			Assert.NotEmpty(rolled);

			string[] tabled = Warnings.Select(x => x.wonder).ToArray();
			foreach (string name in rolled.Distinct())
				Assert.True(tabled.Contains(name),
					$"{name} can be cursed but carries no foreshadow — see docs/cursed_wonders.md rule 4");
		}

		// ── the concept page ─────────────────────────────────────────────────────

		[Fact]
		public void TheConceptPageIsInTheBook()
		{
			Sim.EnsureRuntime();

			Assert.Contains("Cursed Wonders", Reflect.GetConcepts().Select(c => c.Name));
		}

		// The anti-spoiler property, which is the whole reason the page reads the way it does.
		// Naming even one wonder here starts the roster that rule 4 exists to avoid.
		[Fact]
		public void TheConceptPageNamesNoCursedWonder()
		{
			Sim.EnsureRuntime();
			BaseConcept page = Reflect.GetConcepts().OfType<BaseConcept>()
				.Single(c => c.Name == "Cursed Wonders");
			// LOWERCASED. The book shouts wonder names — "The PYRAMIDS", "the LEVIATHAN" —
			// so a case-sensitive check passes on exactly the spelling a spoiler would
			// actually be written in. This test let "The PYRAMIDS" through until a negative
			// check put it there on purpose.
			string text = string.Join(" ", page.GetPageText(1).Concat(page.GetPageText(2)))
				.ToLowerInvariant();

			foreach (string name in new[]
			{
				"Pyramids", "Great Wall", "Lighthouse", "Angkor Wat", "Shakespeare",
				"Newton", "Cure for Cancer", "Manhattan", "Stonehenge", "Portal",
				"Internet", "Nanobot", "Gozira", "Leviathan", "Grey Goo",
			})
				Assert.DoesNotContain(name.ToLowerInvariant(), text);
		}

		// ...and it still says the thing a player actually needs: a curse ends.
		[Fact]
		public void TheConceptPagePromisesAnEndState()
		{
			Sim.EnsureRuntime();
			BaseConcept page = Reflect.GetConcepts().OfType<BaseConcept>()
				.Single(c => c.Name == "Cursed Wonders");
			string text = string.Join(" ", page.GetPageText(1).Concat(page.GetPageText(2)));

			Assert.Contains("fight", text);
			Assert.Contains("has an end", text);
		}
	}
}
