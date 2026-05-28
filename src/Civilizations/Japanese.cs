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
	internal class Japanese : BaseCivilization<Tokugawa>
	{
		public Japanese() : base(Civilization.Japanese, "Japanese", "Japanese")
		{
			StartX = 73;
			StartY = 19;
			CityNames = new string[]
			{
				"Kyoto",
				"Osaka",
				"Tokyo",
				"Nara",
				"Nagoya",
				"Kamakura",
				"Sapporo",
				"Sendai",
				"Hiroshima",
				"Fukuoka",
				"Kagoshima",
				"Nagasaki",
				"Yokohama",
				"Kobe",
				"Niigata",
				"Matsuyama"
			};
		}
	}
}
