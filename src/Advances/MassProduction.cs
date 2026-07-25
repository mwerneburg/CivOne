// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Advances
{
	internal class MassProduction : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"MASS PRODUCTION makes identical",
			"goods in enormous numbers.",
			"",
			"Allows the SUBMARINE and MASS",
			"TRANSIT.",
		};

		private static readonly string[] _page2 =
		{
			"Mass transit removes the pollution",
			"your citizens make, which by now",
			"is as much as your factories'.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MassProduction() : base(5, 1, 0, Advance.Automobile, Advance.TheCorporation)
		{
			Name = "Mass Production";
			Type = Advance.MassProduction;
		}
	}
}