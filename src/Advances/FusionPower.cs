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
	internal class FusionPower : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"FUSION POWER binds light atoms",
			"together and takes the energy of",
			"a star.",
			"",
			"Allows FUSION INFANTRY, the HOVER",
			"TANK and the FUSION CORE.",
		};

		private static readonly string[] _page2 =
		{
			"It also ends the risk of a NUCLEAR",
			"PLANT melting down anywhere in",
			"your empire.",
			"",
			"Beyond it the tree stops being",
			"history and starts being a",
			"question.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public FusionPower() : base(7, 1, 0, Advance.NuclearPower, Advance.SuperConductor)
		{
			Name = "Fusion Power";
			Type = Advance.FusionPower;
		}
	}
}