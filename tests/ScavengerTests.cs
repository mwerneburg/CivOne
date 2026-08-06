// CivOne tests
//
// The fourth visitor archetype, and the only one that is not a judgement. Refugees and Owners
// are both verdicts on a species — worth joining, or worth reclaiming. The Scavengers are not
// assessing anything: they want the water and the ore, and a democracy's water is the same as
// a despotism's. So they are drawn at a FLAT rate, before the character split, and they are
// the one outcome a player's conduct cannot influence.
//
// Mechanically they are attrition on the map rather than a clock: no countdown, no conquest,
// just the world getting smaller. Lakes go first because a lake is enclosed — draining one
// cannot sever a strait or split a sea and strand a fleet — and because Concepts/Lakes makes
// their fresh water what lets adjacent land be irrigated far inland. Take the lake and the
// green around it has nothing behind it.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Advances;
using CivOne.Units;

namespace CivOne.Tests
{
	public class ScavengerTests
	{
		private static Game AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			return Game.Instance;
		}

		// A small enclosed body of water with land all round it: a lake.
		private static (int x, int y)[] ALake(int x0, int y0)
		{
			Map m = Map.Instance;
			for (int y = y0 - 2; y <= y0 + 2; y++)
			for (int x = x0 - 2; x <= x0 + 2; x++)
				m.ChangeTileType(x, y, Terrain.Grassland1);
			var tiles = new[] { (x0, y0), (x0 + 1, y0) };
			foreach (var (x, y) in tiles) m.ChangeTileType(x, y, Terrain.Ocean);
			m.RecalculateContinentsIfDirty();
			m.ComputeFreshwaterLakes();
			return tiles;
		}

		// ── draining ────────────────────────────────────────────────────────

		// The defect, stated directly: water becomes exposed seabed, not nothing.
		[Fact]
		public void ADrainedTileBecomesSaltFlat()
		{
			Game g = AWorld();
			var lake = ALake(40, 25);

			Assert.True(g.DrainTile(lake[0].x, lake[0].y));

			Assert.Equal(Terrain.SaltFlat, Map.Instance[lake[0].x, lake[0].y].Type);
			Assert.False(Map.Instance[lake[0].x, lake[0].y].IsOcean);
		}

		// Never under a city. The warming flood spares cities, and this settles what happens to
		// a floating city founded on ocean by a HydroEngineer.
		[Fact]
		public void ACityOnTheWaterIsNeverDrained()
		{
			Game g = AWorld();
			var lake = ALake(40, 25);
			Player p = g.HumanPlayer;
			p.Explore(lake[0].x, lake[0].y, range: 3);
			g.AddCity(p, 0, lake[0].x, lake[0].y);

			Assert.False(g.DrainTile(lake[0].x, lake[0].y), "the harvesters do not drain a city");
			Assert.True(Map.Instance[lake[0].x, lake[0].y].IsOcean);
		}

		// A ship on a tile that becomes land is lost — the mirror of the warming flood
		// disbanding land units on tiles that become sea.
		[Fact]
		public void AShipCaughtOnTheTileIsLost()
		{
			Game g = AWorld();
			var lake = ALake(40, 25);
			IUnit boat = g.CreateUnit(UnitType.Trireme, lake[0].x, lake[0].y, 1)!;

			g.DrainTile(lake[0].x, lake[0].y);

			Assert.DoesNotContain(g.GetUnits(), u => u == boat);
		}

		// Dry land is not drainable, so a second pass over the same ground does nothing.
		[Fact]
		public void DrainingIsNotRepeatable()
		{
			Game g = AWorld();
			var lake = ALake(40, 25);

			Assert.True(g.DrainTile(lake[0].x, lake[0].y));
			Assert.False(g.DrainTile(lake[0].x, lake[0].y));
		}

		// Lakes first: the extraction pass takes fresh water before it touches the sea.
		[Fact]
		public void TheHarvestTakesLakesFirst()
		{
			Game g = AWorld();
			ALake(40, 25);

			int drained = g.DrainNextLakeTiles(4);

			Assert.True(drained > 0, "a lake should have been found");
			Assert.Contains(Map.Instance.AllTiles(), t => t.Type == Terrain.SaltFlat);
		}

		// ── the harvest clock ───────────────────────────────────────────────

		[Fact]
		public void NothingDrainsWhileTheyAreNotHere()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.GameTurn = 30;                 // a multiple of the interval

			g.ProcessScavengerExtraction();

			Assert.DoesNotContain(Map.Instance.AllTiles(), t => t.Type == Terrain.SaltFlat);
		}

		[Fact]
		public void TheHarvestRunsWhileTheyAreHere()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.ScavengerExtractionUntil = 200;
			// A craft on the ground, not just a clock: the harvesters do the work, so setting
			// the clock alone drains nothing. That is the counterplay, and it means this test
			// has to stand one up.
			g.CreateUnit(UnitType.Harvester, 38, 25, 0);
			g.GameTurn = 30;

			g.ProcessScavengerExtraction();

			Assert.Contains(Map.Instance.AllTiles(), t => t.Type == Terrain.SaltFlat);
		}

		// They take what they came for and go. Unlike the other three arcs there is no victory
		// condition attached — the only question is how much is left when the clock runs out.
		[Fact]
		public void TheHarvestEnds()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.ScavengerExtractionUntil = 100;

			g.GameTurn = 99;
			Assert.True(g.ScavengersExtracting);
			g.GameTurn = 150;
			Assert.False(g.ScavengersExtracting);

			g.ProcessScavengerExtraction();
			Assert.DoesNotContain(Map.Instance.AllTiles(), t => t.Type == Terrain.SaltFlat);
		}

		// ── the harvesters, and the counterplay ─────────────────────────────

		// The craft do the work, not the clock. This is the whole answer to "you watch and
		// cannot do anything": destroy them and the water stops leaving.
		[Fact]
		public void NoHarvestersMeansNoExtraction()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.ScavengerExtractionUntil = 200;
			g.GameTurn = 30;
			Assert.Empty(g.Harvesters());

			g.ProcessScavengerExtraction();

			Assert.DoesNotContain(Map.Instance.AllTiles(), t => t.Type == Terrain.SaltFlat);
		}

		[Fact]
		public void KillingTheLastHarvesterEndsTheHarvestEarly()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.ArriveScavengers();
			g.GameTurn = (ushort)(Game.Instance.GameTurn + 3 - (Game.Instance.GameTurn % 3));
			Assert.NotEmpty(g.Harvesters());

			foreach (IUnit craft in g.Harvesters()) g.DisbandUnit(craft);
			int saltBefore = Map.Instance.AllTiles().Count(t => t.Type == Terrain.SaltFlat);

			for (int t = 0; t < 12; t++) { g.GameTurn++; g.ProcessScavengerExtraction(); }

			Assert.Equal(saltBefore, Map.Instance.AllTiles().Count(t => t.Type == Terrain.SaltFlat));
			Assert.True(g.ScavengersExtracting, "the clock has not run out — the craft are simply gone");
		}

		// They come down on dry land beside fresh water: an army has to be able to reach them,
		// and a craft standing on the lake it is draining would be destroying its own footing.
		[Fact]
		public void HarvestersLandOnReachableGround()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.ArriveScavengers();

			IUnit[] craft = g.Harvesters();
			Assert.NotEmpty(craft);
			Assert.All(craft, c => Assert.False(Map.Instance[c.X, c.Y].IsOcean,
				"a harvester must stand on ground troops can walk to"));
		}

		// Machinery, not a raider. The generic barbarian land AI would send it off to sack a
		// city, which is the one thing it has no interest in.
		[Fact]
		public void AHarvesterDoesNotGoRaiding()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.ArriveScavengers();
			IUnit craft = g.Harvesters().First();
			int x = craft.X, y = craft.Y;

			AI.Instance(g.GetPlayer(0)).Move(craft);

			Assert.Equal((x, y), (craft.X, craft.Y));
		}

		// Nobody can build one, however far up the tree they get.
		[Fact]
		public void NobodyCanBuildAHarvester()
		{
			Game g = AWorld();
			Player p = g.HumanPlayer;
			foreach (IAdvance a in Common.Advances) p.AddAdvance(a, false);

			Assert.False(p.ProductionAvailable(new Harvester()));
		}

		// ── the arrival ─────────────────────────────────────────────────────

		// The moon is the overture: one catastrophic surge from the debris and the tide, then
		// the water starts going the other way. Reuses the warming flood pass verbatim.
		[Fact]
		public void TheMoonBreakingFloodsBeforeTheDrought()
		{
			Game g = AWorld();
			int warmingBefore = g.GlobalWarmingCount;

			g.ArriveScavengers();

			Assert.True(g.GlobalWarmingCount > warmingBefore, "the tide came in once");
			Assert.True(g.ScavengersExtracting, "...and then the pumps started");
		}
	}
}
