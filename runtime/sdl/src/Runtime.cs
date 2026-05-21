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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Events;
using CivOne.IO;
using CivOne.Graphics;
using CoreRes = CivOne.Graphics.Resources;

namespace CivOne
{
	internal class Runtime : IRuntime, IDisposable
	{
		public Profile Profile { get; }
		
		internal static Size CanvasSize { get; set; }

		internal bool SignalQuit { get; private set; }

		internal void InvokeInitialize() => Initialize?.Invoke(this, EventArgs.Empty);
		internal void InvokeDraw() => Draw?.Invoke(this, EventArgs.Empty);
		internal void InvokeUpdate(ref UpdateEventArgs args) => Update?.Invoke(this, args);
		internal void InvokeKeyboardUp(KeyboardEventArgs args) => KeyboardUp?.Invoke(this, args);
		internal void InvokeKeyboardDown(KeyboardEventArgs args) => KeyboardDown?.Invoke(this, args);
		internal void InvokeMouseUp(ScreenEventArgs args) => MouseUp?.Invoke(this, args);
		internal void InvokeMouseDown(ScreenEventArgs args) => MouseDown?.Invoke(this, args);
		internal void InvokeMouseMove(ScreenEventArgs args) => MouseMove?.Invoke(this, args);

		public event EventHandler Initialize, Draw;
		public event UpdateEventHandler Update;
		public event KeyboardEventHandler KeyboardUp, KeyboardDown;
		public event ScreenEventHandler MouseUp, MouseDown, MouseMove;
		internal event EventHandler CursorChanged;
		internal event Action<string> PlaySound;
		internal event Action StopSound;
		internal event Action<string> SetWindowTitle;
		
		public RuntimeSettings Settings { get; private set; }
		public MouseCursor CurrentCursor { internal get; set; }
		public Bytemap[] Layers { get; set; }
		public Palette Palette { get; set; }
		private IBitmap _cursor;
		public IBitmap Cursor
		{
			internal get => _cursor;
			set
			{
				_cursor = value;
				CursorChanged?.Invoke(this, EventArgs.Empty);
			}
		}

#if RELEASE
		public void Log(string value, params object[] formatArgs) { }
#else
		public void Log(string value, params object[] formatArgs) => Console.WriteLine(value, formatArgs);
#endif

		Platform IRuntime.CurrentPlatform => Platform.Windows;
		string IRuntime.StorageDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CivOne");
		string IRuntime.GetSetting(string key) => Profile.GetSetting(key);
		void IRuntime.SetSetting(string key, string value) => Profile.SetSetting(key, value);
		int IRuntime.CanvasWidth => CanvasSize.Width;
		int IRuntime.CanvasHeight => CanvasSize.Height;
		
		string IRuntime.BrowseFolder(string caption) => Native.FolderBrowser(caption);
		string IRuntime.WindowTitle
		{
			set => SetWindowTitle?.Invoke(value);
		}
		void IRuntime.PlaySound(string filename) => PlaySound?.Invoke(filename);
		void IRuntime.StopSound() => StopSound?.Invoke();
		void IRuntime.Quit() => SignalQuit = true;

		private static readonly Dictionary<string, string[]> _defaultAssets = new Dictionary<string, string[]>
		{
			["CivOne.Resources.defaults.splash.png"]                           = new[] { "splash.png" },
			["CivOne.Resources.defaults.data.seti_signal.txt"]                = new[] { "data", "seti_signal.txt" },
			["CivOne.Resources.defaults.data.south_pole_expedition.txt"]      = new[] { "data", "south_pole_expedition.txt" },
			["CivOne.Resources.defaults.data.improvement_art.aqueduct.png"]       = new[] { "data", "improvement_art", "aqueduct.png" },
			["CivOne.Resources.defaults.data.improvement_art.bank.png"]           = new[] { "data", "improvement_art", "bank.png" },
			["CivOne.Resources.defaults.data.improvement_art.barracks.png"]       = new[] { "data", "improvement_art", "barracks.png" },
			["CivOne.Resources.defaults.data.improvement_art.cathedral.png"]      = new[] { "data", "improvement_art", "cathedral.png" },
			["CivOne.Resources.defaults.data.improvement_art.city_walls.png"]     = new[] { "data", "improvement_art", "city_walls.png" },
			["CivOne.Resources.defaults.data.improvement_art.colosseum.png"]      = new[] { "data", "improvement_art", "colosseum.png" },
			["CivOne.Resources.defaults.data.improvement_art.courthouse.png"]     = new[] { "data", "improvement_art", "courthouse.png" },
			["CivOne.Resources.defaults.data.improvement_art.factory.png"]        = new[] { "data", "improvement_art", "factory.png" },
			["CivOne.Resources.defaults.data.improvement_art.granary.png"]        = new[] { "data", "improvement_art", "granary.png" },
			["CivOne.Resources.defaults.data.improvement_art.hydro_plant.png"]    = new[] { "data", "improvement_art", "hydro_plant.png" },
			["CivOne.Resources.defaults.data.improvement_art.library.png"]        = new[] { "data", "improvement_art", "library.png" },
			["CivOne.Resources.defaults.data.improvement_art.lighthouse.png"]     = new[] { "data", "improvement_art", "lighthouse.png" },
			["CivOne.Resources.defaults.data.improvement_art.marketplace.png"]    = new[] { "data", "improvement_art", "marketplace.png" },
			["CivOne.Resources.defaults.data.improvement_art.mass_transit.png"]   = new[] { "data", "improvement_art", "mass_transit.png" },
			["CivOne.Resources.defaults.data.improvement_art.nuclear_plant.png"]  = new[] { "data", "improvement_art", "nuclear_plant.png" },
			["CivOne.Resources.defaults.data.improvement_art.observatory.png"]    = new[] { "data", "improvement_art", "observatory.png" },
			["CivOne.Resources.defaults.data.improvement_art.palace.png"]         = new[] { "data", "improvement_art", "palace.png" },
			["CivOne.Resources.defaults.data.improvement_art.power_plant.png"]    = new[] { "data", "improvement_art", "power_plant.png" },
			["CivOne.Resources.defaults.data.improvement_art.recycling_cntr..png"] = new[] { "data", "improvement_art", "recycling_cntr..png" },
			["CivOne.Resources.defaults.data.improvement_art.sam_battery.png"]    = new[] { "data", "improvement_art", "sam_battery.png" },
			["CivOne.Resources.defaults.data.improvement_art.sewer_system.png"]   = new[] { "data", "improvement_art", "sewer_system.png" },
			["CivOne.Resources.defaults.data.improvement_art.shipyard.png"]       = new[] { "data", "improvement_art", "shipyard.png" },
			["CivOne.Resources.defaults.data.improvement_art.temple.png"]         = new[] { "data", "improvement_art", "temple.png" },
			["CivOne.Resources.defaults.data.improvement_art.university.png"]     = new[] { "data", "improvement_art", "university.png" },
			// Wonders and SDI Defense
			["CivOne.Resources.defaults.data.improvement_art.apollo_program.png"]          = new[] { "data", "improvement_art", "apollo_program.png" },
			["CivOne.Resources.defaults.data.improvement_art.copernicus'_observatory.png"] = new[] { "data", "improvement_art", "copernicus'_observatory.png" },
			["CivOne.Resources.defaults.data.improvement_art.cure_for_cancer.png"]         = new[] { "data", "improvement_art", "cure_for_cancer.png" },
			["CivOne.Resources.defaults.data.improvement_art.darwin's_voyage.png"]         = new[] { "data", "improvement_art", "darwin's_voyage.png" },
			["CivOne.Resources.defaults.data.improvement_art.great_library.png"]           = new[] { "data", "improvement_art", "great_library.png" },
			["CivOne.Resources.defaults.data.improvement_art.great_wall.png"]              = new[] { "data", "improvement_art", "great_wall.png" },
			["CivOne.Resources.defaults.data.improvement_art.hanging_gardens.png"]         = new[] { "data", "improvement_art", "hanging_gardens.png" },
			["CivOne.Resources.defaults.data.improvement_art.hoover_dam.png"]              = new[] { "data", "improvement_art", "hoover_dam.png" },
			["CivOne.Resources.defaults.data.improvement_art.interstellar_probe.png"]      = new[] { "data", "improvement_art", "interstellar_probe.png" },
			["CivOne.Resources.defaults.data.improvement_art.isaac_newton's_college.png"]  = new[] { "data", "improvement_art", "isaac_newton's_college.png" },
			["CivOne.Resources.defaults.data.improvement_art.j.s.bach's_cathedral.png"]    = new[] { "data", "improvement_art", "j.s.bach's_cathedral.png" },
			["CivOne.Resources.defaults.data.improvement_art.magellan's_expedition.png"]   = new[] { "data", "improvement_art", "magellan's_expedition.png" },
			["CivOne.Resources.defaults.data.improvement_art.manhattan_project.png"]       = new[] { "data", "improvement_art", "manhattan_project.png" },
			["CivOne.Resources.defaults.data.improvement_art.michelangelo's_chapel.png"]   = new[] { "data", "improvement_art", "michelangelo's_chapel.png" },
			["CivOne.Resources.defaults.data.improvement_art.oracle.png"]                  = new[] { "data", "improvement_art", "oracle.png" },
			["CivOne.Resources.defaults.data.improvement_art.pyramids.png"]                = new[] { "data", "improvement_art", "pyramids.png" },
			["CivOne.Resources.defaults.data.improvement_art.colossus.png"]                 = new[] { "data", "improvement_art", "colossus.png" },
			["CivOne.Resources.defaults.data.improvement_art.dome_command_hub.png"]        = new[] { "data", "improvement_art", "dome_command_hub.png" },
			["CivOne.Resources.defaults.data.improvement_art.dome_emitter_array.png"]     = new[] { "data", "improvement_art", "dome_emitter_array.png" },
			["CivOne.Resources.defaults.data.improvement_art.dome_kinetic_ring.png"]      = new[] { "data", "improvement_art", "dome_kinetic_ring.png" },
			["CivOne.Resources.defaults.data.improvement_art.dome_power_core.png"]        = new[] { "data", "improvement_art", "dome_power_core.png" },
			["CivOne.Resources.defaults.data.improvement_art.dome_sensor_net.png"]        = new[] { "data", "improvement_art", "dome_sensor_net.png" },
			["CivOne.Resources.defaults.data.improvement_art.sdi_defense.png"]             = new[] { "data", "improvement_art", "sdi_defense.png" },
			["CivOne.Resources.defaults.data.improvement_art.seti_program.png"]           = new[] { "data", "improvement_art", "seti_program.png" },
			["CivOne.Resources.defaults.data.improvement_art.marco_polo's_voyage.png"]   = new[] { "data", "improvement_art", "marco_polo's_voyage.png" },
			["CivOne.Resources.defaults.data.improvement_art.zheng_he's_voyage.png"]     = new[] { "data", "improvement_art", "zheng_he's_voyage.png" },
			["CivOne.Resources.defaults.data.improvement_art.shakespeare's_theatre.png"]   = new[] { "data", "improvement_art", "shakespeare's_theatre.png" },
			["CivOne.Resources.defaults.data.improvement_art.south_pole_expedition.png"]   = new[] { "data", "improvement_art", "south_pole_expedition.png" },
			["CivOne.Resources.defaults.data.improvement_art.united_nations.png"]          = new[] { "data", "improvement_art", "united_nations.png" },
			["CivOne.Resources.defaults.data.improvement_art.women's_suffrage.png"]        = new[] { "data", "improvement_art", "women's_suffrage.png" },
			// Event art
			["CivOne.Resources.defaults.data.event_art.civilunrest0.png"]       = new[] { "data", "event_art", "civilunrest0.png" },
			["CivOne.Resources.defaults.data.event_art.civilunrest1.png"]       = new[] { "data", "event_art", "civilunrest1.png" },
			["CivOne.Resources.defaults.data.event_art.civilunrest2.png"]       = new[] { "data", "event_art", "civilunrest2.png" },
			["CivOne.Resources.defaults.data.event_art.governmentcollapses.png"] = new[] { "data", "event_art", "governmentcollapses.png" },
			["CivOne.Resources.defaults.data.event_art.cityconquered.png"]         = new[] { "data", "event_art", "cityconquered.png" },
			["CivOne.Resources.defaults.data.event_art.cityliberated.png"]        = new[] { "data", "event_art", "cityliberated.png" },
			["CivOne.Resources.defaults.data.event_art.famine.png"]               = new[] { "data", "event_art", "famine.png" },
			["CivOne.Resources.defaults.data.event_art.globalwarming.png"]        = new[] { "data", "event_art", "globalwarming.png" },
			["CivOne.Resources.defaults.data.event_art.nuclearbombdetonation.png"] = new[] { "data", "event_art", "nuclearbombdetonation.png" },
			["CivOne.Resources.defaults.data.event_art.nuclearmeltdown.png"]      = new[] { "data", "event_art", "nuclearmeltdown.png" },
			["CivOne.Resources.defaults.data.event_art.pollution.png"]            = new[] { "data", "event_art", "pollution.png" },
			["CivOne.Resources.defaults.data.event_art.spaceshiparrived.png"]     = new[] { "data", "event_art", "spaceshiparrived.png" },
			["CivOne.Resources.defaults.data.event_art.spaceshipintercepted.png"] = new[] { "data", "event_art", "spaceshipintercepted.png" },
			["CivOne.Resources.defaults.data.event_art.spaceshiplaunched.png"]    = new[] { "data", "event_art", "spaceshiplaunched.png" },
			["CivOne.Resources.defaults.data.event_art.welovethekingday.png"]     = new[] { "data", "event_art", "welovethekingday.png" },
			// Unit tiles
			["CivOne.Resources.defaults.unit_tiles.unit_tiles.txt"] = new[] { "unit_tiles", "unit_tiles.txt" },
			["CivOne.Resources.defaults.unit_tiles.Armor.png"]       = new[] { "unit_tiles", "Armor.png" },
			["CivOne.Resources.defaults.unit_tiles.Artillery.png"]   = new[] { "unit_tiles", "Artillery.png" },
			["CivOne.Resources.defaults.unit_tiles.Battleship.png"]  = new[] { "unit_tiles", "Battleship.png" },
			["CivOne.Resources.defaults.unit_tiles.Bomber.png"]      = new[] { "unit_tiles", "Bomber.png" },
			["CivOne.Resources.defaults.unit_tiles.Cannon.png"]      = new[] { "unit_tiles", "Cannon.png" },
			["CivOne.Resources.defaults.unit_tiles.Caravan.png"]     = new[] { "unit_tiles", "Caravan.png" },
			["CivOne.Resources.defaults.unit_tiles.Carrier.png"]     = new[] { "unit_tiles", "Carrier.png" },
			["CivOne.Resources.defaults.unit_tiles.Catapult.png"]    = new[] { "unit_tiles", "Catapult.png" },
			["CivOne.Resources.defaults.unit_tiles.Chariot.png"]     = new[] { "unit_tiles", "Chariot.png" },
			["CivOne.Resources.defaults.unit_tiles.Diplomat.png"]    = new[] { "unit_tiles", "Diplomat.png" },
			["CivOne.Resources.defaults.unit_tiles.Fighter.png"]     = new[] { "unit_tiles", "Fighter.png" },
			["CivOne.Resources.defaults.unit_tiles.Frigate.png"]     = new[] { "unit_tiles", "Frigate.png" },
			["CivOne.Resources.defaults.unit_tiles.Legion.png"]      = new[] { "unit_tiles", "Legion.png" },
			["CivOne.Resources.defaults.unit_tiles.MechInf.png"]     = new[] { "unit_tiles", "MechInf.png" },
			["CivOne.Resources.defaults.unit_tiles.militia.png"]     = new[] { "unit_tiles", "militia.png" },
			["CivOne.Resources.defaults.unit_tiles.Musketeers.png"]  = new[] { "unit_tiles", "Musketeers.png" },
			["CivOne.Resources.defaults.unit_tiles.Nuclear.png"]     = new[] { "unit_tiles", "Nuclear.png" },
			["CivOne.Resources.defaults.unit_tiles.Phalanx.png"]     = new[] { "unit_tiles", "Phalanx.png" },
			["CivOne.Resources.defaults.unit_tiles.Riflemen.png"]    = new[] { "unit_tiles", "Riflemen.png" },
			["CivOne.Resources.defaults.unit_tiles.Settlers.png"]    = new[] { "unit_tiles", "Settlers.png" },
			["CivOne.Resources.defaults.unit_tiles.Submarine.png"]   = new[] { "unit_tiles", "Submarine.png" },
			["CivOne.Resources.defaults.unit_tiles.Trireme.png"]     = new[] { "unit_tiles", "Trireme.png" },
		};

		private static void InstallDefaults(string storageDir)
		{
			Assembly asm = Assembly.GetExecutingAssembly();
			foreach (var pair in _defaultAssets)
			{
				string targetPath = Path.Combine(new[] { storageDir }.Concat(pair.Value).ToArray());
				using (Stream src = asm.GetManifestResourceStream(pair.Key))
				{
					if (src == null) continue;
					Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
					using (FileStream dst = File.Create(targetPath))
						src.CopyTo(dst);
				}
			}
		}

		public Runtime(RuntimeSettings settings)
		{
			Settings = settings;
			Profile = Profile.Get(this, settings.Get<string>("profile-name"));
			CoreRes.SpacedockImage = Resources.GetSpacedock();
			InstallDefaults(((IRuntime)this).StorageDirectory);
			string splashPath = Path.Combine(((IRuntime)this).StorageDirectory, "splash.png");
			CoreRes.SplashRawImage = PngDecoder.Load(splashPath);
			RuntimeHandler.Register(this);
		}

		public void Dispose()
		{

		}
	}
}