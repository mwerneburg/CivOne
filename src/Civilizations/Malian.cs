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
	internal class Malian : BaseCivilization<MansaMusa>
	{
		public Malian() : base(Civilization.Malians, "Malian", "Malians")
		{
			StartX = 36;
			StartY = 30;
			CityNames = new string[]
			{
				"Timbuktu",
				"Niani",
				"Kumbi Saleh",
				"Djenné",
				"Gao",
				"Walata",
				"Taghaza",
				"Awdaghust",
				"Kangaba",
				"Sikasso",
				"Bamako",
				"Kidal",
				"Mopti",
				"Ségou",
				"Nioro",
				"Kayes",
				"Wagadou",
				"Tadmekka",
				"Takrur",
				"Jenne-Jeno",
				"Bandiagara",
				"Douentza",
				"Hombori",
				"Ansongo",
				"Menaka",
				"Goundam",
				"Niafunke",
				"Tenenkou",
				"Macina",
				"Bougouni",
				"Koutiala",
				"San",
				"Kita",
				"Markala",
				"Yelimane",
				"Bafoulabe",
				"Kolokani",
				"Dioila",
				"Banamba",
				"Sokolo"
			};
		}
	}
}
