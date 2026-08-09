// CivOne tests
//
// Two scoring incentives, added because the score should have an opinion about how a
// civilization treats the world it lands on:
//
//   -1   per jungle or wetland tile a settler erases. Those two habitats are the ones
//        nothing can restore — Plant Jungle only works on forest, and no order makes a
//        wetland at all. Clearing forest is free: it is renewable and replantable.
//   +100 per Scavenger Harvester destroyed. Killing them is the only counterplay to the
//        extraction, so the score pays for it.
//
// The eco tests all assert on MilestoneScore rather than Score: Score also moves with
// population and advances, which drift as turns run.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ScoreIncentiveTests
	{
		private static (Game game, Player player, Settlers settler) ASettlerOn(Terrain terrain)
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player player = g.CurrentPlayer;
			player.Government = new Governments.Monarchy();
			player.Explore(40, 25, range: 5);
			Map.Instance.ChangeTileType(40, 25, terrain);
			Map.Instance.RecalculateContinentsIfDirty();

			Settlers settler = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(player))!;
			settler.MovesLeft = settler.Move;
			Sim.ClearTasks();
			return (g, player, settler);
		}

		// Orders tick down one turn at a time; run enough turns for any of them to land.
		private static void Finish(Settlers settler)
		{
			for (int i = 0; i < 10; i++)
			{
				settler.NewTurn();
				Sim.Settle();
			}
		}

		[Theory]
		[InlineData(Terrain.Jungle)]
		[InlineData(Terrain.Swamp)]     // "Wetland" in play
		public void IrrigatingAHabitatCostsAPoint(Terrain habitat)
		{
			(Game g, Player player, Settlers settler) = ASettlerOn(habitat);
			int before = player.MilestoneScore;

			settler.BuildIrrigation();
			Finish(settler);

			Assert.False(Map.Instance[40, 25].Type == habitat, "the habitat survived; nothing was destroyed");
			Assert.Equal(before - 1, player.MilestoneScore);
		}

		[Theory]
		[InlineData(Terrain.Jungle)]
		[InlineData(Terrain.Swamp)]
		public void MiningAHabitatCostsAPoint(Terrain habitat)
		{
			(Game g, Player player, Settlers settler) = ASettlerOn(habitat);
			int before = player.MilestoneScore;

			settler.BuildMines();
			Finish(settler);

			Assert.Equal(Terrain.Forest, Map.Instance[40, 25].Type);
			Assert.Equal(before - 1, player.MilestoneScore);
		}

		// The third order that can erase a habitat, and the reason the rule lives in one helper
		// rather than at the call sites: engineering a river over jungle destroys it just as
		// thoroughly as irrigating it.
		[Fact]
		public void EngineeringARiverThroughJungleCostsAPoint()
		{
			(Game g, Player player, Settlers settler) = ASettlerOn(Terrain.Jungle);
			player.AddAdvance(new Advances.Hydroengineering(), false);
			Map.Instance.ChangeTileType(41, 25, Terrain.River);
			int before = player.MilestoneScore;

			Assert.True(settler.BuildAddRiver(), "the order was refused");
			Finish(settler);

			Assert.Equal(Terrain.River, Map.Instance[40, 25].Type);
			Assert.Equal(before - 1, player.MilestoneScore);
		}

		// Forest is renewable and replantable — clearing one is husbandry, not a loss.
		[Fact]
		public void ClearingForestIsFree()
		{
			(Game g, Player player, Settlers settler) = ASettlerOn(Terrain.Forest);
			int before = player.MilestoneScore;

			settler.BuildIrrigation();
			Finish(settler);

			Assert.Equal(Terrain.Plains, Map.Instance[40, 25].Type);
			Assert.Equal(before, player.MilestoneScore);
		}

		// The bounty is counted where the dying set is decided, so a stack pays per Harvester.
		[Fact]
		public void KillingHarvestersPaysPerCraft()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			g.Animations = false;   // resolve the kill synchronously, no animation screen
			Player killer = g.HumanPlayer;
			int before = killer.MilestoneScore;

			byte barbarian = 0;
			g.CreateUnit(UnitType.Harvester, 40, 25, barbarian);
			IUnit second = g.CreateUnit(UnitType.Harvester, 40, 25, barbarian)!;

			Assert.True(Screens.DestroyUnit.ResolveIfUnseen(second, true, killer));
			Assert.Equal(before + 200, killer.MilestoneScore);
		}

		// An ordinary barbarian is worth nothing, and a kill with no attacker behind it —
		// disband, upgrade, the capture sweep — pays nothing either.
		[Fact]
		public void OnlyHarvestersAndOnlyAttacksPay()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			g.Animations = false;
			Player killer = g.HumanPlayer;
			int before = killer.MilestoneScore;

			IUnit legion = g.CreateUnit(UnitType.Legion, 40, 25, 0)!;
			Assert.True(Screens.DestroyUnit.ResolveIfUnseen(legion, true, killer));
			Assert.Equal(before, killer.MilestoneScore);

			IUnit harvester = g.CreateUnit(UnitType.Harvester, 42, 25, 0)!;
			Assert.True(Screens.DestroyUnit.ResolveIfUnseen(harvester, true, null));
			Assert.Equal(before, killer.MilestoneScore);
		}
	}
}
