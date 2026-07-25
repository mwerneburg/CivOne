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
	// The Manhattan Project's curse. Something very old sleeps under the sea
	// floor, and the first nuclear detonation is a dinner bell. Barbarian-owned,
	// never producible; spawns veteran beside the detonator's largest port
	// (Game.AwakenGozira) and walks inland on ordinary barbarian AI. Immune to
	// nuclear weapons — radiation is a meal, not a wound (BaseUnit detonation
	// spares it) — it must be put down conventionally.
	internal class Gozira : BaseUnitLand
	{
		public override bool IgnoresTerrainCost => true; // terrain does not slow it; nothing does

		private static readonly string[] _page1 =
		{
			"GOZIRA is no soldier but a",
			"catastrophe — a titan roused from",
			"the deep by the splitting of the",
			"atom.",
			"",
			"It cannot be built. It comes when",
			"the MANHATTAN PROJECT is raised.",
		};

		private static readonly string[] _page2 =
		{
			"The beast rampages across the",
			"land, smashing units and cities",
			"in its path until, at great cost,",
			"it is finally brought down.",
			"",
			"Some powers are not meant to be",
			"unleashed.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Gozira() : base(20, 30, 24, 1)   // never priced for sale; attack 30, defense 24, move 1
		{
			Type = UnitType.Gozira;
			Name = "Gozira";
			RequiredTech = null;
			ObsoleteTech = null;
			SetIcon('D', 0, 1);   // ponytail: wears the Armor sprite until bespoke kaiju art exists
		}
	}
}
