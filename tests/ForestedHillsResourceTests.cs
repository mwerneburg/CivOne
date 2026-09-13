// CivOne tests
//
// Coal on wooded hills.
//
// Game.ResourceAt switches on terrain, and Terrain.ForestedHills was added long after it was
// written. The tile was therefore worth nothing to any rule — no camp could be built on it and
// one inside a city radius unlocked no Factory — while THREE readouts insisted otherwise: the
// map overlay labels it "Coal" (Overlay.cs), the Civilopedia documents a Coal special on it
// with yields (Civilopedia.cs), and the terrain itself is generated from hills.
//
// The quiet one is the third test. Planting forest on a hill produces ForestedHills
// (Settlers.ReplaceTerrain), so a settler improving a coal hill destroyed the seam — and
// because HasResource evaluates ResourceAt live, it did so under a camp that was already
// standing there. Nothing reported it; the Factory simply stopped being buildable.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ForestedHillsResourceTests
	{
		// A tile of the given terrain that carries a special. Specials are positional — see
		// Map.ChangeTileType, which recomputes TileIsSpecial from the coordinates rather than
		// carrying a flag across — so the fixture has to hunt for a square that has one rather
		// than setting it.
		private static ITile ASpecialTileOf(Terrain terrain)
		{
			Sim.NewGame(width: 80, height: 50);
			for (int y = 10; y <= 40; y++)
			for (int x = 10; x <= 70; x++)
				Map.Instance.ChangeTileType(x, y, terrain);
			Map.Instance.RecalculateContinentsIfDirty();
			ITile? found = null;
			for (int y = 10; y <= 40 && found is null; y++)
			for (int x = 10; x <= 70 && found is null; x++)
				if (Map.Instance[x, y].Special && Map.Instance[x, y].Type == terrain)
					found = Map.Instance[x, y];
			Assert.NotNull(found);
			Sim.ClearTasks();
			return found!;
		}

		// The report: a camp is refused on wooded hills.
		[Fact]
		public void ACampMayStandOnSpecialForestedHills()
		{
			ITile tile = ASpecialTileOf(Terrain.ForestedHills);

			Assert.Equal(StrategicResource.Coal, Game.ResourceAt(tile));
			Assert.True(Settlers.CanCampOn(tile), "a camp was refused on a coal seam");
		}

		// ...and the camp actually supplies the resource, which is the whole point of one.
		[Fact]
		public void ACampOnForestedHillsSuppliesCoal()
		{
			ITile tile = ASpecialTileOf(Terrain.ForestedHills);
			Player me = Game.Instance.HumanPlayer;
			Assert.False(Game.Instance.HasResource(me, StrategicResource.Coal),
				"fixture: the coal must come from the camp and nowhere else");

			Game.Instance.ResourceCamps[(tile.X, tile.Y)] = Game.Instance.PlayerNumber(me);

			Assert.True(Game.Instance.HasResource(me, StrategicResource.Coal));
		}

		// The silent one. Planting forest on a coal hill must not destroy the seam — and a
		// camp already standing on it must go on supplying, since HasResource reads the
		// terrain live rather than remembering what the camp was founded on.
		[Fact]
		public void PlantingForestOnACoalHillKeepsTheCoal()
		{
			ITile hill = ASpecialTileOf(Terrain.Hills);
			Player me = Game.Instance.HumanPlayer;
			Game.Instance.ResourceCamps[(hill.X, hill.Y)] = Game.Instance.PlayerNumber(me);
			Assert.True(Game.Instance.HasResource(me, StrategicResource.Coal), "fixture: coal first");

			// What Settlers.ReplaceTerrain does when the plant-forest order completes on hills.
			Map.Instance.ChangeTileType(hill.X, hill.Y, Terrain.ForestedHills);

			ITile now = Map.Instance[hill.X, hill.Y];
			Assert.Equal(Terrain.ForestedHills, now.Type);
			Assert.True(now.Special, "fixture: the special is positional and should have survived");
			Assert.Equal(StrategicResource.Coal, Game.ResourceAt(now));
			Assert.True(Game.Instance.HasResource(me, StrategicResource.Coal),
				"the camp stopped supplying because somebody planted trees on it");
		}

		// The half that must not change: an ordinary forest is not a coal seam.
		[Fact]
		public void PlainForestCarriesNoCoal()
		{
			ITile tile = ASpecialTileOf(Terrain.Forest);

			Assert.Equal(StrategicResource.None, Game.ResourceAt(tile));
			Assert.False(Settlers.CanCampOn(tile));
		}
	}
}
