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
	internal class Chinese : BaseCivilization<Deng>
	{
		public Chinese() : base(Civilization.Chinese, "Chinese", "Chinese", "mao")
		{
			StartX = 66;
			StartY = 19;
			CityNames = new string[]
			{
				"Beijing",
				"Shanghai",
				"Guangzhou",
				"Nanjing",
				"Qingdao",
				"Hangzhou",
				"Tianjin",
				"Datong",
				"Macau",
				"Anyang",
				"Shandong",
				"Jinan",
				"Kaifeng",
				"Ningbo",
				"Baoding",
				"Yangzhou"
			};
		}
	}
}