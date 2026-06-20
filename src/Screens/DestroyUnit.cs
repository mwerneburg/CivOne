// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Graphics.Sprites;
using CivOne.IO;
using CivOne.Units;

namespace CivOne.Screens
{
	// Plays the combat explosion on the unit being destroyed, then disbands it.
	//
	// The screen is a transparent overlay (colour 0) stacked on top of the live game
	// map, so it does not draw the map itself — it only draws the explosion at the
	// unit's current on-screen position, scaled to the active zoom. This is why it
	// works at any window size or zoom level; the old version rendered its own fixed
	// 15x12 / 240x192 map box, which only covered the upper-left corner of a larger
	// viewport (so most explosions fell outside it and were never seen).
	internal class DestroyUnit : BaseScreen
	{
		private const int NOISE_COUNT = 8;

		private readonly DestroyAnimation _animation;
		private readonly IUnit _unit;
		private readonly bool _stack;
		private int _noiseCounter = NOISE_COUNT + 2;
		private readonly IBitmap[]? _destroySprites = null;

		internal DestroyUnit(IUnit unit, bool stack)
		{
			_unit = unit;
			_stack = stack;

			using var p = Common.DefaultPalette;
			Palette = p;

			_animation = Settings.DestroyAnimation;
			if (!Resources.Exists("SP257"))
				_animation = DestroyAnimation.Noise;

			if (_animation == DestroyAnimation.Sprites)
			{
				_destroySprites = new IBitmap[8];
				for (int i = 0; i < 8; i++)
					_destroySprites[i] = Resources["SP257"][16 * i, 96, 16, 16].ColourReplace(9, 0);
			}
		}

		// Nearest-neighbour scale of a 16x16 explosion frame to the current tile size.
		private static Bytemap Scale(IBitmap source, int size)
		{
			Bytemap output = new Bytemap(size, size);
			for (int y = 0; y < size; y++)
			for (int x = 0; x < size; x++)
				output[x, y] = source.Bitmap[(x * source.Bitmap.Width) / size, (y * source.Bitmap.Height) / size];
			return output;
		}

		protected override bool HasUpdate(uint gameTick)
		{
			int px = Common.GamePlay.TilePixelSize;
			int ox = Settings.RightSideBar ? 0 : 80;
			int oy = 8;

			int xx = _unit.X - Common.GamePlay.X;
			while (xx < 0) xx += Map.WIDTH;
			while (xx >= Map.WIDTH) xx -= Map.WIDTH;
			int yy = _unit.Y - Common.GamePlay.Y;
			bool onScreen = xx < Common.GamePlay.TilesX && yy >= 0 && yy < Common.GamePlay.TilesY;

			this.Clear(0);

			if (onScreen)
			{
				int sx = ox + xx * px;
				int sy = oy + yy * px;
				int step = 8 - _noiseCounter;
				if (_animation == DestroyAnimation.Sprites && step >= 0 && step < 8)
				{
					if (px == 16)
					{
						this.AddLayer(_destroySprites![step], sx, sy);
					}
					else
					{
						using Bytemap frame = Scale(_destroySprites![step], px);
						this.AddLayer(frame, sx, sy);
					}
				}
				else if (_animation == DestroyAnimation.Noise)
				{
					// ponytail: crude static block, only reached when SP257 is missing
					Bytemap noise = new Bytemap(px, px);
					for (int y = 0; y < px; y++)
					for (int x = 0; x < px; x++)
						noise[x, y] = (byte)Common.Random.Next(1, NOISE_COUNT);
					this.AddLayer(noise, sx, sy);
				}
			}

			_noiseCounter--;
			if (_noiseCounter == 0)
			{
				IUnit[] units = (_unit.Tile.Units.Length > 1 && _unit.Tile.City is null && !_unit.Tile.Fortress && _stack)
					? _unit.Tile.Units
					: new[] { _unit };
				foreach (IUnit unit in units)
					Game.DisbandUnit(unit);
				Common.GamePlay.RefreshMap();
				Common.GamePlay.Update(gameTick);
				Destroy();
			}

			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args) => false;

		public override bool MouseDown(ScreenEventArgs args) => false;
	}
}
