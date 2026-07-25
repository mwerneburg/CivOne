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
	// Reveals the player's continent and establishes one embassy on it.
	// Requires Writing (primary) + MapMaking (secondary).
	internal class MarcoPoloVoyage : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"MARCO POLO'S VOYAGE opens your own",
			"continent to the map and plants an",
			"embassy upon it.",
			"",
			"Courts and cities you had never",
			"seen are suddenly known to you.",
		};

		private static readonly string[] _page2 =
		{
			"Requires WRITING and MAP MAKING.",
			"",
			"Knowledge of your neighbours lets",
			"you trade, treat, or prepare for",
			"war from a position of sight",
			"rather than blindness.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public MarcoPoloVoyage() : base(20)
		{
			Name = "Marco Polo's Voyage";
			RequiredTech = new Writing();
			ObsoleteTech = null;
			SetSmallIcon(4, 6);
			Type = Wonder.MarcoPoloVoyage;
		}
	}
}
