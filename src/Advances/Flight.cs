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
	internal class Flight : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"FLIGHT lifts machines off the",
			"ground and gives war a third",
			"dimension.",
			"",
			"Allows the FIGHTER.",
		};

		private static readonly string[] _page2 =
		{
			"Aircraft must return to a city or",
			"CARRIER before their fuel runs",
			"out. Count the moves home before",
			"you attack.",
			"",
			"Only a fighter can intercept an",
			"enemy bomber.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Flight() : base(3, 1, 0, Advance.Combustion, Advance.Physics)
		{
			Name = "Flight";
			Type = Advance.Flight;
		}
	}
}