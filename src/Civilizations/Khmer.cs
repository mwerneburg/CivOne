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
	internal class Khmer : BaseCivilization<Jayavarman>
	{
		public Khmer() : base(Civilization.Khmer, "Khmer", "Khmer")
		{
			StartX = 66;
			StartY = 28;
			CityNames = new string[]
			{
				"Angkor",
				"Angkor Thom",
				"Sambor",
				"Banteay Srei",
				"Koh Ker",
				"Preah Khan",
				"Ta Prohm",
				"Phnom Kulen",
				"Lovek",
				"Oudong",
				"Phnom Penh",
				"Battambang",
				"Siem Reap",
				"Kampong Thom",
				"Mahendraparvata",
				"Wat Phu"
			};
		}
	}
}
