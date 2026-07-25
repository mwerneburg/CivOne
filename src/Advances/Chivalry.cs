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
	internal class Chivalry : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"CHIVALRY makes the mounted knight",
			"the master of the battlefield.",
			"",
			"Allows KNIGHTS and the TAJ MAHAL.",
		};

		private static readonly string[] _page2 =
		{
			"It also retires the CHARIOT.",
			"",
			"Knights attack heavily, defend",
			"well and move twice. For centuries",
			"no other unit is worth building",
			"for war.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Chivalry() : base(6, 1, 1, Advance.Feudalism, Advance.HorsebackRiding)
		{
			Name = "Chivalry";
			Type = Advance.Chivalry;
		}
	}
}