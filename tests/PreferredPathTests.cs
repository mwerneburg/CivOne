// CivOne tests
//
// Authored victory inclinations per civilization, on top of the derived scoring.
//
// Doctrine's numbers gave every large empire the same answer. Measured across two complete
// 750-turn runs: Commerce took four to six civs each time and Culture took NONE — the
// arithmetic made it unreachable, since Commerce beat it unless ExpansionAppetite fell below
// 0.625 and almost no leader is that low. A world where the big civs all want the same thing
// is not a world with characters in it.
//
// So the preference is a BIAS, not an override: worth a bonus in the scoring, which a civ
// still abandons when circumstances say otherwise. Diaspora before the SETI signal scores
// zero and stays zero — no amount of inclination buys a spaceship that does not exist yet.
//
// Six civilizations are deliberately unassigned and stay fully derived, so the world keeps
// emergent variety alongside the authored kind.

using System.Linq;
using CivOne.Civilizations;

namespace CivOne.Tests
{
	public class PreferredPathTests
	{
		// Takes the TYPE, not an instance: constructing a civilization touches the palette, so
		// it has to happen after Sim.NewGame has registered a runtime. Passing an instance as
		// an argument evaluated it before the fixture existed, and every case died in
		// Common.GetPalette256.
		private static string PathOf(System.Type civType, bool signal)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			g.SETISignalReceived = signal;
			var civ = (ICivilization)System.Activator.CreateInstance(civType)!;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			// Swap the civilization under a live player: the preference is keyed on the
			// civilization, and staging eighteen real games would be a different test.
			// Civilization is get-only over a readonly field, so the field is what gets set.
			typeof(Player).GetField("_civilization",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.SetValue(p, civ);
			// A middling empire — big enough to have options, small enough that nothing is
			// forced. The point is which way the civ LEANS from an even position.
			for (int i = 0; i < 6; i++)
			{
				Map.Instance.ChangeTileType(34 + i, 25, CivOne.Enums.Terrain.Grassland1);
				g.AddCity(p, (byte)i, 34 + i, 25)!.Size = 6;
			}
			return typeof(AI).GetProperty("Path",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.GetValue(AI.Instance(p))!.ToString()!;
		}

		[Theory]
		[InlineData(typeof(Russian))]
		[InlineData(typeof(Maori))]
		[InlineData(typeof(Greek))]
		public void TheScientistsReachForTheStars(System.Type civ)
			=> Assert.Equal("Diaspora", PathOf(civ, signal: true));

		[Theory]
		[InlineData(typeof(Mongol))]
		[InlineData(typeof(Zulu))]
		[InlineData(typeof(Aztec))]
		[InlineData(typeof(Haida))]
		[InlineData(typeof(English))]
		public void TheConquerorsWantConquest(System.Type civ)
			=> Assert.Equal("Conquest", PathOf(civ, signal: true));

		[Theory]
		[InlineData(typeof(Indian))]
		[InlineData(typeof(Japanese))]
		[InlineData(typeof(Persian))]
		[InlineData(typeof(Khmer))]
		[InlineData(typeof(Babylonian))]
		[InlineData(typeof(Frank))]
		public void TheCulturesWantCulture(System.Type civ)
			=> Assert.Equal("Culture", PathOf(civ, signal: true));

		[Theory]
		[InlineData(typeof(Malian))]
		[InlineData(typeof(Arab))]
		[InlineData(typeof(Iroquois))]
		[InlineData(typeof(Chinese))]
		public void TheTradersWantCommerce(System.Type civ)
			=> Assert.Equal("Commerce", PathOf(civ, signal: true));

		// The bias must not be able to buy a spaceship that does not exist yet. Before the
		// signal Diaspora scores zero, and no bonus applies to zero hard enough to win.
		[Theory]
		[InlineData(typeof(Russian))]
		[InlineData(typeof(Maori))]
		[InlineData(typeof(Greek))]
		public void EvenAScientistWaitsForTheSignal(System.Type civ)
			=> Assert.NotEqual("Diaspora", PathOf(civ, signal: false));

		// The unassigned six keep deriving their own answer. Asserted as "it chose something
		// coherent" rather than a specific path — the whole point of leaving them out is that
		// the answer follows from doctrine and circumstance, and pinning it here would make
		// this test a copy of the scoring function.
		[Theory]
		[InlineData(typeof(Egyptian))]
		[InlineData(typeof(Ethiopian))]
		[InlineData(typeof(Guarani))]
		[InlineData(typeof(Lakota))]
		[InlineData(typeof(Ottoman))]
		[InlineData(typeof(Roman))]
		public void TheUnassignedStillChooseForThemselves(System.Type civ)
		{
			string path = PathOf(civ, signal: true);

			Assert.Contains(path, new[] { "Endurance", "Conquest", "Commerce", "Culture", "Diaspora" });
		}

		// Culture was unreachable before this change — zero civs across two full runs. This is
		// the regression guard for the arithmetic, independent of any one civilization.
		[Fact]
		public void CultureIsReachableAtAll()
		{
			Assert.Equal("Culture", PathOf(typeof(Japanese), signal: true));
		}

		// The derived scoring must produce VARIETY, which is what the Conquest rescale is for.
		//
		// Conquest was WarAppetite * 100 plus up to +80, putting it between 45 and 231 while
		// every other path scored 30-80. Any civ with a WarAppetite above ~0.7 chose Conquest
		// whatever else it was good at — and most leaders are above that — so the unassigned
		// civilizations would all have collapsed onto the same answer, which is exactly the
		// uniformity the authored preferences were introduced to fix.
		//
		// Asserted on the six unassigned civs together rather than one by one: the claim is
		// about the spread, and pinning each civ's answer would just re-encode the arithmetic.
		// Measured on the current numbers they split four ways — Endurance, Commerce,
		// Diaspora, Conquest — so a floor of three leaves room to tune without churn here.
		[Fact]
		public void TheDerivedScoringDoesNotCollapseOntoOnePath()
		{
			string[] paths = new[]
			{
				typeof(Egyptian), typeof(Ethiopian), typeof(Guarani),
				typeof(Lakota), typeof(Ottoman), typeof(Roman),
			}.Select(t => PathOf(t, signal: true)).ToArray();

			Assert.True(paths.Distinct().Count() >= 3,
				$"the unassigned civs collapsed onto {paths.Distinct().Count()} path(s): {string.Join(", ", paths)}");
		}
	}
}
