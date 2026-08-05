// CivOne tests
//
// Three story factions were reaching the general AI production path by fall-through, because
// only the Registry had a block of its own. A turn-750 save showed the consequence: The Thing
// holding the Pyramids and J.S.BACH'S CATHEDRAL — an assimilating organism commissioning
// choral music — and both it and Skynet researching from laboratories neither should have.
//
// The gates here: the organism studies nothing and inherits everything from what it eats; the
// Registry landed knowing all but FutureTech so has nothing to choose; Skynet researches (it
// is meant to out-tech the world) but builds only machine work.
//
// And the third act. The Thing was a doom with no ending — it grew until the game clock ran
// out. Now, once it holds a quarter of the world and has taken Space Flight off somebody, it
// stops spreading and builds The Vessel while its cities are stripped for material. When the
// Vessel completes it leaves and razes everything it held. Razes, not hands on: if departure
// returned a working empire to the survivors, letting it win would be the best play available.

using System.Linq;
using CivOne;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Wonders;

namespace CivOne.Tests
{
	public class ThingAscensionTests
	{
		private static (Game, Player victim) AWorld()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Player v = g.Players.First(p => p is not null && g.PlayerNumber(p) != 0 && p != g.HumanPlayer);
			return (g, v);
		}

		private static City ACity(Player owner, int x, int y = 25, int size = 4)
		{
			Game g = Game.Instance;
			Map.Instance.ChangeTileType(x, y, Terrain.Grassland1);
			Map.Instance.RecalculateContinentsIfDirty();
			owner.Explore(x, y, range: 3);
			City c = g.AddCity(owner, x + y, x, y)!;
			c.Size = (byte)size;
			return c;
		}

		private static Player TheOrganism(Game g)
			=> g.Players.First(p => p is not null && p.Civilization is Civilizations.TheThing);

		// ── what the factions may know ──────────────────────────────────────

		// The organism does not study. Everything it holds came out of somebody it ate.
		[Fact]
		public void TheThingAbsorbsWhatItAssimilates()
		{
			(Game g, Player victim) = AWorld();
			victim.AddAdvance(new SpaceFlight(), false);
			victim.AddAdvance(new Mathematics(), false);
			City c = ACity(victim, 40);

			g.InfectCity(c);
			Player thing = TheOrganism(g);

			Assert.True(thing.HasAdvance<SpaceFlight>(), "it takes what they knew");
			Assert.True(thing.HasAdvance<Mathematics>());
		}

		[Fact]
		public void TheThingNeverResearches()
		{
			(Game g, Player victim) = AWorld();
			g.InfectCity(ACity(victim, 40));
			Player thing = TheOrganism(g);

			AI.Instance(thing).ChooseResearch();
			Assert.Null(thing.CurrentResearch);
		}

		// Skynet is the one faction that ought to out-tech the world; it keeps its laboratories.
		[Fact]
		public void SkynetStillResearches()
		{
			(Game g, Player victim) = AWorld();
			Player machine = new Player(Common.Civilizations.First(c => c is Civilizations.Skynet));
			g.AddPlayer(machine);
			ACity(machine, 44);

			AI.Instance(machine).ChooseResearch();
			Assert.NotNull(machine.CurrentResearch);
		}

		// ── what they may build ─────────────────────────────────────────────

		// The defect, stated directly: no more cathedrals.
		[Fact]
		public void TheThingBuildsNoOrdinaryWonders()
		{
			(Game g, Player victim) = AWorld();
			foreach (IAdvance a in Common.Advances) victim.AddAdvance(a, false);
			City c = ACity(victim, 40, size: 8);
			g.InfectCity(c);
			Player thing = TheOrganism(g);

			// The whole plan, not just what it settled on: a garrison outranks a wonder, so
			// CurrentProduction would stay Militia and hide an ordinary wonder sitting behind it.
			IProduction[] plan = AI.Instance(thing).ProductionPlan(c);
			IProduction[] wonders = plan.Where(p => p is IWonder && p is not TheVessel).ToArray();

			Assert.True(wonders.Length == 0, "the organism considered: "
				+ string.Join(", ", wonders.Select(w => (w as ICivilopedia)?.Name)));
		}

		// The companion: Skynet builds machine work and nothing else. It had been reaching the
		// general list, which is how a network ends up considering Women's Suffrage.
		[Fact]
		public void SkynetBuildsOnlyMachineWonders()
		{
			(Game g, Player victim) = AWorld();
			Player machine = new Player(Common.Civilizations.First(c => c is Civilizations.Skynet));
			g.AddPlayer(machine);
			foreach (IAdvance a in Common.Advances) machine.AddAdvance(a, false);
			City c = ACity(machine, 44, size: 8);

			IProduction[] plan = AI.Instance(machine).ProductionPlan(c);
			IWonder[] wonders = plan.OfType<IWonder>().ToArray();

			Assert.All(wonders, w => Assert.True(
				w is NanobotFactory or ManhattanProject or FusionCore
				  or InterstellarProbe or HumanGenomeProject,
				$"the network considered {(w as ICivilopedia)?.Name}"));
		}

		// ── The Vessel ──────────────────────────────────────────────────────

		[Fact]
		public void OnlyTheThingMayBuildTheVessel()
		{
			(Game g, Player victim) = AWorld();
			victim.AddAdvance(new SpaceFlight(), false);
			Assert.False(victim.ProductionAvailable(new TheVessel()));
			Assert.False(g.HumanPlayer.ProductionAvailable(new TheVessel()));
		}

		// It inherits, it does not invent: no assimilated Space Flight, no way off the planet.
		[Fact]
		public void TheVesselNeedsAssimilatedSpaceFlight()
		{
			(Game g, Player victim) = AWorld();
			g.InfectCity(ACity(victim, 40));
			Player thing = TheOrganism(g);
			Assert.False(thing.ProductionAvailable(new TheVessel()),
				"a world that never reached for space gives it no way out");

			thing.AddAdvance(new SpaceFlight(), false);
			Assert.True(thing.ProductionAvailable(new TheVessel()));
		}

		// ── the third act ───────────────────────────────────────────────────

		// Builds a world where the organism holds `thingCities` of `total`, with Space Flight
		// assimilated unless told otherwise.
		private static (Game, Player thing) AnInfectedWorld(int thingCities, int total, bool spaceFlight = true)
		{
			(Game g, Player victim) = AWorld();
			if (spaceFlight) victim.AddAdvance(new SpaceFlight(), false);

			for (int i = 0; i < total; i++) ACity(victim, 20 + i, y: 20 + (i % 8), size: 4);
			City[] all = g.GetCities().Where(c => c.Size > 0).ToArray();
			foreach (City c in all.Take(thingCities)) g.InfectCity(c);

			return (g, TheOrganism(g));
		}

		[Fact]
		public void ItKeepsSpreadingUntilItHoldsEnoughOfTheWorld()
		{
			(Game g, Player thing) = AnInfectedWorld(thingCities: 3, total: 40);
			g.ProcessThingAscension();
			Assert.False(g.ThingIsAscending, "three cities is not a world worth leaving");
		}

		[Fact]
		public void ItWillNotLeaveAWorldThatNeverReachedForSpace()
		{
			(Game g, Player thing) = AnInfectedWorld(thingCities: 20, total: 40, spaceFlight: false);
			g.ProcessThingAscension();
			Assert.False(g.ThingIsAscending, "no assimilated Space Flight, no way off");
		}

		[Fact]
		public void OnceItHoldsTheWorldItStartsBuildingTheVessel()
		{
			(Game g, Player thing) = AnInfectedWorld(thingCities: 20, total: 40);
			g.ProcessThingAscension();

			Assert.True(g.ThingIsAscending);
			Assert.Contains(thing.Cities, c => c.CurrentProduction is TheVessel);
		}

		// The cities are the material: they are consumed while the Vessel is built.
		[Fact]
		public void ItsCitiesAreStrippedWhileItBuilds()
		{
			(Game g, Player thing) = AnInfectedWorld(thingCities: 20, total: 40);
			int before = thing.Cities.Sum(c => c.Size);

			for (int t = 0; t < 12; t++) { g.GameTurn = (ushort)(100 + t); g.ProcessThingAscension(); }

			Assert.True(thing.Cities.Sum(c => c.Size) < before,
				"the organism consumes what it took to build the way out");
		}

		// Losing ground drops it back to spreading — the ascension is not a one-way latch.
		[Fact]
		public void LosingGroundStopsTheAscension()
		{
			(Game g, Player thing) = AnInfectedWorld(thingCities: 20, total: 40);
			g.ProcessThingAscension();
			Assert.True(g.ThingIsAscending);

			foreach (City c in thing.Cities.Take(15).ToArray()) g.DestroyCity(c);
			g.ProcessThingAscension();
			Assert.False(g.ThingIsAscending, "break its hold and it goes back to eating");
		}

		// Departure: it leaves, and nothing it held is left standing.
		[Fact]
		public void DepartureRazesEverythingItHeld()
		{
			(Game g, Player thing) = AnInfectedWorld(thingCities: 20, total: 40);
			City vessel = thing.Cities.First();
			int survivorsBefore = g.GetCities().Count(c => c.Size > 0 && c.Owner != g.PlayerNumber(thing));

			g.ExecuteThingDeparture(vessel);

			Assert.Empty(thing.Cities);
			Assert.Empty(g.GetUnits().Where(u => u.Owner == g.PlayerNumber(thing)));
			Assert.False(g.ThingIsAscending);
			Assert.Equal(survivorsBefore,
				g.GetCities().Count(c => c.Size > 0 && c.Owner != g.PlayerNumber(thing)));
		}

		// The point of razing rather than transferring: nobody inherits a working empire, so
		// letting it finish is never the best play available.
		[Fact]
		public void NobodyInheritsTheOrganismsCities()
		{
			(Game g, Player thing) = AnInfectedWorld(thingCities: 20, total: 40);
			g.ExecuteThingDeparture(thing.Cities.First());

			foreach (Player p in g.Players.Where(p => p is not null))
				Assert.DoesNotContain(g.GetCities().Where(c => c.Size > 0),
					c => c.Owner == g.PlayerNumber(p) && p.Civilization is Civilizations.TheThing);
		}
	}
}
