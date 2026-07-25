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

namespace CivOne.Wonders
{
	internal class CureForCancer : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The CURE FOR CANCER lifts a great",
			"fear from all humanity.",
			"",
			"Every city in your empire gains",
			"one more HAPPY citizen.",
		};

		private static readonly string[] _page2 =
		{
			"Requires GENETIC ENGINEERING.",
			"",
			"Empire-wide contentment lets you",
			"run larger cities and spend less",
			"trade on LUXURIES.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CureForCancer() : base(60)
		{
			Name = "Cure for Cancer";
			RequiredTech = new GeneticEngineering();
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.CureForCancer;
		}
	}
}