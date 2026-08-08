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
	// The northwest coast: a sea people on rainforest islands, where the food came off the salmon runs rather than the hillsides.
	internal class Haida : BaseCivilization<Edenshaw>
	{
		public Haida() : base(Civilization.Haida, "Haida", "Haida")
		{
			// Legacy 80x50 fallback only. Earth maps place this civ from
			// Game.EarthCentroids, which is real latitude and longitude.
			StartX = 12;
			StartY = 9;
			CityNames = new string[]
			{
				"SGang Gwaay",
				"Skidegate",
				"Masset",
				"Old Massett",
				"Tanu",
				"Skedans",
				"Cumshewa",
				"Kiusta",
				"Yan",
				"Hiellen",
				"Dadens",
				"Kayung",
				"Chaatl",
				"Kaisun",
				"Tian",
				"Naikun",
				"Hlkinul",
				"Sandspit",
				"Tlell",
				"Yakoun",
				"Kunghit",
				"Ninstints",
				"Haina",
				"Tasu",
				"Gwaii Haanas",
				"Juskatla",
				"Port Clements",
				"Rose Spit",
				"Langara",
				"Athlow",
				"Sedgwick",
				"Kiidk",
				"Hotspring",
				"Burnaby Narrows",
				"Windy Bay",
				"Lyell",
				"Ramsay",
				"Murchison",
				"Faraday",
				"Bischof",
			};
		}
	}
}
