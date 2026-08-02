// CivOne tests
//
// Idle Caravans piling up beside foreign cities, two and three deep, and in one
// 2200 AD game they were parked on Nagasaki's polluted tiles — where a foreign
// unit blocks a Settler from ever entering, so the city could not clean up.
//
// A Caravan that reaches a foreign city is consumed (CaravanActions:78 disbands
// it unconditionally), so every Caravan still standing beside one has FAILED to
// enter. These establish which step is failing.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class CaravanDeliveryTests
	{
		// Our city, a foreign city 6 tiles east, and a caravan of ours beside the
		// foreign one. Both civs at peace — which is the case that matters, because at
		// war a foreign unit is a target rather than a wall.
		private static (Player mine, City theirCity, IUnit caravan) AtTheGates(int ringUnits = 0)
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 30; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player[] ps = Game.Instance.Players
				.Where(p => p is not null && Game.Instance.PlayerNumber(p) != 0).ToArray();
			Player mine = ps[0], theirs = ps[1];
			byte mineId = Game.Instance.PlayerNumber(mine);
			byte theirId = Game.Instance.PlayerNumber(theirs);

			mine.Explore(36, 25, range: 12);
			City home = Game.Instance.AddCity(mine, 0, 36, 25)!;
			home.Size = 8;
			City theirCity = Game.Instance.AddCity(theirs, 1, 44, 25)!;
			theirCity.Size = 8;

			// Their garrison, as every real city has.
			Game.Instance.CreateUnit(UnitType.Musketeers, 44, 25, theirId);

			// Optionally ring the city with their units — the "approach is jammed" case.
			if (ringUnits > 0)
			{
				int placed = 0;
				for (int dy = -1; dy <= 1 && placed < ringUnits; dy++)
				for (int dx = -1; dx <= 1 && placed < ringUnits; dx++)
				{
					if (dx == 0 && dy == 0) continue;
					Game.Instance.CreateUnit(UnitType.Musketeers, 44 + dx, 25 + dy, theirId);
					placed++;
				}
			}

			IUnit caravan = Game.Instance.CreateUnit(UnitType.Caravan, 43, 25, mineId)!;
			caravan.SetHome(home);
			Sim.ClearTasks();
			return (mine, theirCity, caravan);
		}

		private static void PumpTasks(int steps = 40)
		{
			for (int i = 0; i < steps; i++) GameTask.Update();
		}

		// The plain case: standing right beside the target, at peace, nothing in the way.
		// It must go in and be consumed.
		[Fact]
		public void ACaravanAtTheGates_DeliversAndIsConsumed()
		{
			var (mine, theirCity, caravan) = AtTheGates();
			byte id = Game.Instance.PlayerNumber(mine);

			for (int turn = 0; turn < 6; turn++)
			{
				Game.Instance.GameTurn++;
				caravan.MovesLeft = caravan.Move;
				AI.Instance(mine).Move(caravan);
				PumpTasks();
				if (!Game.Instance.GetUnits().Any(u => u == caravan)) break;
			}

			Assert.False(Game.Instance.GetUnits().Any(u => u is Caravan && u.Owner == id),
				"a caravan beside a peaceful foreign city should enter it and be consumed");
		}

		// The jammed case: their units fill the approach. It should not deliver — but it
		// also must not simply stand there forever, which is the pile-up on the map.
		[Fact]
		public void AJammedCaravan_DoesNotSitOnTheSameTileForever()
		{
			var (mine, _, caravan) = AtTheGates(ringUnits: 8);
			int startX = caravan.X, startY = caravan.Y;
			bool moved = false;

			for (int turn = 0; turn < 24; turn++)
			{
				Game.Instance.GameTurn++;
				caravan.MovesLeft = caravan.Move;
				AI.Instance(mine).Move(caravan);
				PumpTasks();
				if (!Game.Instance.GetUnits().Any(u => u == caravan)) { moved = true; break; }
				if (caravan.X != startX || caravan.Y != startY) { moved = true; break; }
			}

			Assert.True(moved,
				"a caravan that cannot deliver should go somewhere else, not camp on a rival's tile");
		}
	}
}
