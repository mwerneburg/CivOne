#nullable enable
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
