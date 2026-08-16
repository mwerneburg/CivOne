// CivOne tests
//
// The turn-328 beachball: a 13-civ game ran 327 turns in 80 seconds of turn time, then hung
// with one core pegged. Traced to Player[Khmer].NewTurn -> AI.ConsiderCitizens ->
// City.AutoAssignCitizens, whose first loop never terminated.
//
// The cause is two different accountings of one quantity:
//
//   UpdateSpecialists  counts ResourceTiles  — the PROPERTY, which filters to tiles still
//                                              present in CityTiles
//   SetResourceTile    counts _resourceTiles — the raw list, stale entries included
//
// They disagree by exactly one when the city's OWN CENTRE TILE is not visible to its owner.
// CityRadius drops any tile the owner cannot see (City.cs, `if (!player.Visible(tile))`), so
// an invisible centre falls out of CityTiles, and with it out of the ResourceTiles property.
// A size-6 city with six worked entries then reads:
//
//   filtered = 6  (centre not yielded)  ->  UpdateSpecialists: 6 - (6-1) = 1 idle citizen
//   raw      = 6                        ->  SetResourceTile:   6 >= 6, city is full
//
// AutoAssignCitizens spins between the two. The refusal calls ResetResourceTiles, which
// rebuilds the identical six, so the next pass sees the same idle citizen and the same
// refusal — a fixed point, not a slow convergence.
//
// Observed at turn 328 of a 13-civ game: Khmer, Ctesiphon, size 6, raw 6, filtered 6,
// specialists 1. Two consecutive loop iterations were byte-identical.
//
// The first version of this fixture put a STALE tile in _resourceTiles instead, and every
// test here passed against the unfixed code — ResetResourceTiles heals that shape. Caught by
// the negative check.
//
// Every test here runs under a watchdog. A regression must FAIL, not hang the suite.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class CitizenGovernorHangTests
	{
		private const int WatchdogMs = 15000;

		private static void WithWatchdog(string what, Action body)
		{
			Exception? failure = null;
			Task t = Task.Run(() => { try { body(); } catch (Exception ex) { failure = ex; } });
			Assert.True(t.Wait(WatchdogMs), $"{what} did not return within {WatchdogMs}ms — it is looping");
			if (failure is not null) throw failure;
		}

		// Ctesiphon's shape: Size worked entries and a centre its owner cannot see.
		private static (Game game, City city) ACityBlindToItsOwnCentre()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 32; x <= 48; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0);
			p.Government = new Monarchy();
			p.Explore(40, 25, range: 20);

			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			Sim.ClearTasks();

			var field = typeof(City).GetField("_resourceTiles",
				BindingFlags.NonPublic | BindingFlags.Instance)!;
			var tiles = (System.Collections.Generic.IList<ITile>)field.GetValue(c)!;

			// Swap one worked tile for a tile far outside the city radius. _resourceTiles.Count
			// is unchanged — which is the whole point: the raw count still says "full".
			tiles.Clear();
			foreach ((int dx, int dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1), (-1, -1), (1, 1) })
				tiles.Add(Map.Instance[c.X + dx, c.Y + dy]);

			// Blind the owner to its own centre. This is the whole defect: the centre leaves
			// CityTiles, so the property-based count loses one and the raw count does not.
			var vis = (bool[,])typeof(Player).GetField("_visible",
				BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(p)!;
			vis[c.X, c.Y] = false;

			// Let the game derive the specialists from that list, exactly as it does after a
			// terrain change. This is the step that creates the phantom: the filtered count
			// says one citizen is spare, while the raw count says the city is full.
			UpdateSpecialists(c);

			return (g, c);
		}

		private static void UpdateSpecialists(City c) =>
			typeof(City).GetMethod("UpdateSpecialists",
				BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(c, null);

		// The fixture must actually be inconsistent, or the tests below prove nothing.
		[Fact]
		public void TheFixtureReproducesTheDisagreement()
		{
			(Game g, City c) = ACityBlindToItsOwnCentre();

			var field = typeof(City).GetField("_resourceTiles",
				BindingFlags.NonPublic | BindingFlags.Instance)!;
			int raw = ((System.Collections.Generic.IList<ITile>)field.GetValue(c)!).Count;
			int filtered = c.ResourceTiles.Count();

			Assert.Equal(6, raw);
			Assert.True(raw >= c.Size, "the raw count must read as full");
			Assert.Equal(6, filtered);                  // centre counted ONCE, not added again
			Assert.True(filtered - 1 < c.Size, "the filtered count must leave a citizen spare");

			int specialists = ((System.Collections.IList)typeof(City)
				.GetField("_specialists", BindingFlags.NonPublic | BindingFlags.Instance)!
				.GetValue(c)!).Count;
			Assert.True(specialists > 0, "no specialist — the loop under test would never run");
		}

		// The hang itself. Before the fix this never returns.
		[Fact]
		public void TheGovernorTerminatesOnACityBlindToItsOwnCentre()
		{
			(Game g, City c) = ACityBlindToItsOwnCentre();

			WithWatchdog("AutoAssignCitizens", () => c.AutoAssignCitizens());
		}

		// And through the path the real game took: Player.NewTurn -> AI.ConsiderCitizens.
		[Fact]
		public void APlayerTurnTerminatesOnACityBlindToItsOwnCentre()
		{
			(Game g, City c) = ACityBlindToItsOwnCentre();
			Player p = c.Player;

			WithWatchdog("Player.NewTurn", () => p.NewTurn());
		}

		// The citizen must not simply vanish into a permanent specialist that can never work.
		// Placing them is the point: a phantom citizen is a quieter version of the same bug.
		[Fact]
		public void TheSpareCitizenIsPutBackToWork()
		{
			(Game g, City c) = ACityBlindToItsOwnCentre();

			WithWatchdog("AutoAssignCitizens", () => c.AutoAssignCitizens());

			Assert.Equal(c.Size, c.ResourceTiles.Count() - 1);
		}

		// A healthy city is untouched by the fix — the loop still places specialists normally.
		[Fact]
		public void AHealthyCityStillPlacesItsSpecialists()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 18; y <= 32; y++)
			for (int x = 32; x <= 48; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0);
			p.Government = new Monarchy();
			p.Explore(40, 25, range: 20);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			Sim.ClearTasks();

			var field = typeof(City).GetField("_resourceTiles",
				BindingFlags.NonPublic | BindingFlags.Instance)!;
			var tiles = (System.Collections.Generic.IList<ITile>)field.GetValue(c)!;
			tiles.Clear();   // everyone idle: four specialists to place
			UpdateSpecialists(c);

			WithWatchdog("AutoAssignCitizens", () => c.AutoAssignCitizens());

			Assert.Equal(c.Size, c.ResourceTiles.Count() - 1);
		}
	}
}
