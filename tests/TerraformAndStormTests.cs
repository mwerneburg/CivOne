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
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
				Map.Instance.ChangeTileType(cx + dx, cy + dy, Terrain.Swamp);
			Map.Instance.RecalculateContinentsIfDirty();
			City city = Game.Instance.AddCity(player, 0, cx, cy)!;
			Assert.NotNull(city);

			// A settler on a swamp tile one step off the city centre.
			IUnit settler = Game.Instance.CreateUnit(UnitType.Settlers, cx + 1, cy,
				Game.Instance.PlayerNumber(player))!;
			Assert.NotNull(settler);
			Assert.True(Map.Instance[cx + 1, cy] is Swamp, "precondition: standing on swamp");

			// Already roaded, so the road order (which legitimately comes first in the
			// Expand stance) is not the available work — drainage is.
			Map.Instance[cx + 1, cy].Road = true;

			AI.Instance(player).Move(settler);

			// The order is enqueued as a GameTask; pump the queue so it actually runs.
			for (int i = 0; i < 20 && GameTask.Any(); i++) GameTask.Update();

			// BuildingIrrigation is the countdown the drain order sets (4 turns on swamp).
			Assert.True(((Settlers)settler).BuildingIrrigation > 0,
				"a settler on swamp in its own city radius should start draining it");
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
