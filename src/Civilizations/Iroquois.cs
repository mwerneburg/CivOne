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
	internal class Iroquois : BaseCivilization<Hiawatha>
	{
		public Iroquois() : base(Civilization.Iroquois, "Haudenosaunee", "Haudenosaunee")
		{
			StartX = 16;
			StartY = 15;
			CityNames = new string[]
			{
				"Onondaga",
				"Mohawk",
				"Oneida",
				"Seneca",
				"Cayuga",
				"Tuscarora",
				"Canajoharie",
				"Oriskany",
				"Niagara",
				"Rochester",
				"Albany",
				"Saratoga",
				"Ticonderoga",
				"Oswego",
				"Geneseo",
				"Ganondagan",
				"Kanesatake",
				"Akwesasne",
				"Tyendinaga",
				"Kahnawake",
				"Ohsweken",
				"Deseronto",
				"Genesee",
				"Tonawanda",
				"Allegany",
				"Cattaraugus",
				"Buffalo Creek",
				"Chemung",
				"Tioga",
				"Owego",
				"Elmira",
				"Ithaca",
				"Auburn",
				"Skaneateles",
				"Cazenovia",
				"Utica",
				"Herkimer",
				"Schoharie",
				"Fonda",
				"Caughnawaga"
			};
		}
	}
}
