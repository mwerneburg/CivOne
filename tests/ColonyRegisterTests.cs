// CivOne tests
//
// The colony register — the AI's first piece of memory. Before it, every overseas
// survey was recomputed from nothing each turn: up to 91x91 tiles scanned per idle
// ship, every candidate scored, one kept, the rest discarded, and the whole thing
// repeated next turn and for the next hull. Two ships would sail for the same beach
// because neither knew the other had looked.

using System;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ColonyRegisterTests
	{
		// A home island, open water, and a foreign coast worth settling.
		private static (Player player, IUnit ship) TwoShores(int hx = 20, int hy = 25, int sx = 34)
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			byte id = Game.Instance.PlayerNumber(player);

			for (int y = hy - 8; y <= hy + 8; y++)
			for (int x = hx - 6; x <= sx + 6; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			Map.Instance.ChangeTileType(hx, hy, Terrain.Grassland1);      // home
			for (int dy = -1; dy <= 1; dy++)                              // a foreign shore
			for (int dx = 0; dx <= 2; dx++)
				Map.Instance.ChangeTileType(sx + dx, hy + dy, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Game.Instance.AddCity(player, 0, hx, hy);
			player.Explore(hx, hy, range: 30);
			IUnit ship = Game.Instance.CreateUnit(UnitType.Trireme, hx + 1, hy, id)!;
			return (player, ship);
		}

		// One survey should leave knowledge behind, not just a destination.
		[Fact]
		public void SurveyingTheOcean_LeavesKnowledgeBehind()
		{
			var (player, ship) = TwoShores();
			AI ai = AI.Instance(player);
			Assert.Equal(0, ai.KnownColonySites());

			ITile? site = ai.BestOverseasSite(ship);

			Assert.NotNull(site);
			Assert.True(ai.KnownColonySites() > 1,
				$"a survey that found a coast should record more than the one tile it picked; got {ai.KnownColonySites()}");
		}

		// Two hulls must not sail for the same beach.
		[Fact]
		public void TwoShips_DoNotClaimTheSameShore()
		{
			var (player, first) = TwoShores();
			byte id = Game.Instance.PlayerNumber(player);
			IUnit second = Game.Instance.CreateUnit(UnitType.Trireme, first.X, first.Y, id)!;
			AI ai = AI.Instance(player);

			ITile? a = ai.BestOverseasSite(first);
			ITile? b = ai.BestOverseasSite(second);

			Assert.NotNull(a);
			if (b is not null)
				Assert.False(a!.X == b.X && a.Y == b.Y,
					$"both hulls claimed ({a.X},{a.Y})");
		}

		// A site somebody has since settled is forgotten rather than sailed to.
		[Fact]
		public void SettledSites_ArePrunedFromMemory()
		{
			var (player, ship) = TwoShores();
			AI ai = AI.Instance(player);
			ITile? site = ai.BestOverseasSite(ship);
			Assert.NotNull(site);
			int before = ai.KnownColonySites();
			Assert.True(before > 0);

			// Somebody else gets there first — the whole shore falls inside the 4-tile bar.
			Player rival = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is CivOne.Civilizations.Barbarian));
			Assert.NotNull(Game.Instance.AddCity(rival, 0, site!.X, site.Y));

			Assert.True(ai.KnownColonySites() < before,
				$"settled ground should drop out of the register; {before} -> {ai.KnownColonySites()}");
		}

		// A map is worth what it tells you that you could not have found yourself. Our own
		// survey only ever sees within 45 tiles of one of our hulls, so a coast on the far
		// side of the world is unreachable knowledge — unless a partner who sailed there
		// hands over their charts.
		[Fact]
		public void TradedCharts_CarryColonySitesAcrossTheWorld()
		{
			var (player, ship) = TwoShores();
			Player partner = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is CivOne.Civilizations.Barbarian));

			AI mine = AI.Instance(player);
			AI theirs = AI.Instance(partner);

			// They have surveyed a shore; we have surveyed nothing.
			byte pid = Game.Instance.PlayerNumber(partner);
			IUnit theirShip = Game.Instance.CreateUnit(UnitType.Trireme, ship.X, ship.Y, pid)!;
			Assert.NotNull(theirs.BestOverseasSite(theirShip));
			int theirKnowledge = theirs.KnownColonySites();
			Assert.True(theirKnowledge > 0);
			Assert.Equal(0, mine.KnownColonySites());

			mine.MergeColonyRegister(theirs);

			Assert.True(mine.KnownColonySites() > 0,
				"their charts should leave us knowing somewhere to settle");
			Assert.True(mine.KnownColonySites() <= theirKnowledge,
				"...and no more than they actually knew");
		}

		// Their hull's claim is their business — a traded chart must not arrive pre-claimed,
		// or the receiving civ would treat every site as already taken.
		[Fact]
		public void TradedCharts_ArriveUnclaimed()
		{
			var (player, ship) = TwoShores();
			Player partner = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is CivOne.Civilizations.Barbarian));
			AI mine = AI.Instance(player);
			AI theirs = AI.Instance(partner);

			byte pid = Game.Instance.PlayerNumber(partner);
			IUnit theirShip = Game.Instance.CreateUnit(UnitType.Trireme, ship.X, ship.Y, pid)!;
			ITile? claimed = theirs.BestOverseasSite(theirShip);   // claims it for their hull
			Assert.NotNull(claimed);

			mine.MergeColonyRegister(theirs);

			// Our own hull must still be able to take a site from the traded charts.
			Assert.NotNull(mine.BestOverseasSite(ship));
		}
	}
}
