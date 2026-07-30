// CivOne tests
//
// Three things found in a turn-578 autoplayed game where 30% of Japan's worked land
// was swamp, jungle or forest and no settler would touch any of it:
//
//   1. Draining swamp / clearing jungle is an IRRIGATE order the engine already
//      implements (Settlers.cs:438), but the AI's validIrrigation test excluded
//      those terrains, so that land was permanently worthless to it.
//   2. Tile pollution and the global-warming counter were absent from the .cos
//      writer, so the state that drives warming reset on every load.
//   3. Storms rolled per coastal city per turn — several landfalls a turn on a
//      settled map, each Major one destroying a building and converting a worked
//      coastal tile to swamp.

using System;
using System.IO;
using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;
using CivOne.Buildings;

namespace CivOne.Tests
{
	public class TerraformAndStormTests
	{
		// A settler standing on swamp inside its own city's radius must be given the
		// irrigate (drain) order rather than being left idle. Driven through the real
		// AI.Move path, not by calling the predicate directly.
		[Fact]
		public void Settler_DrainsSwampInsideOwnCityRadius()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;

			// A city, and swamp everywhere it works.
			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Swamp);
			Map.Instance.RecalculateContinentsIfDirty();
			City city = Game.Instance.AddCity(player, 0, cx, cy)!;
			Assert.NotNull(city);

			// A settler on a swamp tile one step off the city centre. Pick one with no
			// strategic resource on it: map generation scatters those at random, and a
			// settler standing on an unclaimed deposit builds a camp instead of terraforming
			// — which made this test pass or fail depending on the seed.
			ITile? spot = null;
			for (int dy = -1; dy <= 1 && spot is null; dy++)
			for (int dx = -1; dx <= 1 && spot is null; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				ITile t = Map.Instance[cx + dx, cy + dy];
				if (t is Swamp && Game.ResourceAt(t) == StrategicResource.None) spot = t;
			}
			Assert.NotNull(spot);

			IUnit settler = Game.Instance.CreateUnit(UnitType.Settlers, spot!.X, spot.Y,
				Game.Instance.PlayerNumber(player))!;
			Assert.NotNull(settler);
			Assert.True(Map.Instance[settler.X, settler.Y] is Swamp, "precondition: standing on swamp");

			// Already roaded, so the road order (which legitimately comes first in the
			// Expand stance) is not the available work — drainage is.
			Map.Instance[settler.X, settler.Y].Road = true;

			Sim.ClearTasks();   // drop the UI tasks AddCity queued; they never finish headlessly
			AI.Instance(player).Move(settler);

			// The order is enqueued as a GameTask; pump the queue so it actually runs.
			for (int i = 0; i < 20 && GameTask.Any(); i++) GameTask.Update();

			// BuildingIrrigation is the countdown the drain order sets (4 turns on swamp).
			Assert.True(((Settlers)settler).BuildingIrrigation > 0,
				"a settler on swamp in its own city radius should start draining it");
		}

		// An interior settler in a warring empire terraforms like a peacetime one. AI wars
		// are rarely concluded, only abandoned, so the empire-wide Militarize stance — where
		// roads outrank irrigation — otherwise covers the whole late game everywhere.
		[Fact]
		public void InteriorSettler_IrrigatesEvenWhileTheEmpireIsAtWar()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			Player enemy = Game.Instance.Players.First(p => p != player && !p.IsDestroyed()
				&& !(p.Civilization is CivOne.Civilizations.Barbarian));
			player.DeclareWar(enemy);
			// Monarchy, so the despot suppression of classic irrigation is not what is being
			// measured here — this test is about the war stance, nothing else.
			player.Government = new CivOne.Governments.Monarchy();

			// Grassland with a river alongside, so classic irrigation is legal, and a road
			// already down so roads are not the available work.
			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Grassland1);
			Map.Instance.ChangeTileType(cx + 2, cy, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();
			City city = Game.Instance.AddCity(player, 0, cx, cy)!;
			Assert.NotNull(city);

			// Clear the neighbourhood: map generation drops starting units wherever it
			// likes, and one foreign scout inside 8 tiles would make this legitimately
			// frontline ground and prove nothing.
			foreach (IUnit stray in Game.Instance.GetUnits()
				.Where(u => u.Owner != Game.Instance.PlayerNumber(player)
				         && Common.DistanceToTile(u.X, u.Y, cx, cy) <= 12).ToArray())
				Game.Instance.DisbandUnit(stray);

			IUnit settler = Game.Instance.CreateUnit(UnitType.Settlers, cx + 1, cy,
				Game.Instance.PlayerNumber(player))!;
			Map.Instance[cx + 1, cy].Road = true;

			var near = Game.Instance.GetUnits().Where(u => u.Owner != Game.Instance.PlayerNumber(player)
				&& Common.DistanceToTile(u.X, u.Y, cx + 1, cy) <= 8).ToArray();
			Assert.True(near.Length == 0,
				"precondition: no hostile unit within 8 tiles, found: " + string.Join(", ",
					near.Select(u => $"{u.GetType().Name}@({u.X},{u.Y}) owner {u.Owner}")));

			Sim.ClearTasks();   // drop the UI tasks AddCity queued; they never finish headlessly
			AI.Instance(player).Move(settler);
			for (int i = 0; i < 20 && GameTask.Any(); i++) GameTask.Update();

			Assert.True(((Settlers)settler).BuildingIrrigation > 0,
				"an interior settler should irrigate even while the empire is at war");
		}

		// Divestment sheds only buildings that are provably inert where they stand, and
		// never a happiness building — a Temple in a content city is usually the reason the
		// city is content, and the riot costs more than the gold.
		[Fact]
		public void Divestment_SellsInertBuildings_ButNeverHappinessOnes()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			City city = Game.Instance.AddCity(player, 0, 40, 25)!;
			Assert.NotNull(city);

			// A size-1 city carrying the infrastructure of a city it no longer is.
			city.AddBuilding(new Temple());
			city.AddBuilding(new Aqueduct());
			player.Gold = 0;
			Assert.True(city.Size <= 4, "precondition: too small for an Aqueduct to do anything");
			Assert.True(city.Taxes < city.TotalMaintenance, "precondition: insolvent");

			AI.Instance(player).ConsiderDivestment();

			Assert.False(city.HasBuilding<Aqueduct>(), "an Aqueduct in a tiny city is doing nothing");
			Assert.True(city.HasBuilding<Temple>(), "happiness buildings must never be divested");
		}

		// A solvent civ sells nothing, however small its cities.
		[Fact]
		public void Divestment_LeavesASolventCivAlone()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			City city = Game.Instance.AddCity(player, 0, 40, 25)!;
			city.AddBuilding(new Aqueduct());
			player.Gold = 500;

			AI.Instance(player).ConsiderDivestment();

			Assert.True(city.HasBuilding<Aqueduct>(), "a solvent civ should not be selling anything");
		}

		// Railroading an existing road must not outrank irrigating a farm tile. validRoad
		// counts road → railroad as available work, so discovering Railroad re-opened the
		// whole network and the big empires re-swept it instead of farming: every civ
		// holding Railroad finished a 750-turn game at 6-9% of its worked land irrigated.
		[Fact]
		public void RailUpgrade_DoesNotOutrankIrrigation()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			player.Government = new CivOne.Governments.Monarchy();   // despot rule not under test
			player.AddAdvance(new CivOne.Advances.RailRoad());       // every road is now upgradable

			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Grassland1);
			Map.Instance.ChangeTileType(cx + 2, cy, Terrain.River);   // irrigation water source
			Map.Instance.RecalculateContinentsIfDirty();
			City city = Game.Instance.AddCity(player, 0, cx, cy)!;
			Assert.NotNull(city);

			foreach (IUnit stray in Game.Instance.GetUnits()
				.Where(u => u.Owner != Game.Instance.PlayerNumber(player)
				         && Common.DistanceToTile(u.X, u.Y, cx, cy) <= 12).ToArray())
				Game.Instance.DisbandUnit(stray);

			ITile spot = Map.Instance[cx + 1, cy];
			spot.Road = true;              // roaded, not railed: the upgrade is available
			Assert.False(spot.RailRoad);
			Assert.False(spot.Irrigation);

			IUnit settler = Game.Instance.CreateUnit(UnitType.Settlers, cx + 1, cy,
				Game.Instance.PlayerNumber(player))!;

			Sim.ClearTasks();
			AI.Instance(player).Move(settler);
			for (int i = 0; i < 20 && GameTask.Any(); i++) GameTask.Update();

			Assert.True(((Settlers)settler).BuildingIrrigation > 0,
				"food should win over a railroad upgrade on an already-roaded tile");
		}

		// ...but laying the FIRST road on a bare tile is finite, useful work and still
		// comes first in the EXPANSION stance. Which stance a one-city civ lands in depends
		// on the generated map — a civ hemmed in at its start is legitimately in Develop,
		// where irrigation leads — so the assertion follows the stance. It still fails if
		// first roads are ever demoted below irrigation in Expand, which is the regression
		// this guards.
		[Fact]
		public void FirstRoad_StillComesBeforeIrrigation()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			player.Government = new CivOne.Governments.Monarchy();

			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Grassland1);
			Map.Instance.ChangeTileType(cx + 2, cy, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();
			Game.Instance.AddCity(player, 0, cx, cy);

			ITile spot = Map.Instance[cx + 1, cy];
			Assert.False(spot.Road, "precondition: bare ground");

			IUnit settler = Game.Instance.CreateUnit(UnitType.Settlers, cx + 1, cy,
				Game.Instance.PlayerNumber(player))!;
			string stance = AI.Instance(player).CurrentStanceName();
			Sim.ClearTasks();
			AI.Instance(player).Move(settler);
			for (int i = 0; i < 20 && GameTask.Any(); i++) GameTask.Update();

			var s = (Settlers)settler;
			bool roaded = s.BuildingRoad > 0 || spot.Road;
			if (stance == "Expand")
				Assert.True(roaded, "in Expand, a bare tile should get its first road before irrigation");
			else
				Assert.True(roaded || s.BuildingIrrigation > 0,
					$"stance {stance}: the settler should be doing SOME useful work on a bare tile");
		}

		// A one-city civ whose ground tops out at size 2 must still be able to build the
		// settler that lifts it. Size 3 needs a food surplus, the surplus needs irrigation,
		// and the irrigation needs a settler — the Lakota sat in that loop for 590 turns
		// with one size-2 city, fifteen production picks and no settler ever built.
		[Fact]
		public void StalledTinyCiv_BuildsTheSettlerThatUnlocksIt()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;

			// Plains all round (1 food each under the despot penalty) with a river beside
			// the city, so there IS improvable land but the city cannot feed a third citizen.
			int cx = 40, cy = 25;
			for (int dy = -3; dy <= 3; dy++)
			for (int dx = -3; dx <= 3; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Plains);
			Map.Instance.ChangeTileType(cx + 1, cy, Terrain.River);
			Map.Instance.RecalculateContinentsIfDirty();
			City city = Game.Instance.AddCity(player, 0, cx, cy)!;
			Assert.NotNull(city);
			player.Explore(cx, cy, range: 3);   // CityRadius only counts tiles the owner has seen
			city.Size = 2;
			city.ResetResourceTiles();          // Size alone does not assign worked tiles

			// Some tribes start with Pottery, and the settler rule deliberately wants the
			// Granary built first in that case — a separate gate from the size one under
			// test here. Give the city its Granary so the scenario is about size alone.
			city.AddBuilding(new Granary());

			// Map generation hands out starting settlers; the hatch deliberately refuses to
			// build a second one while any is alive, so clear them to reach the case.
			foreach (IUnit s0 in Game.Instance.GetUnits()
				.Where(u => u.Owner == Game.Instance.PlayerNumber(player) && u is Settlers).ToArray())
				Game.Instance.DisbandUnit(s0);

			// Bonus resources are seeded from the map word and survive ChangeTileType, so the
			// exact surplus is not fully scriptable. Grow until the city is genuinely stalled
			// — each extra citizen eats 2 food, so this converges.
			for (int guard = 0; guard < 6 && city.FoodIncome > 1; guard++)
			{
				city.Size = (byte)(city.Size + 1);
				city.ResetResourceTiles();
			}
			Assert.True(city.FoodIncome <= 1, $"precondition: stalled, food was {city.FoodIncome}");
			Assert.True(city.Size < 3, $"precondition: below the normal settler gate, size {city.Size}");
			Assert.False(Game.Instance.GetUnits().Any(u => u is Settlers && u.Owner == Game.Instance.PlayerNumber(player)),
				"precondition: no settler in the field");

			Sim.ClearTasks();
			AI.Instance(player).CityProduction(city);
			var plan = new System.Collections.Generic.List<IProduction>();
			if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
			plan.AddRange(city.ProductionQueue);

			Assert.Contains(plan, p => p is Settlers);
		}

		// The random production fallback must respect the caps the deliberate rules
		// enforce. Explorers are capped at two per civ, but the fallback picked from
		// everything available and minted them without limit — Japan reached 21 alive.
		[Fact]
		public void ProductionFallback_RespectsTheExplorerCap()
		{
			Sim.NewGame(width: 80, height: 50);
			Settings.Instance.Autopilot = true;
			Player player = Game.Instance.HumanPlayer;
			City city = Game.Instance.AddCity(player, 0, 40, 25)!;
			byte id = Game.Instance.PlayerNumber(player);

			for (int i = 0; i < 3; i++)
				Game.Instance.CreateUnit(UnitType.Explorer, 40, 25, id);
			Assert.True(Game.Instance.GetUnits().Count(u => u.Owner == id && u is Explorer) >= 2,
				"precondition: already at or past the cap");

			// Re-plan many times: the fallback picks at random, so once proves nothing.
			for (int i = 0; i < 40; i++)
			{
				Sim.ClearTasks();
				AI.Instance(player).CityProduction(city);
				var plan = new System.Collections.Generic.List<IProduction>();
				if (city.CurrentProduction is not null) plan.Add(city.CurrentProduction);
				plan.AddRange(city.ProductionQueue);
				Assert.DoesNotContain(plan, p => p is Explorer);
			}
		}

		// Pollution and the warming counter must survive a save/load round trip.
		[Fact]
		public void PollutionAndWarmingCount_SurviveARoundTrip()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;

			// A polluted tile whose ONLY flag is pollution — the case the writer skipped.
			ITile smoggy = Map.Instance[30, 20];
			smoggy.Pollution = true;
			Assert.False(smoggy.Road || smoggy.Irrigation || smoggy.Mine || smoggy.Hut);
			g.GlobalWarmingCount = 3;
			g.LastHurricaneYear = -250;

			string path = Path.Combine(Settings.Instance.SavesDirectory, "warming_roundtrip.cos");
			Directory.CreateDirectory(Settings.Instance.SavesDirectory);
			g.SaveCos(path);

			Sim.ResetState();
			Assert.True(Game.LoadCos(path), "save should reload");

			Assert.True(Map.Instance[30, 20].Pollution, "pollution should survive the round trip");
			Assert.Equal(3, Game.Instance.GlobalWarmingCount);
			Assert.Equal(-250, Game.Instance.LastHurricaneYear);
		}

		// A save written before the cooldown existed has no LastHurricaneYear. Reading the
		// missing 0 as "a storm in year 0" would suppress every storm in a BC-era game
		// until 5 AD, so the loader must treat it as "never".
		[Fact]
		public void MissingHurricaneYear_DoesNotSuppressAncientStorms()
		{
			Sim.NewGame(width: 80, height: 50);
			Game g = Game.Instance;
			Assert.True(g.LastHurricaneYear < -4000,
				$"default should predate the earliest game year, was {g.LastHurricaneYear}");

			string path = Path.Combine(Settings.Instance.SavesDirectory, "no_storm_year.cos");
			Directory.CreateDirectory(Settings.Instance.SavesDirectory);
			g.SaveCos(path);
			Sim.ResetState();
			Assert.True(Game.LoadCos(path));

			// -4000 BC minus "never" must clear the five-year cooldown.
			Assert.True(-4000 - Game.Instance.LastHurricaneYear >= Game.HurricaneCooldownYears,
				"an ancient-era game must not be storm-locked by an absent save field");
		}
	}
}
