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
	internal class HangingGardens : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The HANGING GARDENS are a wonder",
			"of green amid the city stone.",
			"",
			"Every city in your empire gains",
			"HAPPINESS, easing unrest across",
			"the realm.",
		};

		private static readonly string[] _page2 =
		{
			"Requires POTTERY.",
			"",
			"An early happiness wonder that",
			"buys room to grow before Temples",
			"and Colosseums are built.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public HangingGardens() : base(30)
		{
			Name = "Hanging Gardens";
			RequiredTech = new Pottery();
			ObsoleteTech = new Invention();
			SetSmallIcon(4, 2);
			Type = Wonder.HangingGardens;
		}
	}
}