// CivOne tests
//
// Does a Longboat that exists actually go anywhere?
//
// Production and routing are separate halves and only the first was ever exercised. Across
// four complete runs no civ built a boat at all, so AI.cs's Longboat branch — LandingSite,
// GoAshore, BestOverseasSite — had never run in a real game. The run after the production fix
// gave the Maori two Longboats, and both sat in the port they were built in with full
// movement, unmoved, while 277 legal coastal sites lay within the 45-tile search window.
//
// This file stages the crossing end to end: a boat in a port, an empty island in range, and
// the question of whether the AI ever points it at anything.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class LongboatCrossingTests
	{
		// Home island around x=40, a target island to the west around x=25, open ocean
		// between. Both coastal, both habitable, far enough apart that nothing on one is
		// within the 4-tile exclusion of a city on the other.
		private static (Game g, Player p, City port, IUnit boat) AWorldWithSomewhereToGo()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			for (int y = 10; y <= 40; y++)
			for (int x = 10; x <= 60; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			// Home island: 3x3 at (40,25).
			for (int y = 24; y <= 26; y++)
			for (int x = 39; x <= 41; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			// Target island: 5x5 at (25,25), 15 tiles west — inside OverseasRange.
			for (int y = 23; y <= 27; y++)
			for (int x = 23; x <= 27; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();

			City port = g.AddCity(p, 0, 41, 25)!;
			port.Size = 6;
			p.AddAdvance(new MapMaking(), false);
			p.Explore(40, 25, range: 30);
			IUnit boat = g.CreateUnit(UnitType.Longboat, port.X, port.Y, g.PlayerNumber(p))!;
			boat.MovesLeft = boat.Move;
			Sim.ClearTasks();
			return (g, p, port, boat);
		}

		private static ITile? OverseasSite(Player p, IUnit boat)
			=> (ITile?)typeof(AI).GetMethod("BestOverseasSite",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), new object[] { boat });

		private static void MoveAI(Player p, IUnit unit)
			=> typeof(AI).GetMethod("Move",
					System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Public)!
				.Invoke(AI.Instance(p), new object[] { unit });

		// The fixture: there really is somewhere to go, and it really is across water.
		[Fact]
		public void TheTargetIslandIsADifferentLandmass()
		{
			(Game g, Player p, City port, IUnit boat) = AWorldWithSomewhereToGo();

			Assert.NotEqual(Map.Instance[41, 25].ContinentId, Map.Instance[25, 25].ContinentId);
		}

		// Does the survey find it?
		[Fact]
		public void TheBoatFindsACoastToSailFor()
		{
			(Game g, Player p, City port, IUnit boat) = AWorldWithSomewhereToGo();

			ITile? site = OverseasSite(p, boat);

			Assert.NotNull(site);
		}

		// A ship whose starting tile is a LAND city still has to be able to plan a route.
		// This is the step the crossing dies on if the pathfinder treats the port as
		// impassable water.
		[Fact]
		public void APathExistsFromThePortToTheFarCoast()
		{
			(Game g, Player p, City port, IUnit boat) = AWorldWithSomewhereToGo();

			Assert.NotNull(Common.GotoStep(boat, 27, 25));
		}

		// The real game's geometry, which the small fixture above does not reproduce.
		//
		// The Maori sit at the eastern edge of a 320x200 Earth. Every candidate coast inside
		// the near window (OverseasRange = 15) is on their own landmass and filtered out, so
		// the survey falls through to OverseasRangeFar = 45 — and the nearest legal sites are
		// then ~45 tiles away across open Pacific. BestOverseasSite runs one path probe on the
		// winner and returns null if it fails, and GotoStepInner gives a sea search a
		// 20,000-node budget: an A* fanning out over open ocean toward a goal at the far edge
		// of the window is exactly the shape that exhausts it.
		private static (Game g, Player p, City port, IUnit boat) AnOceanCrossing(int gap)
		{
			Sim.NewGame(width: 320, height: 200);
			Game g = Game.Instance;
			Player p = g.Players.First(x => x is not null && x != g.HumanPlayer && g.PlayerNumber(x) != 0);
			p.Government = new Governments.Monarchy();
			for (int y = 140; y <= 185; y++)
			for (int x = 250; x <= 319; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Ocean);
			// A one-tile home island. The port must have OCEAN on a neighbouring tile or the
			// boat is landlocked in its own harbour — a 3x3 island with the city at its centre
			// has land on all eight sides, and every path probe from it returns null.
			Map.Instance.ChangeTileType(316, 162, Terrain.Grassland1);
			for (int y = 160; y <= 164; y++)
			for (int x = 316 - gap - 2; x <= 316 - gap + 2; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			City port = g.AddCity(p, 0, 316, 162)!;
			port.Size = 6;
			p.AddAdvance(new MapMaking(), false);
			p.Explore(316, 162, range: 60);
			IUnit boat = g.CreateUnit(UnitType.Longboat, port.X, port.Y, g.PlayerNumber(p))!;
			boat.MovesLeft = boat.Move;
			Sim.ClearTasks();
			return (g, p, port, boat);
		}

		[Theory]
		[InlineData(15)]
		[InlineData(30)]
		[InlineData(44)]
		public void APathExistsAcrossOpenOceanAtEveryRangeTheSurveyUses(int gap)
		{
			(Game g, Player p, City port, IUnit boat) = AnOceanCrossing(gap);
			int targetX = 316 - gap + 2;

			Assert.NotNull(Common.GotoStep(boat, targetX, 162));
		}

		[Theory]
		[InlineData(15)]
		[InlineData(30)]
		[InlineData(44)]
		public void TheSurveyReturnsASiteAtEveryRange(int gap)
		{
			(Game g, Player p, City port, IUnit boat) = AnOceanCrossing(gap);

			Assert.NotNull(OverseasSite(p, boat));
		}

		// ...and the whole branch: the AI actually points the boat at it.
		[Fact]
		public void TheAiSendsTheBoatAcross()
		{
			(Game g, Player p, City port, IUnit boat) = AWorldWithSomewhereToGo();

			MoveAI(p, boat);

			Assert.False(boat.Goto.IsEmpty && boat.X == port.X && boat.Y == port.Y,
				"the boat neither set a destination nor moved");
		}
	}
}
