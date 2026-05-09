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

namespace CivOne.Screens.Reports
{
	[Modal, OwnPalette, Expand]
	internal class TopCities : BaseScreen
	{
		private bool _update = true;

		private readonly City[] _cities;

		private int OX => (Width - 320) / 2;

		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			for (int i = 0; i < _cities.Length; i++)
			{
				City city = _cities[i];
				if (city == null || city.Size == 0) continue;

				byte colour = Common.ColourLight[city.Owner];
				Player owner = Game.GetPlayer(city.Owner);

				int xx = OX + 8;
				int yy = 32 + (32 * i);
				int ww = 304;
				int hh = 26;

				this.FillRectangle(xx, yy, ww, hh, colour)
					.FillRectangle(xx + 1, yy + 1, ww - 2, hh - 2, CassetteTheme.BG1);

				// Line 1: rank, name, civilization
				this.DrawText($"{i + 1}. {city.Name} ({owner.Civilization.Name})", 0, CassetteTheme.INK_HIGH,
					Width / 2, yy + 4, TextAlign.Center);

				// Line 2: population, improvements, wonder stars
				int wonders = city.Wonders.Length;
				int buildings = city.Buildings.Length;
				string stars = wonders > 0 ? "  " + new string('★', wonders) : "";
				string detail = $"Pop {city.Size}  ·  {buildings} improvement{(buildings == 1 ? "" : "s")}{stars}";
				this.DrawText(detail, 0, CassetteTheme.INK_MID, Width / 2, yy + 14, TextAlign.Center);
			}

			_update = false;
			return true;
		}

		public override bool KeyDown(KeyboardEventArgs args)
		{
			Destroy();
			return true;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			Destroy();
			return true;
		}

		public TopCities()
		{
			Palette p = Common.DefaultPalette;
			using (Palette cassette = CassetteTheme.CreatePalette())
				p.MergePalette(cassette, 1, 17);
			Palette = p;

			_cities = Game.GetCities()
							.Where(c => c.Size > 0)
							.OrderByDescending(c => c.Wonders.Length)
							.ThenByDescending(c => c.Size)
							.ThenByDescending(c => c.Citizens.Count(x => x == Citizen.HappyMale || x == Citizen.HappyFemale))
							.ThenByDescending(c => c.Citizens.Count(x => x == Citizen.ContentMale || x == Citizen.ContentFemale))
							.ThenBy(c => c.Citizens.Count(x => x == Citizen.UnhappyMale || x == Citizen.UnhappyFemale))
							.Take(5)
							.ToArray();

			this.Clear(CassetteTheme.BG0)
				.FillRectangle(0, 0, Width, 27, CassetteTheme.BG3)
				.FillRectangle(0, 27, Width, 1, CassetteTheme.BORDER)
				.DrawText("The Top Five Cities in the World", 0, CassetteTheme.PHOS_GLOW, Width / 2, 9, TextAlign.Center);
		}
	}
}
