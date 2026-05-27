// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne
{
	/// <summary>
	/// Marker interface for all modification types. Implement one of the concrete base classes
	/// (<see cref="Units.UnitModification"/>, <see cref="Leaders.LeaderModification"/>,
	/// <see cref="Civilizations.CivilizationModification"/>,
	/// <see cref="UserInterface.MenuModification"/>) rather than this interface directly.
	/// CivOne discovers implementations via reflection at startup.
	/// </summary>
	public interface IModification
	{
	}
}