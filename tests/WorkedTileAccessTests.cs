// CivOne tests
//
// Tiles worked by a foreign city are closed to trespassers (BaseUnitLand.
// ValidMoveTarget). The rule additionally demanded Attack > 0, which silently
// excluded the Diplomat — Attack 0 — even from a civ we were at war with. A city
// works a 5x5 radius, so every enemy city sat inside two tiles of ground its
// Diplomat could not cross, which is the only errand a Diplomat has. With no
// enemy units in sight it looked like the map refusing to let the unit move.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class WorkedTileAccessTests
	{
		// Build two neighbours, at war, and put a Diplomat next to the enemy's worked
		// land. Every adjacent land tile must be reachable.
		[Fact]
		public void Diplomat_MayCrossWorkedTiles_OfACivAtWar()
		{
			Sim.NewGame(width: 80, height: 50);
			Player human = Game.Instance.HumanPlayer;
			Player rival = Game.Instance.Players.First(p => p is not null && p != human
			                                             && Game.Instance.PlayerNumber(p) != 0);

			// Solid land, so no neighbour is excluded for being ocean.
			ITile centre = Map.Instance.AllTiles().First(t => !t.IsOcean && t.Y > 6 && t.Y < Map.HEIGHT - 6);
			for (int dy = -4; dy <= 4; dy++)
			for (int dx = -4; dx <= 4; dx++)
				Map.Instance.ChangeTileType((centre.X + dx + Map.WIDTH) % Map.WIDTH, centre.Y + dy, Terrain.Grassland1);
			human.Explore(centre.X, centre.Y, range: 6);
			rival.Explore(centre.X, centre.Y, range: 6);

			// Rival city two tiles away: our tile sits inside its working radius.
			City rivalCity = Game.Instance.AddCity(rival, 0, centre.X + 2, centre.Y);
			Assert.NotNull(rivalCity);
			rivalCity.Size = 6;
			rivalCity.ResetResourceTiles();

			human.DeclareWar(rival);
			Assert.True(human.IsAtWar(rival), "precondition: at war");

			IUnit dip = Game.Instance.CreateUnit(UnitType.Diplomat, centre.X, centre.Y,
				Game.Instance.PlayerNumber(human))!;
			Assert.NotNull(dip);
			Assert.Equal(0, dip.Attack);   // the property that used to disqualify it

			int worked = Map.Instance[dip.X, dip.Y].GetBorderTiles()
				.Count(t => t is not null && Game.Instance.IsWorkedByOther(t.X, t.Y, dip.Owner));
			Assert.True(worked > 0, "precondition: a neighbouring tile is worked by the rival");

			foreach (ITile t in Map.Instance[dip.X, dip.Y].GetBorderTiles().Where(t => t is not null && !t.IsOcean))
				Assert.Contains(dip.MoveTargets, m => m.X == t.X && m.Y == t.Y);
		}
	}
}
