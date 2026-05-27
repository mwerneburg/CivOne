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
	/// AI leader military preference. Militaristic leaders build more units and attack sooner;
	/// Civilized leaders prioritize infrastructure and expansion over combat.
	/// </summary>
	public enum MilitarismLevel
	{
		Civilized = 0,
		Normal = 1,
		Militaristic = 2
	}
}