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
	// The revolution that argued instead of the one that shot: the ideologues
	// prevailing over the bank robbers. A state that treats research as its
	// reason for existing, will not start a war, and fights like a cornered
	// animal when one is started on it.
	//
	// The first leader to declare an explicit Doctrine rather than inherit one
	// derived from the personality enums — which is the whole point of the
	// doctrine layer: character as numbers, not as new branches in the AI.
	public class Trotsky : BaseLeader
	{
		protected override Leader Leader => Leader.Trotsky;

		private readonly Doctrine _doctrine = new Doctrine
		{
			// Everything into the laboratories, and a thin treasury accepted as the
			// price. Communism's +50% science compounds this (Governments/Communism).
			ScienceBias = 70,

			// Will not open hostilities: needs an overwhelming edge before it ever
			// turns on a neighbour. Being ATTACKED still flips it to Militarize —
			// that branch fires on war existing, not on appetite for it.
			WarAppetite = 0.45,

			// Fewer, better cities. Permanent revolution abroad, consolidation at
			// home; it would rather deepen a province than plant another.
			ExpansionAppetite = 0.80,

			// A workers' state cannot shrug at its workers rioting: it drops
			// everything to address unrest far earlier than its rivals.
			UnrestTolerance = 0.30,
		};

		public override Doctrine Doctrine => _doctrine;

		public Trotsky() : base("Leon Trotsky")
		{
			Aggression  = AggressionLevel.Friendly;
			Militarism  = MilitarismLevel.Civilized;
			Development = DevelopmentLevel.Perfectionist;
		}
	}
}
