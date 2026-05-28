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
	internal class Arab : BaseCivilization<HarunAlRashid>
	{
		public Arab() : base(Civilization.Arabs, "Arab", "Arabs")
		{
			StartX = 49;
			StartY = 27;
			CityNames = new string[]
			{
				"Medina",
				"Mecca",
				"Baghdad",
				"Damascus",
				"Tunis",
				"Muscat",
				"Aden",
				"Riyadh",
				"Sanaa",
				"Tripoli",
				"Algiers",
				"Tangier",
				"Fez",
				"Kufah",
				"Oman",
				"Jeddah"
			};
		}
	}
}
