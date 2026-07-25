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
	internal class SpaceFlight : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"SPACE FLIGHT leaves the atmosphere",
			"altogether.",
			"",
			"Allows the SS STRUCTURAL, THE",
			"APOLLO PROGRAM and the SOUTH POLE",
			"EXPEDITION.",
		};

		private static readonly string[] _page2 =
		{
			"The Apollo Program lets every",
			"civilization begin building a",
			"SPACESHIP. Reaching ALPHA CENTAURI",
			"first wins the SPACE RACE.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public SpaceFlight() : base(4, 2, 2, Advance.Computers, Advance.Rocketry)
		{
			Name = "Space Flight";
			Type = Advance.SpaceFlight;
		}
	}
}