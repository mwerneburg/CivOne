// CivOne tests
//
// The tile under the city.
//
// It is the one tile a city is guaranteed to work — from its first turn, for free, forever —
// and the only one it can never reassign. SiteSuitability counted it as one of twenty-one, so
// a barren centre inside a good ring scored like a good site, and the founder's own legality
// test (CanFoundOn) only ever refused Arctic and Mountains.
//
// Salt Flat is the case that exposed it: 0 food, and not irrigable at any point in the game.
// ShieldValue and TradeValue floor the centre at 1 apiece, but there is no food floor, so such
// a city starts at zero food from its own tile and stays there. Across two 2200 AD runs the
// Malians founded 1 city on Salt Flat and then 8 — the rise almost certainly caused by routing
// settlers off Mountains, which had to send that traffic somewhere.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CitySiteCentreTests
	{
		private static (Game g, Player p) AWorld(Terrain ring)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Explore(40, 25, range: 15);
			for (int y = 15; y <= 35; y++)
			for (int x = 30; x <= 55; x++)
			{
				Map.Instance.ChangeTileType(x, y, ring);
				((BaseTile)Map.Instance[x, y]).Special = false;
			}
			Map.Instance.RecalculateContinentsIfDirty();
			Sim.ClearTasks();
			return (g, p);
		}

		// ── the feed gate ─────────────────────────────────────────────────────────

		[Theory]
		[InlineData(Terrain.SaltFlat, false)]
		[InlineData(Terrain.Arctic,   false)]
		[InlineData(Terrain.Desert,   true)]   // 0 food today, but irrigates to 1
		[InlineData(Terrain.Tundra,   true)]   // 1 food as it stands
		[InlineData(Terrain.Grassland1, true)]
		public void OnlyASiteThatCanEverEatCounts(Terrain terrain, bool expected)
		{
			AWorld(Terrain.Grassland1);
			Map.Instance.ChangeTileType(40, 25, terrain);
			((BaseTile)Map.Instance[40, 25]).Special = false;

			Assert.Equal(expected, AI.CentreCanFeed(Map.Instance[40, 25]));
		}

		// Desert is the reason this tests terrain type rather than IrrigationFoodBonus: it
		// reports -2 there and irrigates perfectly well. The sign is a yield modifier on some
		// terrain and a "never" flag on others, so it cannot carry the question.
		[Fact]
		public void DesertIsNotRefusedDespiteItsNegativeIrrigationBonus()
		{
			AWorld(Terrain.Grassland1);
			Map.Instance.ChangeTileType(40, 25, Terrain.Desert);
			ITile desert = Map.Instance[40, 25];

			Assert.True(desert.IrrigationFoodBonus < 0, "fixture: Desert should still report a negative bonus");
			Assert.True(AI.CentreCanFeed(desert));
		}

		// The behaviour that produced the eight cities. A settler WALKS and then founds where
		// it stands, so the founder's own gate is what actually decides this — and a settler
		// sitting on a salt flat with room around it used to found on the spot.
		[Fact]
		public void ASettlerStandingOnASaltFlatDoesNotFound()
		{
			(Game g, Player p) = AWorld(Terrain.Grassland1);
			Map.Instance.ChangeTileType(40, 25, Terrain.SaltFlat);
			((BaseTile)Map.Instance[40, 25]).Special = false;
			Map.Instance.RecalculateContinentsIfDirty();
			// A home city well clear of the site. Without one the civ is CITYLESS, lastChance
			// fires, and it founds on anything — which is the documented exemption, not the
			// rule under test. The first version of this fixture had no city and failed here.
			g.AddCity(p, 0, 50, 25)!.Size = 4;
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(p))!;
			Sim.ClearTasks();

			AI.Instance(p).Move(s);
			Sim.Settle();

			Assert.DoesNotContain(g.GetCities(), c => c.X == 40 && c.Y == 25);
		}

		// ...and on ground that can eat, it still does. The gate is a floor, not a veto on
		// marginal land: desert yields nothing today and irrigates to 1, and desert cities
		// are perfectly normal.
		[Fact]
		public void ASettlerStandingOnDesertStillFounds()
		{
			(Game g, Player p) = AWorld(Terrain.Grassland1);
			Map.Instance.ChangeTileType(40, 25, Terrain.Desert);
			((BaseTile)Map.Instance[40, 25]).Special = false;
			Map.Instance.RecalculateContinentsIfDirty();
			// A home city well clear of the site. Without one the civ is CITYLESS, lastChance
			// fires, and it founds on anything — which is the documented exemption, not the
			// rule under test. The first version of this fixture had no city and failed here.
			g.AddCity(p, 0, 50, 25)!.Size = 4;
			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(p))!;
			Sim.ClearTasks();

			AI.Instance(p).Move(s);
			Sim.Settle();

			Assert.Contains(g.GetCities(), c => c.X == 40 && c.Y == 25);
		}

		// The scan carries the same test as the founder — not because it changes the pick in
		// any ordinary landscape (there is nearly always a better legal tile next door, and
		// the centre weight is what steers away from these sites), but because a scan that can
		// name ground the founder refuses is the disagreement that has cost this project four
		// separate bugs. It is a backstop, and it is deliberately redundant.
		[Fact]
		public void TheScanCarriesTheSameTestAsTheFounder()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "src", "AI.Strategy.cs"));
			int at = src.IndexOf("private ITile? BestSettleSiteWithin");
			Assert.True(at > 0, "the settle scan has moved or been rewritten");
			string body = src.Substring(at, src.IndexOf("\n\t\t}", at) - at);

			// The CALL, not the mention: an earlier version matched "CentreCanFeed" and was
			// satisfied by the comment above it, so deleting the guard left the test green.
			Assert.Contains("!CentreCanFeed(tile)", body);
		}

		// The exemption. A civ down to its last settler is choosing between a poor city and
		// not existing at all, and the AI.cs note on `lastChance` is explicit that the
		// ordinary questions are the wrong ones to ask it. Pinned at the source: staging a
		// genuinely cityless civ and driving it to found takes a whole turn pipeline, and
		// what matters here is that the gate is not on that branch.
		[Fact]
		public void ACivWithNoCitiesIsExemptFromTheFeedGate()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "src", "AI.cs"));

			int at = src.IndexOf("if (validCity && (lastChance");
			Assert.True(at > 0, "the founding gate has moved or been rewritten");
			string line = src.Substring(at, src.IndexOf('\n', at) - at);

			// The feed test must sit inside the ordinary branch, after lastChance's `||`.
			int chance = line.IndexOf("lastChance");
			int feed   = line.IndexOf("CentreCanFeed");
			Assert.True(feed > chance,
				"CentreCanFeed must not gate the cityless last-chance founding");
		}

		// ── the centre weight ─────────────────────────────────────────────────────

		// Two sites with identical rings and different centres are not the same proposition.
		[Fact]
		public void ARicherCentreScoresHigher()
		{
			(Game g, Player p) = AWorld(Terrain.Plains);
			var method = typeof(AI).GetMethod("SiteSuitability",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

			int grass = (int)method.Invoke(AI.Instance(p), new object[] { Map.Instance[40, 25] })!;
			Map.Instance.ChangeTileType(40, 25, Terrain.Desert);
			((BaseTile)Map.Instance[40, 25]).Special = false;
			int desert = (int)method.Invoke(AI.Instance(p), new object[] { Map.Instance[40, 25] })!;

			Assert.True(grass > desert,
				$"a plains centre ({grass}) did not outscore a desert one ({desert}) in the same ring");
		}

		// Food is weighted hardest because it is the only yield with no centre floor: the
		// city always gets a shield and a coin from its own tile, never a guaranteed bite.
		[Fact]
		public void FoodWeighsHeaviestOfTheThree()
		{
			(Game g, Player p) = AWorld(Terrain.Plains);
			var method = typeof(AI).GetMethod("SiteSuitability",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

			// Grassland: 2 food, 0 shield. Hills: 0 food, 2 shield. Same ring either way.
			Map.Instance.ChangeTileType(40, 25, Terrain.Grassland1);
			((BaseTile)Map.Instance[40, 25]).Special = false;
			int food = (int)method.Invoke(AI.Instance(p), new object[] { Map.Instance[40, 25] })!;
			Map.Instance.ChangeTileType(40, 25, Terrain.Hills);
			((BaseTile)Map.Instance[40, 25]).Special = false;
			int shields = (int)method.Invoke(AI.Instance(p), new object[] { Map.Instance[40, 25] })!;

			// The GAP is the assertion, not the sign. The radius pass already counts the
			// centre once, so a food centre edges a shield one by a point or two without any
			// weighting at all — measured at 22 against 21, which is how the first version of
			// this test passed with the weight deleted. Weighted, the same pair reads 34
			// against 27.
			Assert.True(food - shields > 3,
				$"a food centre ({food}) barely outscored a shield centre ({shields}) — the centre is being counted, not weighted");
		}
	}
}
