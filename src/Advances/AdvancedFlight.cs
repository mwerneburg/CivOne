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
	internal class AdvancedFlight : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"ADVANCED FLIGHT builds aircraft",
			"with the range and the load to",
			"matter strategically.",
			"",
			"Allows the BOMBER and the CARRIER.",
		};

		private static readonly string[] _page2 =
		{
			"Bombers empty a city but cannot",
			"take it; send ARMOR behind them.",
			"",
			"Carriers refuel aircraft at sea,",
			"putting any coast in the world",
			"within reach.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public AdvancedFlight() : base(2, 1, 0, Advance.Flight, Advance.Electricity)
		{
			Name = "Advanced Flight";
			Type = Advance.AdvancedFlight;
		}
	}
}