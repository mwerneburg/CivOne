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
	internal class Babylonian : BaseCivilization<Hammurabi>
	{
		public Babylonian() : base(Civilization.Babylonians, "Babylonian", "Babylonians")
		{
			StartX = 45;
			StartY = 22;
			CityNames = new string[]
			{
				"Babylon",
				"Sumer",
				"Uruk",
				"Ninevah",
				"Ashur",
				"Ellipi",
				"Akkad",
				"Eridu",
				"Kish",
				"Nippur",
				"Shuruppak",
				"Zariqum",
				"Izibia",
				"Nimrud",
				"Arbela",
				"Zamua",
				"Lagash",
				"Umma",
				"Larsa",
				"Isin",
				"Mari",
				"Sippar",
				"Borsippa",
				"Girsu",
				"Adab",
				"Ur",
				"Dilbat",
				"Kutha",
				"Opis",
				"Terqa",
				"Harran",
				"Carchemish",
				"Dur-Sharrukin",
				"Kalhu",
				"Til-Barsip",
				"Ekallatum",
				"Eshnunna",
				"Tuttul",
				"Nuzi",
				"Hatra"
			};
		}
	}
}