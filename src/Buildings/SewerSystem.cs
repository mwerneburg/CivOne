// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Buildings
{
	internal class SewerSystem : BaseBuilding
	{
		private static readonly string[] _page1 = new[]
		{
			"A SEWER SYSTEM provides underground",
			"drainage and waste treatment for an",
			"entire city. By removing contaminated",
			"water and accumulated refuse, it",
			"dramatically reduces the spread of",
			"disease among the population.",
			"",
			"Cities with a Sewer System may grow",
			"beyond size 12, as the infrastructure",
			"can support a larger, healthier",
			"population in sanitary conditions.",
		};

		private static readonly string[] _page2 = new[]
		{
			"The great sewer of Rome, the Cloaca",
			"Maxima, was constructed in the 6th",
			"century BC and remains in partial use",
			"today. Roman cities that invested in",
			"sanitation infrastructure consistently",
			"outlasted those that did not, their",
			"populations swelling while neighbours",
			"were ravaged by plague and famine.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public SewerSystem() : base(12, 4)
		{
			Name = "Sewer System";
			RequiredTech = new Engineering();
			SetIcon(1, 3, false);
			SetSmallIcon(1, 3);
			Type = Building.SewerSystem;
		}
	}
}
