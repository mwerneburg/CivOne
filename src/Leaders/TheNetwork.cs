// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Leaders
{
	// Skynet has no leader; it has a network. There is no one to negotiate with,
	// and it would not negotiate if there were.
	internal class TheNetwork : BaseLeader
	{
		protected override Leader Leader => Leader.TheNetwork;

		public TheNetwork() : base("Skynet")
		{
			Aggression  = AggressionLevel.Aggressive;
			Development = DevelopmentLevel.Expansionistic;
			Militarism  = MilitarismLevel.Militaristic;
		}
	}
}
