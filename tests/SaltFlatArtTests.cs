// CivOne tests
//
// The salt flats rendered as plain green land in the 2200 AD run. Nothing was missing and
// nothing threw: MapTile.TileLayer's switch had no SaltFlat case, so a drained tile fell
// through to `return null` and got only TileBase — the bare land field every terrain is
// composited on top of. The drained seabed looked exactly like grassland.
//
// The silent-asset corollary in CLAUDE.md, arriving through a new door: the art existed
// ([salt_flat] in free_tiles.txt) and the free-mode fallback in GetTileLayer named it, but
// nothing ever asked for it. A file-exists test would have passed the whole time.

using System.Linq;
using CivOne;
using CivOne.Enums;
using CivOne.Graphics.Sprites;
using CivOne.Tiles;

namespace CivOne.Tests
{
	public class SaltFlatArtTests
	{
		// The defect, stated directly.
		[Fact]
		public void ASaltFlatHasATileLayer()
		{
			Sim.NewGame(width: 80, height: 50);
			Map.Instance.ChangeTileType(40, 25, Terrain.SaltFlat);

			Assert.NotNull(MapTile.TileLayer(Map.Instance[40, 25]));
		}

		// ...and it is not the same picture as the ground it replaced. A layer that matched
		// LandBase would satisfy the test above and still be invisible on screen.
		[Fact]
		public void ItDoesNotLookLikeBareGround()
		{
			Sim.NewGame(width: 80, height: 50);
			Map.Instance.ChangeTileType(40, 25, Terrain.SaltFlat);

			byte[] flat = MapTile.TileLayer(Map.Instance[40, 25])!.Bitmap.ToByteArray();
			byte[] ground = MapTile.TileBase(Map.Instance[40, 25]).Bitmap.ToByteArray();

			Assert.NotEqual(ground, flat);
		}

		// The art itself: [salt_flat] must stay in free_tiles.txt. Free.SaltFlat falls back to
		// Desert when the section is gone, which is a sensible fallback and a silent one —
		// exactly the failure mode this project keeps getting bitten by.
		[Fact]
		public void TheSaltFlatSectionIsInFreeTiles()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			if (dir is null) return;   // not running from the source tree; nothing to check

			string path = System.IO.Path.Combine(dir.FullName, "free_tiles.txt");
			Assert.True(System.IO.File.Exists(path), $"missing: {path}");
			Assert.Contains("[salt_flat]", System.IO.File.ReadAllText(path));
		}
	}
}
