// CivOne tests
//
// First come, first claim. The civ that lays a SEA tube owns the tile; no other civ may
// build on it or set foot on it.
//
// The rule exists because an unowned tile had no way to refuse a squatter. In game 3de868a5
// an Olvir settler stood on the Frankish trans-Atlantic line north-east of Panama for the
// last hundred turns of the game: at peace so it could not be attacked, unarmed so it could
// not be pushed, and Common.Blocks makes any foreign unit impassable to a non-combat unit
// even at peace — so the line was severed and every caravan bound for Panama walked ashore.
//
// Land tubes are deliberately NOT claimed: a tube through your own country is road, not
// territory. And a tube carried over from a save written before claims existed loads
// unowned, which every civ may still use — the alternative would sever working lines in
// somebody's finished game.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class SeaTubeClaimTests
	{
		private static (Game game, Player human, Player ai) TwoCivs()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = false;
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(40, 25, Terrain.Ocean);
			Map.Instance.ChangeTileType(41, 25, Terrain.Ocean);
			Map.Instance.ChangeTileType(42, 25, Terrain.Grassland1);
			Sim.ClearTasks();
			Player ai = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			return (g, g.HumanPlayer, ai);
		}

		// Laying a sea tube claims the water it crosses.
		[Fact]
		public void BuildingASeaTubeClaimsTheTile()
		{
			(Game g, Player human, _) = TwoCivs();
			byte me = g.PlayerNumber(human);
			HydroEngineer h = (HydroEngineer)g.CreateUnit(UnitType.HydroEngineer, 40, 25, me)!;

			h.BuildSeaTube();
			for (int i = 0; i < 4; i++) h.NewTurn();

			Assert.True(Map.Instance[40, 25].TransportTube);
			Assert.Equal(me, Map.Instance[40, 25].TubeOwner);
		}

		// The reported case: a foreign unit may not stand on it.
		[Fact]
		public void AForeignUnitMayNotEnterAClaimedSeaTube()
		{
			(Game g, Player human, Player ai) = TwoCivs();
			ITile t = Map.Instance[40, 25];
			t.TransportTube = true;
			t.TubeOwner = g.PlayerNumber(human);

			Assert.True(Common.TubeBarred(t, g.PlayerNumber(ai)));
			Assert.False(Common.TubeBarred(t, g.PlayerNumber(human)));
		}

		// A tube from an older save has no owner and stays open to everybody, or loading a
		// finished game severs lines that worked when it was saved.
		[Fact]
		public void AnUnownedTubeIsOpenToAll()
		{
			(Game g, _, Player ai) = TwoCivs();
			ITile t = Map.Instance[40, 25];
			t.TransportTube = true;

			Assert.Equal(BaseTile.TubeUnowned, t.TubeOwner);
			Assert.False(Common.TubeBarred(t, g.PlayerNumber(ai)));
		}

		// Pillaging the tube releases the water. The claim is the tube, not the sea.
		[Fact]
		public void RemovingTheTubeClearsTheClaim()
		{
			(Game g, Player human, Player ai) = TwoCivs();
			ITile t = Map.Instance[40, 25];
			t.TransportTube = true;
			t.TubeOwner = g.PlayerNumber(human);

			t.TransportTube = false;

			Assert.Equal(BaseTile.TubeUnowned, t.TubeOwner);
			Assert.False(Common.TubeBarred(t, g.PlayerNumber(ai)));
		}

		// A city on the tile is never barred — a tube running into a city must not lock the
		// city's own tile against its owner or anybody with a right to be there.
		[Fact]
		public void ACityTileIsNeverBarred()
		{
			(Game g, Player human, Player ai) = TwoCivs();
			Map.Instance.ChangeTileType(42, 25, Terrain.Grassland1);
			g.AddCity(human, 0, 42, 25);
			ITile t = Map.Instance[42, 25];
			t.TransportTube = true;
			t.TubeOwner = g.PlayerNumber(human);

			Assert.False(Common.TubeBarred(t, g.PlayerNumber(ai)));
		}

		// The claim survives a save, or every reload hands the sea back.
		[Fact]
		public void TheClaimSurvivesASave()
		{
			(Game g, Player human, Player ai) = TwoCivs();
			byte me = g.PlayerNumber(human);
			Map.Instance[40, 25].TransportTube = true;
			Map.Instance[40, 25].TubeOwner = me;
			string path = System.IO.Path.Combine(Settings.Instance.SavesDirectory, "tubeclaim.cos");
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "load failed");

			Assert.True(Map.Instance[40, 25].TransportTube);
			Assert.Equal(me, Map.Instance[40, 25].TubeOwner);
		}
	}
}
