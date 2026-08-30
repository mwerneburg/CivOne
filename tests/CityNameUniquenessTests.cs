// CivOne tests
//
// Two cities could end up with the same name, by three routes.
//
// The pool is indexed and CityNameId hands out unused INDICES, which is not the same rule as
// unused names: eleven names appear twice in the canonical list under different civilizations
// — Antioch is Roman and Greek, Thebes Greek and Egyptian, Hastings English and Maori, Basra
// Arab and Mongol — so two civilizations can each draw an index of their own and arrive at the
// same word, with nobody renaming anything.
//
// The other two routes hand AddCity an index that is already on the map, bypassing that filter
// entirely: CityNameId returns 0 when a player has skipped past the end of the pool, and
// FoundOlvirCity wraps modulo the Olvir's own list once the colony outgrows it.
//
// And the human can simply type a name that is already there.
//
// A city has no handle on it but its name — the city report, WLTK notifications and every
// trade-route partner list identify it that way — so two of them are ambiguous everywhere.

using System.Collections.Generic;
using System.Linq;
using CivOne.Civilizations;
using CivOne.Enums;

namespace CivOne.Tests
{
	public class CityNameUniquenessTests
	{
		private static Game AWorld()
		{
			Sim.NewGame(width: 80, height: 50, competition: 4);
			Game g = Game.Instance;
			for (int y = 10; y <= 40; y++)
			for (int x = 10; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			g.HumanPlayer.Explore(35, 25, range: 40);
			Sim.ClearTasks();
			return g;
		}

		// The first pair of indices in the pool that spell the same name, skipping the blocks
		// belonging to the barbarians and the story factions — those are reserved and filtered
		// out of an ordinary civilization's choices for a different reason, which would make a
		// test built on them pass without saying anything about this one.
		private static (int first, int second, string name) TwinNames()
		{
			var reserved = new HashSet<int>();
			int at = 0;
			foreach (ICivilization c in Common.Civilizations)
			{
				bool skip = c is Civilizations.Barbarian or Civilizations.Olvir
				         or Civilizations.TheOthers or Civilizations.TheThing
				         or Civilizations.Skynet;
				for (int i = 0; i < c.CityNames.Length; i++, at++)
					if (skip) reserved.Add(at);
			}

			string[] pool = Common.AllCityNames.ToArray();
			var seen = new Dictionary<string, int>();
			for (int i = 0; i < pool.Length; i++)
			{
				if (reserved.Contains(i)) continue;
				if (seen.TryGetValue(pool[i], out int first)) return (first, i, pool[i]);
				seen[pool[i]] = i;
			}
			throw new System.InvalidOperationException("no repeated name in the pool");
		}

		// The premise. If the canonical list ever loses its repeats this whole file is testing
		// a condition that cannot arise, and it should say so rather than pass quietly.
		[Fact]
		public void ThePoolReallyDoesRepeatItself()
		{
			(int first, int second, string name) = TwinNames();

			Assert.True(second > first);
			Assert.Equal(Common.AllCityNames.ElementAt(first), Common.AllCityNames.ElementAt(second));
			Assert.False(string.IsNullOrWhiteSpace(name));
		}

		// The route that needs no rename and no exhausted pool: one civilization's Antioch is
		// founded, and the other civilization's own separate index for Antioch is refused.
		[Fact]
		public void TheTwinIndexIsRefused()
		{
			Game g = AWorld();
			(int first, int second, string name) = TwinNames();
			Player p = g.HumanPlayer;
			City one = g.AddCity(p, first, 20, 20)!;
			Sim.ClearTasks();

			City two = g.AddCity(p, second, 30, 30)!;
			Sim.ClearTasks();

			Assert.Equal(name, one.Name);
			Assert.NotEqual(one.Name, two.Name);
		}

		// The two index fallbacks, in the shape they take: an index that is already on the map
		// handed straight to AddCity. CityNameId's `return 0` and the Olvir's modulo wrap both
		// arrive here.
		[Fact]
		public void AnIndexAlreadyOnTheMapIsRefused()
		{
			Game g = AWorld();
			Player p = g.HumanPlayer;
			int nameId = g.CityNameId(p);
			City one = g.AddCity(p, nameId, 20, 20)!;
			Sim.ClearTasks();

			City two = g.AddCity(p, nameId, 30, 30)!;
			Sim.ClearTasks();

			Assert.NotEqual(one.Name, two.Name);
		}

		// ...and index 0 specifically, since that is the literal the fallback returns.
		[Fact]
		public void TheZeroFallbackDoesNotDuplicate()
		{
			Game g = AWorld();
			Player p = g.HumanPlayer;
			City one = g.AddCity(p, 0, 20, 20)!;
			Sim.ClearTasks();

			City two = g.AddCity(p, 0, 30, 30)!;
			Sim.ClearTasks();

			Assert.NotEqual(one.Name, two.Name);
		}

		// The guard substitutes a name; it must not refuse the founding. A settler that walks
		// twenty turns to a site and then silently fails to build is far worse than a repeat.
		[Fact]
		public void TheCityIsStillFounded()
		{
			Game g = AWorld();
			Player p = g.HumanPlayer;
			g.AddCity(p, 0, 20, 20);
			Sim.ClearTasks();

			City two = g.AddCity(p, 0, 30, 30)!;
			Sim.ClearTasks();

			Assert.NotNull(two);
			Assert.Equal(30, two.X);
			Assert.False(string.IsNullOrWhiteSpace(two.Name));
		}

		// The offer itself, walked end to end: whatever a player has already skipped, the name
		// CityNameId comes back with is never one that is already on the map. This is the check
		// on the filter rather than on AddCity's guard behind it — the twin index sits deep in
		// another civilization's block, so it only surfaces once the player has skipped that far.
		[Fact]
		public void TheNameOfferedIsNeverOneAlreadyOnTheMap()
		{
			Game g = AWorld();
			(int first, int second, string name) = TwinNames();
			Player p = g.HumanPlayer;
			g.AddCity(p, first, 20, 20);
			Sim.ClearTasks();

			var offered = new List<string>();
			for (int skipped = 0; skipped < g.CityNames.Length; skipped++)
			{
				p.CityNamesSkipped = skipped;
				offered.Add(g.CityNames[g.CityNameId(p)]);
			}
			p.CityNamesSkipped = 0;

			Assert.DoesNotContain(name, offered);
		}

		// The check reads living cities. A razed city releases its name — the pool is finite and
		// a long game would otherwise strand names on cities that no longer exist.
		[Fact]
		public void ARazedNameIsFreeAgain()
		{
			Game g = AWorld();
			Player p = g.HumanPlayer;
			City one = g.AddCity(p, 0, 20, 20)!;
			string name = one.Name;
			Sim.ClearTasks();
			g.DestroyCity(one);
			Sim.ClearTasks();

			City two = g.AddCity(p, 0, 30, 30)!;
			Sim.ClearTasks();

			Assert.Equal(name, two.Name);
		}

		// Case is not a difference worth keeping two cities apart on.
		[Fact]
		public void TheCheckIgnoresCase()
		{
			Game g = AWorld();
			City one = g.AddCity(g.HumanPlayer, 0, 20, 20)!;
			Sim.ClearTasks();

			Assert.True(g.CityNameTaken(one.Name.ToUpperInvariant()));
			Assert.True(g.CityNameTaken(one.Name.ToLowerInvariant()));
			Assert.False(g.CityNameTaken(one.Name + " II"));
		}

		// The third route: the player types a name that is already on the map. The dialog is a
		// screen that needs a live display, so the refusal is pinned at the source — without it
		// CityNameAccept writes the typed string into the shared name table and founds on it.
		[Fact]
		public void TheNamingDialogRefusesADuplicate()
		{
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				Sim.RepoRoot(), "src", "Tasks", "Orders.cs"));
			int at = src.IndexOf("private void CityNameAccept");
			Assert.True(at > 0, "the naming dialog's accept handler has moved");
			string block = src.Substring(at, src.IndexOf("private void CityNameCancel", at) - at);

			// Refused BEFORE the name is written into the table and the city founded.
			Assert.Contains("Game.CityNameTaken(value)", block);
			Assert.True(block.IndexOf("Game.CityNameTaken(value)") < block.IndexOf("CreateCity(nameId)"));
			Assert.Contains("duplicate: true", block);
		}
	}
}
