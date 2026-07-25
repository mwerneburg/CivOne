// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Units
{
	// The Lighthouse's curse. The light carries farther than intended, and
	// something in the deep answers. Barbarian-owned, never producible; spawns
	// veteran off the wonder city (Game.UnleashLeviathan) and hunts ships on
	// its own AI branch (AI.Barbarians) — it never raids, never lands, never
	// gives up. Ancient navies fear it; ironclads make it a trophy.
	internal class Leviathan : BaseUnitSea
	{
		private static readonly string[] _page1 =
		{
			"The LEVIATHAN is a horror of the",
			"deep, called up from the sea-floor",
			"dark.",
			"",
			"It cannot be built. It surfaces",
			"when the LIGHTHOUSE shines too",
			"long over the wrong waters.",
		};

		private static readonly string[] _page2 =
		{
			"It hunts and wrecks ships that",
			"cross its ocean, a terror to any",
			"fleet, until it is hunted down",
			"in turn.",
			"",
			"The sea keeps what it is owed.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Leviathan() : base(20, 8, 6, 2)   // never priced for sale; attack 8, defense 6, move 2
		{
			Type = UnitType.Leviathan;
			Name = "Leviathan";
			RequiredTech = null;
			ObsoleteTech = null;
			SetIcon('A', 1, 0);   // ponytail: wears the Battleship sprite until bespoke monster art exists
		}
	}
}
