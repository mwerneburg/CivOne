// CivOne tests
//
// The unit-destruction animation ran its full ten-tick countdown for EVERY combat death in
// the world — BaseUnit.Confront inserts it without asking whose war it is — and `onScreen`
// guarded only the drawing, never whether the player had explored the tile. Two consequences:
//
//   cost   8,789 paced samples in 32 turns of a war-heavy save, most of it drawing nothing
//   leak   explosions played on fogged ground, announcing battles the player could not see
//
// The countdown is load-bearing: Game.DisbandUnit runs when it reaches zero. So the fix
// skips the FRAMES, never the death — which is what these pin.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Units;

namespace CivOne.Tests
{
	public class DestroyAnimationTests
	{
		private static (Game, Player, IUnit) AUnitToKill(int x, int y)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(q => q is not null && g.PlayerNumber(q) != 0
			                             && q != g.HumanPlayer);
			IUnit victim = g.CreateUnit(UnitType.Militia, x, y, g.PlayerNumber(p))!;
			Sim.ClearTasks();
			return (g, p, victim);
		}

		// The death must happen even when no frame is ever drawn — this is the half that
		// breaks if the animation is simply suppressed.
		[Fact]
		public void AnUnseenDeathStillKillsTheUnit()
		{
			(Game g, Player p, IUnit victim) = AUnitToKill(70, 40);
			Assert.False(g.HumanPlayer.Visible(70, 40), "the test tile must be fogged");
			Assert.Contains(g.GetUnits(), u => u == victim);

			Assert.True(CivOne.Screens.DestroyUnit.ResolveIfUnseen(victim, false),
				"an unwatchable death should resolve without a screen");
			Assert.DoesNotContain(g.GetUnits(), u => u == victim);
		}

		// A fogged tile is not watchable however the camera happens to be pointed. This is
		// the intel leak: onScreen alone said yes here.
		[Fact]
		public void AFoggedTileIsNotWatchable()
		{
			(Game g, Player p, IUnit victim) = AUnitToKill(70, 40);
			Assert.False(g.HumanPlayer.Visible(70, 40));
			Assert.False(CivOne.Screens.DestroyUnit.CanBeSeen(victim));
		}

		// Stacked units on an open tile die together; the shortcut shares the rule with the
		// animated path rather than reimplementing it.
		[Fact]
		public void AnUnseenStackDiesTogether()
		{
			(Game g, Player p, IUnit victim) = AUnitToKill(70, 40);
			IUnit second = g.CreateUnit(UnitType.Militia, 70, 40, g.PlayerNumber(p))!;

			CivOne.Screens.DestroyUnit.ResolveIfUnseen(victim, true);

			Assert.DoesNotContain(g.GetUnits(), u => u == victim);
			Assert.DoesNotContain(g.GetUnits(), u => u == second);
		}

		// ...but a stack inside a city does not: only the defender falls.
		[Fact]
		public void AStackInACityLosesOnlyTheDefender()
		{
			(Game g, Player p, IUnit victim) = AUnitToKill(70, 40);
			p.Explore(70, 40, range: 2);
			g.AddCity(p, 0, 70, 40);
			IUnit second = g.CreateUnit(UnitType.Militia, 70, 40, g.PlayerNumber(p))!;

			CivOne.Screens.DestroyUnit.ResolveIfUnseen(victim, true);

			Assert.DoesNotContain(g.GetUnits(), u => u == victim);
			Assert.Contains(g.GetUnits(), u => u == second);
		}
	}
}
