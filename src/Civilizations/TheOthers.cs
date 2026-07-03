// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;
using CivOne.Leaders;

namespace CivOne.Civilizations
{
	// The Owners — ancient cephalopod sector power; humanity is their escaped cargo.
	// They join mid-game via ExecuteOwnersLanding (Game.cs) when the Owners archetype
	// invades an undomed world. They seize existing cities rather than founding their
	// own, so these city names only appear if they ever build a settlement; they are
	// ledger entries, because that is what a world is to them.
	internal class TheOthers : BaseCivilization<TheRegistry>
	{
		public TheOthers() : base(Civilization.TheOthers, "Other", "Others")
		{
			CityNames = new string[]
			{
				"Intake",
				"Manifest",
				"Custody",
				"Ledger",
				"Census",
				"Tally",
				"Docket",
				"Inventory",
				"Recovery",
				"Storage",
				"Transit",
				"Account",
				"Audit",
				"Archive",
				"Holding",
				"Consignment",
			};
		}
	}
}
