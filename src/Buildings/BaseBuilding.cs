// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.IO;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Graphics;
using CivOne.Graphics.ImageFormats;
using CivOne.IO;

namespace CivOne.Buildings
{
	internal abstract class BaseBuilding : BaseInstance, IBuilding
	{
		private static IBitmap[,] _iconsCache = new IBitmap[6, 4], _iconsCacheGrass = new IBitmap[6, 4];
		
		private IBitmap GrassIcon => Resources["CITYPIX2"][250, 0, 50, 50].ColourReplace(1, 0);
		
		private IBitmap _icon = null!;
		public virtual IBitmap Icon
		{
			// Hand-drawn art wins when present. Mirrors the leader-portrait path
			// (BaseLeader.LoadPngPortrait): drop <name>.png into
			// {StorageDirectory}/data/building_art/ and it replaces the sprite-sheet
			// or procedural icon with no code change. Falls back silently, so a
			// missing or unreadable file simply leaves the old icon in place.
			get => LoadPngIcon() ?? _icon;
			protected set => _icon = value;
		}
		public virtual IBitmap SmallIcon { get; protected set; } = null!;

		private static string? BuildingArtPath(string name)
		{
			try
			{
				string file = name.ToLower()
					.Replace('.', '_').Replace(' ', '_').Replace('\'', '_') + ".png";
				string path = Path.Combine(Settings.Instance.DataDirectory, "building_art", file);
				return File.Exists(path) ? path : null;
			}
			catch { return null; }
		}

		// The city-screen building icon is 50x50 (see SetIcon).
		private const int IconSize = 50;

		// Area-average downscale. Alpha is averaged too, so a mostly-transparent
		// destination pixel stays transparent and the icon keeps its silhouette
		// instead of acquiring a halo of half-lit edge pixels.
		private static byte[] Downscale(byte[] src, int sw, int sh, int dw, int dh)
		{
			byte[] dst = new byte[dw * dh * 4];
			for (int dy = 0; dy < dh; dy++)
			for (int dx = 0; dx < dw; dx++)
			{
				int x0 = dx * sw / dw, x1 = System.Math.Max(x0 + 1, (dx + 1) * sw / dw);
				int y0 = dy * sh / dh, y1 = System.Math.Max(y0 + 1, (dy + 1) * sh / dh);
				long r = 0, g = 0, b = 0, a = 0; int n = 0;
				for (int sy = y0; sy < y1; sy++)
				for (int sx = x0; sx < x1; sx++)
				{
					int i = (sy * sw + sx) * 4;
					r += src[i]; g += src[i + 1]; b += src[i + 2]; a += src[i + 3];
					n++;
				}
				int o = (dy * dw + dx) * 4;
				dst[o]     = (byte)(r / n);
				dst[o + 1] = (byte)(g / n);
				dst[o + 2] = (byte)(b / n);
				dst[o + 3] = (byte)(a / n);
			}
			return dst;
		}

		private bool _pngChecked;
		private Picture? _pngIcon;
		private Picture? LoadPngIcon()
		{
			if (_pngChecked) return _pngIcon;
			_pngChecked = true;
			if (Name is null) return null;
			string? path = BuildingArtPath(Name);
			if (path is null) return null;
			try
			{
				byte[] rgba = PngFile.ReadRgba(path, out int w, out int h);
				if (rgba is null) return null;
				// Source art is authored at whatever size suits drawing it — the
				// Hospital came in at 1264x848 — so box-filter it down to the icon
				// slot BEFORE quantising. Averaging first and quantising second
				// keeps far more of the shape than picking one source pixel in 25.
				rgba = Downscale(rgba, w, h, IconSize, IconSize);
				w = h = IconSize;

				Palette pal = Common.DefaultPalette;
				CassetteTheme.ApplyTo(pal);
				byte[,] idx = PngFile.ToIndices(rgba, w, h, pal);
				Picture pic = new Picture(w, h, pal);
				for (int y = 0; y < h; y++)
				for (int x = 0; x < w; x++)
					pic.Bitmap[x, y] = idx[y, x];
				_pngIcon = pic;
			}
			catch { _pngIcon = null; }
			return _pngIcon;
		}
		public string Name { get; protected set; } = null!;
		public byte PageCount => 2;

		// Override in a derived building to supply custom Civilopedia text.
		// Return non-empty array for page 1 (description) or page 2 (extra detail).
		// Empty array falls through to the original BLURB1 game-data lookup.
		public virtual string[] GetPageText(byte pageNumber) => new string[0];

		public Picture DrawPage(byte pageNumber)
		{
			string[] text = new string[0];
			switch (pageNumber)
			{
				case 1:
					text = Resources.GetCivilopediaText("BLURB1/" + Name.ToUpper());
					break;
				case 2:
					text = Resources.GetCivilopediaText("BLURB1/" + Name.ToUpper() + "2");
					break;
				default:
					Log("Invalid page number: {0}", pageNumber);
					break;
			}
			
			Picture output = new Picture(320, 200);
			
			int yy = 76;
			foreach (string line in text)
			{
				Log(line);
				output.DrawText(line, 6, 1, 12, yy);
				yy += 9;
			}
			
			if (pageNumber == 2)
			{
				yy += 8;
				string requiredTech = "";
				if (RequiredTech is not null) requiredTech = RequiredTech.Name;
				output.DrawText($"Requires {requiredTech}", 6, 9, 12, yy); yy += 8;
				output.DrawText($"Cost: {Price}0 shields.", 6, 9, 12, yy); yy += 8;
				output.DrawText($"Maintenance: ${Maintenance}", 6, 12, 12, yy);
			}
			
			return output;
		}
		
		protected Building Type { get; set; }
		
		public IAdvance? RequiredTech { get; protected set; }
		public short SellPrice { get; protected set; }
		public short BuyPrice { get; private set; }
		public byte ProductionId => (byte)(255 - Type);
		public byte Price { get; protected set; }
		public byte Maintenance { get; protected set; }
		
		protected void SetIcon(int col, int row, bool grassTile)
		{
			if ((grassTile && _iconsCacheGrass[col, row] is null) || (!grassTile && _iconsCache[col, row] is null))
			{
				if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("CITYPIX2"))
				{
					Icon = new Picture(Free.Instance.BuildingIcon(), Common.GetPalette256);
				}
				else
				{
					Icon = new Picture(50, 50, Resources["CITYPIX2"].Palette);

					if (grassTile)
						Icon.AddLayer(GrassIcon);

					Icon.AddLayer(Resources["CITYPIX2"][col * 50, row * 50, 50, 50]
									.ColourReplace(1, 0));
				}

				if (grassTile) _iconsCacheGrass[col, row] = Icon;
				else _iconsCache[col, row] = Icon;
			}
			Icon = (grassTile ? _iconsCacheGrass[col, row] : _iconsCache[col, row]);
		}
		
		protected void SetSmallIcon(int col, int row)
		{
			string picFile = GFX256 ? "SP299" : "SPRITES";
			if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists(picFile))
			{
				SmallIcon = new Picture(Free.Instance.BuildingIconSmall(), Common.GetPalette256);
				return;
			}
			SmallIcon = Resources[picFile][160 + (19 * col), 50 + (10 * row), 20, 10]
				.ColourReplace(0, 5)
				.FillRectangle(0, 0, 1, 10, 0)
				.FillRectangle(19, 0, 1, 10, 0);
		}
		
		public byte Id => (byte)Type;
		
		protected BaseBuilding(byte price = 1, byte maintenance = 0)
		{
			Price = price;
			Maintenance = maintenance;
			BuyPrice = (short)(40 * price);
			SellPrice = (short)(10 * price);
		}
	}
}