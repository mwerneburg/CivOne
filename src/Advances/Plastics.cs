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
	internal class Plastics : BaseAdvance
	{
		private static readonly string[] _page1 =
		{
			"PLASTICS give materials shaped to",
			"purpose rather than found in the",
			"ground.",
			"",
			"Allows the SS COMPONENT.",
		};

		private static readonly string[] _page2 =
		{
			"Space components provide a ship's",
			"propulsion and fuel. More of them",
			"mean a shorter voyage — and the",
			"race is won by arrival, not by",
			"launch.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Plastics() : base(4, 0, 0, Advance.Refining, Advance.SpaceFlight)
		{
			Name = "Plastics";
			Type = Advance.Plastics;
		}
	}
}