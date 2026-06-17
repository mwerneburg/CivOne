// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Graphics.Sprites;
using CivOne.Governments;

namespace CivOne.Graphics
{
	internal class Icons
	{
		private static Resources Resources => Resources.Instance;
		private static IBitmap _food = null!;
		public static IBitmap Food
		{
			get
			{
				if (_food is null)
				{
					if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("SP257"))
					{
						_food = new Picture(Free.Instance.Food, Common.GetPalette256);
					}
					else
					{
						_food = Resources["SP257"][128, 32, 8, 8]
							.ColourReplace(3, 0)
							.FillRectangle(0, 0, 1, 8, 0);
					}
				}
				return _food;
			}
		}

		private static IBitmap _foodLoss = null!;
		public static IBitmap FoodLoss
		{
			get
			{
				if (_foodLoss is null)
				{
					_foodLoss = Resources["SP257"][128, 32, 8, 8]
						.ColourReplace((3, 0), (15, 5))
						.FillRectangle(0, 0, 1, 8, 0);
				}
				return _foodLoss;
			}
		}
		
		private static IBitmap _shield = null!;
		public static IBitmap Shield
		{
			get
			{
				if (_shield is null)
				{
					if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("SP257"))
					{
						_shield = new Picture(Free.Instance.Shield, Common.GetPalette256);
					}
					else
					{
						_shield = Resources["SP257"][136, 32, 8, 8].ColourReplace(3, 0);
					}
				}
				return _shield;
			}
		}
		
		private static IBitmap _shieldLoss = null!;
		public static IBitmap ShieldLoss
		{
			get
			{
				if (_shieldLoss is null)
				{
					_shieldLoss = Resources["SP257"][136, 32, 8, 8].ColourReplace((3, 0), (15, 5));
				}
				return _shieldLoss;
			}
		}
		
		private static IBitmap _trade = null!;
		public static IBitmap Trade
		{
			get
			{
				if (_trade is null)
				{
					if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("SP257"))
					{
						_trade = new Picture(Free.Instance.Trade, Common.GetPalette256);
					}
					else
					{
						_trade = Resources["SP257"][144, 32, 8, 8].ColourReplace(3, 0);
					}
				}
				return _trade;
			}
		}

		private static IBitmap _corruption = null!;
		public static IBitmap Corruption
		{
			get
			{
				if (_corruption is null)
				{
					_corruption = Resources["SP257"][144, 32, 8, 8].ColourReplace((3, 0), (15, 5));
				}
				return _corruption;
			}
		}
		
		private static IBitmap _unhappy = null!;
		public static IBitmap Unhappy
		{
			get
			{
				if (_unhappy is null)
				{
					_unhappy = Resources["SP257"][136, 40, 8, 8].ColourReplace(3, 0);
				}
				return _unhappy;
			}
		}
		
		private static IBitmap _luxuries = null!;
		public static IBitmap Luxuries
		{
			get
			{
				if (_luxuries is null)
				{
					if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("SP257"))
					{
						_luxuries = new Picture(Free.Instance.Luxuries, Common.GetPalette256);
					}
					else
					{
						_luxuries = Resources["SP257"][144, 40, 8, 8].ColourReplace(3, 0);
					}
				}
				return _luxuries;
			}
		}
		
		private static IBitmap _taxes = null!;
		public static IBitmap Taxes
		{
			get
			{
				if (_taxes is null)
				{
					if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("SP257"))
					{
						_taxes = new Picture(Free.Instance.Taxes, Common.GetPalette256);
					}
					else
					{
						_taxes = Resources["SP257"][152, 32, 8, 8].ColourReplace(3, 0);
					}
				}
				return _taxes;
			}
		}
		
		private static IBitmap _science = null!;
		public static IBitmap Science
		{
			get
			{
				if (_science is null)
				{
					if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("SP257"))
					{
						_science = new Picture(Free.Instance.Science, Common.GetPalette256);
					}
					else
					{
						_science = Resources["SP257"][128, 40, 8, 8].ColourReplace(3, 0);
					}
				}
				return _science;
			}
		}
		
		private static IBitmap _spy = null!;
		public static IBitmap Spy
		{
			get
			{
				if (_spy is null)
				{
					if (RuntimeHandler.Runtime.Settings.Free || !Resources.Exists("SP299"))
					{
						_spy = new Picture(Free.Instance.PanelGrey, Common.GetPalette256);
					}
					else
					{
						_spy = Resources["SP299"][160, 142, 40, 52].ColourReplace(3, 0);
					}
				}
				return _spy;
			}
		}
		
		private static IBitmap _newspaper = null!;
		public static IBitmap Newspaper
		{
			get
			{
				if (_newspaper is null)
				{
					_newspaper = Resources["SP257"][176, 128, 32, 16];
				}
				return _newspaper;
			}
		}

		private static IBitmap _sellButton = null!;
		public static IBitmap SellButton
		{
			get
			{
				if (_sellButton is null)
				{
					byte[] bytemap = new byte[] {
						0,  0,  5,  5,  5,  0,  0,  0,
						0,  5, 15, 15, 15,  5,  0,  0,
						5, 15, 12, 12, 12, 15,  5,  0,
						5, 15, 12, 12, 12, 15,  5,  0,
						5, 15, 12, 12, 12, 15,  5,  0,
						0,  5, 15, 15, 15,  5,  0,  0,
						0,  0,  5,  5,  5,  0,  0,  0
					};
					_sellButton = new Picture(8, 7, bytemap, Food.Palette);
				}
				return _sellButton;
			}
		}

		private static IBitmap[] _helperArrow = null!;
		public static IBitmap? HelperArrow(Direction direction)
		{
			if (_helperArrow is null)
			{
				_helperArrow = new IBitmap[4];
				_helperArrow[0] = new Picture(16, 16, new byte[] {
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5,  5,  5,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  5,  5,  5,  5,  5, 15, 15,  5,  5,  5,  5,  5,  0,  0,
					0,  0,  0,  5, 15, 15, 15, 15, 15, 15, 15, 15,  5,  0,  0,  0,
					0,  0,  0,  0,  5, 15, 15, 15, 15, 15, 15,  5,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  5, 15, 15, 15, 15,  5,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				}, Food.Palette);
				_helperArrow[1] = new Picture(16, 16, new byte[] {
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  5, 15, 15, 15, 15,  5,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  5, 15, 15, 15, 15, 15, 15,  5,  0,  0,  0,  0,
					0,  0,  0,  5, 15, 15, 15, 15, 15, 15, 15, 15,  5,  0,  0,  0,
					0,  0,  5,  5,  5,  5,  5, 15, 15,  5,  5,  5,  5,  5,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5,  5,  5,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				}, Food.Palette);
				_helperArrow[2] = new Picture(16, 16, new byte[] {
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  5, 15,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  5, 15, 15, 15,  5,  5,  5,  5,  5,  5,  5,  0,  0,
					0,  0,  5, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,  5,  0,  0,
					0,  0,  5, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,  5,  0,  0,
					0,  0,  0,  5, 15, 15, 15,  5,  5,  5,  5,  5,  5,  5,  0,  0,
					0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  5, 15,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  5,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				}, Food.Palette);
				_helperArrow[3] = new Picture(16, 16, new byte[] {
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5, 15,  5,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,
					0,  0,  5,  5,  5,  5,  5,  5,  5, 15, 15, 15,  5,  0,  0,  0,
					0,  0,  5, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,  5,  0,  0,
					0,  0,  5, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15,  5,  0,  0,
					0,  0,  5,  5,  5,  5,  5,  5,  5, 15, 15, 15,  5,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5, 15, 15,  5,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5, 15,  5,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5,  5,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  5,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
					0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,  0,
				}, Food.Palette);
			}

			switch (direction)
			{
				case Direction.South: return _helperArrow[0];
				case Direction.North: return _helperArrow[1];
				case Direction.West: return _helperArrow[2];
				case Direction.East: return _helperArrow[3];
			}
			return null;
		}

		private static IBitmap[] _citizen = new Picture[9];
		public static IBitmap Citizen(Citizen citizen)
		{
			if (_citizen[(int)citizen] is null)
			{
				_citizen[(int)citizen] = Resources["SP257"][(8 * (int)citizen), 128, 8, 16];
			}
			return _citizen[(int)citizen];
		}

		private static IBitmap[] _lamp = new Picture[4];
		public static IBitmap? Lamp(int stage)
		{
			if (stage < 0 || stage > 3)
				return null;
			
			if (_lamp[stage] is null)
			{
				_lamp[stage] = Resources["SP257"][128 + (8 * stage), 48, 8, 8];
			}
			return _lamp[stage];
		}

		private static IBitmap[,] _governmentPortrait = new Picture[7, 4];
		public static IBitmap GovernmentPortrait(IGovernment government, Advisor advisor, bool modern)
		{
			string filename;
			int governmentId;
			if (government is Monarchy)
			{
				governmentId = (modern ? 3 : 2);
				filename = $"GOVT1" + (modern ? "M" : "A");
			}
			else if (government is Republic || government is Democracy)
			{
				governmentId = (modern ? 5 : 4);
				filename = $"GOVT2" + (modern ? "M" : "A");
			}
			else if (government is Communism)
			{
				governmentId = 6;
				filename = "GOVT3A";
			}
			else // Anarchy or Despotism
			{
				governmentId = (modern ? 1 : 0);
				filename = "GOVT0" + (modern ? "M" : "A");
			}
			if (_governmentPortrait[governmentId, (int)advisor] is null)
				_governmentPortrait[governmentId, (int)advisor] = Resources[filename][(40 * (int)advisor), 0, 40, 60];
			return _governmentPortrait[governmentId, (int)advisor];
		}

		public static IBitmap City(City city, bool smallFont = false)
		{
			if (Game.Instance?.GetPlayer(city.Owner)?.Civilization is Olvir)
				return OlvirCity(city, smallFont);

			IBitmap output = new Picture(16, 16);

			// Black field
			output.FillRectangle(0, 0, 16, 16, CassetteTheme.BG0);

			// Units-present: 1-px outline so the unit stack shows behind the icon
			if (city.Tile.Units.Length > 0)
			{
				output.FillRectangle(0, 0, 16, 16, CassetteTheme.BORDER);
				output.FillRectangle(1, 1, 14, 14, CassetteTheme.BG0);
			}

			// Heraldic three-merlon crenellation
			output
				.FillRectangle(3, 1, 3, 2, CassetteTheme.INK_MID)   // left merlon
				.FillRectangle(7, 1, 3, 2, CassetteTheme.INK_MID)   // centre merlon
				.FillRectangle(11, 1, 3, 2, CassetteTheme.INK_MID)  // right merlon
				.FillRectangle(3, 3, 11, 1, CassetteTheme.INK_MID); // base wall

			// City size numeral: amber phosphor normally, red when in disorder
			byte numCol = city.IsInDisorder ? CassetteTheme.ALERT : CassetteTheme.PHOS;
			output.DrawText($"{city.Size}", (smallFont ? 1 : 0), numCol, 8, 6, TextAlign.Center);

			if (city.HasBuilding<CityWalls>())
				output.AddLayer(Generic.Fortify, 0, 0);

			output.FillRectangle(0, 13, 16, 2, Common.ColourLight[city.Owner]);
			output.FillRectangle(0, 15, 16, 1, Common.ColourDark[city.Owner]);
			return output;
		}

		private static IBitmap OlvirCity(City city, bool smallFont)
		{
			IBitmap output = new Picture(16, 16);
			output.FillRectangle(0, 0, 16, 16, CassetteTheme.BG0);

			if (city.Tile.Units.Length > 0)
			{
				output.FillRectangle(0, 0, 16, 16, CassetteTheme.BORDER);
				output.FillRectangle(1, 1, 14, 14, CassetteTheme.BG0);
			}

			// Dome silhouette in CYAN
			const byte dc = CassetteTheme.CYAN;
			// Top cap (row 1, 6px wide)
			for (int x = 5; x <= 10; x++) output.Bitmap[x, 1] = dc;
			// Arc shoulders (row 2)
			output.Bitmap[4, 2] = dc; output.Bitmap[5, 2] = dc;
			output.Bitmap[10, 2] = dc; output.Bitmap[11, 2] = dc;
			// Dome narrows (row 3)
			output.Bitmap[3, 3] = dc; output.Bitmap[12, 3] = dc;
			// Vertical sides (rows 4–7)
			for (int y = 4; y <= 7; y++) { output.Bitmap[2, y] = dc; output.Bitmap[13, y] = dc; }
			// Base bar (row 8)
			for (int x = 2; x <= 13; x++) output.Bitmap[x, 8] = dc;

			// City size numeral inside the dome
			byte numCol = city.IsInDisorder ? CassetteTheme.ALERT : CassetteTheme.PHOS;
			output.DrawText($"{city.Size}", (smallFont ? 1 : 0), numCol, 8, 6, TextAlign.Center);

			if (city.HasBuilding<CityWalls>())
				output.AddLayer(Generic.Fortify, 0, 0);

			output.FillRectangle(0, 13, 16, 2, Common.ColourLight[city.Owner]);
			output.FillRectangle(0, 15, 16, 1, Common.ColourDark[city.Owner]);
			return output;
		}
	}
}