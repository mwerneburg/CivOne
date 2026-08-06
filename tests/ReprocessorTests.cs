// CivOne tests
//
// The machines' mission. The Thing wants off the planet, the Registry wants the planet, and
// Skynet wants it uninhabitable — three endings, one clock each, none of them the same clock.
//
// The Reprocessor does not end the game. It changes the board: the climate turns against
// everything that has to breathe while the faction driving it is indifferent to the result.
// It leans on machinery that already exists (WarmingIndicator, HandleGlobalWarming,
// HurricaneCheck) rather than inventing a second climate system.
//
// REVERSIBLE by design: Game.ReprocessorActive is recomputed from whether a living Skynet
// still holds the city, not latched at completion. That is the part with teeth, and the part
// that would silently rot into a latch if nobody pinned it.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class ReprocessorTests
	{
		private static (Game, Player machine, Player human) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player m = new Player(Common.Civilizations.First(c => c is Skynet));
			g.AddPlayer(m);
			foreach (IAdvance a in Common.Advances) m.AddAdvance(a, false);
			return (g, m, g.HumanPlayer);
		}

		private static City ACity(Player owner, int x, int y = 25)
		{
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(x, y, range: 3);
			City c = g.AddCity(owner, x + y, x, y)!;
			c.Size = 6;
			return c;
		}

		// ── whose mission it is ─────────────────────────────────────────────

		[Fact]
		public void OnlyTheMachinesMayBuildIt()
		{
			(Game g, Player machine, Player human) = AWorld();
			foreach (IAdvance a in Common.Advances) human.AddAdvance(a, false);

			Assert.True(machine.ProductionAvailable(new TheReprocessor()));
			Assert.False(human.ProductionAvailable(new TheReprocessor()));
		}

		// It is the mission, so it outranks the materiel in their production plan.
		[Fact]
		public void TheMachinesReachForItFirst()
		{
			(Game g, Player machine, Player human) = AWorld();
			City c = ACity(machine, 40);

			IProduction[] plan = AI.Instance(machine).ProductionPlan(c);
			IWonder? firstWonder = plan.OfType<IWonder>().FirstOrDefault();

			Assert.True(firstWonder is TheReprocessor,
				$"the machines reached for {(firstWonder as ICivilopedia)?.Name ?? "nothing"} first");
		}

		// ── what it does ────────────────────────────────────────────────────

		// The defect it exists to create: the sky reads badly over a world burning nothing.
		[Fact]
		public void TheAirTurnsOverACleanWorld()
		{
			(Game g, Player machine, Player human) = AWorld();
			City c = ACity(machine, 40);
			Assert.Equal(0, g.WarmingIndicator);   // nothing is polluted

			c.AddWonder(new TheReprocessor());
			g.GameTurn++;                          // the reading is cached per turn

			Assert.True(g.ReprocessorActive);
			Assert.True(g.WarmingIndicator >= 3,
				$"the machines are working the sky, but it reads {g.WarmingIndicator}");
		}

		// A floor, not an addition: a genuinely filthy world already reads worse and keeps its
		// own number, so this cannot push the indicator past its own maximum.
		[Fact]
		public void ItNeverPushesTheAirPastTheMaximum()
		{
			(Game g, Player machine, Player human) = AWorld();
			City c = ACity(machine, 40);
			c.AddWonder(new TheReprocessor());
			foreach (ITile t in Map.Instance.AllTiles()) t.Pollution = true;
			g.GameTurn++;

			Assert.Equal(4, g.WarmingIndicator);
		}

		// ── reversibility, which is the whole design ────────────────────────

		// Take the city and the air stops getting worse.
		[Fact]
		public void TakingTheCityStopsIt()
		{
			(Game g, Player machine, Player human) = AWorld();
			City c = ACity(machine, 40);
			c.AddWonder(new TheReprocessor());
			g.GameTurn++;
			Assert.True(g.ReprocessorActive);

			c.Owner = g.PlayerNumber(human);
			g.GameTurn++;

			Assert.False(g.ReprocessorActive, "the machines no longer hold it");
			Assert.Equal(0, g.WarmingIndicator);
		}

		// Razing it works too — the wonder goes with the city.
		[Fact]
		public void DestroyingTheCityStopsIt()
		{
			(Game g, Player machine, Player human) = AWorld();
			City c = ACity(machine, 40);
			c.AddWonder(new TheReprocessor());
			g.GameTurn++;
			Assert.True(g.ReprocessorActive);

			g.DestroyCity(c);
			g.GameTurn++;

			Assert.False(g.ReprocessorActive);
		}

		// ...and it is genuinely two-way: hand it back and the sky turns again. A latch set at
		// completion would pass every test above and fail this one.
		[Fact]
		public void ItIsNotALatch()
		{
			(Game g, Player machine, Player human) = AWorld();
			City c = ACity(machine, 40);
			c.AddWonder(new TheReprocessor());
			g.GameTurn++;
			Assert.True(g.ReprocessorActive);

			c.Owner = g.PlayerNumber(human);
			g.GameTurn++;
			Assert.False(g.ReprocessorActive);

			c.Owner = g.PlayerNumber(machine);
			g.GameTurn++;
			Assert.True(g.ReprocessorActive, "the machines have it back");
		}

		// What has already drowned stays drowned — the process stops, the damage does not undo.
		// Stated as a test so nobody later reads "reversible" as "harmless".
		[Fact]
		public void TheDamageAlreadyDoneIsNotUndone()
		{
			(Game g, Player machine, Player human) = AWorld();
			City c = ACity(machine, 40);
			c.AddWonder(new TheReprocessor());
			g.GlobalWarmingCount = 4;
			g.GameTurn++;

			c.Owner = g.PlayerNumber(human);
			g.GameTurn++;

			Assert.False(g.ReprocessorActive);
			Assert.Equal(4, g.GlobalWarmingCount);
		}
	}
}
