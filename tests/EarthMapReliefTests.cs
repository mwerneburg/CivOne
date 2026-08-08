// CivOne tests
//
// resources/earth_epic.bin is a checked-in binary that nothing verified. It is produced by
// design/build_earth_map.py from an elevation image that is NOT in the repo, so a rebuild
// with the wrong flags — or with a different source image — lands silently and the only
// symptom is a world that plays oddly.
//
// It landed silently once already. The generator classified rough ground by thresholding a
// tile's MEAN elevation against cuts chosen as percentiles of source PIXELS. Calibrated
// against eleven named summits that image is a linear DEM at 1 px = 33.6 m, so the shipped
// cuts demanded a 100 km cell AVERAGING 2020 m to be a hill and 3870 m to be a mountain.
// Only plateaus can pass that: Tibet came out 60% mountains while the Alps, Pyrenees, Zagros
// and Sierra Madre had exactly zero, and Scandinavia, the Appalachians and the Great Dividing
// Range were perfectly flat — no hills either.
//
// These read the file directly rather than through the engine: the bytes are what ships.

using System.IO;
using System.Linq;

namespace CivOne.Tests
{
	public class EarthMapReliefTests
	{
		private const byte Ocean = 0, Hills = 12, Mountains = 13;

		private sealed class EarthMap
		{
			public int Width, Height;
			public byte[] Tiles = null!;
			public float[]? LatEdges;

			public byte At(double lat, double lon)
			{
				int x = (int)((lon + 180.0) / 360.0 * Width) % Width;
				return Tiles[Row(lat) * Width + x];
			}

			public int Row(double lat)
			{
				if (LatEdges is null) return (int)((90.0 - lat) / 180.0 * Height);
				if (lat >= LatEdges[0]) return 0;
				for (int y = 0; y < Height; y++)
					if (lat >= LatEdges[y + 1]) return y;
				return Height - 1;
			}

			// Share of land tiles in a lat/lon box that are of the given terrain.
			public double Share(byte terrain, double north, double south, double west, double east)
			{
				int hit = 0, land = 0;
				for (int y = Row(north); y <= Row(south); y++)
				for (int x = (int)((west + 180.0) / 360.0 * Width); x <= (int)((east + 180.0) / 360.0 * Width); x++)
				{
					byte t = Tiles[y * Width + x];
					if (t == Ocean) continue;
					land++;
					if (t == terrain) hit++;
				}
				return land == 0 ? 0 : (double)hit / land;
			}
		}

		private static EarthMap? Load(string name)
		{
			DirectoryInfo? dir = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			if (dir is null) return null;   // not running from the source tree

			string path = Path.Combine(dir.FullName, "resources", name);
			Assert.True(File.Exists(path), $"missing map resource: {path}");
			byte[] d = File.ReadAllBytes(path);
			Assert.Equal("CIVE", System.Text.Encoding.ASCII.GetString(d, 0, 4));

			var m = new EarthMap
			{
				Width  = System.BitConverter.ToInt32(d, 8),
				Height = System.BitConverter.ToInt32(d, 12),
			};
			m.Tiles = d.Skip(16).Take(m.Width * m.Height).ToArray();
			int tail = 16 + m.Width * m.Height;
			if (d.Length >= tail + 4 * (m.Height + 1))
				m.LatEdges = Enumerable.Range(0, m.Height + 1)
					.Select(i => System.BitConverter.ToSingle(d, tail + 4 * i)).ToArray();
			return m;
		}

		// The defect, stated directly, on the four ranges that had not one mountain tile
		// between them. Deliberately a low bar: the point is "this range exists", not a
		// particular share, so retuning the cuts does not have to come here.
		[Theory]
		[InlineData("Alps",        47.5, 44.5,   6.0, 15.0)]
		[InlineData("Pyrenees",    43.3, 42.3,  -1.5,  2.5)]
		[InlineData("Zagros",      37.0, 31.0,  45.0, 55.0)]
		[InlineData("SierraMadre", 27.0, 17.0, -107.0, -97.0)]
		[InlineData("Rockies",     49.0, 35.0, -121.0, -105.0)]
		[InlineData("Andes",       -8.0, -35.0, -73.0, -67.0)]
		[InlineData("Himalaya",    36.0, 27.0,  73.0, 97.0)]
		public void ARangeHasMountains(string _, double n, double s, double w, double e)
		{
			EarthMap? m = Load("earth_epic.bin");
			if (m is null) return;
			Assert.True(m.Share(Mountains, n, s, w, e) > 0.05);
		}

		// The lower ranges: hills at minimum. Scandinavia, the Appalachians and the Great
		// Dividing Range were flat ground, which is what started this.
		[Theory]
		[InlineData("Scandes",      69.0, 60.0,   6.0,  16.0)]
		[InlineData("Appalachians", 44.0, 34.0, -83.0, -74.0)]
		[InlineData("GreatDivide", -17.0, -37.0, 145.0, 152.0)]
		public void ALowerRangeIsAtLeastHilly(string _, double n, double s, double w, double e)
		{
			EarthMap? m = Load("earth_epic.bin");
			if (m is null) return;
			double rough = m.Share(Hills, n, s, w, e) + m.Share(Mountains, n, s, w, e);
			Assert.True(rough > 0.10, $"only {rough:P0} rough");
		}

		// The control. Relief classification must not invent ranges on genuinely flat ground —
		// the Amazon basin and the Ganges plain stay flat, or the rule is just noise.
		[Theory]
		[InlineData("Amazon",  -2.0,  -8.0, -68.0, -58.0)]
		[InlineData("Ukraine", 50.0,  47.0,  28.0,  38.0)]
		public void FlatCountryStaysFlat(string _, double n, double s, double w, double e)
		{
			EarthMap? m = Load("earth_epic.bin");
			if (m is null) return;
			Assert.Equal(0.0, m.Share(Mountains, n, s, w, e));
		}

		// Map.ResolveEarthBin walks FIVE directories up from the executable to reach the repo's
		// resources/ — tuned for runtime/sdl/bin/{Debug,Release}/net10.0/. That count is
		// hard-coded arithmetic against a directory layout, and if either moves the source
		// build silently stops finding the shipped map: it falls back to the user data
		// directory, which is how a stale July copy shadowed the regenerated one for a whole
		// autoplay run and the new mountains never appeared.
		//
		// Asserted here rather than through Map.EarthEpicPath because the test assembly lives
		// at a different depth; what matters is that the arithmetic is right for the binary
		// that ships.
		[Theory]
		[InlineData("Debug")]
		[InlineData("Release")]
		public void TheSourceBuildCanReachTheShippedMap(string configuration)
		{
			DirectoryInfo? root = new DirectoryInfo(System.AppContext.BaseDirectory);
			while (root is not null && !File.Exists(Path.Combine(root.FullName, "CivOne.csproj")))
				root = root.Parent;
			if (root is null) return;

			string exeDir = Path.Combine(root.FullName, "runtime", "sdl", "bin", configuration, "net10.0");
			string resolved = Path.GetFullPath(Path.Combine(exeDir,
				"..", "..", "..", "..", "..", "resources", "earth_epic.bin"));

			Assert.True(File.Exists(resolved), $"five levels up from {exeDir} is not the map: {resolved}");
		}

		// Board-size independence. Relief is measured inside a tile, so it grows with the
		// tile: applied unscaled, the same cuts gave the 80x50 board 23% mountains against
		// Epic's 10%. Both boards must be playable worlds, not one world and one wall.
		[Theory]
		[InlineData("earth_epic.bin")]
		[InlineData("earth_standard.bin")]
		public void MountainsAreAPlausibleShareOfLand(string name)
		{
			EarthMap? m = Load(name);
			if (m is null) return;
			int land = m.Tiles.Count(t => t != Ocean);
			double share = (double)m.Tiles.Count(t => t == Mountains) / land;
			Assert.InRange(share, 0.04, 0.16);
		}
	}
}
