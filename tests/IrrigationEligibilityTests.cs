// CivOne tests
//
// "A lot of terrain development is failing — and I mean a lot — because the Settlers
// don't realize they need a water source." Dozens of "This tile has no water source"
// warnings a round, 2200 AD.
//
// AI.Strategy.WorkAvailable stated the irrigation rule independently of the order that
// enforces it (Settlers.BuildIrrigation) and drifted: the order requires the cardinal
// water-source neighbour to satisfy `t.City is null`, the predicate did not. So a tile
// whose only water was a CITY looked farmable and was refused on arrival — and an AI
// settler gets no message, it just loses the turn and comes back tomorrow.
//
// The guard here is agreement: whatever WorkAvailable claims about irrigation, the
// order must accept.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class IrrigationEligibilityTests
	{
		private static Player OnGrass()
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 20; y <= 30; y++)
			for (int x = 35; x <= 50; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = Game.Instance.Players
				.First(x => x is not null && Game.Instance.PlayerNumber(x) != 0);
			p.Explore(42, 25, range: 10);
			Sim.ClearTasks();
			return p;
		}

		// Does the ORDER accept it? The only authority that matters.
		private static bool OrderAccepts(Player p, int x, int y)
		{
			IUnit s = Game.Instance.CreateUnit(UnitType.Settlers, x, y,
				Game.Instance.PlayerNumber(p))!;
			bool ok = ((Settlers)s).BuildIrrigation();
			Game.Instance.DisbandUnit(s);
			return ok && Map.Instance[x, y].Irrigation == false;   // accepted = work started
		}

		// The finding: a city is not a water source.
		[Fact]
		public void ATileWateredOnlyByACity_IsNotOfferedAsIrrigable()
		{
			Player p = OnGrass();
			// A river city, and a grassland tile cardinally adjacent to it with no other
			// water anywhere near.
			Map.Instance.ChangeTileType(42, 25, Terrain.River);
			Game.Instance.AddCity(p, 0, 42, 25);
			ITile candidate = Map.Instance[43, 25];

			Assert.False(AI.Instance(p).WorkAvailable(candidate).Irrigation,
				"the city tile is the only cardinal water, and the order refuses a city");
		}

		// The control: the same tile beside the same river, with no city on it, IS.
		[Fact]
		public void TheSameTileBesideAnOpenRiver_IsOffered()
		{
			Player p = OnGrass();
			Map.Instance.ChangeTileType(42, 25, Terrain.River);
			ITile candidate = Map.Instance[43, 25];

			Assert.True(AI.Instance(p).WorkAvailable(candidate).Irrigation);
		}

		// The property that must hold generally: anything the AI calls irrigable, the
		// order must accept. This is what drifted, and what will drift again.
		[Fact]
		public void EverythingTheAiCallsIrrigable_TheOrderAccepts()
		{
			Player p = OnGrass();
			Map.Instance.ChangeTileType(40, 25, Terrain.River);
			Map.Instance.ChangeTileType(45, 22, Terrain.Swamp);
			Game.Instance.AddCity(p, 0, 42, 25);            // the trap
			Game.Instance.AddCity(p, 1, 47, 27);
			Map.Instance[44, 24].Irrigation = true;

			int checkedTiles = 0;
			for (int y = 21; y <= 29; y++)
			for (int x = 36; x <= 49; x++)
			{
				ITile t = Map.Instance[x, y];
				if (t is null || t.City is not null) continue;
				var work = AI.Instance(p).WorkAvailable(t);
				// Conversion orders (swamp/jungle/forest) need no water and always succeed.
				if (!work.Irrigation || work.Conversion) continue;
				checkedTiles++;
				Assert.True(OrderAccepts(p, x, y),
					$"WorkAvailable offered irrigation at ({x},{y}) but the order refused it");
			}
			Assert.True(checkedTiles > 0, "scenario produced no irrigable tiles to check");
		}
	}
}
