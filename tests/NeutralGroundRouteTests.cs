// CivOne tests
//
// A combat unit at peace may not set foot on land a foreign city works (BaseUnitLand.
// ValidMoveTarget), and the GoTo planner did not know it. It routed armies through a
// neutral's fields; the step was refused; the unit kept its order and asked again next turn,
// for ever. Measured on the d721724c save: nineteen English Chariots and Knights stood at
// (216,79) in Indian country for the whole of a thirty-turn replay, every one handed a step
// beside an Indian city that MoveTo would not take. The Lakota army "wedged at the Guarani
// border" was the same rule.

using System.Drawing;
using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class NeutralGroundRouteTests
	{
		private static (Game g, IUnit armor, City neutral) AnArmyBesideANeutral()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 15; y <= 35; y++)
			for (int x = 28; x <= 52; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player[] ps = g.Players.Where(p => p is not null && g.PlayerNumber(p) != 0).ToArray();
			Player us = ps[0], them = ps[1];
			foreach (Player p in new[] { us, them }) p.Explore(40, 25, range: 12);
			City neutral = g.AddCity(them, 0, 40, 25)!;
			neutral.Size = 10;
			neutral.ResetResourceTiles();
			Assert.False(us.IsAtWar(them), "fixture: they must be at peace");
			IUnit armor = g.CreateUnit(UnitType.Armor, 36, 25, g.PlayerNumber(us))!;
			Assert.True(new[] { 38, 39 }.Any(x => g.IsWorkedByOther(x, 25, armor.Owner)), "fixture: the straight line is not worked");
			Sim.ClearTasks();
			return (g, armor, neutral);
		}

		[Fact]
		public void EveryStepOfTheRouteIsOneTheUnitMayTake()
		{
			(Game _, IUnit armor, City _) = AnArmyBesideANeutral();
			Point goal = new Point(44, 25);

			for (int i = 0; i < 40 && (armor.X, armor.Y) != (goal.X, goal.Y); i++)
			{
				ITile? next = Common.GotoStep(armor, goal.X, goal.Y);
				Assert.NotNull(next);
				Assert.True(armor.MoveTargets.Any(t => t.X == next!.X && t.Y == next.Y),
					$"routed onto ({next!.X},{next.Y}), worked by a neutral city");
				armor.X = next.X; armor.Y = next.Y;
			}

			Assert.Equal((goal.X, goal.Y), (armor.X, armor.Y));
		}

		// A route laid at war is not walked on through the fields after the peace: the cached
		// plan is checked against the same rule as a fresh one.
		[Fact]
		public void ARoutePlannedAtWarIsReplannedAtPeace()
		{
			(Game g, IUnit armor, City neutral) = AnArmyBesideANeutral();
			Player us = g.GetPlayer(armor.Owner);
			us.DeclareWar(neutral.Player);
			ITile first = Common.GotoStep(armor, 44, 25)!;
			Assert.Equal((37, 25), (first.X, first.Y));   // the straight line, through the fields
			us.MakePeace(neutral.Player);
			Sim.ClearTasks();
			Assert.False(us.IsAtWar(neutral.Player), "fixture: peace was not made");
			armor.X = first.X; armor.Y = first.Y;

			ITile? next = Common.GotoStep(armor, 44, 25);

			Assert.True(armor.MoveTargets.Any(t => t.X == next!.X && t.Y == next.Y),
				$"the old plan walked on to ({next!.X},{next.Y})");
		}

		// Civilians may cross at peace (ValidMoveTarget lets them), so their straight line stands.
		[Fact]
		public void ADiplomatStillTakesTheStraightLine()
		{
			(Game g, IUnit armor, City _) = AnArmyBesideANeutral();
			IUnit spy = g.CreateUnit(UnitType.Diplomat, 36, 25, armor.Owner)!;

			ITile? next = Common.GotoStep(spy, 44, 25);

			Assert.Equal((37, 25), (next!.X, next.Y));
		}

		// At war the fields are open, and the straight line is taken.
		[Fact]
		public void AtWarTheFieldsAreOpen()
		{
			(Game g, IUnit armor, City neutral) = AnArmyBesideANeutral();
			g.GetPlayer(armor.Owner).DeclareWar(neutral.Player);
			Sim.ClearTasks();

			ITile? next = Common.GotoStep(armor, 44, 25);

			Assert.Equal(37, next!.X);
			Assert.Equal(25, next.Y);
		}
	}
}
