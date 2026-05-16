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
		private Bytemap _panelGrey, _panelBlue;
		private Bytemap _landBase, _seaBase, _city, _fortify;
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
			byte[] colours = [42, 41, 47, 15];
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

		public Bytemap LandBase
		{
			get
			{
				if (_landBase is null)
				{
					_landBase = new Bytemap(16, 16).FromByteArray(GenerateNoise(37, 38, 39).Take(16 * 16).ToArray());
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
			byte[] loaded = TryLoadTile("lakes");
			if (loaded is not null)
				return new Bytemap(16, 16).FromByteArray(loaded);
			// Fallback: CYAN(17) primary with sparse deep-blue(18) specks — visually lighter than ocean.
			return new Bytemap(16, 16).FromByteArray(GenerateNoise(17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 17, 18).Take(16 * 16).ToArray());
		}

		public Bytemap Plains => new Bytemap(16, 16).FromByteArray(GenerateNoise(0, 0, 0, 47, 0, 0, 0, 7, 0, 0, 0, 0).Take(16 * 16).ToArray());

		public Bytemap Arctic => new Bytemap(16, 16).FromByteArray(GenerateNoise(16, 7, 17, 18, 7, 15, 20, 19, 15).Skip(380).Take(16 * 16).ToArray());

		public Bytemap Tundra => new Bytemap(16, 16).FromByteArray(GenerateNoise(7, 0, 0, 0, 0, 0, 7, 0, 15).Skip(590).Take(16 * 16).ToArray());

		public Bytemap Desert
		{
			get
			{
				byte[] loaded = TryLoadTile("desert");
				if (loaded is not null)
					return new Bytemap(16, 16).FromByteArray(loaded);

				return new Bytemap(16, 16).FromByteArray(GenerateNoise(42, 0, 43, 0, 44, 0, 45, 0, 46, 0, 47).Skip(914).Take(16 * 16).ToArray());
			}
		}

		public Bytemap Forest
		{
			get
			{
				byte[] loaded = TryLoadTile("forest");
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
				byte[] loaded = TryLoadTile("mountains");
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
				byte[] loaded = TryLoadTile("jungle");
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
				byte[] loaded = TryLoadTile("swamp");
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

		public Bytemap Special(Terrain type)
		{
			string specialSection = type switch
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
				byte[] loaded = TryLoadTile(specialSection);
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

		private Dictionary<string, byte[]> _tileOverrides;
		private Dictionary<string, byte[]> _shoreOverrides;

		private byte[] TryLoadTile(string name)
		{
			if (_tileOverrides is null)
				_tileOverrides = ParseTilesFile(TilesFilePath);
			return _tileOverrides.TryGetValue(name, out byte[] data) ? data : null;
		}

		private byte[] TryLoadShore(string name)
		{
			if (_shoreOverrides is null)
				_shoreOverrides = ParseTilesFile(ShoresFilePath);
			return _shoreOverrides.TryGetValue(name, out byte[] data) ? data : null;
		}

		// Composite per-direction shore wave overlays from shore_tiles.txt.
		// Returns null when the file or required sections are absent (falls back to CoastLayer).
		public Bytemap ShoreLayer(Direction land)
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
				byte[] tile = TryLoadShore(pair.Item2);
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
					byte[] tile = TryLoadShore(pair.Item4);
					if (tile is null) continue;
					output.AddLayer(new Bytemap(16, 16).FromByteArray(tile));
					any = true;
				}
			}

			return any ? output : null;
		}

		private Dictionary<string, byte[]> ParseTilesFile(string path)
		{
			var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
			if (!File.Exists(path))
				return result;

			string currentSection = null;
			var pixels = new List<byte>();

			foreach (string raw in File.ReadAllLines(path))
			{
				string line = raw.Trim();
				if (line.Length == 0 || line.StartsWith("#")) continue;

				if (line.StartsWith("[") && line.EndsWith("]"))
				{
					if (currentSection is not null && pixels.Count == 256)
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
			if (currentSection is not null && pixels.Count == 256)
				result[currentSection] = pixels.ToArray();

			return result;
		}

		// Hills: single SW-NE ridge, 2px lit (OK/14) + 3px transparent hilltop + 2px shadow (INK_LOW/6)
		// Standalone: ridge centered in tile (rows 1–12).
		// Connected (directions != None): ridge extends to tile edges (rows 0–15) for visual continuity.
		public Bytemap HillTexture(Direction directions)
		{
			string section = directions == None ? "hill_standalone" : "hill_connected";
			byte[] loaded = TryLoadTile(section);
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

		// Plains: sparse light speckles (INK_HIGH/8) over transparent — LandBase shows through as warm base
		public Bytemap PlainsTexture()
		{
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

		// Grassland: transparent (LandBase shows through) with scattered dark speckles (INK_LOW/6)
		public Bytemap GrasslandTexture()
		{
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
			byte[] loaded = TryLoadTile("irrigation");
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
			Bytemap shore = ShoreLayer(land);
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
					5,  2,  2,  5, 14, 14, 14,  5,
					5,  2,  2, 14, 14, 14,  5,  0,
					5,  2,  2, 14, 14,  5,  0,  0,
					5,  2,  2,  2,  2,  2,  5,  0,
					5,  2,  2,  2,  2,  2,  5,  0,
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

		public Bytemap Difficulties
		{
			get
			{
				Bytemap output = new Bytemap(320, 200);
				
				DiffPanel(ref output, 155, 29, 131, 137);
				
				(int Skip, byte[] Colours)[] backgrounds =
				[
					(139, [23, 24, 25, 26, 27, 1]),
					(2075, [24, 25, 26, 27, 28, 2]),
					(4085, [25, 26, 27, 28, 29, 3]),
					(6750, [26, 27, 28, 29, 30, 6]),
					(8412, [27, 28, 29, 30, 31, 4])
				];
				for (int i = 0; i < 5; i++)
				{
					int xx = (i % 2) == 0 ? 21 : 80;
					int yy = 6 + (35 * i);
					DiffPanel(ref output, xx, yy, 53, 47);
					output.AddLayer(new Bytemap(47, 41).FromByteArray(GenerateNoise(backgrounds[i].Colours).Skip(backgrounds[i].Skip).Take(47 * 41).ToArray()), xx + 3, yy + 3);
				}

				return output;
			}
		}

		private static Free _instance;
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