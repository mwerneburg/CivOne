// CivOne tests
//
// Nuking your own ground.
//
// A missile could only ever go off through Confront, and MoveTo reaches Confront only for a
// FOREIGN unit or a foreign city. Aimed at one of your own cities it simply walked in. That
// left Game.SterilizeGoo unreachable in exactly the case it was written for — its own comment
// calls a strike on grey goo "the one time the game rewards nuking your own land" — and the
// Nuclear unit carried no order that could do it either.
//
// The order is now on the unit. These test what it does when it goes off, which is the part
// that matters and the part a screen cannot be asked about: the blast, the sterilisation, and
// the fact that scouring your own country is not an atrocity against anybody.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Units;
using CivOne.UserInterface;

namespace CivOne.Tests
{
	public class SelfDetonationTests
	{
		private static (Game game, Player me, City city) AHomeland()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			Player me = g.HumanPlayer;
			me.Explore(42, 25, range: 8);
			City city = g.AddCity(me, 0, 42, 25)!;
			city.Size = 8;
			Sim.ClearTasks();
			return (g, me, city);
		}

		// The order exists at all, which is the reported gap.
		[Fact]
		public void AMissileCarriesADetonateOrder()
		{
			var (g, me, city) = AHomeland();
			IUnit missile = g.CreateUnit(UnitType.Nuclear, 42, 25, g.PlayerNumber(me))!;

			Assert.Contains(missile.MenuItems.Where(i => i is not null),
				i => i.Text.Contains("Detonate"));
		}

		// It goes off on our own city, which no other path allowed.
		[Fact]
		public void ItHalvesOurOwnCity()
		{
			var (g, me, city) = AHomeland();

			g.ApplyNuclearStrike(city.X, city.Y, me);

			Assert.Equal(4, city.Size);
		}

		// Scouring your own country offends nobody: CondemnNuclearStrike wants a victim who
		// counts as people, and there is none.
		[Fact]
		public void ThereIsNoCondemnationForOurOwnGround()
		{
			var (g, me, city) = AHomeland();

			g.ApplyNuclearStrike(city.X, city.Y, me);

			Assert.False(g.IsNuclearPariah(me));
		}

		// The reason to do it: the whole connected goo region goes, not just the blast.
		[Fact]
		public void ItSterilizesTheWholeGooRegion()
		{
			var (g, me, city) = AHomeland();
			g.SeedGreyGoo(city);
			// A tail of goo running away from the city, well outside the 3x3 blast.
			for (int x = city.X + 1; x <= city.X + 6; x++)
				g.GooTiles[(x, city.Y)] = (uint)g.GameTurn;
			Assert.True(g.GooTiles.Count >= 7, "fixture: the tail did not take");

			g.ApplyNuclearStrike(city.X, city.Y, me);

			Assert.Empty(g.GooTiles);
		}

		// ...and the first detonation of a game is still the first detonation of a game,
		// whoever it was aimed at. Deliberate: the player was asked and said yes.
		[Fact]
		public void ItStillWakesWhatADetonationWakes()
		{
			var (g, me, city) = AHomeland();
			Settings.Instance.CursedWonders = true;
			Assert.Equal(0, g.GoziraState);

			g.ApplyNuclearStrike(city.X, city.Y, me);

			Assert.Equal(1, g.GoziraState);
		}
	}
}
