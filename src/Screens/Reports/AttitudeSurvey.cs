#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using CivOne.Buildings;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;

namespace CivOne.Screens.Reports
{
	internal class AttitudeSurvey : BaseReport
	{
		private const byte FONT_ID = 0;

		private readonly City[] _cities;

		private bool _update = true;
		private int _page = 0;

		// Buildings/wonders that influence local happiness. T/M/C/B match the user's mental
		// model; we also surface the post-contact Exchange/Neural/Civic buildings and the
		// happiness-flavoured wonders (J.S. Bach's, Michelangelo's Chapel, Hagia Sofia,
		// Shakespeare's Theatre, Hanging Gardens, Cure for Cancer, Taj Mahal).
		private static readonly (System.Type type, string code, bool isWonder)[] _moodSlots =
		{
			(typeof(Temple),            "T", false),
			(typeof(MarketPlace),       "M", false),
			(typeof(Cathedral),         "C", false),
			(typeof(Bank),              "B", false),
			(typeof(Colosseum),         "L", false),
			(typeof(ExchangeCenter),    "X", false),
			(typeof(NeuralLab),         "N", false),
			(typeof(CivicMonument),     "V", false),
			(typeof(CivOne.Wonders.HangingGardens),       "g", true),
			(typeof(CivOne.Wonders.ShakespearesTheatre),  "s", true),
			(typeof(CivOne.Wonders.JSBachsCathedral),     "j", true),
			(typeof(CivOne.Wonders.MichelangelosChapel),  "h", true),
			(typeof(CivOne.Wonders.HagiaSofia),           "f", true),
			(typeof(CivOne.Wonders.CureForCancer),        "k", true),
			(typeof(CivOne.Wonders.TajMahal),             "t", true),
		};

		private int BuildingPanelW => _moodSlots.Length * 8 + 4;

		private void DrawCitizens(City city, int x, int y, int xMax)
		{
			int group = -1;
			Citizen[] citizens = city.Citizens.ToArray();
			for (int j = 0; j < city.Size && x + 8 <= xMax; j++)
			{
				x += 8;
				if (group != (group = Common.CitizenGroup(citizens[j])) && group > 0 && j > 0)
				{
					x += 2;
					if (group == 3) x += 4;
				}
				this.DrawCitizenToken(citizens[j], x, y - 4);
			}
		}

		private void DrawBuildings(City city, int y)
		{
			// Right-aligned panel of single-character codes, so even a Size-30 row of citizens
			// has room to render to its left. Present buildings glow; absent ones are dim, so
			// the panel doubles as a per-city checklist of which mood structures exist.
			int x = Width - BuildingPanelW;
			foreach (var (type, code, isWonder) in _moodSlots)
			{
				bool has = isWonder
					? city.Player?.Cities.Any(c => c.HasWonder(type)) ?? false
					: city.Buildings.Any(b => b.GetType() == type);
				byte colour = has ? CassetteTheme.PHOS_GLOW : CassetteTheme.INK_LOW;
				this.DrawText(code, FONT_ID, colour, x, y);
				x += 8;
			}
		}

		private void DrawLegend(int y)
		{
			if (y > Height - 10) return;
			byte cap = CassetteTheme.PHOS;
			byte mid = CassetteTheme.INK_MID;
			this.DrawText("T", FONT_ID, cap, OX + 16,  y); this.DrawText("emple ",    FONT_ID, mid, OX + 22,  y);
			this.DrawText("M", FONT_ID, cap, OX + 58,  y); this.DrawText("arket ",    FONT_ID, mid, OX + 64,  y);
			this.DrawText("C", FONT_ID, cap, OX + 96,  y); this.DrawText("athedral ", FONT_ID, mid, OX + 102, y);
			this.DrawText("B", FONT_ID, cap, OX + 152, y); this.DrawText("ank ",      FONT_ID, mid, OX + 158, y);
			this.DrawText("L", FONT_ID, cap, OX + 180, y); this.DrawText("Colosseum ",FONT_ID, mid, OX + 186, y);
			this.DrawText("X", FONT_ID, cap, OX + 240, y); this.DrawText("change ",   FONT_ID, mid, OX + 246, y);
			this.DrawText("N", FONT_ID, cap, OX + 286, y); this.DrawText("eural ",    FONT_ID, mid, OX + 292, y);
			int y2 = y + 9;
			if (y2 > Height - 10) return;
			this.DrawText("V", FONT_ID, cap, OX + 16,  y2); this.DrawText("Civic ",         FONT_ID, mid, OX + 22,  y2);
			this.DrawText("g", FONT_ID, cap, OX + 56,  y2); this.DrawText("Hang.Gardens ",  FONT_ID, mid, OX + 62,  y2);
			this.DrawText("s", FONT_ID, cap, OX + 130, y2); this.DrawText("Shakespeare ",   FONT_ID, mid, OX + 136, y2);
			this.DrawText("j", FONT_ID, cap, OX + 198, y2); this.DrawText("J.S.Bach ",      FONT_ID, mid, OX + 204, y2);
			this.DrawText("h", FONT_ID, cap, OX + 250, y2); this.DrawText("Michelangelo ",  FONT_ID, mid, OX + 256, y2);
			int y3 = y2 + 9;
			if (y3 > Height - 10) return;
			this.DrawText("f", FONT_ID, cap, OX + 16,  y3); this.DrawText("Hagia Sofia ",   FONT_ID, mid, OX + 22,  y3);
			this.DrawText("k", FONT_ID, cap, OX + 86,  y3); this.DrawText("Cure for Cancer ",FONT_ID, mid, OX + 92, y3);
			this.DrawText("t", FONT_ID, cap, OX + 174, y3); this.DrawText("Taj Mahal",      FONT_ID, mid, OX + 180, y3);
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (!_update) return false;

			this.FillRectangle(0, 28, Width, Height - 28, 9);

			int pageSize = (Height - 32) / 10;
			int y = 32;
			for (int i = (_page++ * pageSize); i < _cities.Length && i < (_page * pageSize); i++)
			{
				City city = _cities[i];

				this.DrawText($"{city.Name}:", FONT_ID, CassetteTheme.PHOS, OX + 16, y);

				int citizenStart = OX + ((i % 2 == 0) ? 72 : 76);
				int citizenLimit = Width - BuildingPanelW - 4;
				DrawCitizens(city, citizenStart, y, citizenLimit);
				DrawBuildings(city, y);

				y += 10;
			}
			y += 8;
			if (y <= Height - 20)
			{
				Citizen[] citizens = Human.Cities.SelectMany(x => x.Citizens).ToArray();
				string population = Common.NumberSeperator(Human.Population);
				if (Human.Population == 0) population = "00,000";
				int totalCitizens = citizens.Length;
				int happyCitizens = citizens.Count(c => c == Citizen.HappyMale || c == Citizen.HappyFemale);
				int unhappyCitizens = citizens.Count(c => c == Citizen.UnhappyMale || c == Citizen.UnhappyFemale);
				int contentCitizens = totalCitizens - happyCitizens - unhappyCitizens;

				if (totalCitizens > 0)
				{
					int happy = (int)Math.Floor((double)(100 / totalCitizens) * happyCitizens);
					int content = (int)Math.Floor((double)(100 / totalCitizens) * contentCitizens);
					int unhappy = (int)Math.Floor((double)(100 / totalCitizens) * unhappyCitizens);
					this.DrawText($"Population: {population} Happy:{happy}% Content:{content}% Unhappy:{unhappy}%", 0, CassetteTheme.INK_MID, OX + 16, y);
				}
				DrawLegend(y + 10);
			}

			_update = false;
			return true;
		}

		private bool NextPage()
		{
			int pageSize = (Height - 32) / 10;
			if ((_page * pageSize) < _cities.Length)
			{
				_update = true;
			}
			else
			{
				Destroy();
			}
			return true;
		}
		
		public override bool KeyDown(KeyboardEventArgs args)
		{
			return NextPage();
		}
		
		public override bool MouseDown(ScreenEventArgs args)
		{
			int pageSize = (Height - 32) / 10;
			if (args.Y >= 32)
			{
				int rowIdx  = (args.Y - 32) / 10;
				int cityIdx = (_page - 1) * pageSize + rowIdx;
				if (cityIdx >= 0 && cityIdx < _cities.Length)
				{
					Destroy();
					Common.AddScreen(new CityManager(_cities[cityIdx]));
					return true;
				}
			}
			return NextPage();
		}

		public AttitudeSurvey() : base("SENTIMENT SURVEY", 9, MouseCursor.Pointer)
		{
			_cities = Game.GetCities().Where(c => Human == c.Owner && c.Size > 0).ToArray();
		}
	}
}