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
using CivOne.Tiles;
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

		// ── coastal recession ───────────────────────────────────────────────

		// Open sea ABOVE a coastline, land below. The sea is on top deliberately: the scan
		// is row-major from y=0, so deep water is visited BEFORE the shore. With the land on
		// top, scan order alone puts the shallows first and a "shallow-first" test passes
		// even against code that has no such rule — which a first version of this did.
		private static void ACoast(int shoreY)
		{
			Map m = Map.Instance;
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				m.ChangeTileType(x, y, y >= shoreY ? Terrain.Grassland1 : Terrain.Ocean);
			m.RecalculateContinentsIfDirty();
			m.ComputeFreshwaterLakes();
		}

		// Shallow-first, never at random: only ocean that already touches land is eligible, so
		// the shoreline recedes instead of the sea going moth-eaten.
		[Fact]
		public void OnlyTheShallowsGo()
		{
			Game g = AWorld();
			ACoast(20);

			g.DrainNextCoastTiles(8);

			// Everything taken sits on the old shoreline (y == 19, the row touching land at
			// y == 20); nothing from the deep water above it.
			var taken = Map.Instance.AllTiles().Where(t => t.Type == Terrain.SaltFlat).ToArray();
			Assert.NotEmpty(taken);
			Assert.All(taken, t => Assert.True(t.Y >= 19,
				$"deep water at ({t.X},{t.Y}) went before the shallows"));
		}

		// The bite widens from where they started rather than nibbling the whole coast at once.
		[Fact]
		public void TheBiteWidensFromWhereItStarted()
		{
			Game g = AWorld();
			ACoast(20);
			Map.Instance.ChangeTileType(40, 19, Terrain.SaltFlat);   // an existing bite

			g.DrainNextCoastTiles(3);

			// Tiles beside the existing salt are taken before untouched coast elsewhere. Row 18
			// is deep water everywhere EXCEPT where it now touches the bite at (40,19).
			Assert.Contains(Map.Instance.AllTiles(),
				t => t.Type == Terrain.SaltFlat && t.Y == 18 && System.Math.Abs(t.X - 40) <= 1);
		}

		// The hazard this slice exists for. Draining can close a strait, and water-body ids are
		// a reachability oracle — so after a batch the renumbering must have run, or ships will
		// keep planning routes across ground.
		[Fact]
		public void TheReachabilityOracleIsRebuiltAfterTheCoastMoves()
		{
			Game g = AWorld();
			ACoast(20);
			g.ScavengerExtractionUntil = 400;
			g.CreateUnit(UnitType.Harvester, 10, 10, 0);
			g.GameTurn = 30;

			g.ProcessScavengerExtraction();

			// Every remaining sea tile carries a water-body id: a stale oracle leaves drained
			// ground still numbered as water, or new coast unnumbered.
			Assert.All(Map.Instance.AllTiles().Where(t => t.Type == Terrain.SaltFlat),
				t => Assert.False(t.IsOcean, "drained ground must not still read as sea"));
		}

		// A city on the coast is never drained out from under, but it can be left inland —
		// which is the intended catastrophe, not a crash.
		[Fact]
		public void APortCanBeLeftInland()
		{
			Game g = AWorld();
			ACoast(20);
			Player p = g.HumanPlayer;
			p.Explore(40, 20, range: 3);
			City port = g.AddCity(p, 0, 40, 20)!;
			Assert.Contains(Map.Instance[40, 20].GetBorderTiles(), t => t is not null && t.IsOcean);

			// Take the whole shoreline in front of it.
			for (int pass = 0; pass < 6; pass++) g.DrainNextCoastTiles(400);

			Assert.NotNull(Map.Instance[40, 20].City);
			Assert.DoesNotContain(Map.Instance[40, 20].GetBorderTiles(),
				t => t is not null && t.IsOcean);
		}

		// Lakes before the sea: fresh water is what they lift cheapest, and taking a lake can
		// strand nothing.
		[Fact]
		public void LakesGoBeforeTheSea()
		{
			Game g = AWorld();
			ACoast(20);
			// A lake inland, well clear of the coast.
			Map.Instance.ChangeTileType(10, 40, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			Map.Instance.ComputeFreshwaterLakes();
			g.ScavengerExtractionUntil = 400;
			// ONE craft, so the budget is one tile: whichever water goes is the water they
			// reached for first. With a bigger budget both go and the ordering is invisible —
			// which is how a weaker version of this passed against coast-first code.
			g.CreateUnit(UnitType.Harvester, 12, 42, 0);
			g.GameTurn = 30;

			g.ProcessScavengerExtraction();

			Assert.Equal(Terrain.SaltFlat, Map.Instance[10, 40].Type);
			Assert.DoesNotContain(Map.Instance.AllTiles(),
				t => t.Type == Terrain.SaltFlat && t.Y < 20);
		}

		// ── the seams ───────────────────────────────────────────────────────

		// Strategic resources are derived from tile specials (Game.ResourceAt), so emptying the
		// seam removes the Coal — and the production a city or camp was drawing from it.
		[Fact]
		public void AHarvesterTakesTheCoal()
		{
			Game g = AWorld();
			Map.Instance.ChangeTileType(30, 30, Terrain.Hills);
			((CivOne.Tiles.BaseTile)Map.Instance[30, 30]).Special = true;
			Assert.Equal(StrategicResource.Coal, Game.ResourceAt(Map.Instance[30, 30]));

			Assert.True(g.StripSpecial(30, 30));

			Assert.Equal(StrategicResource.None, Game.ResourceAt(Map.Instance[30, 30]));
			Assert.Equal(Terrain.Hills, Map.Instance[30, 30].Type);   // still a hill, just an ordinary one
		}

		// A camp on a stripped seam has nothing left to ship.
		[Fact]
		public void StrippingASeamClosesTheCampOnIt()
		{
			Game g = AWorld();
			Map.Instance.ChangeTileType(30, 30, Terrain.Hills);
			((CivOne.Tiles.BaseTile)Map.Instance[30, 30]).Special = true;
			g.ResourceCamps[(30, 30)] = 1;

			g.StripSpecial(30, 30);

			Assert.DoesNotContain(g.ResourceCamps.Keys, k => k == (30, 30));
		}

		// Ordinary terrain has nothing to take: only Iron, Coal and Oil count.
		[Fact]
		public void ThereIsNothingToTakeFromGrass()
		{
			Game g = AWorld();
			Map.Instance.ChangeTileType(30, 30, Terrain.Grassland1);

			Assert.False(g.StripSpecial(30, 30));
		}

		// ── walking ─────────────────────────────────────────────────────────

		// A craft with nothing left in reach moves on, which is what makes them a front rather
		// than six fixed nuisances — and what gives a player time to march on them.
		[Fact]
		public void AHarvesterWalksOnWhenItsSiteIsEmpty()
		{
			Game g = AWorld();
			// All land, one lake far away: nothing to work where it stands.
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.ChangeTileType(60, 30, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();
			Map.Instance.ComputeFreshwaterLakes();

			g.ScavengerExtractionUntil = 400;
			IUnit craft = g.CreateUnit(UnitType.Harvester, 20, 30, 0)!;
			int startX = craft.X;
			g.GameTurn = 30;

			g.ProcessScavengerExtraction();

			Assert.True(craft.X > startX, $"the craft should have walked toward the water ({craft.X} vs {startX})");
		}

		// It works what it is standing beside before it goes anywhere.
		[Fact]
		public void AHarvesterWorksItsSiteBeforeMovingOn()
		{
			Game g = AWorld();
			ALake(40, 25);
			g.ScavengerExtractionUntil = 400;
			IUnit craft = g.CreateUnit(UnitType.Harvester, 39, 25, 0)!;
			int x = craft.X, y = craft.Y;
			g.GameTurn = 30;

			g.ProcessScavengerExtraction();

			Assert.Equal((x, y), (craft.X, craft.Y));
			Assert.Equal(Terrain.SaltFlat, Map.Instance[40, 25].Type);
		}

		// They walk the shoreline; they do not invade.
		[Fact]
		public void AHarvesterNeverWalksIntoACity()
		{
			Game g = AWorld();
			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.ChangeTileType(60, 30, Terrain.Ocean);
			Map.Instance.RecalculateContinentsIfDirty();

			Player p = g.HumanPlayer;
			p.Explore(21, 30, range: 2);
			g.AddCity(p, 0, 21, 30);
			g.ScavengerExtractionUntil = 400;
			IUnit craft = g.CreateUnit(UnitType.Harvester, 20, 30, 0)!;
			g.GameTurn = 30;

			g.ProcessScavengerExtraction();

			Assert.NotEqual((21, 30), (craft.X, craft.Y));
		}

		// ── what draws them ─────────────────────────────────────────────────

		// The design question: the Scavengers read the LARDER, not the character. An untouched
		// world is a full pantry; a settled, worked one is picked over. That runs opposite to
		// the Refugees/Owners axis, where conduct is everything.
		[Fact]
		public void AnUntouchedWorldIsAFullerLarderThanAWorkedOne()
		{
			Game g = AWorld();
			// Seams nobody is near.
			for (int i = 0; i < 12; i++)
			{
				Map.Instance.ChangeTileType(10 + i * 4, 40, Terrain.Hills);
				((CivOne.Tiles.BaseTile)Map.Instance[10 + i * 4, 40]).Special = true;
			}
			double wild = g.LarderScore();

			// Now work them: a camp on each.
			for (int i = 0; i < 12; i++) g.ResourceCamps[(10 + i * 4, 40)] = 1;
			double worked = g.LarderScore();

			Assert.True(worked < wild,
				$"a worked world must read as a poorer target ({worked:0.00} vs {wild:0.00})");
		}

		// A drained world is a poorer target than a wet one — so a previous harvest, or a run
		// of global warming, changes the odds for the next visitor.
		[Fact]
		public void ADrainedWorldIsAPoorerTarget()
		{
			Game g = AWorld();
			double wet = g.LarderScore();

			for (int y = 0; y < Map.HEIGHT; y++)
			for (int x = 0; x < Map.WIDTH; x++)
				if (Map.Instance[x, y].IsOcean) Map.Instance.ChangeTileType(x, y, Terrain.SaltFlat);
			double dry = g.LarderScore();

			Assert.True(dry < wet, $"an emptied world must read as poorer ({dry:0.00} vs {wet:0.00})");
		}

		// Never zero and never certain: a draw a perfect player can drive to impossible stops
		// being a threat and becomes a checklist.
		[Fact]
		public void TheLarderIsAlwaysWithinBounds()
		{
			Game g = AWorld();
			double larder = g.LarderScore();
			Assert.InRange(larder, 0.0, 1.0);
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
