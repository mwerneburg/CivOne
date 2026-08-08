// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;
using CivOne.Leaders;

namespace CivOne.Civilizations
{
	// Aotearoa and the wider Pacific: nobody has ever occupied Australasia in these games.
	internal class Maori : BaseCivilization<TeRauparaha>
	{
		public Maori() : base(Civilization.Maori, "Maori", "Maori")
		{
			// Legacy 80x50 fallback only. Earth maps place this civ from
			// Game.EarthCentroids, which is real latitude and longitude.
			StartX = 79;
			StartY = 39;
			CityNames = new string[]
			{
				"Rotorua",
				"Kaitaia",
				"Waitangi",
				"Kororareka",
				"Otaki",
				"Kapiti",
				"Taupo",
				"Whanganui",
				"Tauranga",
				"Whakatane",
				"Gisborne",
				"Napier",
				"Hastings",
				"Wairau",
				"Kaiapoi",
				"Akaroa",
				"Waikato",
				"Maungatautari",
				"Ohinemutu",
				"Te Awamutu",
				"Ngaruawahia",
				"Kawhia",
				"Raglan",
				"Mokau",
				"Taranaki",
				"Patea",
				"Wairoa",
				"Mahia",
				"Turanga",
				"Opotiki",
				"Torere",
				"Maketu",
				"Matata",
				"Rangiriri",
				"Meremere",
				"Paeroa",
				"Thames",
				"Coromandel",
				"Hokianga",
				"Ahipara",
			};
		}
	}
}
