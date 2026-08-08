// CivOne tests
//
// The settlers still raced back and forth after the retry budget went in, because the retry
// budget was not the cause. Two halves of the settler AI disagreed about what "claimed" means:
//
//   Settlers.IsTileClaimed  — another own settler's Goto points here, OR one is standing here
//                             and Busy. Checked at AI.cs:376, and it REFUSES the work.
//   BestImproveSiteInner    — another own settler's Goto points here. Full stop.
//
// A settler that has arrived and is building has an empty Goto (it clears on arrival) and is
// Busy for the whole 2-5 turn job, and the tile still reads as work available because
// irrigation does not land until the build completes. So the site scan happily routed every
// other settler in range to ground already being worked. Each one arrived, was refused, found
// that same tile was still the nearest site — now at distance 0, which the caller discards —
// and drifted home to do it again next turn.
//
// The file already warns about exactly this ("so that this half of the settler AI and
// BestImproveSite cannot disagree about it — three separate bugs came from them doing exactly
// that"). This is the fourth.

using System.Drawing;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SettlerClaimAgreementTests
	{
		// PLAINS with a river through it. Plains, not grassland: DespotBlocksIrrigation skips
		// grassland under Despotism (the tile penalty eats the yield), and an AI starts as a
		// despot — so a grassland world gives the scan nothing to find and every assertion
		// below passes vacuously.
		private static (Game game, Player ai, City city) AWorldWithACity()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 15; y <= 35; y++)
			for (int x = 20; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Plains);
			for (int y = 20; y <= 30; y++)
				Map.Instance.ChangeTileType(39, y, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();

			Game g = Game.Instance;
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			ai.Explore(40, 25, range: 20);
			City city = g.AddCity(ai, 0, 40, 25)!;
			Sim.ClearTasks();
			return (g, ai, city);
		}

		private static Settlers ASettlerAt(Game g, Player ai, int x, int y)
			=> (Settlers)g.CreateUnit(UnitType.Settlers, x, y, g.PlayerNumber(ai))!;

		// The defect, stated directly: ground under a settler that is building is not a
		// destination for anybody else.
		[Fact]
		public void GroundUnderAWorkingSettlerIsNotOfferedToAnother()
		{
			var (g, ai, _) = AWorldWithACity();
			Settlers working = ASettlerAt(g, ai, 40, 26);
			Settlers idle    = ASettlerAt(g, ai, 41, 26);
			working.BuildIrrigation();               // arrived, Goto empty, now Busy
			Assert.True(working.Busy && working.Goto.IsEmpty, "scenario: building, not travelling");

			ITile? site = AI.Instance(ai).BestImproveSite(idle);

			Assert.False(site is not null && site.X == 40 && site.Y == 26,
				"routed to ground IsTileClaimed will refuse — it will arrive, be turned away, and go home");
		}

		// The two rules have to agree in general, not just on this one tile: whatever the scan
		// hands back must be something the work gate would actually let the settler do.
		[Fact]
		public void WhateverTheScanPicksTheWorkGateAccepts()
		{
			var (g, ai, _) = AWorldWithACity();
			Settlers working = ASettlerAt(g, ai, 40, 26);
			Settlers idle    = ASettlerAt(g, ai, 41, 26);
			working.BuildIrrigation();

			ITile? site = AI.Instance(ai).BestImproveSite(idle);
			if (site is null) return;   // nothing to route to is a fine answer

			Assert.False(idle.IsTileClaimed(site.X, site.Y),
				$"scan chose ({site.X},{site.Y}), which the work gate refuses");
		}

		// A travelling settler's target is still claimed — the original rule has not been
		// traded away for the new one.
		[Fact]
		public void ATravellingSettlersTargetIsStillClaimed()
		{
			var (g, ai, _) = AWorldWithACity();
			Settlers travelling = ASettlerAt(g, ai, 44, 30);
			Settlers idle       = ASettlerAt(g, ai, 41, 26);
			travelling.Goto = new Point(40, 26);

			ITile? site = AI.Instance(ai).BestImproveSite(idle);

			Assert.False(site is not null && site.X == 40 && site.Y == 26);
		}

		// And a rival's settler claims nothing of ours: the set is scoped to our own units, or
		// an enemy worker parked nearby would blank out our countryside.
		[Fact]
		public void AForeignSettlerClaimsNothing()
		{
			var (g, ai, _) = AWorldWithACity();
			Player other = g.Players.First(p => p is not null && p != ai && g.PlayerNumber(p) != 0);
			Settlers idle = ASettlerAt(g, ai, 41, 26);

			ITile? before = AI.Instance(ai).BestImproveSite(idle);
			Assert.NotNull(before);

			Settlers foreigner = (Settlers)g.CreateUnit(
				UnitType.Settlers, (byte)before!.X, (byte)before.Y, g.PlayerNumber(other))!;
			foreigner.BuildIrrigation();

			ITile? after = AI.Instance(ai).BestImproveSite(idle);

			Assert.Equal((before.X, before.Y), (after?.X, after?.Y));
		}
	}
}
