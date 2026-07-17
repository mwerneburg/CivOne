// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Enums
{
	/// <summary>
	/// Strategic resources, derived from existing special tiles (Game.ResourceAt):
	/// Iron on Mountains specials, Coal on Hills specials, Oil on Desert/Swamp
	/// specials. Possession (worked tile or owned camp) soft-gates industrial
	/// production: without the material, shields cost +50% (City.ProductionCost).
	/// Planned expansion: Copper (electronics), luxury resources, and Salt —
	/// the mineral that trades like a luxury; millennia of caravans agree.
	/// </summary>
	public enum StrategicResource
	{
		None = 0,
		Iron = 1,
		Coal = 2,
		Oil  = 3,
	}
}
