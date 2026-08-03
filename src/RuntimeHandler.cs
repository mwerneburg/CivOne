// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CivOne.Enums;
using CivOne.Events;
using CivOne.IO;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;
using CivOne.Screens;
using CivOne.Graphics.Sprites;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne
{
	public class RuntimeHandler
	{
		private static RuntimeHandler _instance = null!;
		internal static RuntimeHandler Instance => _instance;
		internal static IRuntime Runtime { get; private set; } = null!;
		
		private Settings Settings => Settings.Instance;
		private IScreen TopScreen => Common.TopScreen;
		private MouseCursor _currentCursor = MouseCursor.None;
		private CursorType _cursorType = CursorType.Native;

		internal int CanvasWidth => Math.Max(320, Runtime.CanvasWidth);
		internal int CanvasHeight => Math.Max(200, Runtime.CanvasHeight);

		private Stopwatch _tickWatch = new Stopwatch();
		private uint TickWatch
		{
			get
			{
				if (!_tickWatch.IsRunning)
				{
					_tickWatch.Start();
				}
				return Convert.ToUInt32(((double)_tickWatch.ElapsedMilliseconds / 1000) * 60);
			}
		}
		private uint _gameTick = 0;

		// True when calling OnUpdate right now would actually do something: either the 60 Hz
		// tick clock has moved on, or we are fast-forwarding an unwatched turn and want to
		// drain the queue as fast as the machine allows.
		//
		// The host loop uses this to decide whether it may sleep. Gating that on
		// GameTask.Any() instead — "is there work queued" rather than "is there work ready" —
		// made the loop free-spin for the whole of every turn: 113 million iterations across
		// 271 turns, ~417,000 per turn, against roughly 4,000 that did anything. That spin
		// was the `other_ms` remainder.
		public static bool WorkReady
		{
			get
			{
				RuntimeHandler h = _instance;
				if (h is null) return false;
				return h._gameTick < h.TickWatch || h.FastForwarding;
			}
		}

		private bool Update()
		{
			long __q = TurnMetrics.Now;
			bool taskRan = GameTask.Update();
			TurnMetrics.AddTaskQueue(__q);

			if (!taskRan && (!GameTask.Fast && (_gameTick % 4) > 0)) return false;

			long __s = TurnMetrics.Now;
			try
			{
				if (Common.Screens.Any(x => Common.HasAttribute<Modal>(x)))
					return Common.Screens.Last(x => Common.HasAttribute<Modal>(x)).Update(_gameTick / 4);

				bool update = false;
				foreach (IScreen screen in Common.Screens.Reverse())
				{
					if (screen.Update(_gameTick / 4)) update = true;
					if (Common.HasAttribute<Break>(screen)) return update;
				}
				return update;
			}
			finally { TurnMetrics.AddScreenUpdate(__s); }
		}

		private IEnumerable<Type> StartupScreens
		{
			get
			{
				// --mapgen-preview short-circuits the whole startup pipeline: skip data
				// check / demo / setup / splash / credits / main menu and go straight to
				// the world-generation preview screen. Used to iterate on Map.Generate
				// knobs without clicking through the game shell each time.
				if (Runtime.Settings.Get<bool>("mapgen-preview"))
				{
					yield return typeof(MapPreview);
					yield break;
				}
				if (Runtime.Settings.Demo) yield return typeof(Demo);
				if (Runtime.Settings.Setup) yield return typeof(Setup);
				if (Resources.SplashRawImage is not null) yield return typeof(Splash);
				yield return typeof(Credits);
			}
		}

		private void OnInitialize(object sender, EventArgs args)
		{
			Runtime.WindowTitle = Settings.WindowTitle;
			GameTask.Enqueue(Show.Screens(StartupScreens));
		}

		// Autopilot: dwell briefly on each task-driven screen blocking gameplay, then fire a
		// synthetic Enter to dismiss / advance. We restrict to task-driven screens (i.e. there
		// is a GameTask waiting on the screen to close) so user-initiated UI like the in-game
		// menu, Options, and the Civilopedia don't auto-dismiss themselves — toggling
		// Autopilot inside Options would otherwise immediately untoggle it.
		private uint _autopilotDwell = 0;
		private IScreen? _autopilotLastTop = null;
		private const uint AUTOPILOT_DWELL_TICKS = 30;  // ~0.5s at the 60-tick rate

		// Screens where pressing Enter would be a user-initiated action (toggling an option,
		// confirming a save, picking a menu entry) — never auto-dismiss these. Identification
		// is by type-name suffix so it covers the GameOptions screen, the Civilopedia, the
		// Debug menus, and Save/Load without each one having to opt out.
		private static bool IsUserInteractiveScreen(IScreen s)
		{
			string ns = s.GetType().Namespace ?? "";
			if (ns.StartsWith("CivOne.Screens.Debug")) return true;
			// Civilopedia is user-driven when the player opens it for reading, but
			// ProcessScience also shows one as a tech-discovery notification — those need
			// to auto-dismiss so research can continue.
			if (s is Civilopedia c) return !c.IsDiscoveryNotification;
			string n = s.GetType().Name;
			return n == "GameOptions" || n == "SaveGame" || n == "LoadGame"
				|| n == "ChangeHumanPlayer";
			// NB: ChooseGovernment, CityName, ChooseTech, NewGame, Setup, Credits, Splash and
			// CustomizeWorld are intentionally NOT blacklisted — they need auto-Enter to pick
			// the highlighted default and let the start-up flow run through to GamePlay.
			// In-game menu overlays are handled separately via GamePlay.HasOpenMenuOverlay.
		}

		private void AutopilotTick()
		{
			if (!Settings.Autopilot) return;
			IScreen top = TopScreen;
			// Also skip when GamePlay has a menu overlay open — the user is navigating and
			// our Enter would just select whatever item is currently highlighted.
			bool gamePlayMenuOpen = top is GamePlay gp && gp.HasOpenMenuOverlay;
			if (top is null || IsUserInteractiveScreen(top) || gamePlayMenuOpen)
			{
				_autopilotDwell = 0;
				_autopilotLastTop = top;
				return;
			}
			if (top != _autopilotLastTop)
			{
				_autopilotLastTop = top;
				_autopilotDwell = 0;
			}
			if (++_autopilotDwell < AUTOPILOT_DWELL_TICKS) return;
			_autopilotDwell = 0;

			// Enter on GamePlay enqueues Turn.End when no unit is active (the path the
			// player would normally hit at the "End of Turn / Press Enter" prompt). When a
			// unit IS active, KeyDownActiveUnit ignores Enter, so this is a safe no-op.
			try { top.KeyDown(new KeyboardEventArgs(Key.Enter)); }
			catch (Exception ex) { Runtime?.Log($"[Autopilot] dismiss failed on {top.GetType().Name}: {ex.Message}"); }
		}

		// True when the queue is grinding through work that has no animation worth pacing
		// to 60 Hz: an AI player's turn, or a human turn under autopilot. Gating on
		// GameTask.Fast means the moment a non-Fast task fronts the queue — an advisor
		// message, a story screen, the human's own interactive turn — fast-forward stops
		// and normal 60 Hz pacing resumes, so nothing that needs the player's eyes gets
		// blurred past.
		private bool FastForwarding =>
			GameTask.Any() && GameTask.Fast && Game.Started &&
			(Settings.Autopilot || !Game.Instance.CurrentPlayer.IsHuman);

		private const long FAST_BUDGET_MS = 8;

		// Wall-clock budget for one batch of ticks before handing control back to SDL.
		// TickWatch advances 60/sec in REAL time, so if a tick costs more than ~16ms
		// the loop can never catch up and simply never returns — measured at 40+
		// seconds with ZERO frames presented, which is what the OS reports as a
		// beachball.
		//
		// This is a TIME budget, not a tick count: a single tick can carry an AI unit
		// move costing tens of milliseconds, so a fixed count of 12 still meant ~600ms
		// per iteration and a cursor that only moved twice a second. Budgeting time
		// keeps the loop returning at roughly frame rate whatever a tick happens to
		// cost. Same 8ms figure the fast-forward drain below uses.
		private const long TICK_BUDGET_MS = 8;

		// If we fall further behind than this, the backlog is unrecoverable and
		// chasing it just starves the loop forever. Drop it and resynchronise —
		// animation frames are skipped, which is the standard trade and matches what
		// the existing fast-forward path already does for unwatched turns.
		private const uint MAX_TICK_BACKLOG = 120;

		// TEMPORARY (2026-08-03) — which task type the 60 Hz pacing wait is spent on.
		//
		// tick:LoopIdle is 7,085 sleeps a turn, 8.25s, a quarter of the whole turn, and it is
		// the loop correctly waiting because FastForwarding is false. FastForwarding needs the
		// task AT THE HEAD OF THE QUEUE to carry [Fast], and only Turn and MoveUnit do — but
		// which of the other eight is actually holding the queue is unknown, and marking the
		// wrong one [Fast] risks blurring past something a player needs to see. This attributes
		// the wall time between updates to the task that was waiting, so the next run names it.
		private long _lastUpdateStamp;

		private void OnUpdate(object sender, UpdateEventArgs args)
		{
			long previousUpdate = _lastUpdateStamp;
			_lastUpdateStamp = TurnMetrics.Now;
			if (previousUpdate != 0 && GameTask.Any() && !FastForwarding)
				TurnMetrics.AddBucket("pace:" + GameTask.CurrentName, previousUpdate);

			// Always run at least one tick so the game cannot stall, then keep going
			// only while inside the budget.
			Stopwatch tickBudget = Stopwatch.StartNew();
			bool first = true;
			while (_gameTick < TickWatch && (first || tickBudget.ElapsedMilliseconds < TICK_BUDGET_MS))
			{
				first = false;
				_gameTick++;
				AutopilotTick();
				if (!Update()) continue;
				args.HasUpdate = true;
			}

			uint behind = TickWatch > _gameTick ? TickWatch - _gameTick : 0;
			if (behind > MAX_TICK_BACKLOG) _gameTick = TickWatch;

			// Fast-forward AI / autopilot turns. The TickWatch loop above caps task-queue
			// drain at 60 steps/sec to keep animations smooth — but an AI turn has no
			// animation to keep smooth (enemy moves are skipped or unwatched), so that cap
			// is pure waiting. Drain extra Fast tasks under a strict ~8 ms budget: the
			// between-turns pause shrinks by roughly an order of magnitude while we still
			// return to the SDL loop every frame, so the OS event queue never starves (no
			// spinning wheel). A SINGLE task step that itself blocks for seconds (e.g. the
			// once-per-round global tick) is not helped by this — the instrumentation in
			// Game.EndTurn is there to find that case.
			if (FastForwarding)
			{
				Stopwatch budget = Stopwatch.StartNew();
				while (FastForwarding && budget.ElapsedMilliseconds < FAST_BUDGET_MS)
					if (Update()) args.HasUpdate = true;
			}
		}

		private void OnDraw(object sender, EventArgs args)
		{
			if (TopScreen is null) return;

			Runtime.Palette?.Dispose();
			// Build a composite palette:
			//   Base  = TopScreen palette  (preserves portrait colours at indices 144-255 etc.)
			//   When a small non-[Expand] dialog (advisor, message box, etc.) sits on top of a
			//   Cassette-themed [Expand] screen, the dialog's DefaultPalette doesn't have
			//   Cassette colors at 1-17, so we pull them from the nearest [Expand] screen below.
			//   Skip this when the TopScreen is itself [Expand] — it owns its palette.
			Palette composite = Common.TopScreen.Palette.Copy();
			bool skipMerge = Common.HasAttribute<Expand>(Common.TopScreen)
			              || Common.HasAttribute<OwnPalette>(Common.TopScreen);
			if (!skipMerge)
			{
				IScreen themedScreen = Common.Screens
					.LastOrDefault(s => s != Common.TopScreen && Common.HasAttribute<Expand>(s));
				if (themedScreen is not null)
				{
					composite.MergePalette(themedScreen.Palette, 1, 18);
					composite.MergePalette(themedScreen.Palette, 96, 8);
				}
			}
			Runtime.Palette = composite;
			
			if (Common.HasAttribute<Modal>(TopScreen))
			{
				Runtime.Layers = [TopScreen.Bitmap];
			}
			else
			{
				Runtime.Layers = Common.Screens.Select(x => x.Bitmap).ToArray();
			}

			if (_currentCursor != Common.MouseCursor || _cursorType != Settings.Instance.CursorType)
			{
				_currentCursor = Common.MouseCursor;
				_cursorType = Settings.Instance.CursorType;
				Runtime.CurrentCursor = _currentCursor;
				if (Cursor.Current?.Bitmap is not null)
				{
					Runtime.Cursor = Cursor.Current.ToBitmap();
				}
				else
				{
					Runtime.Cursor = null!;
				}
			}
		}

		private void OnKeyboardUp(object sender, KeyboardEventArgs args)
		{
		}

		private void OnKeyboardDown(object sender, KeyboardEventArgs args)
		{
			if (args[KeyModifier.Control, Key.F5])
			{
				string filename = Common.CaptureFilename;
				if (Runtime.Layers is null) return;
				using (IBitmap bitmap = new Picture(CanvasWidth, CanvasHeight, Common.TopScreen.Palette.Copy()))
				{
					bitmap.Palette[0] = Colour.Black;
					if (Common.HasAttribute<Modal>(TopScreen))
					{
						bitmap.AddLayer(TopScreen);
					}
					else
					{
						Runtime.Layers.ToList().ForEach(x => bitmap.AddLayer(x));
					}

					using (GifFile file = new GifFile(bitmap))
					using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
					{
						byte[] output = file.GetBytes();
						fs.Write(output, 0, output.Length);
						Runtime.Log($"Screenshot saved: {filename}");
					}
				}
				return;
			}

			if (args[KeyModifier.Control, Key.F6] && Game.Started)
			{
				string filename = Common.CaptureFilename;
				using (IBitmap tilesPicture = Map.Instance[0, 0, Map.WIDTH, Map.HEIGHT].ToBitmap())
				using (GifFile file = new GifFile(tilesPicture))
				using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
				{
					byte[] output = file.GetBytes();
					fs.Write(output, 0, output.Length);
					Runtime.Log($"Screenshot saved: {filename}");
				}
				return;
			}

			TopScreen?.KeyDown(args);
		}

		private void OnMouseUp(object sender, ScreenEventArgs args) => TopScreen?.MouseUp(args);

		private void OnMouseDown(object sender, ScreenEventArgs args) => TopScreen?.MouseDown(args);

		private void OnMouseMove(object sender, ScreenEventArgs args)
		{
			if (args.Buttons != MouseButton.None)
			{
				TopScreen?.MouseDrag(args);
			}
			TopScreen?.MouseMove(args);
		}

		private void OnMouseWheel(object sender, ScreenEventArgs args) => TopScreen?.MouseWheel(args);

		public static void Register(IRuntime runtime)
		{
			if (_instance is not null)
			{
				throw new Exception("Only one runtime can be registered.");
			}

			_instance = new RuntimeHandler(runtime);
		}

		private RuntimeHandler(IRuntime runtime)
		{
			Runtime = runtime;

			runtime.Initialize += OnInitialize;
			runtime.Update += OnUpdate;
			runtime.Draw += OnDraw;
			runtime.KeyboardUp += OnKeyboardUp;
			runtime.KeyboardDown += OnKeyboardDown;
			runtime.MouseUp += OnMouseUp;
			runtime.MouseDown += OnMouseDown;
			runtime.MouseMove += OnMouseMove;
			runtime.MouseWheel += OnMouseWheel;

			foreach (Plugin plugin in Reflect.Plugins())
			{
				runtime.Log($"Plugin loaded: {plugin.Name} version {plugin.Version} by {plugin.Author}");
			}

			Task.Run(() => Reflect.PreloadCivilopedia());
		}
	}
}