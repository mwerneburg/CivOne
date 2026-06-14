#nullable enable
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
	internal class Ethiopian : BaseCivilization<HaileSelassie>
	{
		public Ethiopian() : base(Civilization.Ethiopians, "Ethiopian", "Ethiopians")
		{
			StartX = 48;
			StartY = 31;
			CityNames = new string[]
			{
				"Aksum",
				"Lalibela",
				"Gondar",
				"Adwa",
				"Addis Ababa",
				"Harar",
				"Jimma",
				"Mekele",
				"Dire Dawa",
				"Bahir Dar",
				"Dessie",
				"Awash",
				"Jijiga",
				"Arba Minch",
				"Nekemte",
				"Gambela"
			};
		}
	}
}
