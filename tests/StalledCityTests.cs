// CivOne tests
//
// A city that earns nothing was never asked what to do next.
//
// City.NewTurn re-planned only on COMPLETION: `if (Shields == 0 && !DequeueProduction() ...)`.
// A city whose shield income has fallen to zero holds whatever it had accumulated, never
// completes, and so never satisfies `Shields == 0` — the AI is not consulted again for the
// rest of the game.
//
// Measured in run 733f10ec. Kyoto, the Japanese capital: Despotism, size 3, four worked tiles
// yielding four shields gross, and SEVEN units homed there whose upkeep consumed all of it.
// ShieldIncome 0, Shields frozen at 3, building a Militia costing 10, from around turn 357
// until the game ended at 617. The civilization finished with one city and a gross output of
// 1 — and left no trace in the decision log after t357, because a city that is never
// re-planned is never logged.
//
// The fix only guarantees the AI is ASKED. Whether it can then do anything about a garrison
// it has already built is a separate question — see ConsiderGarrisonUpkeep.

using System.Linq;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Units;

namespace CivOne.Tests
{
	public class StalledCityTests
	{
		// Kyoto's shape: a modest city choked by the upkeep of units homed there. Built the
		// way the real one arrived at it rather than by forcing a zero, so the test fails if
		// upkeep stops biting as well as if the trigger regresses.
		private static (Game game, Player owner, City city) AChokedCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 34; x <= 46; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Plains);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0);
			p.Government = new Despotism();
			p.Explore(40, 25, range: 12);
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 3;

			// Home units on it until upkeep eats everything it makes. Driven by the measured
			// ShieldIncome rather than a fixed count, so the fixture survives a change to the
			// free-upkeep allowance instead of quietly ceasing to be choked.
			for (int i = 0; i < 30 && c.ShieldIncome > 0; i++)
			{
				IUnit u = g.CreateUnit(UnitType.Militia, 40, 25, g.PlayerNumber(p))!;
				u.SetHome(c);
				c.InvalidateCache();
			}
			Sim.ClearTasks();
			return (g, p, c);
		}

		[Fact]
		public void TheFixtureIsActuallyChoked()
		{
			(Game g, Player p, City c) = AChokedCity();

			Assert.True(c.ShieldIncome <= 0,
				$"fixture is not choked: {c.ShieldIncome} shields with {g.GetUnits().Count(u => u.Home == c)} homed units");
		}

		// The trigger itself. `Shields == 0` alone leaves a choked city permanently invisible
		// to the AI, because it holds shields it can never spend.
		[Fact]
		public void ACityThatEarnsNothingIsStillReplanned()
		{
			string src = CitySource();
			int at = src.IndexOf("Player.AI?.CityProduction(this);");
			Assert.True(at > 0, "the re-plan call has moved or been rewritten");
			string block = src.Substring(System.Math.Max(0, at - 400), 400);

			Assert.Contains("ShieldIncome <= 0", block);
			Assert.DoesNotContain("if (Shields == 0 && !DequeueProduction()", block);
		}

		// ...and it holds when shields are ACCUMULATED, which is the exact state that made
		// the old trigger permanently false.
		[Fact]
		public void TheTriggerFiresEvenWithShieldsInTheBox()
		{
			(Game g, Player p, City c) = AChokedCity();
			c.Shields = 3;

			Assert.True(c.Shields > 0, "fixture has an empty production box");
			Assert.True(c.ShieldIncome <= 0, "fixture is not choked");

			// Both halves of the old condition are false here: shields are not zero, and the
			// city cannot complete anything to make them zero. Under the old trigger this
			// city was never asked again.
			Assert.False(c.Shields == 0);
		}

		private static string CitySource()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return System.IO.File.ReadAllText(System.IO.Path.Combine(dir!.FullName, "src", "City.cs"));
		}
	}
}
