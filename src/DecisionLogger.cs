// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.Units;

// Writes one JSON-Lines record per AI decision to:
//   ~/Library/Application Support/CivOne/data/decisions.jsonl
//
// Schema (flat, intentionally simple for easy pandas/numpy ingestion):
//
//   type          string   "settler" | "city_prod" | "game_outcome"
//   game_id       string   8-char hex, stable per session
//   turn          int      Game.GameTurn at time of decision
//   is_human      bool     false for AI decisions
//
//   --- settler fields ---
//   terrain       string   tile terrain name at current position
//   food_r2       int      sum of food yields in radius-2 diamond
//   shield_r2     int      sum of shield yields
//   trade_r2      int      sum of trade yields
//   coastal       bool     any adjacent ocean tile
//   river_adj     bool     any adjacent river tile
//   nearest_city  int      distance to nearest city of any player
//   nearest_own   int      distance to nearest own city
//   own_cities    int      total own city count
//   action        string   "found" | "road" | "irrigate" | "mine" | "move"
//
//   --- city_prod fields ---
//   city_size     int
//   food_surplus  int      city food per turn (may be negative)
//   shields       int      city shield output
//   defenders     int      defensive units on the city tile
//   nearest_enemy int      distance to nearest enemy city
//   at_war        bool
//   own_gold      int
//   own_cities    int
//   stance        string   AI stance label ("Chieftain", "Aggressive", etc.)
//   action        string   production name (e.g. "Settlers", "Granary")
//
//   --- game_outcome fields ---
//   score         int      human player's final score
//   victory       string   victory type label
//   human_won     bool
//   turns         int      total turns played
//
// Future outcome annotations will use:
//   type="outcome", ref=<decision_id>, turn, metric_name, metric_value
// The decision_id field will be added to decision records when outcome
// tracking is wired in.

namespace CivOne
{
	internal static class DecisionLogger
	{
		private static string _gameId = null!;
		private static StreamWriter? _writer;
		private static readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
		private static readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
		private static Task _writerTask = null!;
		private static volatile bool _active;

		// ── lifecycle ────────────────────────────────────────────────────────────

		internal static void BeginGame()
		{
			_gameId = Guid.NewGuid().ToString("N").Substring(0, 8);
			try
			{
				string dir  = Settings.Instance.DataDirectory;
				string path = Path.Combine(dir, "decisions.jsonl");
				_writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = false };
				_active = true;
				_writerTask = Task.Run((Action)WriteLoop);
			}
			catch (Exception ex)
			{
				RuntimeHandler.Runtime.Log($"DecisionLogger disabled, could not open decisions.jsonl: {ex.GetType().Name}: {ex.Message}");
				_active = false;
			}
		}

		internal static void EndGame(int score, string victory, bool humanWon, int turns)
		{
			if (!_active) return;
			Enqueue(Fmt(new[] {
				KV("type",      "game_outcome"),
				KV("game_id",   _gameId),
				KV("score",     score),
				KV("victory",   victory),
				KV("human_won", humanWon),
				KV("turns",     turns),
			}));
			_active = false;
			_signal.Release();
			_writerTask?.Wait(3000);
			_writer?.Flush();
			_writer?.Dispose();
			_writer = null;
		}

		// ── decision log points ──────────────────────────────────────────────────

		internal static void LogSettlerAction(IUnit unit, string action)
		{
			if (!_active) return;
			ITile tile = unit.Tile;
			if (tile is null) return;
			Map map = Map.Instance;
			if (map is null) return;

			int mapW = Map.WIDTH, mapH = Map.HEIGHT;
			int foodR2 = 0, shieldR2 = 0, tradeR2 = 0;
			bool coastal = false, riverAdj = false;

			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2) continue;
				int tx = (tile.X + dx + mapW) % mapW;
				int ty = tile.Y + dy;
				if (ty < 0 || ty >= mapH) continue;
				ITile t = map[tx, ty];
				if (t is null) continue;
				foodR2   += t.Food;
				shieldR2 += t.Shield;
				tradeR2  += t.Trade;
				if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && (dx != 0 || dy != 0))
				{
					if (t.IsOcean)  coastal  = true;
					if (t is River) riverAdj = true;
				}
			}

			Game game = Game.Instance;
			ushort turn = game?.GameTurn ?? 0;
			City[] cities    = game?.GetCities() ?? Array.Empty<City>();
			int nearestCity  = cities.Length > 0
				? cities.Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y))
				: 255;
			City[] ownCities = cities.Where(c => c.Owner == unit.Owner).ToArray();
			int nearestOwn   = ownCities.Length > 0
				? ownCities.Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y))
				: 255;

			Player? ownerPlayer = game?.GetPlayer(unit.Owner);
			string civName    = ownerPlayer?.Civilization?.NamePlural ?? "?";
			string leaderName = ownerPlayer?.LeaderName ?? "?";

			Enqueue(Fmt(new[] {
				KV("type",         "settler"),
				KV("game_id",      _gameId),
				KV("turn",         turn),
				KV("is_human",     ownerPlayer is not null && ownerPlayer == game?.HumanPlayer),
				KV("civ",          civName),
				KV("leader",       leaderName),
				KV("terrain",      tile.GetType().Name),
				KV("food_r2",      foodR2),
				KV("shield_r2",    shieldR2),
				KV("trade_r2",     tradeR2),
				KV("coastal",      coastal),
				KV("river_adj",    riverAdj),
				KV("nearest_city", nearestCity),
				KV("nearest_own",  nearestOwn),
				KV("own_cities",   ownCities.Length),
				KV("action",       action),
			}));
		}

		// Per-turn phase timing (see TurnMetrics). One record per full round, so the
		// autoplay log answers "where did the turn go" directly: AI production
		// planning, AI unit movement, rendering, and whatever is left over.
		internal static void LogTurnTiming(int turn, double wallMs, int cities, int units, int players)
		{
			if (!_active) return;

			double prod = TurnMetrics.AiProductionMs, move = TurnMetrics.AiMoveMs, render = TurnMetrics.RenderMs;
			Enqueue(Fmt(new[] {
				KV("type",            "turn_timing"),
				KV("game_id",         _gameId),
				KV("turn",            turn),
				KV("wall_ms",         (int)wallMs),
				KV("ai_prod_ms",      (int)prod),
				KV("ai_prod_calls",   TurnMetrics.AiProductionCalls),
				KV("ai_move_ms",      (int)move),
				KV("ai_move_calls",   TurnMetrics.AiMoveCalls),
				KV("render_ms",       (int)render),
				KV("frames",          TurnMetrics.Frames),
				KV("city_turn_ms",    (int)TurnMetrics.CityTurnMs),
				KV("city_turn_calls", TurnMetrics.CityTurnCalls),
				KV("unit_turn_ms",    (int)TurnMetrics.UnitTurnMs),
				KV("unit_turn_calls", TurnMetrics.UnitTurnCalls),
				KV("player_turn_ms",  (int)TurnMetrics.PlayerTurnMs),
				KV("autosave_ms",     (int)TurnMetrics.AutosaveMs),
				KV("score_ms",        (int)TurnMetrics.ScoreMs),
				KV("task_queue_ms",   (int)TurnMetrics.TaskQueueMs),
				KV("task_queue_calls",TurnMetrics.TaskQueueCalls),
				KV("screen_ms",       (int)TurnMetrics.ScreenUpdateMs),
				KV("screen_calls",    TurnMetrics.ScreenUpdateCalls),
				// Nested inside screen_ms — subtract it to get actual drawing time.
				KV("game_update_ms",  (int)TurnMetrics.GameUpdateMs),
				KV("game_update_calls", TurnMetrics.GameUpdateCalls),
				// Remainder after every phase above. AI production/move time is nested
				// inside the city/unit turns, so it is not subtracted twice.
				// Task-queue and screen-update time SUBSUME the city/unit/player turns
				// (those run as task steps), so only the outermost phases are subtracted.
				KV("other_ms",        (int)Math.Max(0, wallMs - TurnMetrics.TaskQueueMs - TurnMetrics.ScreenUpdateMs
				                                       - render - TurnMetrics.AutosaveMs - TurnMetrics.ScoreMs)),
				KV("cities",          cities),
				KV("units",           units),
				KV("players",         players),
			}));
		}

		// Goody hut outcomes. "AdvancedTribe" founds a city outright (Orders.NewCity in
		// BaseUnitLand.TribalHut) without any settler, so it leaves no trace in the
		// settler log — a civ's city count can climb with no matching "found" action.
		// Logging the outcome here makes free cities directly countable instead of
		// inferred from the gap.
		internal static void LogHut(IUnit? unit, string outcome)
		{
			if (!_active) return;
			if (unit is null) return;
			ITile? tile = unit.Tile;
			if (tile is null) return;

			Game game = Game.Instance;
			Player? ownerPlayer = game?.GetPlayer(unit.Owner);
			City[] cities = game?.GetCities() ?? Array.Empty<City>();
			int nearestCity = cities.Length > 0
				? cities.Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y))
				: 255;
			int ownCities = cities.Count(c => c.Owner == unit.Owner);

			Enqueue(Fmt(new[] {
				KV("type",         "hut"),
				KV("game_id",      _gameId),
				KV("turn",         game?.GameTurn ?? 0),
				KV("is_human",     ownerPlayer is not null && ownerPlayer == game?.HumanPlayer),
				KV("civ",          ownerPlayer?.Civilization?.NamePlural ?? "?"),
				KV("leader",       ownerPlayer?.LeaderName ?? "?"),
				KV("x",            tile.X),
				KV("y",            tile.Y),
				KV("terrain",      tile.GetType().Name),
				KV("land_value",   tile.LandValue),
				KV("nearest_city", nearestCity),
				KV("own_cities",   ownCities),
				KV("outcome",      outcome),
			}));
		}

		internal static void LogCityProduction(City city, IProduction choice, string stance, bool isHuman = false, bool hasRoom = false)
		{
			if (!_active) return;
			if (city is null || choice is null) return;

			Game game = Game.Instance;
			if (game is null) return;

			// City may be in a transient sentinel state (X==255 / Y==255) when destroyed or
			// in-flight; Tile is null there. Bail rather than NRE on city.Tile.Units.
			ITile cityTile = city.Tile;
			if (cityTile is null) return;

			byte owner = city.Owner;
			City[] allCities = game.GetCities();
			City[] ownCities = allCities.Where(c => c.Owner == owner).ToArray();
			Player player    = city.Player;

			int nearestEnemy = allCities
				.Where(c => c.Owner != owner)
				.Select(c => Common.DistanceToTile(c.X, c.Y, city.X, city.Y))
				.DefaultIfEmpty(255)
				.Min();

			int defenders = cityTile.Units.Count(u => u.Role == UnitRole.Defense && u.Owner == owner);

			bool atWar = game.Players
				.Where(p => p != player)
				.Any(p => player.IsAtWar(p));

			string productionName = (choice as ICivilopedia)?.Name ?? choice.GetType().Name;

			string civName    = player?.Civilization?.NamePlural ?? "?";
			string leaderName = player?.LeaderName ?? "?";

			Enqueue(Fmt(new[] {
				KV("type",          "city_prod"),
				KV("game_id",       _gameId),
				KV("turn",          game.GameTurn),
				KV("is_human",      isHuman),
				KV("civ",           civName),
				KV("leader",        leaderName),
				KV("city",          city.Name),
				KV("city_size",     city.Size),
				KV("food_surplus",  city.FoodIncome),
				KV("shields",       city.ShieldIncome),
				KV("defenders",     defenders),
				KV("nearest_enemy", nearestEnemy),
				KV("at_war",        atWar),
				KV("own_gold",      player?.Gold ?? 0),
				KV("own_cities",    ownCities.Length),
				KV("stance",        stance),
				KV("has_room",      hasRoom),
				KV("action",        productionName),
			}));
		}

		// ── internal helpers ─────────────────────────────────────────────────────

		private static void Enqueue(string line)
		{
			_queue.Enqueue(line);
			_signal.Release();
		}

		private static void WriteLoop()
		{
			int idle = 0;
			while (_active || !_queue.IsEmpty)
			{
				_signal.Wait(500);
				int flushed = 0;
				while (_queue.TryDequeue(out string line))
				{
					_writer?.WriteLine(line);
					flushed++;
				}
				if (flushed > 0) { _writer?.Flush(); idle = 0; }
				else if (!_active) idle++;
				if (idle > 4) break;
			}
		}

		// ── JSON formatting (no external dependency) ─────────────────────────────

		private static string Fmt((string key, string val)[] fields)
		{
			var sb = new StringBuilder("{");
			for (int i = 0; i < fields.Length; i++)
			{
				if (i > 0) sb.Append(',');
				sb.Append(fields[i].val);
			}
			sb.Append('}');
			return sb.ToString();
		}

		private static (string, string) KV(string key, string value)
			=> (key, $"\"{key}\":\"{Esc(value)}\"");

		private static (string, string) KV(string key, int value)
			=> (key, $"\"{key}\":{value}");

		private static (string, string) KV(string key, bool value)
			=> (key, $"\"{key}\":{(value ? "true" : "false")}");

		private static string Esc(string s) =>
			s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? "";
	}
}