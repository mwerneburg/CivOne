// CivOne tests
//
// Right-click on the map gated on MoveTargets, which answers "may I walk here
// peacefully". That is the wrong question for an attack. BaseUnitLand.ValidMoveTarget
// closes a foreign city's worked tiles to trespassers, so a hostile STANDING ON those
// tiles could not be attacked at all while we were at peace with the civ that farms
// them — the click fell through to the Civilopedia terrain page with no hint that a
// rule had fired. Reported from a live game: Gozira bearing down on Athens, the player
// one tile behind it, and Greek farmland protecting the monster eating Greece.
//
// MoveTo has always handled this correctly — it dispatches to Confront for an occupied
// tile BEFORE it tests trespass or zone of control — so the gate was stricter than the
// move it guarded. BaseUnit.ActionTargets asks the looser question.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class AttackAcrossWorkedTilesTests
	{
		// centre is solid grassland with a neutral civ's city two tiles east, so the
		// tile immediately east of centre is inside that city's working radius.
		private static (Player human, Player neutral, ITile centre) Field()
		{
			Sim.NewGame(width: 80, height: 50);
			Player human = Game.Instance.HumanPlayer;
			Player neutral = Game.Instance.Players.First(p => p is not null && p != human
			                                                && Game.Instance.PlayerNumber(p) != 0);

			ITile centre = Map.Instance.AllTiles().First(t => !t.IsOcean && t.Y > 6 && t.Y < Map.HEIGHT - 6);
			for (int dy = -4; dy <= 4; dy++)
			for (int dx = -4; dx <= 4; dx++)
				Map.Instance.ChangeTileType((centre.X + dx + Map.WIDTH) % Map.WIDTH, centre.Y + dy, Terrain.Grassland1);
			human.Explore(centre.X, centre.Y, range: 6);
			neutral.Explore(centre.X, centre.Y, range: 6);

			City city = Game.Instance.AddCity(neutral, 0, centre.X + 2, centre.Y);
			Assert.NotNull(city);
			city.Size = 6;
			city.ResetResourceTiles();

			Assert.False(human.IsAtWar(neutral), "precondition: at peace with the civ that farms the ground");
			return (human, neutral, centre);
		}

		[Fact]
		public void AMonsterOnAFriendlyCivsFarmlandCanBeAttacked()
		{
			(Player human, _, ITile centre) = Field();

			IUnit musket = Game.Instance.CreateUnit(UnitType.Musketeers, centre.X, centre.Y,
				Game.Instance.PlayerNumber(human))!;
			Assert.NotNull(musket);

			int gx = centre.X + 1, gy = centre.Y;
			Assert.True(Game.Instance.IsWorkedByOther(gx, gy, musket.Owner),
				"precondition: the monster stands on ground the neutral city works");

			IUnit gozira = Game.Instance.CreateUnit(UnitType.Gozira, gx, gy, 0)!;
			Assert.NotNull(gozira);
			Assert.Equal(0, gozira.Owner);   // barbarian

			BaseUnit attacker = (BaseUnit)musket;
			Assert.DoesNotContain(attacker.MoveTargets, t => t.X == gx && t.Y == gy);   // still no trespassing
			Assert.Contains(attacker.ActionTargets, t => t.X == gx && t.Y == gy);       // but it can be attacked
		}

		[Fact]
		public void AForeignCityRingedByItsOwnFieldsCanBeReached()
		{
			(Player human, _, ITile centre) = Field();

			// Stand next to the city itself: its own tile is worked ground too.
			IUnit musket = Game.Instance.CreateUnit(UnitType.Musketeers, centre.X + 1, centre.Y,
				Game.Instance.PlayerNumber(human))!;
			Assert.NotNull(musket);

			int cx = centre.X + 2, cy = centre.Y;
			Assert.NotNull(Map.Instance[cx, cy].City);

			BaseUnit attacker = (BaseUnit)musket;
			Assert.Contains(attacker.ActionTargets, t => t.X == cx && t.Y == cy);
		}

		// The looser question must not become "anything goes": open ground that a
		// neutral city farms, with nothing standing on it, stays closed.
		[Fact]
		public void EmptyFarmlandIsStillClosedToTrespassers()
		{
			(Player human, _, ITile centre) = Field();

			IUnit musket = Game.Instance.CreateUnit(UnitType.Musketeers, centre.X, centre.Y,
				Game.Instance.PlayerNumber(human))!;
			Assert.NotNull(musket);

			int wx = centre.X + 1, wy = centre.Y;
			Assert.True(Game.Instance.IsWorkedByOther(wx, wy, musket.Owner));
			Assert.Empty(Map.Instance[wx, wy].Units);

			BaseUnit walker = (BaseUnit)musket;
			Assert.DoesNotContain(walker.ActionTargets, t => t.X == wx && t.Y == wy);
		}
	}
}
