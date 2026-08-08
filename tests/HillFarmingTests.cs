// CivOne tests
//
// The AI could mine a hill but never farm one. Settlers.BuildIrrigation accepts Hills,
// Hills.Food reads Irrigation (1 -> 2), the human's auto-improve admits them, and
// TileExtensions.AllowIrrigation lists them — every path except AI.Strategy.WorkAvailable,
// which is the only one an AI settler takes.
//
// A hill yields 1 food; a citizen eats 2. A city founded on hills gets an auto-irrigated
// centre tile (Game.AddCity), so it reaches size 2 and stops: centre 2 + two hills at 1 is
// 4 food for 4 mouths, forever. Measured over 750 turns on the relief-based Earth map, where
// hills went from 6.9% to 26.5% of land:
//
//     Khmer     35 cities -> 2, mean size 1.9      Lakota   55 -> 72 cities
//     Japanese  13 cities -> 1, mean size 4.0      Russians 40 -> 43 cities
//     Persians  35 cities -> 0
//
// The three strangled civs have mountainous homelands; the two that grew are on flat ground.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class HillFarmingTests
	{
		// A hill country with a river down one side, so some hills have a cardinal water
		// source and the dry interior does not.
		private static (Game game, Player ai, City city) AHillCountry()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Hills);
			for (int y = 20; y <= 30; y++)
				Map.Instance.ChangeTileType(39, y, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Explore(40, 25, range: 20);
			City c = g.AddCity(ai, 0, 41, 25)!;
			Sim.ClearTasks();
			return (g, ai, c);
		}

		// The defect, stated directly: a hill beside water is farm work.
		[Fact]
		public void AHillBesideWaterIsFarmWork()
		{
			var (_, ai, _) = AHillCountry();
			ITile hill = Map.Instance[40, 24];      // cardinal neighbour of the river at (39,24)
			Assert.IsType<Hills>(hill);

			Assert.True(AI.Instance(ai).WorkAvailable(hill).Irrigation,
				"the AI can mine this hill but has never been able to farm it");
		}

		// ...and a dry upland is not. The water-source requirement is what keeps this from
		// turning every hill on a 26%-hill map into farmland.
		[Fact]
		public void ADryUplandIsStillMineCountry()
		{
			var (_, ai, _) = AHillCountry();
			ITile hill = Map.Instance[50, 30];      // no river, lake or irrigation in reach
			var work = AI.Instance(ai).WorkAvailable(hill);

			Assert.False(work.Irrigation);
			Assert.True(work.Mine, "with no water it should still be worth mining");
		}

		// Food beats shields when both are available on the same hill — the whole point for a
		// civ that cannot grow past size 2.
		//
		// The tile is roaded first because a FIRST road outranks everything by design (see the
		// roadFirst comment in ChooseSettlerImprovement: connectivity is finite work). Without
		// that, this asserts the road rule rather than the arbitration it means to test — the
		// first draft did exactly that and read "Road".
		[Fact]
		public void FoodBeatsShieldsOnAFarmableHill()
		{
			var (g, ai, _) = AHillCountry();
			Map.Instance[40, 24].Road = true;
			IUnit settler = g.CreateUnit(UnitType.Settlers, 40, 24, g.PlayerNumber(ai))!;
			Sim.ClearTasks();

			var work = AI.Instance(ai).WorkAvailable(settler.Tile);
			Assert.True(work.Irrigation && work.Mine, "scenario: both are on the table here");

			Assert.Equal("Irrigation", AI.Instance(ai).TestSettlerPlanAt(settler));
		}

		// The counterpart: on a dry hill, where food is not on the table, it still mines.
		// Without this the change could be "always irrigate" and nothing would notice.
		[Fact]
		public void ADryHillIsStillMined()
		{
			var (g, ai, _) = AHillCountry();
			Map.Instance[50, 30].Road = true;
			IUnit settler = g.CreateUnit(UnitType.Settlers, 50, 30, g.PlayerNumber(ai))!;
			Sim.ClearTasks();

			Assert.Equal("Mine", AI.Instance(ai).TestSettlerPlanAt(settler));
		}

		// The early game is the only part the strangled civs ever reached, so it has to work
		// under Despotism — where the tile penalty blocks irrigating grassland but not hills,
		// because a hill's bonus lives in ITile.Food rather than the government-gated branch.
		[Fact]
		public void ItWorksUnderDespotism()
		{
			var (_, ai, _) = AHillCountry();
			ai.Government = new Despotism();
			ITile hill = Map.Instance[40, 24];

			Assert.True(AI.Instance(ai).WorkAvailable(hill).Irrigation);
			Assert.False(AI.Instance(ai).DespotBlocksIrrigation(hill),
				"a despot CAN usefully farm a hill; that is the escape hatch");
		}

		// Routing, not just eligibility: a settler with no work underfoot must be sent to the
		// farmable hill. Eligibility without routing is the bug this file's cousin fixed.
		[Fact]
		public void ASettlerIsRoutedToTheFarmableHill()
		{
			var (g, ai, _) = AHillCountry();
			IUnit settler = g.CreateUnit(UnitType.Settlers, 42, 25, g.PlayerNumber(ai))!;
			Sim.ClearTasks();

			ITile? site = AI.Instance(ai).BestImproveSite(settler);

			Assert.NotNull(site);
			Assert.True(AI.Instance(ai).WorkAvailable(site!).Irrigation,
				$"routed to ({site!.X},{site.Y}), which is not farmable");
		}

		// The payoff, in the engine rather than the AI: an irrigated hill really does feed its
		// own worker. If this ever stopped being true the whole change would be pointless.
		[Fact]
		public void AnIrrigatedHillFeedsItsWorker()
		{
			AHillCountry();
			ITile hill = Map.Instance[40, 24];
			Assert.Equal((sbyte)1, hill.Food);

			hill.Irrigation = true;

			Assert.Equal((sbyte)2, hill.Food);
		}
	}
}
