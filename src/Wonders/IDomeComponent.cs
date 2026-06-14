#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Wonders
{
	// Marker interface for the five collaborative planetary defence dome components.
	// Used to enforce mutual exclusivity with the Alpha Centauri space-race path.
	internal interface IDomeComponent : IWonder { }
}
