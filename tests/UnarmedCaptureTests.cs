// CivOne tests
//
// An unarmed unit cannot take a city.
//
// Reported from a game, 20 Aug 2026: a city was founded within a tile of an enemy EXPLORER,
// and the next turn the explorer destroyed it. There was no defender — but an explorer has
// attack 0, and a size-1 city that changes hands is razed, so an unarmed scout levelled a
// city the turn after it was founded.
//
// Confront guarded this with a list of type names — Diplomat, Caravan, Settlers,
// HydroEngineer — and the Explorer was simply missing from it, despite having the same
// attack 0 as all four. The guard now asks the unit what its attack is.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class UnarmedCaptureTests
	{
		// A defenceless city of a civ we are at war with, and an enemy unit standing beside it.
		private static (Game game, Player owner, Player enemy, City city, IUnit raider) ACityAndARaider(UnitType type)
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			// NEITHER side is the human. A capture the human can see routes through a
			// CaptureCity/EventArt screen whose Done handler is what actually changes the
			// owner, and headless that screen never completes — so a human-visible capture
			// silently does not happen here. That is a property of the test rig, not of the
			// rule under test.
			Player[] ps = g.Players
				.Where(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer).ToArray();
			Player owner = ps[0], enemy = ps[1];
			foreach (Player p in new[] { owner, enemy })
			{
				p.Government = new Monarchy();
				p.Explore(40, 25, range: 12);
			}

			City city = g.AddCity(owner, 0, 40, 25)!;
			city.Size = 1;
			Assert.Empty(Map.Instance[40, 25].Units);   // no defender, as reported

			g.CreateUnit(type, 41, 25, g.PlayerNumber(enemy), false);
			IUnit raider = g.GetUnits().First(u => u.Owner == g.PlayerNumber(enemy) && u.X == 41 && u.Y == 25);
			enemy.DeclareWar(owner);
			Sim.ClearTasks();
			return (g, owner, enemy, city, raider);
		}

		// The report, stated directly.
		[Fact]
		public void AnExplorerCannotTakeAnUndefendedCity()
		{
			(Game g, Player owner, Player enemy, City city, IUnit explorer) = ACityAndARaider(UnitType.Explorer);
			Assert.Equal(0, explorer.Attack);   // scenario: it is unarmed

			explorer.MoveTo(-1, 0);
			Sim.Settle();

			Assert.Contains(g.GetCities(), c => c.X == 40 && c.Y == 25 && c.Size > 0);
			Assert.Equal(g.PlayerNumber(owner), g.GetCities().First(c => c.X == 40 && c.Y == 25).Owner);
		}

		// Every other unarmed land unit, for the same reason. These were already on the old
		// list; they are here so the rule is pinned by behaviour rather than by a roster.
		[Theory]
		[InlineData((int)UnitType.Settlers)]
		[InlineData((int)UnitType.Diplomat)]
		[InlineData((int)UnitType.Caravan)]
		public void NorCanAnyOtherUnarmedLandUnit(int type)
		{
			(Game g, Player owner, Player enemy, City city, IUnit raider) = ACityAndARaider((UnitType)type);

			raider.MoveTo(-1, 0);
			Sim.Settle();

			Assert.Equal(g.PlayerNumber(owner), g.GetCities().First(c => c.X == 40 && c.Y == 25).Owner);
		}

		// ...and an ARMED one still walks in and takes it. A guard that stopped everybody
		// would break the capture rule instead of fixing it.
		[Fact]
		public void AnArmedUnitStillTakesAnUndefendedCity()
		{
			(Game g, Player owner, Player enemy, City city, IUnit legion) = ACityAndARaider(UnitType.Legion);
			Assert.True(legion.Attack > 0, "scenario: this one is armed");

			// MoveTo returns FALSE here even when the attack succeeds — Confront ends with
			// `GameTask.Insert(Movement); return false;` and the capture happens in the task's
			// Done handler. Asserting on the return value would test the wrong thing.
			legion.MoveTo(-1, 0);
			Sim.Settle();

			// A size-1 city is razed when it changes hands, so either outcome counts as taken:
			// gone from the map, or standing under the new flag.
			City? survivor = g.GetCities().FirstOrDefault(c => c.X == 40 && c.Y == 25 && c.Size > 0);
			Assert.True(survivor is null || survivor.Owner == g.PlayerNumber(enemy),
				"the city is still standing, unharmed, under its original owner");
		}
	}
}
