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
	internal class HooverDam : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"HOOVER DAM harnesses a continent's",
			"rivers for power.",
			"",
			"Every city on the SAME CONTINENT",
			"gains the benefit of a POWER",
			"PLANT, boosting production.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ELECTRONICS.",
			"",
			"One wonder electrifies a whole",
			"continent — and unlike a power",
			"plant, it makes no pollution.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public HooverDam() : base(60)
		{
			Name = "Hoover Dam";
			RequiredTech = new Electronics();
			ObsoleteTech = null;
			SetSmallIcon(7, 0);
			Type = Wonder.HooverDam;
		}
	}
}