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
	internal class Colossus : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The COLOSSUS towers over its",
			"harbour, drawing merchants from",
			"every sea.",
			"",
			"Each tile worked by its city that",
			"already earns TRADE earns +1 more.",
		};

		private static readonly string[] _page2 =
		{
			"Requires BRONZE WORKING.",
			"",
			"Raise it in a coastal city rich in",
			"ocean and river tiles to make a",
			"great center of commerce.",
			"",
			"FLIGHT eventually makes it",
			"obsolete.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Colossus() : base(20)
		{
			Name = "Colossus";
			RequiredTech = new BronzeWorking();
			ObsoleteTech = new Electricity();
			SetSmallIcon(4, 3);
			Type = Wonder.Colossus;
		}
	}
}