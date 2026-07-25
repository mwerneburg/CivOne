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
	internal class SteamEngine : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"THE STEAM ENGINE turns heat into",
			"motion, and work is no longer",
			"limited by muscle or wind.",
			"",
			"Allows the IRONCLAD.",
		};

		private static readonly string[] _page2 =
		{
			"The ironclad sinks any wooden",
			"fleet afloat. For a short age the",
			"civilization that has them owns",
			"the sea entirely.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SteamEngine() : base(6, 1, 2, Advance.Physics, Advance.Invention)
		{
			Name = "Steam Engine";
			Type = Advance.SteamEngine;
		}
	}
}