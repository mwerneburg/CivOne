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
using System.IO;
using System.Linq;
using CivOne.Enums;
using CivOne.IO;

using static CivOne.Enums.Direction;

namespace CivOne.Graphics
{
	internal class Free
	{
		private Bytemap _panelGrey = null!, _panelBlue = null!;
		private Bytemap _landBase = null!, _seaBase = null!, _city = null!, _fortify = null!;
		private Bytemap[] _terrain = new Bytemap[10];

		private IEnumerable<byte> GenerateNoise(params byte[] values)
		{
			Random r = new Random(0x4701);
			while (true)
			{
				yield return values[r.Next(values.Length)];
			}
		}

		private IEnumerable<byte> GenerateUnit()
		{
			for (int yy = 0; yy < 16; yy++)
			for (int xx = 0; xx < 16; xx++)
			{
				if ((xx == 0 || xx == 15 || yy == 0 || yy == 15) || ((xx == 1 || xx == 14) && (yy == 1 || yy == 14)))
				{
					yield return 0;
				}
				else if (xx == 1 || yy == 14)
				{
					yield return 15;
				}
				else if (xx == 14 || yy == 1)
				{
					yield return 2;
				}
				else
				{
					yield return 10;
				}
			}
		}

		private void DiffPanel(ref Bytemap bytemap, int left, int top, int width, int height)
		{
			// Cassette chrome: dark outline, amber trim, dark line, cream face.
			// Was [42, 41, 47, 15] — indices that meant something in the original
			// asset palette but land on terrain green/garnet in the Free palette,
			// framing the difficulty screen in colours nothing else on screen uses.
			byte[] colours = [CassetteTheme.BORDER, CassetteTheme.PHOS_DIM, CassetteTheme.BORDER, CassetteTheme.WHITE];
			for (int i = 0; i < colours.Length; i++)
			{
				bytemap.FillRectangle(left + i, top + i, width - (i * 2), height - (i * 2), colours[i]);
			}
		}

		public Bytemap PanelGrey
		{
			get
			{
				if (_panelGrey is null)
				{
					_panelGrey = new Bytemap(16, 16).FromByteArray(GenerateNoise(3, 4).Take(16 * 16).ToArray());
				}
				return _panelGrey;
			}
		}

		public Bytemap PanelBlue
		{
			get
			{
				if (_panelBlue is null)
				{
					_panelBlue = new Bytemap(16, 16).FromByteArray(GenerateNoise(57, 9).Take(16 * 16).ToArray());
				}
				return _panelBlue;
			}
		}

		// The base field drawn UNDER every land tile. The terrain textures
		// ([desert], [plains], [grassland], …) layer on top, so this is the
		// colour that shows through their transparent (0) pixels. Override the
		// whole land base with a [land] section; a terrain that wants a fully
		// distinct field (e.g. deserts) should instead paint an OPAQUE section
		// of its own so no LandBase shows through.
		public Bytemap LandBase
		{
			get
			{
				if (_landBase is null)
				{
					byte[]? loaded = TryLoadTile("land");
					_landBase = new Bytemap(16, 16).FromByteArray(
						loaded ?? GenerateNoise(37, 38, 39).Take(16 * 16).ToArray());
				}
				return _landBase;
			}
		}

		public Bytemap OceanBase
		{
			get
			{
				if (_seaBase is null)
				{
					// Static OCEAN(18) ~82% + sparse green/beige/border specks for depth variety.
					_seaBase = new Bytemap(16, 16).FromByteArray(GenerateNoise(18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 18, 14, 14, 7, 5).Take(16 * 16).ToArray());
				}
				return _seaBase;
			}
		}

		public Bytemap LakeTile()
		{
			byte[]? loaded = TryLoadTile("lakes");
			if (loaded is not null)
				return new Bytemap(16, 16).FromByteArray(loaded);
			// Fallback: CYAN(17) primary with sparse deep-blue(18) specks — visually lighter than ocean.
			return new Bytemap(16, 16).FromByteArray(GenerateNoise(17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 18).Take(16 * 16).ToArray());
		}

		public Bytemap Plains => new Bytemap(16, 16).FromByteArray(GenerateNoise(0, 0, 0, 47, 0, 0, 0, 7, 0, 0, 0, 0).Take(16 * 16).ToArray());

		public Bytemap Arctic => new Bytemap(16, 16).FromByteArray(
			TryLoadTile("arctic") ?? GenerateNoise(16, 7, 17, 18, 7, 15, 20, 19, 15).Skip(380).Take(16 * 16).ToArray());

		public Bytemap Tundra => new Bytemap(16, 16).FromByteArray(
			TryLoadTile("tundra") ?? GenerateNoise(7, 0, 0, 0, 0, 0, 7, 0, 15).Skip(590).Take(16 * 16).ToArray());

		public Bytemap Desert
		{
			get
			{
				byte[]? loaded = TryLoadTile("desert");
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);

				return new Bytemap(16, 16).FromByteArray(GenerateNoise(42, 0, 43, 0, 44, 0, 45, 0, 46, 0, 47).Skip(914).Take(16 * 16).ToArray());
			}
		}

		public Bytemap Forest
		{
			get
			{
				byte[]? loaded = TryLoadTile("forest");
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);

				return new Bytemap(16, 16).FromByteArray(
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  5,  5,  0,  0,  0,
					0,  0,  0,  0,  0,  5, 39, 38,  5,  0,  5, 38, 39,  5,  0,  0,
					0,  0,  0,  0,  0,  5,  5, 39,  5,  0,  5, 39, 38,  5,  0,  0,
					0,  0,  0,  0,  5, 39, 38,  5, 39,  5, 38, 38, 39, 38,  5,  0,
					0,  0,  0,  0,  5, 38, 39,  5, 38,  5, 39, 39, 38, 38,  5,  0,
					0,  0,  0,  5, 39, 39, 38, 39,  5, 38, 38, 38, 39, 38, 38,  5,
					0,  0,  0,  5, 39, 38, 38, 38,  5,  5,  5, 40, 41,  5,  5,  5,
					0,  0,  5, 38, 38, 38, 38, 39, 39,  5, 39,  5,  5,  0,  0,  0,
					0,  0,  5, 39, 38, 39, 39, 38, 38,  5,  5,  5,  0,  0,  0,  0,
					0,  5, 38, 38, 39, 38, 39, 39, 38, 39,  5,  0,  0,  0,  0,  0,
					0,  5,  5,  5, 38, 39, 39, 38,  5,  5,  5,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  5, 40, 41,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
				);
			}
		}

		public Bytemap Hills
		{
			get
			{
				return new Bytemap(16, 16).FromByteArray(
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5,  5,  5,  5,  5,  5,  5,  0,  0,  0,
					0,  0,  0,  0,  0,  5,  0,  0,  0,  0,  0,  0,  0,  5,  0,  0,
					0,  0,  0,  5,  5,  5,  5,  0,  0,  0,  0,  0,  0,  0,  5,  0,
					0,  0,  5,  0,  0,  0,  0,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  5,  0,  0,  0,  0,  0,  0,  5,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
				);
			}
		}

		public Bytemap Mountains
		{
			get
			{
				byte[]? loaded = TryLoadTile("mountains");
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);

				return new Bytemap(16, 16).FromByteArray(
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15,  7, 15,  5,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  5, 15,  7, 15, 15,  5,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  5, 15, 15,  7, 15, 7,  5,  0,  0,  0,  0,
					0,  0,  0,  0,  5, 15, 15, 41, 15, 15, 15, 15,  5,  0,  0,  0,
					0,  0,  0,  5, 40, 40,  7, 40,  7, 41, 15, 40,  5,  0,  0,  0,
					0,  0,  0,  5, 41, 40, 41, 41, 41, 41, 41, 40,  5,  0,  0,  0,
					0,  0,  5, 40, 40, 41, 40, 40, 41, 40,  7, 41, 41,  5,  0,  0,
					0,  0,  5, 41, 41, 41,  7, 41, 40, 41, 41, 41,  7,  5,  0,  0,
					0,  5, 41, 41,  7, 40, 41, 41, 41, 40, 41, 40, 40, 41,  5,  0,
					0,  0,  0, 40, 41, 41, 41, 40, 41, 40, 41, 40, 41,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
				);
			}
		}

		public Bytemap Jungle
		{
			get
			{
				byte[]? loaded = TryLoadTile("jungle");
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);

				return new Bytemap(16, 16).FromByteArray(
					0,  0,  0,  0,  0,  0,  0,  5,  0,  5,  0,  0,  0,  5,  0,  0,
					0,  0,  0,  5,  0,  0,  5, 39,  5, 39,  5,  0,  5,  0,  5,  0,
					0,  0,  5, 39,  5,  5, 39, 38, 37, 39, 38,  5, 37, 38, 37,  5,
					0,  5, 38, 38,  5, 37, 38, 39, 39, 38, 39, 37, 38, 39, 39,  5,
					0,  5, 39, 38, 37, 38, 37, 38, 37, 38, 38, 39, 39, 38, 37,  5,
					0,  5, 37, 39, 39, 37, 39, 39, 37, 38, 39, 39, 39, 37,  5,  0,
					0,  5, 38, 39, 38,  5, 38, 38, 38, 37, 37, 38, 38, 40,  5,  0,
					0,  0,  5, 41,  5,  0,  5, 40,  5,  5,  5, 41,  5, 40,  5,  0,
					0,  0,  5, 41,  5,  0,  5, 41,  5,  0,  5, 40,  5, 41,  5,  0,
					0,  0,  5, 40,  5,  0,  5, 40,  5,  0,  5, 40,  5, 41,  5,  0,
					0,  0,  5, 41,  5,  0,  5, 41,  5,  0,  5, 41,  5, 41,  5,  0,
					0,  0,  5, 41,  5,  0,  5, 40,  5,  0,  5, 41,  5, 40,  5,  0,
					0,  0,  5, 40,  5,  0,  5, 41,  5,  0,  5, 40,  5, 41,  5,  0,
					0,  0,  5, 41,  5,  0,  5, 40,  5,  0,  5, 40,  5, 40,  5,  0,
					0,  0,  5, 40,  5,  0,  0,  0,  0,  0,  5, 41,  5,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
				);
			}
		}

		public Bytemap Swamp
		{
			get
			{
				byte[]? loaded = TryLoadTile("swamp");
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);

				return new Bytemap(16, 16).FromByteArray(
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0, 14,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  6,  6,  0,  0,  0,  0,  0, 14,  0,  6,  0,  0,  0,
					 0,  0,  7, 14,  0,  6,  0,  0,  0,  0,  6,  7,  6,  6,  0,  0,
					 0,  0, 14,  6,  0,  0,  0,  0,  0,  6,  0,  6, 14, 14,  0,  0,
					 0,  6,  0, 14,  6,  0,  0,  0,  0,  7,  6,  0,  7,  0,  0,  0,
					 0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  6, 14,  0,  0,  0,  0,
					 0,  0,  0,  0,  0, 17,  0,  0,  0,  0,  7,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 17,  0,  0,  0,
					 0,  0,  0,  0, 14,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  7,  0,  0,  0,  0,  0, 14,  6,  6,  0,  0,  0,  0,
					 0,  0,  6,  0, 14,  7,  0,  0,  0,  7,  7,  0,  7,  0,  0,  0,
					 0,  6, 14, 14,  6, 14,  0,  0,  0,  6,  0, 14,  0,  0,  0,  0,
					 0,  0,  0,  6,  7,  0,  0,  0,  0,  0,  7,  6,  0,  0,  0,  0,
					 0,  0,  0, 14,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
				);
			}
		}

		public Bytemap Grassland
		{
			get
			{
				return new Bytemap(16, 16).FromByteArray(
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0, 34,  0,  0,  0,  0, 35,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 34,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0, 36,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0, 36,  0,  0,  0,  0,  0, 36,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0, 34,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 35,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0, 35,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
				);
			}
		}

		// Returns the fauna-specific section name for continent-aware Plains/Forest/Tundra specials,
		// or null for standard/non-fauna terrain (falls back to Special(type)).
		private static string? FaunaSection(Terrain terrain, byte continentId)
		{
			int theme = continentId switch {
				1 or 2 => 0,
				15     => 3,
				_ when continentId % 2 == 1 => 1,
				_                           => 2,
			};
			return (theme, terrain) switch {
				(1, Terrain.Plains) => "special_plains_terror_bird",
				(1, Terrain.Forest) => "special_forest_cassowary",
				(1, Terrain.Tundra) => "special_tundra_mammoth",
				(2, Terrain.Plains) => "special_plains_kangaroo",
				(2, Terrain.Forest) => "special_forest_wallaby",
				(2, Terrain.Tundra) => "special_tundra_wombat",
				(3, Terrain.Plains) => "special_plains_emu",
				(3, Terrain.Forest) => "special_forest_kiwi",
				(3, Terrain.Tundra) => "special_tundra_moa",
				_                   => null,
			};
		}

		public Bytemap Special(Terrain type, byte continentId)
		{
			string? section = FaunaSection(type, continentId);
			if (section is not null)
			{
				byte[]? loaded = TryLoadTile(section);
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);
			}
			return Special(type);
		}

		public Bytemap Special(Terrain type)
		{
			string? specialSection = type switch
			{
				Terrain.Ocean     => "special_ocean",
				Terrain.Jungle    => "special_jungle",
				Terrain.Mountains => "special_mountains",
				Terrain.Desert    => "special_desert",
				Terrain.Forest    => "special_forest",
				Terrain.Plains    => "special_plains",
				Terrain.Hills     => "special_hills",
				Terrain.Swamp     => "special_swamp",
				Terrain.Arctic    => "special_arctic",
				Terrain.Tundra    => "special_tundra",
				_ => null
			};
			if (specialSection is not null)
			{
				byte[]? loaded = TryLoadTile(specialSection);
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);
			}

			switch(type)
			{
				case Terrain.Ocean:
					return new Bytemap(16, 16).FromByteArray(
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  7,  0,  7,  7,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  7,  7,  7,  1,  7,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  7,  7,  7,  7,  7,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  7,  0,  7,  7,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  7,  0,  7,  7,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  7,  7,  7,  1,  7,  0,  0,
						0,  0,  7,  0,  7,  7,  0,  0,  0,  7,  7,  7,  7,  7,  0,  0,
						0,  0,  7,  7,  7,  1,  7,  0,  0,  7,  0,  7,  7,  0,  0,  0,
						0,  0,  7,  7,  7,  7,  7,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  7,  0,  7,  7,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
					);

				// Ideal-cut diamond ~8px girdle; outline=5 body=17(CYAN) highlight=8(INK_HIGH)
				case Terrain.Jungle:
					return new Bytemap(16, 16).FromByteArray(
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  5,  5,  5,  5,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  5, 17, 17, 17, 17,  5,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  5, 17, 17,  8, 17, 17, 17,  5,  0,  0,  0,  0,
						0,  0,  0,  0,  5,  5,  5,  5,  5,  5,  5,  5,  0,  0,  0,  0,
						0,  0,  0,  0,  5, 17, 17, 17, 17, 17, 17,  5,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  5, 17, 17, 17, 17,  5,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  5, 17, 17,  5,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
					);

				// Ideal-cut diamond ~8px girdle; outline=5 body=12(PHOS/gold) highlight=13(PHOS_GLOW)
				case Terrain.Mountains:
					return new Bytemap(16, 16).FromByteArray(
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  5,  5,  5,  5,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  5, 12, 12, 12, 12,  5,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  5, 12, 12, 13, 12, 12, 12,  5,  0,  0,  0,  0,
						0,  0,  0,  0,  5,  5,  5,  5,  5,  5,  5,  5,  0,  0,  0,  0,
						0,  0,  0,  0,  5, 12, 12, 12, 12, 12, 12,  5,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  5, 12, 12, 12, 12,  5,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  5, 12, 12,  5,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
					);

				// Palm tree: trunk=INK_MID(7)/INK_LOW(6), fronds=OK(14), crown shadows=BORDER(5)
				case Terrain.Desert:
					return new Bytemap(16, 16).FromByteArray(
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0, 14,  0,  0,  0,  0,  0, 14,  0,  0,  0,  0,  0, 14,  0,  0,
						0,  0, 14,  0,  0,  0, 14,  0, 14,  0,  0,  0, 14,  0,  0,  0,
						0,  0,  0, 14,  0, 14,  0,  0,  0, 14,  0, 14,  0,  0,  0,  0,
						0,  0,  0,  0, 14,  0, 14,  0, 14,  0, 14,  0,  0,  0,  0,  0,
						0, 14, 14, 14,  5,  5,  5,  7,  5,  5,  5, 14, 14, 14,  0,  0,
						0,  0, 14, 14,  5,  0,  0,  7,  6,  0,  0, 14, 14,  0,  0,  0,
						0,  0,  0, 14, 14,  0,  0,  7,  6,  0,  0, 14,  0,  0,  0,  0,
						0,  0,  0,  0, 14,  0,  0,  7,  6,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  7,  6,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  7,  6,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  7,  6,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
					);
			}

			return new Bytemap(16, 16).FromByteArray(
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 12,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 12,  0, 12,  0, 12,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 12, 12, 12,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 12, 12,  0, 12, 12,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
			);
		}

		// ── Tile file loader ─────────────────────────────────────────────────────
		// Reads named 16×16 tile sections from free_tiles.txt in the working directory.
		// Format:  [section_name]  followed by 16 rows of 16 space/comma-separated palette indices.
		// Lines starting with # are comments.  Missing sections fall back to hardcoded values.

		private static readonly string TilesFilePath =
			Path.Combine(Environment.CurrentDirectory, "free_tiles.txt");

		private static readonly string ShoresFilePath =
			Path.Combine(Environment.CurrentDirectory, "shore_tiles.txt");

		private static readonly string LakeShoresFilePath =
			Path.Combine(Environment.CurrentDirectory, "lake_shores.txt");

		private static readonly string RiverOverlaysFilePath =
			Path.Combine(Environment.CurrentDirectory, "river_overlays.txt");

		private static readonly string ImprovementsFilePath =
			Path.Combine(Environment.CurrentDirectory, "improvement_tiles.txt");

		private static readonly string DifficultiesFilePath =
			Path.Combine(Environment.CurrentDirectory, "difficulty_tiles.txt");

		private static readonly string AdvisorBadgesFilePath =
			Path.Combine(Environment.CurrentDirectory, "advisor_badges.txt");

		// Ministry badges shown beside advisor messages, and by Icons.Spy.
		public const int BadgeSize = 28;

		// The five New Game difficulty panels are 47x41, not 16x16 like the map tiles.
		public const int DifficultyWidth = 47;
		public const int DifficultyHeight = 41;

		private Dictionary<string, byte[]> _tileOverrides = null!;
		private Dictionary<string, byte[]> _shoreOverrides = null!;
		private Dictionary<string, byte[]> _lakeShoreOverrides = null!;
		private Dictionary<string, byte[]> _riverOverrides = null!;
		private Dictionary<string, byte[]> _improvementOverrides = null!;
		private Dictionary<string, byte[]> _difficultyOverrides = null!;
		private Dictionary<string, byte[]> _advisorBadges = null!;

		public void ReloadTiles()
		{
			_tileOverrides    = null!;
			_shoreOverrides   = null!;
			_lakeShoreOverrides = null!;
			_riverOverrides   = null!;
			_improvementOverrides = null!;
			_difficultyOverrides = null!;
			_advisorBadges = null!;
			// Drop the cached base fields so they regenerate. MapTile.ReloadTileCaches
			// disposes the sprites wrapping these very Bytemaps, so both must be nulled
			// here or Free would hand back freed (disposed) memory — an AccessViolation
			// the next time an ocean/land tile is drawn.
			_landBase = null!;
			_seaBase  = null!;
		}

		private byte[]? TryLoadTile(string name)
		{
			if (_tileOverrides is null)
				_tileOverrides = ParseTilesFile(TilesFilePath);
			return _tileOverrides.TryGetValue(name, out byte[] data) ? data : null;
		}

		// Per-name 16×16 override for map improvements (mine/fortress/hut and the
		// directional road_*/rail_* spokes) from improvement_tiles.txt. Null when the
		// file or section is absent, so MapTile falls back to its procedural draw.
		public byte[]? Improvement(string name)
		{
			if (_improvementOverrides is null)
				_improvementOverrides = ParseTilesFile(ImprovementsFilePath);
			return _improvementOverrides.TryGetValue(name, out byte[] data) ? data : null;
		}

		// Per-level 47x41 emblem for the New Game difficulty screen, from
		// difficulty_tiles.txt. Null when the file or section is absent, so
		// Difficulties falls back to the noise placeholder for that panel alone.
		public byte[]? DifficultyEmblem(string name)
		{
			if (_difficultyOverrides is null)
				_difficultyOverrides = ParseTilesFile(DifficultiesFilePath, DifficultyWidth * DifficultyHeight);
			return _difficultyOverrides.TryGetValue(name, out byte[] data) ? data : null;
		}

		// 28x28 ministry badge by name (defense/domestic/foreign/science/intelligence)
		// from advisor_badges.txt. Null when the file or section is missing, so callers
		// fall back to whatever they drew before.
		public Bytemap? AdvisorBadge(string name)
		{
			if (_advisorBadges is null)
				_advisorBadges = ParseTilesFile(AdvisorBadgesFilePath, BadgeSize * BadgeSize);
			return _advisorBadges.TryGetValue(name, out byte[] data)
				? new Bytemap(BadgeSize, BadgeSize).FromByteArray(data)
				: null;
		}

		private byte[]? TryLoadShore(string name)
		{
			if (_shoreOverrides is null)
				_shoreOverrides = ParseTilesFile(ShoresFilePath);
			return _shoreOverrides.TryGetValue(name, out byte[] data) ? data : null;
		}

		// Composite per-direction shore wave overlays from shore_tiles.txt.
		// Returns null when the file or required sections are absent (falls back to CoastLayer).
		public Bytemap? ShoreLayer(Direction land)
		{
			if (_shoreOverrides is null)
				_shoreOverrides = ParseTilesFile(ShoresFilePath);
			if (_shoreOverrides.Count == 0)
				return null;

			Bytemap output = new Bytemap(16, 16);
			bool any = false;

			foreach (var pair in ((Direction, string)[])
			[
				(North,     "shore_N"),
				(South,     "shore_S"),
				(East,      "shore_E"),
				(West,      "shore_W"),
			])
			{
				if (!land.And(pair.Item1)) continue;
				byte[]? tile = TryLoadShore(pair.Item2);
				if (tile is null) continue;
				output.AddLayer(new Bytemap(16, 16).FromByteArray(tile));
				any = true;
			}

			// Diagonal-only corner patches
			foreach (var pair in ((Direction, Direction, Direction, string)[])
			[
				(NorthWest, North, West, "shore_NW"),
				(NorthEast, North, East, "shore_NE"),
				(SouthWest, South, West, "shore_SW"),
				(SouthEast, South, East, "shore_SE"),
			])
			{
				if (land.And(pair.Item1) && land.Not(pair.Item2) && land.Not(pair.Item3))
				{
					byte[]? tile = TryLoadShore(pair.Item4);
					if (tile is null) continue;
					output.AddLayer(new Bytemap(16, 16).FromByteArray(tile));
					any = true;
				}
			}

			return any ? output : null;
		}

		private byte[]? TryLoadLakeShore(string name)
		{
			if (_lakeShoreOverrides is null)
				_lakeShoreOverrides = ParseTilesFile(LakeShoresFilePath);
			return _lakeShoreOverrides.TryGetValue(name, out byte[] data) ? data : null;
		}

		// Composite per-direction shore overlays for lake tiles from lake_shores.txt.
		// Falls back to the same foam/sand pattern as CoastLayer when the file is absent.
		public Bytemap? LakeShoreLayer(Direction land)
		{
			if (_lakeShoreOverrides is null)
				_lakeShoreOverrides = ParseTilesFile(LakeShoresFilePath);

			bool hasFile = _lakeShoreOverrides.Count > 0;
			Bytemap output = new Bytemap(16, 16);
			bool any = false;

			if (hasFile)
			{
				foreach (var pair in ((Direction, string)[])
				[
					(North, "lake_N"),
					(South, "lake_S"),
					(East,  "lake_E"),
					(West,  "lake_W"),
				])
				{
					if (!land.And(pair.Item1)) continue;
					byte[]? tile = TryLoadLakeShore(pair.Item2);
					if (tile is null) continue;
					output.AddLayer(new Bytemap(16, 16).FromByteArray(tile));
					any = true;
				}

				foreach (var pair in ((Direction, Direction, Direction, string)[])
				[
					(NorthWest, North, West, "lake_NW"),
					(NorthEast, North, East, "lake_NE"),
					(SouthWest, South, West, "lake_SW"),
					(SouthEast, South, East, "lake_SE"),
				])
				{
					if (land.And(pair.Item1) && land.Not(pair.Item2) && land.Not(pair.Item3))
					{
						byte[]? tile = TryLoadLakeShore(pair.Item4);
						if (tile is null) continue;
						output.AddLayer(new Bytemap(16, 16).FromByteArray(tile));
						any = true;
					}
				}

				return any ? output : null;
			}

			// Fallback: same two-pixel foam/sand strip as CoastLayer
			const byte foam = 8;
			const byte sand = 7;
			bool[] wH = { true, false, false, true, false, true, false, false,
			              true, false, false, true, true, false, false, true };
			bool[] wV = { false, true, false, false, true, true, false, true,
			              false, false, true, false, true, false, false, true };

			if (land.And(North))
				for (int x = 0; x < 16; x++) { output[x, 0] = foam; if (wH[x]) output[x, 1] = sand; any = true; }
			if (land.And(South))
				for (int x = 0; x < 16; x++) { output[x, 15] = foam; if (wH[x]) output[x, 14] = sand; any = true; }
			if (land.And(East))
				for (int y = 0; y < 16; y++) { output[15, y] = foam; if (wV[y]) output[14, y] = sand; any = true; }
			if (land.And(West))
				for (int y = 0; y < 16; y++) { output[0, y] = foam; if (wV[y]) output[1, y] = sand; any = true; }

			if (land.And(NorthWest) && land.Not(North | West)) { output[0, 0] = foam; output[1, 0] = sand; output[0, 1] = sand; any = true; }
			if (land.And(NorthEast) && land.Not(North | East)) { output[15, 0] = foam; output[14, 0] = sand; output[15, 1] = sand; any = true; }
			if (land.And(SouthWest) && land.Not(South | West)) { output[0, 15] = foam; output[1, 15] = sand; output[0, 14] = sand; any = true; }
			if (land.And(SouthEast) && land.Not(South | East)) { output[15, 15] = foam; output[14, 15] = sand; output[15, 14] = sand; any = true; }

			return any ? output : null;
		}

		private Dictionary<string, byte[]> ParseTilesFile(string path, int expected = 256)
		{
			var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(path))
				return result;

			string? currentSection = null;
			var pixels = new List<byte>();

			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;

				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					if (currentSection is not null && pixels.Count == expected)
						result[currentSection] = pixels.ToArray();
					currentSection = line.Substring(1, line.Length - 2);
					pixels = new List<byte>();
				}
				else if (currentSection is not null)
				{
					foreach (string tok in line.Split([' ', '\t', ','],
						StringSplitOptions.RemoveEmptyEntries))
					{
						if (byte.TryParse(tok, out byte v))
							pixels.Add(v);
					}
				}
			}
			if (currentSection is not null && pixels.Count == expected)
				result[currentSection] = pixels.ToArray();

			return result;
		}

		// Hills: single SW-NE ridge, 2px lit (OK/14) + 3px transparent hilltop + 2px shadow (INK_LOW/6)
		// Standalone: ridge centered in tile (rows 1–12).
		// Connected (directions != None): ridge extends to tile edges (rows 0–15) for visual continuity.
		public Bytemap HillTexture(Direction directions)
		{
			string section = directions == None ? "hill_standalone" : "hill_connected";
			byte[]? loaded = TryLoadTile(section);
			if (loaded is not null)
				return new Bytemap(16, 16).FromByteArray(loaded);

			if (directions == None)
			{
				return new Bytemap(16, 16).FromByteArray(
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 14,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  0,  0,
					 0,  0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,
					 0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,
					 0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,
					 0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,
					 0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,
					 0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  0,
					 0,  0, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
				);
			}

			// Connected: same ridge extended to tile edges so adjacent hill tiles flow together
			return new Bytemap(16, 16).FromByteArray(
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 14,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  0,  0,
				 0,  0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,
				 0,  0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,
				 0,  0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,
				 0,  0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,
				 0,  0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,
				 0,  0, 14, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  0,
				 0,  0, 14,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
			);
		}

		// Plains: sparse light speckles (INK_HIGH/8) over transparent — LandBase shows through as warm base.
		// Override with a [plains] section (make it opaque to fully replace the field, or keep 0s to speckle
		// over LandBase like the default below).
		public Bytemap PlainsTexture()
		{
			byte[]? loaded = TryLoadTile("plains");
			if (loaded is not null)
				return new Bytemap(16, 16).FromByteArray(loaded);

			return new Bytemap(16, 16).FromByteArray(
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  8,  0,  0,  0,  0,  0,  0,  0,  0,  8,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  8,  0,  0,  0,  0,  0,  0,  8,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  8,  0,  0,  0,  0,  0,  0,  0,  8,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  8,  0,  0,  0,  0,  0,  0,  0,  8,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  8,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  8,  0,  0,  0,  0,  0,  0,  8,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  8,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
			);
		}

		// Grassland: transparent (LandBase shows through) with scattered dark speckles (INK_LOW/6).
		// Override with a [grassland] section (opaque to fully replace the field, or keep 0s to speckle).
		public Bytemap GrasslandTexture()
		{
			byte[]? loaded = TryLoadTile("grassland");
			if (loaded is not null)
				return new Bytemap(16, 16).FromByteArray(loaded);

			return new Bytemap(16, 16).FromByteArray(
				 0,  0,  6,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  6,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,
				 6,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  6,
				 0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,
				 6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,
				 0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,
				 0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  6,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  6,
				 0,  0,  0,  0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0,  0,  0,
				 0,  0,  6,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  6,  0,  0
			);
		}

		// Hay bale: golden cylinder, PHOS_GLOW top / PHOS body / PHOS_DIM sides / INK_LOW shadow
		public Bytemap HayBale()
		{
			// Honour [special_grassland] in free_tiles.txt as the override, matching
			// the pattern used by Irrigation/Desert/Forest/etc. Without this check,
			// the user's hand-edited cassette-palette shield silhouette would never
			// be read and the hardcoded SP257-style bright amber blob below would
			// always win.
			byte[]? loaded = TryLoadTile("special_grassland");
			if (loaded is not null)
				return new Bytemap(16, 16).FromByteArray(loaded);

			return new Bytemap(16, 16).FromByteArray(
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0, 13, 13, 13, 13, 13, 13,  0,  0,  0,  0,  0,
				 0,  0,  0,  0, 12, 12, 12, 12, 12, 12, 12,  6,  0,  0,  0,  0,
				 0,  0,  0,  0, 12, 11,  7, 11,  7, 11, 11,  6,  0,  0,  0,  0,
				 0,  0,  0,  0, 12, 11, 11, 11, 11, 11, 11,  6,  0,  0,  0,  0,
				 0,  0,  0,  0, 12,  6,  6,  6,  6,  6,  6,  6,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  5,  5,  5,  5,  5,  5,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
			);
		}

		// Irrigation overlay: channels (CYAN/14) + 2×2 soil patches (BORDER/5, INK_LOW/6) in each field cell
		public Bytemap Irrigation()
		{
			byte[]? loaded = TryLoadTile("irrigation");
			if (loaded is not null)
				return new Bytemap(16, 16).FromByteArray(loaded);

			return new Bytemap(16, 16).FromByteArray(
				 0,  0,  0,  0,  0,  0,  0, 17,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  6,  6,  0,  0,  0,  0,
				 0,  0,  5,  5,  0,  0,  0, 17,  0,  0,  6,  6,  0,  0,  0,  0,
				14,  0, 14,  0, 14,  0, 14, 14, 14,  0, 14,  0, 14,  0, 14,  0,
				 0,  0,  6,  6,  0,  0,  0, 17,  0,  0,  5,  5,  0,  0,  0,  0,
				 0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0, 17,  0,  0,  0,  0,  0,  0,  0,  0,
				17,  0, 17,  0, 17,  0, 17, 17, 17,  0, 17,  0, 17,  0, 17,  0,
				 0,  0,  0,  0,  0,  0,  0, 17,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  6,  6,  0,  0,  0,  0,
				 0,  0,  5,  5,  0,  0,  0, 17,  0,  0,  6,  6,  0,  0,  0,  0,
				14,  0, 14,  0, 14,  0, 14, 14, 14,  0, 14,  0, 14,  0, 14,  0,
				 0,  0,  6,  6,  0,  0,  0, 17,  0,  0,  5,  5,  0,  0,  0,  0,
				 0,  0,  6,  6,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0, 17,  0,  0,  0,  0,  0,  0,  0,  0,
				 0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0
			);
		}

		// Coastline strip painted on ocean tiles that border land.
		// If shore_tiles.txt is present, uses animated wave indices (96-99) from ShoreLayer.
		// Otherwise falls back to the static two-pixel foam/sand pattern below.
		public Bytemap CoastLayer(Direction land)
		{
			Bytemap? shore = ShoreLayer(land);
			if (shore is not null) return shore;

			const byte foam = 8;  // INK_HIGH — surf/foam
			const byte sand = 7;  // INK_MID  — wet sand / shallows

			bool[] wH = { true, false, false, true, false, true, false, false,
			              true, false, false, true, true, false, false, true };
			bool[] wV = { false, true, false, false, true, true, false, true,
			              false, false, true, false, true, false, false, true };

			Bytemap output = new Bytemap(16, 16);

			if (land.And(North))
				for (int x = 0; x < 16; x++) { output[x, 0] = foam; if (wH[x]) output[x, 1] = sand; }

			if (land.And(South))
				for (int x = 0; x < 16; x++) { output[x, 15] = foam; if (wH[x]) output[x, 14] = sand; }

			if (land.And(East))
				for (int y = 0; y < 16; y++) { output[15, y] = foam; if (wV[y]) output[14, y] = sand; }

			if (land.And(West))
				for (int y = 0; y < 16; y++) { output[0, y] = foam; if (wV[y]) output[1, y] = sand; }

			// Isolated diagonal corners — small sand patch where no cardinal edge was drawn
			if (land.And(NorthWest) && land.Not(North | West)) { output[0, 0] = foam; output[1, 0] = sand; output[0, 1] = sand; }
			if (land.And(NorthEast) && land.Not(North | East)) { output[15, 0] = foam; output[14, 0] = sand; output[15, 1] = sand; }
			if (land.And(SouthWest) && land.Not(South | West)) { output[0, 15] = foam; output[1, 15] = sand; output[0, 14] = sand; }
			if (land.And(SouthEast) && land.Not(South | East)) { output[15, 15] = foam; output[14, 15] = sand; output[15, 14] = sand; }

			// Corner fill — water squeezed to a point (both cardinals + diagonal all land)
			// The cardinal strips already draw foam on the two edges; add a sand pixel one step inward
			if (land.And(North) && land.And(West)  && land.And(NorthWest)) { output[1, 1] = sand; }
			if (land.And(North) && land.And(East)  && land.And(NorthEast)) { output[14, 1] = sand; }
			if (land.And(South) && land.And(West)  && land.And(SouthWest)) { output[1, 14] = sand; }
			if (land.And(South) && land.And(East)  && land.And(SouthEast)) { output[14, 14] = sand; }

			return output;
		}

		// ── River overlays (river_overlays.txt) ────────────────────────────────
		// Named 16×16 sections in the free_tiles.txt format replace the legacy
		// 1991 river sprites. The connection piece is picked by the cardinal
		// river mask (letters in N,E,S,W order; empty mask = river_isolated);
		// straights and bends ship two cuts (_a/_b) chosen by the caller's
		// stable coordinate hash; rivermouth_<dir> deltas composite on top
		// wherever a cardinal neighbour is sea. Indices 96–99 sit on the wave-
		// cycling palette range, so rivers shimmer on the shore-surf cycle.

		private Dictionary<string, byte[]> RiverOverrides =>
			_riverOverrides ??= ParseTilesFile(RiverOverlaysFilePath);

		// When false (file absent/empty), callers fall back to the legacy
		// sprite-sheet rivers and the TER257 sea-side mouths.
		public bool HasRiverOverlays => RiverOverrides.Count > 0;

		public Bytemap? RiverOverlay(Direction rivers, Direction seaMouths, int variant)
		{
			if (!HasRiverOverlays) return null;

			string name = "river_";
			if (rivers.And(North)) name += "N";
			if (rivers.And(East))  name += "E";
			if (rivers.And(South)) name += "S";
			if (rivers.And(West))  name += "W";
			if (name == "river_") name = "river_isolated";

			var candidates = new List<string>(3);
			if (RiverOverrides.ContainsKey(name)) candidates.Add(name);
			if (RiverOverrides.ContainsKey(name + "_a")) candidates.Add(name + "_a");
			if (RiverOverrides.ContainsKey(name + "_b")) candidates.Add(name + "_b");
			if (candidates.Count == 0) return null; // section missing — legacy fallback

			Bytemap output = new Bytemap(16, 16)
				.FromByteArray(RiverOverrides[candidates[variant % candidates.Count]]);

			foreach (var (dir, mouth) in ((Direction, string)[])
			[
				(North, "rivermouth_N"),
				(East,  "rivermouth_E"),
				(South, "rivermouth_S"),
				(West,  "rivermouth_W"),
			])
			{
				if (!seaMouths.And(dir)) continue;
				if (!RiverOverrides.TryGetValue(mouth, out byte[] delta)) continue;
				output.AddLayer(new Bytemap(16, 16).FromByteArray(delta));
			}

			return output;
		}

		public Bytemap River(Direction directions)
		{
			Picture output = new Picture(16, 16);
			foreach (Direction direction in (Direction[])[North, East, South, West])
			{
				switch ((Direction)(directions & direction))
				{
					case North:
						output.DrawLine(6, -1, 8, 5, 77)
							.DrawLine(7, -1, 9, 5, 78)
							.DrawLine(8, 4, 7, 8, 79)
							.DrawLine(9, 4, 8, 8, 77);
						break;
					case South:
						output.DrawLine(7, 7, 5, 12, 77)
							.DrawLine(8, 7, 6, 12, 78)
							.DrawLine(5, 11, 6, 16, 79)
							.DrawLine(6, 11, 7, 16, 77);
						break;
					case West:
						output.DrawLine(0, 6, 5, 8, 79)
							.DrawLine(0, 7, 5, 9, 77)
							.DrawLine(4, 8, 8, 7, 78)
							.DrawLine(4, 9, 8, 8, 79);
						break;
					case East:
						output.DrawLine(7, 7, 12, 5, 78)
							.DrawLine(7, 8, 12, 6, 79)
							.DrawLine(11, 5, 16, 6, 77)
							.DrawLine(11, 6, 16, 7, 78);
						break;
				}
			}
			return output.Bitmap;
		}

		public Bytemap City
		{
			get
			{
				if (_city is null)
				{
					Random r = new Random(0x4701);
					_city = new Picture(16, 16)
						.DrawLine(7, 3, 11, 3)
						.DrawLine(4, 5, 9, 5)
						.DrawLine(3, 7, 11, 7)
						.DrawLine(5, 9, 9, 9)
						.DrawLine(3, 11, 6, 11)
						.DrawLine(3, 6, 3, 8)
						.DrawLine(7, 3, 7, 11)
						.DrawLine(11, 5, 11, 11).Bitmap;
				}
				return _city;
			}
		}

		public Bytemap Fortify
		{
			get
			{
				if (_fortify is null)
				{
					_fortify = new Bytemap(16, 16)
						.FromByteArray(GenerateNoise(26, 27, 28).Take(16 * 16).ToArray())
						.AddLayer(new Bytemap(14, 14).FromByteArray(GenerateNoise(24, 25, 26).Take(14 * 14).ToArray()))
						.FillRectangle(2, 2, 12, 12, 0);
				}
				return _fortify;
			}
		}

		public Bytemap Fog(Direction direction)
		{
			Bytemap output = new Bytemap(16, 16);
			switch(direction)
			{
				case Direction.West:
					output.AddLayer(new Bytemap(3, 16).FromByteArray(GenerateNoise(0, 28, 29, 30, 31).Take(3 * 16).ToArray()), 0, 0);
					break;
				case Direction.South:
					output.AddLayer(new Bytemap(16, 3).FromByteArray(GenerateNoise(28, 0, 29, 30, 31).Take(16 * 3).ToArray()), 0, 13);
					break;
				case Direction.East:
					output.AddLayer(new Bytemap(3, 16).FromByteArray(GenerateNoise(28, 29, 0, 30, 31).Take(3 * 16).ToArray()), 13, 0);
					break;
				case Direction.North:
					output.AddLayer(new Bytemap(16, 3).FromByteArray(GenerateNoise(28, 29, 30, 0, 31).Take(16 * 3).ToArray()), 0, 0);
					break;
			}
			return output;
		}

		public Bytemap GetUnit(UnitType type)
		{
			Bytemap output = new Bytemap(16, 16).FromByteArray(GenerateUnit().ToArray());
			char text = ' ';
			switch (type)
			{
				case UnitType.Settlers:
					output.AddLayer(new Bytemap(10, 10).FromByteArray(
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  5,  5,  5,  5,  5,  5,  5,  0,  0,
						5, 15, 15,  8, 15, 15,  8,  7,  5,  0,
						5, 15, 15,  8, 15, 15,  8,  7,  5,  0,
						5, 15, 15, 15, 15, 15,  8,  7,  5,  0,
						0,  5,  6,  6, 15, 15,  6,  6,  5,  0,
						0,  6,  8,  0,  6,  6,  8,  0,  6,  0,
						0,  6,  0,  0,  6,  6,  0,  0,  6,  0,
						0,  0,  6,  6,  0,  0,  6,  6,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0
					), 3, 3);
					break;
				case UnitType.Militia:
					output.AddLayer(new Bytemap(10, 10).FromByteArray(
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0,  4,  4,  4,  0,  0,  0,  0,
						0,  0,  4,  4,  4,  4,  4,  0,  0,  0,
						0,  0,  4, 14, 14, 14,  4,  0,  7,  0,
						0,  0, 14, 14, 14, 14, 14,  0,  7,  7,
						0,  0, 14,  9, 14,  9, 14,  0,  7,  7,
						0,  0, 14, 14, 14, 14, 14,  0,  0,  7,
						0,  0, 14,  8,  8,  8, 14,  0,  6,  6,
						0,  0,  5, 14, 14, 14,  5,  0,  5,  5,
						0,  5,  5,  5,  5,  5,  5,  5,  5,  0
					), 3, 3);
					break;
				case UnitType.Phalanx:
					output.AddLayer(new Bytemap(10, 10).FromByteArray(
						0,  0,  0,  0,  8,  8,  0,  0,  0,  0,
						0,  0,  0,  8,  8,  8,  8,  0,  0,  0,
						0,  0,  8, 14, 14, 14,  7,  8,  0,  0,
						0,  0,  5,  5,  5, 14,  7,  7,  0,  0,
						0,  5, 15,  8,  8,  5,  7,  6,  0,  0,
						0,  5, 15,  7,  8,  5,  6,  0,  0,  0,
						0,  5, 15,  7,  8,  5,  6,  0,  0,  0,
						0,  5, 15,  7,  8,  5,  6,  0,  0,  0,
						0,  0,  5, 15,  5,  0,  6,  0,  0,  0,
						0,  0,  0,  5,  0,  0,  0,  0,  0,  0
					), 3, 3);
					break;
				case UnitType.Legion: text = 'L'; break;
				case UnitType.Musketeers: text = 'M'; break;
				case UnitType.Riflemen: text = 'R'; break;
				case UnitType.Cavalry: text = 'c'; break;
				case UnitType.Knights: text = 'K'; break;
				case UnitType.Catapult: text = 'C'; break;
				case UnitType.Cannon: text = 'X'; break;
				case UnitType.Chariot: text = 'W'; break;
				case UnitType.Armor: text = 'a'; break;
				case UnitType.MechInf: text = 'I'; break;
				case UnitType.Artillery: text = 'A'; break;
				case UnitType.Fighter: text = 'F'; break;
				case UnitType.Bomber: text = 'B'; break;
				case UnitType.Trireme: text = 'T'; break;
				case UnitType.Sail: text = 's'; break;
				case UnitType.Frigate: text = 'f'; break;
				case UnitType.Ironclad: text = 'i'; break;
				case UnitType.Cruiser: text = 'Y'; break;
				case UnitType.Battleship: text = 'Z'; break;
				case UnitType.Submarine: text = 'U'; break;
				case UnitType.Carrier: text = 'G'; break;
				case UnitType.Transport: text = 'H'; break;
				case UnitType.Nuclear: text = 'N'; break;
				case UnitType.Diplomat: text = 'D'; break;
				case UnitType.Caravan: text = 't'; break;
				case UnitType.Explorer: text = 'e'; break;
				case UnitType.HydroEngineer:
					output.AddLayer(new Bytemap(10, 10).FromByteArray(
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
						0,  0,  0, 11, 11, 11, 11,  0,  0,  0,
						0,  0, 11, 15, 13, 13, 15, 11,  0,  0,
						0, 11, 15, 15, 11, 11, 15, 15, 11,  0,
						0, 11, 15, 15, 13, 13, 15, 15, 11,  0,
						0,  0,  5,  5,  5,  5,  5,  5,  0,  0,
						0,  5,  5,  8,  8,  8,  8,  5,  5,  0,
						0,  5,  8, 11,  8,  8, 11,  8,  5,  0,
						0,  0,  5,  5,  5,  5,  5,  5,  0,  0,
						0,  0,  0,  0,  0,  0,  0,  0,  0,  0
					), 3, 3);
					break;
			}
			if (text != ' ')
			{
				output.AddLayer(
					new Picture(16, 16)
						.DrawText(text.ToString(), 0, 8, 8, 5, TextAlign.Center)
						.DrawText(text.ToString(), 0, 7, 8, 4, TextAlign.Center)
						.Bitmap);
			}
			return output;
		}

		public Bytemap Food
		{
			get
			{
				return new Bytemap(8, 8).FromByteArray(
					0,  0,  0,  0,  0,  5,  5,  0,
					0,  5,  5,  0,  5, 14, 14,  5,
					5, 11, 11,  5, 14, 14, 14,  5,
					5, 11, 11, 14, 14, 14,  5,  0,
					5, 11, 11, 14, 14,  5,  0,  0,
					5, 11, 11, 11, 11, 11,  5,  0,
					5, 11, 11, 11, 11, 11,  5,  0,
					0,  5,  5,  5,  5,  5,  0,  0
				);
			}
		}

		public Bytemap Shield
		{
			get
			{
				return new Bytemap(8, 8).FromByteArray(
					5,  5,  5,  5,  5,  5,  5,  5,
					5, 15,  7,  7,  8,  8,  8,  5,
					5, 15,  7,  7,  7,  8,  8,  5,
					5, 15,  7,  7,  7,  8,  8,  5,
					5, 15,  7,  7,  7,  8,  8,  5,
					0,  5, 15,  7,  8,  8,  5,  0,
					0,  0,  5, 15,  8,  5,  0,  0,
					0,  0,  0,  5,  5,  0,  0,  0
				);
			}
		}

		public Bytemap Trade
		{
			get
			{
				return new Bytemap(8, 8).FromByteArray(
					0,  0,  5, 12,  0,  0,  0,  0,
					0,  5, 12, 12,  5,  5,  5,  0,
					5, 12, 12, 12, 12, 12, 12,  0,
					0,  0, 12, 12, 10,  5,  0,  0,
					0,  0,  0, 12, 10, 10,  5,  0,
					0, 10, 10, 10, 10, 10, 10,  5,
					0,  0,  0,  0, 10, 10,  5,  0,
					0,  0,  0,  0, 10,  5,  0,  0
				);
			}
		}

		public Bytemap Luxuries
		{
			get
			{
				return new Bytemap(8, 8).FromByteArray(
					0,  0,  5,  5,  5,  5,  0,  0,
					0,  5, 15, 15, 15, 11,  5,  0,
					5, 15, 15, 15, 15, 15, 11,  5,
					0,  5, 15, 15, 15, 11,  5,  0,
					0,  5, 15, 15, 15, 11,  5,  0,
					0,  0,  5, 15, 11,  5,  0,  0,
					0,  0,  5, 15, 11,  5,  0,  0,
					0,  0,  0,  5,  5,  0,  0,  0
				);
			}
		}

		public Bytemap Taxes
		{
			get
			{
				return new Bytemap(8, 8).FromByteArray(
					0,  0,  5,  5,  5,  5,  0,  0,
					0,  5, 14, 14, 14, 15,  5,  0,
					5, 14, 14, 14, 14, 14, 15,  5,
					5, 14, 14, 14, 14, 14, 15,  5,
					5, 14, 14, 14, 14, 14, 15,  5,
					5, 14, 14, 14, 14, 14, 15,  5,
					0,  5, 14, 14, 14, 15,  5,  0,
					0,  0,  5,  5,  5,  5,  0,  0
				);
			}
		}

		public Bytemap Science
		{
			get
			{
				return new Bytemap(8, 8).FromByteArray(
					0,  0,  0,  5,  7,  0,  0,  0,
					0,  0,  0,  5,  7,  0,  0,  0,
					0,  0,  0,  5,  7,  0,  0,  0,
					0,  0,  0,  5,  7,  0,  0,  0,
					0,  0,  5, 15,  7,  5,  0,  0,
					0,  5, 11, 11, 11, 11,  5,  0,
					5, 11, 11, 11, 11, 11, 11,  5,
					0,  5,  5,  5,  5,  5,  5,  0
				);
			}
		}

		// Unhappy citizen mood icon — red frowning face (ALERT/16 on a dark outline).
		public Bytemap Unhappy
		{
			get
			{
				return new Bytemap(8, 8).FromByteArray(
					0,  5,  5,  5,  5,  5,  5,  0,
					5, 16, 16, 16, 16, 16, 16,  5,
					5, 16,  1, 16, 16,  1, 16,  5,
					5, 16, 16, 16, 16, 16, 16,  5,
					5, 16, 16,  1,  1, 16, 16,  5,
					5, 16,  1, 16, 16,  1, 16,  5,
					0,  5, 16, 16, 16, 16,  5,  0,
					0,  0,  5,  5,  5,  5,  0,  0
				);
			}
		}

		// City-population citizen — a plain figure (8×16). The body pixels are index
		// 15 so Icons can recolour them per citizen type via ColourReplace.
		public Bytemap Citizen
		{
			get
			{
				return new Bytemap(8, 16).FromByteArray(
					0,  0,  0,  5,  5,  0,  0,  0,
					0,  0,  5,  7,  7,  5,  0,  0,
					0,  0,  5,  7,  7,  5,  0,  0,
					0,  0,  0,  5,  5,  0,  0,  0,
					0,  0,  5, 15, 15,  5,  0,  0,
					0,  5, 15, 15, 15, 15,  5,  0,
					5, 15, 15, 15, 15, 15, 15,  5,
					5, 15, 15, 15, 15, 15, 15,  5,
					5, 15, 15, 15, 15, 15, 15,  5,
					0,  5, 15, 15, 15, 15,  5,  0,
					0,  0,  5, 15, 15,  5,  0,  0,
					0,  0,  5, 15, 15,  5,  0,  0,
					0,  0,  5, 15, 15,  5,  0,  0,
					0,  0,  5,  5,  5,  5,  0,  0,
					0,  0,  5,  0,  0,  5,  0,  0,
					0,  0,  5,  0,  0,  5,  0,  0
				);
			}
		}

		// Research "lamp" fill indicator; brightness climbs with stage 0→3.
		public Bytemap Lamp(int stage)
		{
			byte glow = stage switch { 0 => (byte)9, 1 => (byte)11, 2 => (byte)12, _ => (byte)13 };
			return new Bytemap(8, 8).FromByteArray(
				0,  0,  5,  5,  5,  0,  0,  0,
				0,  5, glow, glow, glow,  5,  0,  0,
				5, glow, glow, glow, glow, glow,  5,  0,
				5, glow, glow, glow, glow, glow,  5,  0,
				0,  5, glow, glow, glow,  5,  0,  0,
				0,  0,  5, glow,  5,  0,  0,  0,
				0,  0,  5,  5,  5,  0,  0,  0,
				0,  0,  0,  5,  0,  0,  0,  0
			);
		}

		// Procedural nuclear blast frame (44×44) for Free mode — an expanding
		// fireball: glow core, amber body, alert-red rim. frame 0→27 grows the radius.
		public Bytemap NukeBlast(int frame)
		{
			Bytemap o = new Bytemap(44, 44);
			int cx = 22, cy = 22;
			int r = System.Math.Min(22, 4 + frame);
			for (int y = 0; y < 44; y++)
			for (int x = 0; x < 44; x++)
			{
				double d = System.Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
				if (d > r) continue;
				o[x, y] = d < r * 0.4 ? (byte)13 : d < r * 0.7 ? (byte)12 : (byte)16;
			}
			return o;
		}

		// Full-screen themed backdrop for Free mode — a dark cassette panel with a
		// border, standing in for the original set-piece art (Conquest/GameOver/…).
		public Bytemap Backdrop(int width, int height)
		{
			const byte fill = 2, edge = 5;   // BG1 panel fill / BORDER
			Bytemap o = new Bytemap(width, height);
			o.FillRectangle(0, 0, width, height, fill);
			o.FillRectangle(0, 0, width, 2, edge)
				.FillRectangle(0, height - 2, width, 2, edge)
				.FillRectangle(0, 0, 2, height, edge)
				.FillRectangle(width - 2, 0, 2, height, edge);
			return o;
		}

		// Civilopedia terrain illustration for Free mode: the terrain's own Free
		// texture tiled across a width×height panel (base field + overlay).
		public Bytemap TerrainThumbnail(Terrain type, int width, int height)
		{
			Bytemap baseField = (type == Terrain.Ocean) ? OceanBase : LandBase;
			Bytemap? overlay = type switch
			{
				Terrain.Arctic => Arctic,
				Terrain.Tundra => Tundra,
				Terrain.Desert => Desert,
				Terrain.Forest => Forest,
				Terrain.Mountains => Mountains,
				Terrain.Jungle => Jungle,
				Terrain.Swamp => Swamp,
				Terrain.Grassland1 or Terrain.Grassland2 => GrasslandTexture(),
				Terrain.Plains => PlainsTexture(),
				Terrain.Hills => HillTexture(Direction.None),
				_ => null,   // Ocean / River: base field only
			};

			Bytemap o = new Bytemap(width, height);
			for (int y = 0; y < height; y += 16)
			for (int x = 0; x < width; x += 16)
			{
				o.AddLayer(baseField, x, y);
				if (overlay is not null) o.AddLayer(overlay, x, y);
			}
			return o;
		}

		// Generic building icon placeholder (50×50) for Free mode — a stylised
		// structure with a peaked roof, door and lit windows. One glyph for every
		// improvement; the building name accompanies it in the UI.
		public Bytemap BuildingIcon()
		{
			const byte wall = 7, roof = 5, door = 1, lit = 12, ground = 6;
			Bytemap o = new Bytemap(50, 50);
			o.FillRectangle(4, 44, 42, 3, ground);   // ground line
			o.FillRectangle(10, 18, 30, 26, wall);   // walls
			o.FillRectangle(8, 13, 34, 5, roof);     // eaves
			o.FillRectangle(15, 8, 20, 5, roof);     // roof peak
			o.FillRectangle(21, 33, 8, 11, door);    // doorway
			foreach (int wx in (int[])[13, 31])
			foreach (int wy in (int[])[22, 33])
				o.FillRectangle(wx, wy, 6, 6, lit);   // lit windows
			return o;
		}

		// Small building icon placeholder (20×10) — a two-tone rooflet.
		public Bytemap BuildingIconSmall()
		{
			const byte wall = 7, roof = 5;
			Bytemap o = new Bytemap(20, 10);
			o.FillRectangle(2, 4, 16, 5, wall);
			o.FillRectangle(1, 2, 18, 2, roof);
			o.FillRectangle(6, 0, 8, 2, roof);
			return o;
		}

		// Leader portrait placeholder — a bust silhouette on a panel, sized to fit.
		public Bytemap Portrait(int width, int height)
		{
			const byte panel = 3, edge = 5, body = 7, head = 8;
			Bytemap o = new Bytemap(width, height);
			o.FillRectangle(0, 0, width, height, panel);
			o.FillRectangle(0, 0, width, 1, edge)
				.FillRectangle(0, height - 1, width, 1, edge)
				.FillRectangle(0, 0, 1, height, edge)
				.FillRectangle(width - 1, 0, 1, height, edge);
			int cx = width / 2;
			int hr = System.Math.Max(3, width / 5);          // head radius-ish
			o.FillRectangle(cx - hr, height / 6, hr * 2, hr * 2, head);          // head
			int sw = (int)(width * 0.7);                       // shoulders
			o.FillRectangle(cx - sw / 2, height / 6 + hr * 2 + 2, sw, height, body);
			return o;
		}

		public Bytemap Difficulties
		{
			get
			{
				Bytemap output = new Bytemap(320, 200);
				
				// Wider than the original DIFFS.PIC panel (155,29,131,137): the tribe
				// picker needs two columns, and 24 civilizations do not fit one column
				// on a 200px screen at any row height the 7px glyphs survive. Taller too,
				// so the 15-entry competition list gets full 8px rows instead of cramped
				// 7px ones that let each row's glyphs bleed into the row below.
				DiffPanel(ref output, 143, 29, 150, 156);
				
				(int Skip, byte[] Colours)[] backgrounds =
				[
					(139, [23, 24, 25, 26, 27, 1]),
					(2075, [24, 25, 26, 27, 28, 2]),
					(4085, [25, 26, 27, 28, 29, 3]),
					(6750, [26, 27, 28, 29, 30, 6]),
					(8412, [27, 28, 29, 30, 31, 4])
				];
				string[] levels = ["chieftain", "warlord", "prince", "king", "emperor"];
				for (int i = 0; i < 5; i++)
				{
					int xx = (i % 2) == 0 ? 21 : 80;
					int yy = 6 + (35 * i);
					DiffPanel(ref output, xx, yy, 53, 47);
					byte[]? emblem = DifficultyEmblem(levels[i]);
					Bytemap panel = emblem is not null
						? new Bytemap(DifficultyWidth, DifficultyHeight).FromByteArray(emblem)
						: new Bytemap(47, 41).FromByteArray(GenerateNoise(backgrounds[i].Colours).Skip(backgrounds[i].Skip).Take(47 * 41).ToArray());
					output.AddLayer(panel, xx + 3, yy + 3);
				}

				return output;
			}
		}

		private static Free _instance = null!;
		public static Free Instance
		{
			get
			{
				if (_instance is null)
					_instance = new Free();
				return _instance;
			}
		}

		private Free()
		{
		}
	}
}