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
	internal class GreatWall : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The GREAT WALL raises a rampart",
			"across your whole realm.",
			"",
			"Your cities defend as if all had",
			"CITY WALLS — a strong bonus",
			"against attackers.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MASONRY.",
			"",
			"Especially powerful against early",
			"aggressors and barbarians.",
			"",
			"Its worth crumbles once METALLURGY",
			"brings the cannon.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public GreatWall() : base(30)
		{
			Name = "Great Wall";
			RequiredTech = new Masonry();
			ObsoleteTech = new Gunpowder();
			SetSmallIcon(5, 2);
			Type = Wonder.GreatWall;
		}
	}
}