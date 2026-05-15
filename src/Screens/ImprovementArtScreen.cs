// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.IO;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;

namespace CivOne.Screens
{
	[Modal, Expand]
	internal class ImprovementArtScreen : BaseScreen
	{
		private readonly string _name;
		private readonly byte[,] _indices; // [y, x] pre-mapped to Cassette palette
		private readonly int _imgW, _imgH;
		private bool _update = true;

		internal static string FindArtPath(string improvementName)
		{
			if (string.IsNullOrEmpty(improvementName)) return null;
			try
			{
				string file = improvementName.ToLower().Replace(' ', '_') + ".png";
				string path = Path.Combine(Settings.Instance.DataDirectory, "improvement_art", file);
				return File.Exists(path) ? path : null;
			}
			catch { return null; }
		}

		private static Palette BuildPalette()
		{
			Palette p = Common.DefaultPalette;
			CassetteTheme.ApplyTo(p);
			return p;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;
			_update = false;

			this.CassetteBackground();

			if (_indices != null)
			{
				int ox = Math.Max(0, (Width  - _imgW) / 2);
				int oy = Math.Max(0, (Height - _imgH) / 2);
				int dw = Math.Min(_imgW, Width  - ox);
				int dh = Math.Min(_imgH, Height - oy);
				for (int y = 0; y < dh; y++)
				for (int x = 0; x < dw; x++)
					Bitmap[ox + x, oy + y] = _indices[y, x];
			}

			this.AddScanlines();

			// Subtle dismiss hint at bottom
			string hint = "[ ANY KEY OR CLICK TO CONTINUE ]";
			this.DrawText(hint, 0, CassetteTheme.BORDER, Width / 2, Height - 9, TextAlign.Center);

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)  { Destroy(); return true; }
		public override bool MouseDown(ScreenEventArgs args) { Destroy(); return true; }

		internal ImprovementArtScreen(string artPath, string improvementName)
		{
			_name = improvementName;
			OnResize += (s, e) => _update = true;

			using (Palette pal = BuildPalette())
			{
				Palette = pal;

				byte[] rgba = PngFile.ReadRgba(artPath, out _imgW, out _imgH);
				if (rgba != null)
					_indices = PngFile.ToIndices(rgba, _imgW, _imgH, pal);
			}
		}
	}
}
