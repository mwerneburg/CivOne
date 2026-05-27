// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;

namespace CivOne
{
	/// <summary>
	/// Identifies a CivOne plugin assembly. Implement this interface alongside one or more
	/// <see cref="IModification"/> subclasses, then build the assembly and drop it in the
	/// plugins directory — CivOne discovers it via reflection at startup.
	/// </summary>
	public interface IPlugin
	{
		/// <summary>Display name of the plugin.</summary>
		string Name { get; }
		/// <summary>Author's name.</summary>
		string Author { get; }
		/// <summary>Version string.</summary>
		string Version { get; }
	}
}