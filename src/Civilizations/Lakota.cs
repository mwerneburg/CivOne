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
	internal class Lakota : BaseCivilization<SittingBull>
	{
		public Lakota() : base(Civilization.Lakota, "Lakota", "Lakota")
		{
			StartX = 10;
			StartY = 14;
			CityNames = new string[]
			{
				"Laramie",
				"Deadwood",
				"Pine Ridge",
				"Rosebud",
				"Standing Rock",
				"Sisseton",
				"Fort Laramie",
				"Wind Cave",
				"Bear Butte",
				"Slim Buttes",
				"Tongue River",
				"Wolf Point",
				"Circle",
				"Wounded Knee",
				"Ash Hollow",
				"Cheyenne",
				"Oglala",
				"Brule",
				"Hunkpapa",
				"Minneconjou",
				"Sans Arc",
				"Two Kettle",
				"Yankton",
				"Santee",
				"Greasy Grass",
				"Powder River",
				"Little Bighorn",
				"Black Hills",
				"Badlands",
				"Missouri Breaks",
				"White River",
				"Cheyenne River",
				"Grand River",
				"Moreau",
				"Belle Fourche",
				"Spearfish",
				"Sturgis",
				"Rapid Creek",
				"Thunder Butte",
				"Medicine Root"
			};
		}
	}
}
