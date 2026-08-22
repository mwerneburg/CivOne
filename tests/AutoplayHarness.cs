// CivOne tests
//
// Headless autoplay: run a fresh game forward N turns with no renderer and report how
// the world develops. This exists because single-turn checks on a loaded save cannot
// see slow failures — a change that stalls expansion looks fine on turn 1 and produces
// a world of one-city civs 600 turns later. That regression shipped once; this is the
// cheap way to catch the next one.
//
// Opt-in, because a long run takes minutes:
//     CIVONE_HARNESS_TURNS=200 dotnet test --filter Autoplay_DevelopsAWorld
//
// With the variable unset it runs a short smoke pass and just asserts the loop advances.
//
// One thing to know before reading the numbers:
//   - The seed is pinned (CIVONE_HARNESS_SEED, default 4242). Map generation and every AI
//     die roll come off Common.Random, so without pinning, two runs get different
//     continents and any comparison is noise.

using System;
using System.IO;
using System.Linq;
using CivOne;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tests
{
	public class AutoplayHarness
	{
		private static string Snapshot(int turn)
		{
			Game g = Game.Instance;
			Player[] live = g.Players
				.Where(p => !p.IsDestroyed() && p.Cities.Length > 0
				         && p.Civilization is not CivOne.Civilizations.Barbarian)
				.ToArray();
			if (live.Length == 0)
			{
				int units = g.GetUnits().Length;
				int settlers0 = g.GetUnits().Count(u => u is Settlers);
				int allCities = g.GetCities().Length;
				return $"turn {turn,4}: no living civs (units {units}, settlers {settlers0}, cities-any-owner {allCities}, "
				     + $"players {g.Players.Count()}, currentPlayer {g.PlayerNumber(g.CurrentPlayer)})";
			}

			int cities = live.Sum(p => p.Cities.Length);
			int settlers = g.GetUnits().Count(u => u is Settlers && u.Owner != 0);
			double meanSize = live.SelectMany(p => p.Cities).Average(c => (double)c.Size);
			int advances = live.Sum(p => p.Advances.Length);
			var cityList = live.SelectMany(p => p.Cities).ToArray();
			int riot = cityList.Count(c => c.IsInDisorder);
			double unhappy = cityList.Average(c => (double)c.UnhappyCitizens);
			double lux = live.Average(p => (double)p.LuxuriesRate);
			// Colonisation: civs holding cities on more than one landmass, and how many
			// cities sit away from their civ's main one.
			int colonisers = 0, colonies = 0;
			foreach (Player p in live)
			{
				var byCont = p.Cities.GroupBy(c => Map.Instance[c.X, c.Y].ContinentId)
					.OrderByDescending(x => x.Count()).ToArray();
				if (byCont.Length > 1) { colonisers++; colonies += p.Cities.Length - byCont[0].Count(); }
			}
			// Polluted tiles worldwide, and how many of them sit on worked land. Added
			// when a pollution change came back byte-identical on all three seeds and the
			// only way to tell "no effect" from "no pollution to have an effect on" was to
			// count it.
			int pollutedWorld = Map.Instance.AllTiles().Count(t => t is not null && t.Pollution);
			int landWorked = 0, improved = 0, mined = 0, pollutedWorked = 0;
			foreach (City c in live.SelectMany(p => p.Cities))
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2) continue;
				int ty = c.Y + dy;
				if (ty < 0 || ty >= Map.HEIGHT) continue;
				ITile t = Map.Instance[(c.X + dx + Map.WIDTH) % Map.WIDTH, ty];
				if (t is null || t.IsOcean) continue;
				landWorked++;
				if (t.Road || t.Irrigation) improved++;
				if (t.Mine) mined++;
				if (t.Pollution) pollutedWorked++;
			}
			return $"turn {turn,4}: warm {g.GlobalWarmingCount}  civs {live.Length,2}  cities {cities,4}  biggest {live.Max(p => p.Cities.Length),3}"
			     + $"  meanSize {meanSize,4:F1}  settlers {settlers,3}  advances {advances,4}"
			     + $"  improved {(landWorked > 0 ? improved * 100 / landWorked : 0),3}%  mined {mined,3}"
			     + $"  riot {riot,3}/{cityList.Length,-3} unhappy/city {unhappy,4:F1} lux {lux,3:F1}"
			     + $"  colonisers {colonisers,2} overseasCities {colonies,3}"
			     + $"  ships {g.GetUnits().Count(u => u.Class == CivOne.Enums.UnitClass.Water && u.Owner != 0),3}"
			     + $"  polluted {pollutedWorked,3}/{pollutedWorld,-4}";
		}

		[Fact]
		public void Autoplay_DevelopsAWorld()
		{
			int turns = int.TryParse(Environment.GetEnvironmentVariable("CIVONE_HARNESS_TURNS"), out int t) ? t : 12;
			string? logPath = Environment.GetEnvironmentVariable("CIVONE_HARNESS_LOG");

			// Pinned by default so A/B comparisons measure the code, not a different map.
			short seed = short.TryParse(Environment.GetEnvironmentVariable("CIVONE_HARNESS_SEED"), out short sd) ? sd : (short)4242;
			// Prince (2), because that is what the real runs are played at and difficulty is not
			// cosmetic here: it slows the HUMAN's research and nobody else's (Player.cs — the AI
			// always pays the Chieftain rate). At 0 the harness human researched at the cheapest
			// rate in the game and reached 49 cities by turn 220 while the AIs held 3 to 12,
			// which is not a world worth tuning against.
			int diff = int.TryParse(Environment.GetEnvironmentVariable("CIVONE_HARNESS_DIFFICULTY"), out int d) ? d : 2;

			// Sweep knobs. The default is the old 80x50 generated world with 7 rivals, so every
			// existing invocation behaves exactly as it did.
			//
			// CIVONE_HARNESS_MAP=earth-epic is what a batch should use. On a GENERATED map the
			// seed decides the continents, so a 13-civ run and a 7-civ run differ in the shape
			// of the planet as well as in the size of the field, and no amount of repetition
			// separates the two. Earth holds the ground still and lets the seed vary only the
			// die rolls — same world, independent histories, one variable at a time.
			int civs = int.TryParse(Environment.GetEnvironmentVariable("CIVONE_HARNESS_CIVS"), out int nc) ? nc : 7;
			int w = int.TryParse(Environment.GetEnvironmentVariable("CIVONE_HARNESS_WIDTH"), out int ww) ? ww : 80;
			int h = int.TryParse(Environment.GetEnvironmentVariable("CIVONE_HARNESS_HEIGHT"), out int hh) ? hh : 50;
			string map = Environment.GetEnvironmentVariable("CIVONE_HARNESS_MAP") ?? "generated";

			// Cursed wonders default OFF here, which is the opposite of the code default and
			// deliberate: the profile the real runs use has CursedWonders=0, and a test world
			// where Gozira and the Grey Goo are loose is not the world being tuned. The knob is
			// here so the arc can still be exercised on purpose.
			bool cursed = (Environment.GetEnvironmentVariable("CIVONE_HARNESS_CURSED") ?? "0") != "0";

			Sim.NewGame(width: w, height: h, competition: civs, difficulty: diff, seed: seed, map: map,
			            varyHuman: true);
			Settings.Instance.Autopilot = true;
			Settings.Instance.CursedWonders = cursed;

			var lines = new System.Collections.Generic.List<string>();
			void Report(string s)
			{
				lines.Add(s);
				Console.WriteLine(s);
				if (logPath is not null) File.AppendAllText(logPath, s + Environment.NewLine);
			}

			int every = Math.Max(1, turns / 20);
			Report($"seed {seed}, {turns} turns, difficulty {diff} (contentFloor {6 - diff}, "
			     + $"empireFree {Math.Max(6, 12 - diff)}), map {map} {Map.WIDTH}x{Map.HEIGHT}, "
			     + $"{civs} rivals, cursed {cursed}, storage {Settings.Instance.StorageDirectory}");
			Report(Snapshot(0));
			int reached = Sim.RunTurns(turns, turn =>
			{
				// Headless parity: nothing answers the human's research screen here. See
				// Sim.KeepHumanResearching — without it the largest civ in the world sits on
				// two advances all game and the space race never happens.
				Sim.KeepHumanResearching();
				if (turn % every == 0) Report(Snapshot(turn));
			}, stop: Sim.GameDecided);
			Report(Snapshot(reached));
			if (Sim.GameDecided())
				Report($"decided at turn {reached} — see game_outcome in decisions.jsonl");

			Assert.True(reached > 0, "the harness must advance the game at least one turn");

			// Only judge development on a run long enough to show it. Expansion is the
			// signal that matters: the regression this guards against left every civ on
			// one city for the whole game.
			if (turns >= 100)
			{
				Player[] live = Game.Instance.Players
					.Where(p => !p.IsDestroyed() && p.Cities.Length > 0
					         && p.Civilization is not CivOne.Civilizations.Barbarian)
					.ToArray();
				int biggest = live.Length == 0 ? 0 : live.Max(p => p.Cities.Length);
				Assert.True(biggest >= 3,
					$"after {reached} turns the largest civ still had {biggest} cities — expansion has stalled");
			}
		}
	}
}
