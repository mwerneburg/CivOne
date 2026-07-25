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
	internal class Rocketry : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ROCKETRY builds engines that carry",
			"their own air and need none.",
			"",
			"Allows the SAM BATTERY and, with",
			"THE MANHATTAN PROJECT, the NUCLEAR",
			"missile.",
		};

		private static readonly string[] _page2 =
		{
			"The same engine that delivers a",
			"warhead across the world will",
			"shortly carry colonists away from",
			"it.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Rocketry() : base(8, 0, 1, Advance.AdvancedFlight, Advance.Electronics)
		{
			Name = "Rocketry";
			Type = Advance.Rocketry;
		}
	}
}