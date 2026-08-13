// CivOne tests
//
// The settle scan and the founder must agree about where a city may stand.
//
// They didn't. BestSettleSiteWithin rejected only ocean and occupied tiles; the founder
// (AI.MoveInner's validCity) also refuses Arctic and Mountains. So the scan could route a
// settler to a peak the founder would refuse forever: it arrives, cannot found, loses its
// Goto, drifts home, and re-targets the same peak next turn — logging no settler action at
// any point, which is why the decision log looked clean.
//
// Observed at 1804 AD in a 377-turn game: six Malian settlers between (167,107) and
// (168,110), every one of them targeting (167,111), Mountains. This is the same class of bug
// the WorkAvailable extraction was written to stop, and the fourth time these two halves of
// the settler AI have drifted apart.
//
// BestOverseasSiteWithin had the Arctic/Mountains test all along — the land scan was the
// only one missing it.

using System.Linq;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SettleSiteTerrainTests
	{
		// A settler standing on a small massif in open water. Every land tile it can reach is
		// Mountains, so there is genuinely nowhere to found — and the honest answer is "no
		// site", not "that peak over there".
		private static (Game g, Player p, Settlers s) AMassifInTheSea()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.HumanPlayer;
			p.Explore(40, 25, range: 20);

			for (int y = 5; y < 45; y++)
			for (int x = 20; x < 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			for (int y = 22; y <= 28; y++)
			for (int x = 37; x <= 43; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Mountains);
			Map.Instance.RecalculateContinentsIfDirty();

			Settlers s = (Settlers)g.CreateUnit(UnitType.Settlers, 40, 25, g.PlayerNumber(p))!;
			Sim.ClearTasks();
			return (g, p, s);
		}

		[Fact]
		public void TheScanNeverRoutesASettlerToAMountain()
		{
			(Game g, Player p, Settlers s) = AMassifInTheSea();

			ITile? site = AI.Instance(p).BestSettleSite(s);

			Assert.True(site is null || site is not Mountains,
				$"routed to a mountain at ({site?.X},{site?.Y})");
		}

		// Arctic is refused by the founder on the same line and was missing from the scan the
		// same way. A polar shelf is not a colony site.
		[Fact]
		public void TheScanNeverRoutesASettlerToArctic()
		{
			(Game g, Player p, Settlers s) = AMassifInTheSea();
			for (int y = 22; y <= 28; y++)
			for (int x = 37; x <= 43; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Arctic);
			Map.Instance.RecalculateContinentsIfDirty();

			ITile? site = AI.Instance(p).BestSettleSite(s);

			Assert.True(site is null || site is not Arctic,
				$"routed to arctic at ({site?.X},{site?.Y})");
		}

		// The property that matters, and the one that stops a fifth drift: whatever the scan
		// returns, the founder must accept. Asserted against the shared predicate itself.
		[Fact]
		public void WhateverTheScanReturnsTheFounderWouldAccept()
		{
			(Game g, Player p, Settlers s) = AMassifInTheSea();
			// A legal tile out past the massif, with land under the whole walk: LandReachable
			// tests the ground, so a grassland across open water is no answer at all.
			Map.Instance.ChangeTileType(44, 25, Terrain.Plains);
			Map.Instance.ChangeTileType(45, 25, Terrain.Plains);
			Map.Instance.ChangeTileType(46, 25, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			AI ai = AI.Instance(p);

			ITile? site = ai.BestSettleSite(s);

			Assert.NotNull(site);
			Assert.True(ai.CanFoundOn(site!), $"the founder would refuse ({site!.X},{site.Y})");
		}

		// The symptom as it appeared in play: several settlers in one neighbourhood all
		// naming the same unfoundable tile, because nothing in the scan could rule it out.
		[Fact]
		public void SeveralSettlersDoNotAllNameTheSameUnfoundableTile()
		{
			(Game g, Player p, Settlers s) = AMassifInTheSea();
			AI ai = AI.Instance(p);
			var crowd = new[] { s }
				.Concat(new[] { (39, 24), (41, 24), (39, 26), (41, 26), (40, 23) }
					.Select(t => (Settlers)g.CreateUnit(UnitType.Settlers, t.Item1, t.Item2, g.PlayerNumber(p))!))
				.ToArray();

			ITile?[] targets = crowd.Select(u => ai.BestSettleSite(u)).ToArray();

			Assert.DoesNotContain(targets, t => t is Mountains);
		}
	}
}
