// CivOne tests
//
// Starlab, the orbital that takes the Interstellar Probe's slot at SPACE FLIGHT.
//
// This file covers step 2 only: that the wonder exists, is reachable, and that its
// Civilopedia entry carries the one fact a player can act on. The effects themselves
// (trade, science, storm demotion, the free-port corruption) arrive in later steps and
// get their own tests.

using System.IO;
using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class StarlabTests
	{
		private static Starlab Book()
		{
			Sim.EnsureRuntime();
			// Through the registry, not `new Starlab()`: Common.Wonders is built by
			// reflection, so a wonder can compile perfectly and still never appear in the
			// game. That is the failure this resolves.
			return Reflect.GetWonders().OfType<Starlab>().Single();
		}

		private static string Pages(Starlab w) =>
			string.Join(" ", w.GetPageText(1).Concat(w.GetPageText(2)));

		[Fact]
		public void StarlabIsInTheRegistry()
		{
			Starlab w = Book();

			Assert.Equal("Starlab", w.Name);
			Assert.Equal((byte)Wonder.Starlab, w.Id);
		}

		// Its id must not collide with an existing wonder's, because ProductionId is derived
		// from it and a collision would make two wonders the same build order.
		[Fact]
		public void NoTwoWondersShareAnId()
		{
			Sim.EnsureRuntime();
			var ids = Reflect.GetWonders().Select(w => w.Id).ToArray();

			Assert.Equal(ids.Length, ids.Distinct().Count());
		}

		[Fact]
		public void StarlabIsBuiltAtSpaceFlight()
		{
			Assert.IsType<SpaceFlight>(Book().RequiredTech);
		}

		// The actionable fact, and the only one: Starlab's quality is not luck, it is a
		// verdict on the builder (Game.AssessCharacter). A player who does not know that
		// cannot steer it, and steering it is the whole mechanic — so the page has to say
		// so in words, and keep saying so through any rewrite of the prose around them.
		//
		// Pinned on the three levers the rubric actually reads and a player can actually
		// pull. Government and war are on the page as "free, peaceful" against "despotism
		// at war"; pollution as the air they breathe.
		[Fact]
		public void ThePageSaysTheBuilderDecidesWhatGetsBuilt()
		{
			string text = Pages(Book());

			Assert.Contains("depends on who builds", text);
			Assert.Contains("despotism at war", text);
			Assert.Contains("clean air", text);
			// ...and names the bad end, so the player knows what they are steering away from.
			Assert.Contains("CORRUPTION", text);
		}

		// Rule 4 of docs/cursed_wonders.md, applied by analogy: the warning has to be on
		// PAGE ONE, because page 2 is unreachable with "Civipedia Text" off (Civilopedia.cs
		// remaps it back to page-1 text). Starlab is not a cursed wonder — there is no
		// one-in-four roll — but a player can still end up with the seedy station, and the
		// foreshadow is worthless if they cannot turn to it.
		[Fact]
		public void TheWarningIsReadableOnPageOne()
		{
			Assert.Contains("keeps their habits", string.Join(" ", Book().GetPageText(1)));
		}

		// A missing PNG degrades silently to the sprite-sheet icon, so demand the file.
		// Same guard as MissionControlTests.TheArtIsShipped and HarbourTests.
		[Fact]
		public void TheArtIsShipped()
		{
			string path = Path.Combine(Sim.RepoRoot(), "runtime", "sdl", "Resources",
				"defaults", "data", "improvement_art", "starlab.png");

			Assert.True(File.Exists(path), $"starlab art is missing: {path}");
		}
	}
}
