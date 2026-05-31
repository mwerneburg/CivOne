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
		private static RuntimeHandler _instance;
		internal static RuntimeHandler Instance => _instance;
		internal static IRuntime Runtime { get; private set; }
		
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

		private bool Update()
		{
			if (!GameTask.Update() && (!GameTask.Fast && (_gameTick % 4) > 0)) return false;
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

		private IEnumerable<Type> StartupScreens
		{
			get
			{
				if (Runtime.Settings.DataCheck && !FileSystem.DataFilesExist()) yield return typeof(MissingFiles);
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
		private IScreen _autopilotLastTop = null;
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

		private void OnUpdate(object sender, UpdateEventArgs args)
		{
			while (_gameTick < TickWatch)
			{
				_gameTick++;
				AutopilotTick();
				if (!Update()) continue;
				args.HasUpdate = true;
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
					Runtime.Cursor = null;
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

			foreach (Plugin plugin in Reflect.Plugins())
			{
				runtime.Log($"Plugin loaded: {plugin.Name} version {plugin.Version} by {plugin.Author}");
			}

			Task.Run(() => Reflect.PreloadCivilopedia());
		}
	}
}