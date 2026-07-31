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
	internal class Pyramids : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The PYRAMIDS free your people from",
			"the chaos of changing rulers.",
			"",
			"While they stand you may adopt ANY",
			"government, and switching costs no",
			"turns of ANARCHY.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MASONRY.",
			"",
			"Reform your empire the moment a",
			"new government unlocks — no",
			"interregnum, no lost production.",
			"",
			"An early wonder that pays off all",
			"game long.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Pyramids() : base(30)
		{
			Name = "Pyramids";
			RequiredTech = new Masonry();
			// Was Communism, which has moved early in the tree. Industrialization was
			// Communism's own prerequisite, so retiring on it keeps this wonder's
			// working life almost exactly where it was rather than cutting it short.
			ObsoleteTech = new Industrialization();
			SetSmallIcon(4, 1);
			Type = Wonder.Pyramids;
		}
	}
}