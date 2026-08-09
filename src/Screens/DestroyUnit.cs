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
		private readonly Player? _credit;
		private int _noiseCounter = NOISE_COUNT + 2;
		private readonly IBitmap[]? _destroySprites = null;

		internal DestroyUnit(IUnit unit, bool stack, Player? credit = null)
		{
			_unit = unit;
			_stack = stack;
			_credit = credit;

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
			if (_noiseCounter == 0) FinishAndDestroy(gameTick);

			return true;
		}

		// The part that must happen whether or not a frame was ever drawn.
		private void FinishAndDestroy(uint gameTick)
		{
			Kill(_unit, _stack, _credit);
			Common.GamePlay.RefreshMap();
			Common.GamePlay.Update(gameTick);
			Destroy();
		}

		// Which units a death takes with it: a stack dies together in the open, but not in a
		// city or a fortress. Shared so the no-animation path cannot drift from the animated
		// one — this is the rule, not a rendering detail.
		// Killing a Scavenger extraction craft is the only counterplay to the draining, and
		// the score says so. `credit` is the player whose attack did it — null for every
		// other way a unit leaves the world (disband, upgrade, capture sweep), which must
		// not pay. Awarded here rather than at the call site because this is where the set
		// that actually dies is decided: `stack` can turn one kill into six.
		private const int HarvesterBounty = 100;

		private static void Kill(IUnit unit, bool stack, Player? credit = null)
		{
			IUnit[] units = (unit.Tile.Units.Length > 1 && unit.Tile.City is null && !unit.Tile.Fortress && stack)
				? unit.Tile.Units
				: new[] { unit };
			if (credit is not null)
			{
				int bounty = units.Count(u => u is Units.Harvester) * HarvesterBounty;
				if (bounty > 0) credit.AwardMilestone(bounty);
			}
			foreach (IUnit u in units)
				Game.DisbandUnit(u);
		}

		// True when the player could actually watch this happen: the tile is explored AND
		// inside the viewport. `onScreen` alone was not enough — it asks where the camera is,
		// never what the player has discovered, so explosions played on fogged ground and
		// announced battles the fog was supposed to hide.
		internal static bool CanBeSeen(IUnit unit)
		{
			Player? human = Game.Instance?.HumanPlayer;
			if (human is null || !human.Visible(unit.X, unit.Y)) return false;
			GamePlay? gamePlay = Common.GamePlay;
			if (gamePlay is null) return false;

			int xx = unit.X - gamePlay.X;
			while (xx < 0) xx += Map.WIDTH;
			while (xx >= Map.WIDTH) xx -= Map.WIDTH;
			int yy = unit.Y - gamePlay.Y;
			return xx < gamePlay.TilesX && yy >= 0 && yy < gamePlay.TilesY;
		}

		// Resolve a death nobody can watch, without queueing a screen at all.
		//
		// The animation is inserted for EVERY combat death in the world — BaseUnit.Confront
		// does not ask whose war it is — and it ran its full ten-tick countdown regardless,
		// drawing nothing. That was 8,789 paced samples in 32 turns of a war-heavy save.
		// Returns true when it has handled the death and no screen is needed.
		internal static bool ResolveIfUnseen(IUnit unit, bool stack, Player? credit = null)
		{
			if (Game.Animations && CanBeSeen(unit)) return false;
			Kill(unit, stack, credit);
			Common.GamePlay?.RefreshMap();
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args) => false;

		public override bool MouseDown(ScreenEventArgs args) => false;
	}
}
