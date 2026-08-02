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
	internal class Ottoman : BaseCivilization<Suleiman>
	{
		public Ottoman() : base(Civilization.Ottomans, "Ottoman", "Ottomans")
		{
			StartX = 42;
			StartY = 17;
			CityNames = new string[]
			{
				"Constantinople",
				"Edirne",
				"Bursa",
				"Gallipoli",
				"Ankara",
				"Konya",
				"Smyrna",
				"Belgrade",
				"Mosul",
				"Thessaloniki",
				"Sarajevo",
				"Sofia",
				"Skopje",
				"Plovdiv",
				"Erzurum",
				"Trabzon",
				"Izmit",
				"Amasya",
				"Sivas",
				"Kayseri",
				"Adana",
				"Antalya",
				"Diyarbakir",
				"Van",
				"Kars",
				"Bitola",
				"Shkoder",
				"Nis",
				"Vidin",
				"Ruse",
				"Varna",
				"Bucharest",
				"Iasi",
				"Buda",
				"Pecs",
				"Mostar",
				"Banja Luka",
				"Podgorica",
				"Ioannina",
				"Larnaca"
			};
		}
	}
}
