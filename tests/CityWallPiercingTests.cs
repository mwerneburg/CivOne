// CivOne tests
//
// Who shoots past City Walls, and why it is no longer a number.
//
// The rule was written as `attackUnit.Attack == 12` in the two places DefendStrength applies
// the wall bonus — the attack VALUE standing in for the rule. That worked only as long as
// exactly the intended units happened to have attack 12, and it stopped being true the moment
// the Cruise Missile was given a bomber's punch: it silently acquired wall-piercing nobody had
// chosen, and any future unit assigned a 12 would have too.
//
// Now an explicit IgnoresCityWalls on the three units that mean it. These tests are the reason
// the property is worth having: they name the units, so adding a fourth is a decision rather
// than an arithmetic coincidence.

using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CityWallPiercingTests
	{
		private static (Game g, Player p, City c) AWalledCity()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			p.Explore(40, 25, range: 12);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 45; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City c = g.AddCity(p, 0, 40, 25)!;
			c.Size = 6;
			c.AddBuilding(new CityWalls());
			c.InvalidateCache();
			Sim.ClearTasks();
			return (g, p, c);
		}

		private static int DefenceAgainst(IUnit attacker, IUnit defender)
			=> (int)typeof(BaseUnit).GetMethod("DefendStrength",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
				.Invoke(attacker, new object[] { defender, attacker })!;

		// A LAND defender behind walls, attacked by a land unit. The air units are excluded
		// from this comparison deliberately: DefendStrength returns early for an air attacker
		// against a land defender (the SAM branch), so the wall rule is not on their path at
		// all — theirs is the property test below.
		[Theory]
		[InlineData(UnitType.Musketeers, false)]   // ordinary infantry: walls hold
		[InlineData(UnitType.Cannon, false)]       // heavy, but not heavy enough
		[InlineData(UnitType.Artillery, true)]     // the gun the rule was written for
		public void WallsHoldExceptAgainstTheGunsThatPierceThem(UnitType attackerType, bool pierces)
		{
			(Game g, Player p, City c) = AWalledCity();
			Player foe = g.Players.First(x => x is not null && x != p && x != g.HumanPlayer
			                               && g.PlayerNumber(x) != 0);
			IUnit attacker = g.CreateUnit(attackerType, 39, 25, g.PlayerNumber(foe))!;
			IUnit defender = g.CreateUnit(UnitType.Musketeers, c.X, c.Y, g.PlayerNumber(p))!;
			defender.Fortify = true;

			int walled = DefenceAgainst(attacker, defender);
			c.RemoveBuilding<CityWalls>();
			c.InvalidateCache();
			int open = DefenceAgainst(attacker, defender);

			if (pierces)
				Assert.Equal(open, walled);
			else
				Assert.True(walled > open, $"the walls did nothing: {open} without, {walled} with");
		}

		// The roster, stated by name. Adding a fourth unit here should be a decision somebody
		// makes on purpose.
		[Theory]
		[InlineData(UnitType.Artillery, true)]
		[InlineData(UnitType.Bomber, true)]
		[InlineData(UnitType.CruiseMissile, true)]
		[InlineData(UnitType.Cannon, false)]
		[InlineData(UnitType.Catapult, false)]
		[InlineData(UnitType.Musketeers, false)]
		[InlineData(UnitType.ReaperDrone, false)]
		[InlineData(UnitType.Nuclear, false)]
		public void OnlyTheseUnitsIgnoreCityWalls(UnitType type, bool expected)
		{
			(Game g, Player p, City c) = AWalledCity();
			IUnit unit = g.CreateUnit(type, 39, 25, g.PlayerNumber(p))!;

			Assert.Equal(expected, unit.IgnoresCityWalls);
		}

		// The rule must not be keyed on the attack value again. A pure refactor cannot be
		// caught by behaviour — the whole point is that behaviour is unchanged — so this pins
		// the shape instead.
		[Fact]
		public void TheRuleIsNotAMagicNumber()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			string src = System.IO.File.ReadAllText(System.IO.Path.Combine(
				dir!.FullName, "src", "Units", "BaseUnit.cs"));

			Assert.DoesNotContain("Attack == 12", src);
			Assert.Contains("attackUnit.IgnoresCityWalls", src);
		}
	}
}
