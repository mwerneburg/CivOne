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
	internal class ManhattanProject : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The MANHATTAN PROJECT splits the",
			"atom for war.",
			"",
			"Once it is built, ANY civilization",
			"may construct NUCLEAR weapons.",
		};

		private static readonly string[] _page2 =
		{
			"Requires NUCLEAR FISSION.",
			"",
			"The bomb is loosed upon the world",
			"for all, not just its maker.",
			"",
			"And some say the blast wakes",
			"something worse — GOZIRA.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public ManhattanProject() : base(60)
		{
			Name = "Manhattan Project";
			RequiredTech = new NuclearFission();
			ObsoleteTech = null;
			SetSmallIcon(7, 2);
			Type = Wonder.ManhattanProject;
		}
	}
}