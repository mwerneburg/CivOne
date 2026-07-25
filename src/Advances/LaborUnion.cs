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
	internal class LaborUnion : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE LABOR UNION gives working",
			"people a voice their employers",
			"must answer.",
			"",
			"Allows MECHANIZED INFANTRY.",
		};

		private static readonly string[] _page2 =
		{
			"Mech. Inf. is the finest defensive",
			"unit in the game, and fast enough",
			"that a handful can garrison a",
			"whole frontier by moving to",
			"whichever city needs them.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public LaborUnion() : base(1, 0, 1, Advance.MassProduction, Advance.Communism)
		{
			Name = "Labor Union";
			Type = Advance.LaborUnion;
		}
	}
}