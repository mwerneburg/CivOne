#nullable enable
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
	internal class OlvirCouncil : BaseLeader
	{
		protected override Leader Leader => Leader.OlvirCouncil;

		public OlvirCouncil() : base("The Council")
		{
			Aggression  = AggressionLevel.Normal;
			Development = DevelopmentLevel.Expansionistic;
			Militarism  = MilitarismLevel.Normal;
		}
	}
}
