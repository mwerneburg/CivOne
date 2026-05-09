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
		};

		private static void InstallDefaults(string storageDir)
		{
			Assembly asm = Assembly.GetExecutingAssembly();
			foreach (var pair in _defaultAssets)
			{
				string targetPath = Path.Combine(new[] { storageDir }.Concat(pair.Value).ToArray());
				if (File.Exists(targetPath)) continue;
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