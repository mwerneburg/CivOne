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
	internal class Olvir : BaseCivilization<OlvirCouncil>
	{
		public Olvir() : base(Civilization.Olvir, "Olvir", "Olvir")
		{
			CityNames = new string[]
			{
				"Vel'Thara",
				"Ossiveth",
				"Kalindru",
				"Tharaxis",
				"Sundrevaal",
				"Mirethkai",
				"Drossivan",
				"Quelthara",
				"Innaveth",
				"Halundra",
				"Cesviri",
				"Orindath",
				"Pheluvaan",
				"Brassiveth",
				"Talimeru",
				"Voss'khai",
			};
		}
	}
}
