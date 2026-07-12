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
	// The Thing does not have a leader; it has consensus at the cellular level.
	// The class is named for the organism so it doesn't collide with the
	// Civilizations.TheThing faction class — the display name is the same.
	internal class TheOrganism : BaseLeader
	{
		protected override Leader Leader => Leader.TheOrganism;

		public TheOrganism() : base("The Thing")
		{
			Aggression  = AggressionLevel.Aggressive;
			Development = DevelopmentLevel.Expansionistic;
			Militarism  = MilitarismLevel.Militaristic;
		}
	}
}
