// CivOne tests
//
// "It seems we need more city names; the Olvir names already appear in human
// civilizations." Two separate faults behind that.
//
// 1. Game.CityNameId walled off the reserved name blocks with a single threshold —
//    everything at or above spareIndex. That index landed at 458 of 474, so it
//    excluded the Machine block and nothing else, purely because Skynet sorts last.
//    Barbarian, Olvir, The Others and The Thing all sat below the line and were free
//    for anyone. A Roman city forty names deep was named Vel'Thara.
//
// 2. Sixteen names per civilization. On a 320x200 map a civ reaches thirty or forty
//    cities, so it exhausts its own block and eats the shared pool regardless.

using System.Collections.Generic;
using System.Linq;
using CivOne;
using CivOne.Civilizations;

namespace CivOne.Tests
{
	public class CityNamingTests
	{
		private static bool IsReserved(ICivilization c)
			=> c is Civilizations.Barbarian or Civilizations.Olvir
			or Civilizations.TheOthers or Civilizations.TheThing or Civilizations.Skynet;

		// Reservation is by POSITION in the flattened array, not by spelling. The Barbarian
		// list is real-world cities and has always shared strings with the civilizations
		// (Mecca, Damascus, Hamburg, Salzburg), so a name-based test would fail on names
		// that are perfectly legitimate for the civ drawing them.
		private static HashSet<int> ReservedIndices()
		{
			var set = new HashSet<int>();
			int at = 0;
			foreach (ICivilization c in Common.Civilizations)
			{
				if (IsReserved(c))
					for (int i = 0; i < c.CityNames.Length; i++) set.Add(at + i);
				at += c.CityNames.Length;
			}
			return set;
		}

		// The mechanism: found cities for one ordinary civ well past its own block and
		// check it is never handed an index belonging to a reserved civilization.
		[Fact]
		public void AnOrdinaryCivilization_NeverDrawsAReservedName()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			HashSet<int> reserved = ReservedIndices();
			Player p = g.Players.First(x => x is not null && g.PlayerNumber(x) != 0
			                             && !IsReserved(x.Civilization));

			// Far enough to exhaust every unreserved name (24 civs x 40 = 960) and keep
			// asking. This count is the point of the test: at 80 draws the reserved blocks
			// are still ~900 indices away and a broken guard looks identical to a good one.
			var leaked = new List<string>();
			for (int i = 0; i < g.CityNames.Length + 50; i++)
			{
				int id = g.CityNameId(p);
				if (reserved.Contains(id)) leaked.Add($"{g.CityNames[id]} (#{id})");
				p.CityNamesSkipped++;
			}

			Assert.True(leaked.Count == 0,
				$"{p.Civilization.Name} was offered reserved names: {string.Join(", ", leaked.Distinct())}");
		}

		// ...and every ordinary civ, not just the one that happens to be first.
		[Fact]
		public void NoOrdinaryCivilization_DrawsAReservedName()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			HashSet<int> reserved = ReservedIndices();

			foreach (Player p in g.Players.Where(x => x is not null && g.PlayerNumber(x) != 0
			                                       && !IsReserved(x.Civilization)))
			{
				for (int i = 0; i < 45; i++)
				{
					int id = g.CityNameId(p);
					Assert.False(reserved.Contains(id),
						$"{p.Civilization.Name} drew reserved name '{g.CityNames[id]}' at city {i + 1}");
					p.CityNamesSkipped++;
				}
			}
		}

		// The reported symptom, stated as itself: the story factions' invented names are
		// unique strings, so if one ever shows up on a human civ's city it is unambiguous.
		[Fact]
		public void NoHumanCivilization_IsEverNamedVelThara()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			var invented = new HashSet<string>(Common.Civilizations
				.Where(c => c is Civilizations.Olvir or Civilizations.TheOthers
				         or Civilizations.TheThing or Civilizations.Skynet)
				.SelectMany(c => c.CityNames));

			foreach (Player p in g.Players.Where(x => x is not null && g.PlayerNumber(x) != 0
			                                       && !IsReserved(x.Civilization)))
			for (int i = 0; i < 45; i++)
			{
				string name = g.CityNames[g.CityNameId(p)];
				Assert.False(invented.Contains(name),
					$"{p.Civilization.Name} city {i + 1} was named '{name}'");
				p.CityNamesSkipped++;
			}
		}

		// A civ's own block still comes first — the fix must not scramble the ordering that
		// makes the Romans found Rome.
		[Fact]
		public void ACivilizationStillGetsItsOwnNamesFirst()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;

			foreach (Player p in g.Players.Where(x => x is not null && g.PlayerNumber(x) != 0))
			{
				string first = g.CityNames[g.CityNameId(p)];
				Assert.Equal(p.Civilization.CityNames[0], first);
			}
		}

		// The Olvir keep their own names — the reservation runs one way.
		[Fact]
		public void TheOlvir_StillGetOlvirNames()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			ICivilization olvir = Common.Civilizations.First(c => c is Civilizations.Olvir);
			Player? p = g.Players.FirstOrDefault(x => x is not null && x.Civilization is Civilizations.Olvir);
			if (p is null) return;   // the Olvir only enter play on contact

			Assert.Contains(g.CityNames[g.CityNameId(p)], olvir.CityNames);
		}

		// The capacity half. An epic map reaches 30-40 cities for a leading civ, and 16
		// names meant the shared pool (and then reserved names) carried the rest.
		[Fact]
		public void EveryOrdinaryCivilization_HasEnoughNamesForAnEpicEmpire()
		{
			Sim.NewGame(width: 80, height: 50);
			foreach (ICivilization c in Common.Civilizations.Where(x => !IsReserved(x)))
				Assert.True(c.CityNames.Length >= 40,
					$"{c.Name} has only {c.CityNames.Length} city names");
		}

		// Names must be distinct within a civilization, or a city is named twice and the
		// "already used" filter silently costs that civ a name.
		[Fact]
		public void NoCivilizationRepeatsAName()
		{
			Sim.NewGame(width: 80, height: 50);
			foreach (ICivilization c in Common.Civilizations)
			{
				var dupes = c.CityNames.GroupBy(n => n).Where(x => x.Count() > 1)
					.Select(x => x.Key).ToArray();
				Assert.True(dupes.Length == 0, $"{c.Name} repeats: {string.Join(", ", dupes)}");
			}
		}

		// The shared pool must actually cover a crowded world: the last run ended with 472
		// cities. Reserved blocks are excluded, so this counts only what is really on offer.
		[Fact]
		public void TheSharedPool_CoversACrowdedWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			int shared = Common.Civilizations.Where(c => !IsReserved(c)).Sum(c => c.CityNames.Length);
			Assert.True(shared >= 500, $"only {shared} unreserved names for a 472-city world");
		}
	}
}
