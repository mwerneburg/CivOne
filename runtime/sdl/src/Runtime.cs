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
		internal void InvokeMouseWheel(ScreenEventArgs args) => MouseWheel?.Invoke(this, args);

		public event EventHandler Initialize, Draw;
		public event UpdateEventHandler Update;
		public event KeyboardEventHandler KeyboardUp, KeyboardDown;
		public event ScreenEventHandler MouseUp, MouseDown, MouseMove, MouseWheel;
		internal event EventHandler CursorChanged;
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
		void IRuntime.Quit() => SignalQuit = true;

		private const string DefaultsPrefix = "CivOne.Resources.defaults.";

		// Directory segments inside the embedded defaults tree. Resource names mangle '/' -> '.',
		// and filenames may themselves contain '.', so InstallDefaults peels known (dot-free)
		// directory segments off the front and treats whatever remains as the filename. Only a
		// brand-new defaults *subdirectory* ever needs adding here -- individual files never do.
		private static readonly HashSet<string> DefaultsDirs = new HashSet<string>
		{
			"data", "garrison_icons", "unit_tiles", "leader_art", "event_art", "improvement_art",
		};

		// Deploys every embedded defaults asset to the storage dir on first run (skipping files
		// that already exist). Enumerates the assembly manifest, so any EmbeddedResource added
		// under Resources/defaults/ is installed automatically -- no per-file registration. This
		// replaced a hand-maintained table that silently dropped newly-added art (leader/event/
		// improvement images) from fresh installs.
		private static void InstallDefaults(string storageDir)
		{
			Assembly asm = Assembly.GetExecutingAssembly();
			foreach (string name in asm.GetManifestResourceNames())
			{
				if (!name.StartsWith(DefaultsPrefix)) continue;

				string[] segments = name.Substring(DefaultsPrefix.Length).Split('.');
				var dirs = new List<string>();
				int i = 0;
				while (i < segments.Length - 2 && DefaultsDirs.Contains(segments[i]))
					dirs.Add(segments[i++]);
				string fileName = string.Join(".", segments.Skip(i));

				string targetPath = Path.Combine(new[] { storageDir }.Concat(dirs).Append(fileName).ToArray());
				if (File.Exists(targetPath)) continue;
				using (Stream src = asm.GetManifestResourceStream(name))
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