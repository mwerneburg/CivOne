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
	internal class Conscription : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"CONSCRIPTION calls up the whole",
			"citizenry and drills it.",
			"",
			"Allows RIFLEMEN.",
		};

		private static readonly string[] _page2 =
		{
			"It retires MUSKETEERS, CAVALRY and",
			"LEGIONS together — one advance",
			"sweeps away the old army.",
			"",
			"Fortified riflemen behind CITY",
			"WALLS are very hard to shift",
			"without ARTILLERY.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Conscription() : base(7, 0, 0, Advance.TheRepublic, Advance.Explosives)
		{
			Name = "Conscription";
			Type = Advance.Conscription;
		}
	}
}