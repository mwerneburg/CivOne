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
	// The Parana basin, which finishes most games unoccupied.
	internal class Guarani : BaseCivilization<SepeTiaraju>
	{
		public Guarani() : base(Civilization.Guarani, "Guarani", "Guarani")
		{
			// Legacy 80x50 fallback only. Earth maps place this civ from
			// Game.EarthCentroids, which is real latitude and longitude.
			StartX = 27;
			StartY = 34;
			CityNames = new string[]
			{
				"Asuncion",
				"Itapua",
				"Yapeyu",
				"Sao Miguel",
				"Santa Maria",
				"Candelaria",
				"Concepcion",
				"Trinidad",
				"Jesus",
				"Loreto",
				"San Ignacio",
				"Santa Ana",
				"Corpus",
				"San Cosme",
				"Caazapa",
				"Yuty",
				"Villarrica",
				"Paraguari",
				"Caacupe",
				"Ypane",
				"Itaugua",
				"Aregua",
				"Luque",
				"Capiata",
				"Altos",
				"Tobati",
				"Piribebuy",
				"Ybycui",
				"Quiindy",
				"Carapegua",
				"San Lorenzo",
				"Limpio",
				"Emboscada",
				"Atyra",
				"Valenzuela",
				"Escobar",
				"Acahay",
				"Sapucai",
				"Mbuyapey",
				"Guarambare",
			};
		}
	}
}
