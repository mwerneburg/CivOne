// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Diagnostics;
using System.Threading;

namespace CivOne
{
	// Per-turn phase timing, emitted to the decision log once per full round
	// (Game.cs, at the GameTurn++ wrap) as a "turn_timing" record.
	//
	// The question this exists to answer: when autoplay stalls, is the time going
	// into AI turn processing or into drawing? Those need opposite fixes, and the
	// symptom — the screen not keeping up — looks identical either way. Frame time
	// is accumulated by the SDL runtime (GameWindow.Render), which is why the
	// counters are public rather than internal.
	//
	// Stopwatch.GetTimestamp() is a cheap monotonic read (tens of nanoseconds), so
	// the counters can sit in the per-city and per-unit paths without distorting
	// what they measure. Interlocked because the render thread and the game logic
	// touch different fields but share the class.
	public static class TurnMetrics
	{
		private static long _aiProductionTicks, _aiMoveTicks, _renderTicks;
		private static int _aiProductionCalls, _aiMoveCalls, _frames;

		// Second-layer probes. The first pass showed 92% of a 181-second round in
		// code none of the phases above covered, so these split the remainder:
		// per-city / per-unit / per-player turn processing (all dispatched through
		// Tasks/Turn.cs), plus the two global-tick suspects.
		private static long _cityTurnTicks, _unitTurnTicks, _playerTurnTicks, _autosaveTicks, _scoreTicks;
		private static int _cityTurnCalls, _unitTurnCalls, _playerTurnCalls;

		// Third layer. Layer two still left 94% unaccounted, and the two things the
		// per-turn loop does that nothing above covers are draining the game-task
		// queue and re-composing the screen bitmaps. The screen composition is the
		// one cost that scales with canvas size — the 2K-display suspect, and it is
		// NOT the SDL render probe, which only times the texture upload.
		private static long _taskQueueTicks, _screenUpdateTicks;
		private static int _taskQueueCalls, _screenUpdateCalls;

		// GamePlay.HasUpdate calls Game.Update() — the turn engine — before doing any
		// drawing, so it sits INSIDE the screen-update probe. Split it out: "screen
		// time" that is really turn processing would send us optimising the renderer
		// for a cost that has nothing to do with drawing.
		private static long _gameUpdateTicks;
		private static int _gameUpdateCalls;

		// Fourth layer: A* pathfinding (Common.GotoStep). AI unit movement is now
		// ~70% of a turn and its cost per unit rises with world population — 18.7ms
		// per move at 731 units, 175ms at 1157 — which is the signature of a search
		// that explores more as the map fills. Failures are counted separately: an
		// unreachable goal exhausts the open set, so it is the most expensive
		// possible outcome and the one most worth short-circuiting.
		private static long _pathTicks;
		private static int _pathCalls, _pathFails;

		public static long Now => Stopwatch.GetTimestamp();

		private static double ToMs(long ticks) => (ticks * 1000.0) / Stopwatch.Frequency;

		public static void AddAiProduction(long startTimestamp)
		{
			Interlocked.Add(ref _aiProductionTicks, Stopwatch.GetTimestamp() - startTimestamp);
			Interlocked.Increment(ref _aiProductionCalls);
		}

		public static void AddAiMove(long startTimestamp)
		{
			Interlocked.Add(ref _aiMoveTicks, Stopwatch.GetTimestamp() - startTimestamp);
			Interlocked.Increment(ref _aiMoveCalls);
		}

		// Called by the SDL runtime once per presented frame.
		public static void AddRenderFrame(long startTimestamp)
		{
			Interlocked.Add(ref _renderTicks, Stopwatch.GetTimestamp() - startTimestamp);
			Interlocked.Increment(ref _frames);
		}

		public static void AddCityTurn(long t)   { Interlocked.Add(ref _cityTurnTicks, Stopwatch.GetTimestamp() - t);   Interlocked.Increment(ref _cityTurnCalls); }
		public static void AddUnitTurn(long t)   { Interlocked.Add(ref _unitTurnTicks, Stopwatch.GetTimestamp() - t);   Interlocked.Increment(ref _unitTurnCalls); }
		public static void AddPlayerTurn(long t) { Interlocked.Add(ref _playerTurnTicks, Stopwatch.GetTimestamp() - t); Interlocked.Increment(ref _playerTurnCalls); }
		public static void AddAutosave(long t)   => Interlocked.Add(ref _autosaveTicks, Stopwatch.GetTimestamp() - t);
		public static void AddScoreSnapshot(long t) => Interlocked.Add(ref _scoreTicks, Stopwatch.GetTimestamp() - t);

		public static double CityTurnMs   => ToMs(Interlocked.Read(ref _cityTurnTicks));
		public static double UnitTurnMs   => ToMs(Interlocked.Read(ref _unitTurnTicks));
		public static double PlayerTurnMs => ToMs(Interlocked.Read(ref _playerTurnTicks));
		public static double AutosaveMs   => ToMs(Interlocked.Read(ref _autosaveTicks));
		public static double ScoreMs      => ToMs(Interlocked.Read(ref _scoreTicks));
		public static int CityTurnCalls   => _cityTurnCalls;
		public static int UnitTurnCalls   => _unitTurnCalls;
		public static int PlayerTurnCalls => _playerTurnCalls;

		public static void AddTaskQueue(long t)    { Interlocked.Add(ref _taskQueueTicks, Stopwatch.GetTimestamp() - t);    Interlocked.Increment(ref _taskQueueCalls); }
		public static void AddScreenUpdate(long t) { Interlocked.Add(ref _screenUpdateTicks, Stopwatch.GetTimestamp() - t); Interlocked.Increment(ref _screenUpdateCalls); }
		public static void AddPathfind(long t, bool found)
		{
			Interlocked.Add(ref _pathTicks, Stopwatch.GetTimestamp() - t);
			Interlocked.Increment(ref _pathCalls);
			if (!found) Interlocked.Increment(ref _pathFails);
		}
		public static double PathMs   => ToMs(Interlocked.Read(ref _pathTicks));
		public static int PathCalls   => _pathCalls;
		public static int PathFails   => _pathFails;

		public static void AddGameUpdate(long t) { Interlocked.Add(ref _gameUpdateTicks, Stopwatch.GetTimestamp() - t); Interlocked.Increment(ref _gameUpdateCalls); }
		public static double GameUpdateMs   => ToMs(Interlocked.Read(ref _gameUpdateTicks));
		public static int GameUpdateCalls   => _gameUpdateCalls;

		public static double TaskQueueMs    => ToMs(Interlocked.Read(ref _taskQueueTicks));
		public static double ScreenUpdateMs => ToMs(Interlocked.Read(ref _screenUpdateTicks));
		public static int TaskQueueCalls    => _taskQueueCalls;
		public static int ScreenUpdateCalls => _screenUpdateCalls;

		public static double AiProductionMs => ToMs(Interlocked.Read(ref _aiProductionTicks));
		public static double AiMoveMs       => ToMs(Interlocked.Read(ref _aiMoveTicks));
		public static double RenderMs       => ToMs(Interlocked.Read(ref _renderTicks));
		public static int AiProductionCalls => _aiProductionCalls;
		public static int AiMoveCalls       => _aiMoveCalls;
		public static int Frames            => _frames;

		public static void Reset()
		{
			Interlocked.Exchange(ref _aiProductionTicks, 0);
			Interlocked.Exchange(ref _aiMoveTicks, 0);
			Interlocked.Exchange(ref _renderTicks, 0);
			Interlocked.Exchange(ref _aiProductionCalls, 0);
			Interlocked.Exchange(ref _aiMoveCalls, 0);
			Interlocked.Exchange(ref _frames, 0);
			Interlocked.Exchange(ref _cityTurnTicks, 0);
			Interlocked.Exchange(ref _unitTurnTicks, 0);
			Interlocked.Exchange(ref _playerTurnTicks, 0);
			Interlocked.Exchange(ref _autosaveTicks, 0);
			Interlocked.Exchange(ref _scoreTicks, 0);
			Interlocked.Exchange(ref _cityTurnCalls, 0);
			Interlocked.Exchange(ref _unitTurnCalls, 0);
			Interlocked.Exchange(ref _playerTurnCalls, 0);
			Interlocked.Exchange(ref _taskQueueTicks, 0);
			Interlocked.Exchange(ref _screenUpdateTicks, 0);
			Interlocked.Exchange(ref _taskQueueCalls, 0);
			Interlocked.Exchange(ref _screenUpdateCalls, 0);
			Interlocked.Exchange(ref _gameUpdateTicks, 0);
			Interlocked.Exchange(ref _gameUpdateCalls, 0);
			Interlocked.Exchange(ref _pathTicks, 0);
			Interlocked.Exchange(ref _pathCalls, 0);
			Interlocked.Exchange(ref _pathFails, 0);
		}
	}
}
