// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Concepts
{
	// Iron / Coal / Oil shipped without a reference entry. A player who builds
	// a Factory for half again the shields deserves to be able to find out why.
	//
	// Kept exact against Game.ResourceAt, Game.RequiredResource and
	// Game.HasResource: deposits are derived from existing map specials (no new
	// map state), possession is a worked tile OR a camp, and the penalty is
	// +50% shields — a tax, never a wall. Ancient and medieval production is
	// untouched on purpose, so nobody's 3000 BC start is ruined.
	internal class StrategicResources : BaseConcept
	{
		private static readonly string[] _page1 =
		{
			"IRON, COAL and OIL lie in the",
			"special resources of the land.",
			"",
			"IRON in the mountains.",
			"COAL in the hills.",
			"OIL in desert and wetland.",
			"",
			"You hold a deposit when one of",
			"your cities works its tile, or",
			"when you keep a camp on it.",
			"",
			"WITHOUT THE MATERIAL, the work",
			"costs half again as much. Iron",
			"for cannon, coal for factories,",
			"oil for the engines.",
			"",
			"Nothing is ever unbuildable. It",
			"is a tax, not a wall.",
		};

		private static readonly string[] _page2 =
		{
			"Exactly what wants what:",
			"",
			"IRON: Cannon, Artillery,",
			"Ironclad.",
			"COAL: Factory, Power Plant.",
			"OIL: Armor, Fighter, Bomber,",
			"Cruiser, Battleship, Submarine,",
			"Carrier, Transport.",
			"",
			"The penalty falls only on the",
			"industrial age — no ancient start",
			"is spoiled by the ground it was",
			"given.",
			"",
			"Camps change hands by occupation:",
			"stand a unit on a rival's camp",
			"and it is yours. Flags on mines,",
			"not ashes.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public StrategicResources()
		{
			Name = "Strategic Resources";
		}
	}
}
