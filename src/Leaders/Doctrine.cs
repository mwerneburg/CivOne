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
	// What a leader BELIEVES, as numbers the AI can reason with.
	//
	// The three personality enums (Development / Aggression / Militarism) set a
	// leader's targets, but every AI then measured those targets against the same
	// hardcoded thresholds — so when the map filled, every civ crossed the same
	// line within a few turns of each other. Whole games showed the entire field
	// peaking and crashing in chorus, which reads as machinery rather than as
	// history.
	//
	// A doctrine gives each leader their own thresholds. Two leaders with identical
	// enums still differ, because the defaults carry a deterministic per-leader
	// offset derived from the name — the same leader always plays the same way, but
	// no two of them flip stance on the same turn. Thematic doctrines (a state that
	// pours everything into research, one that will not start wars) then sit on top
	// as explicit overrides rather than as new special cases in the AI.
	public sealed class Doctrine
	{
		// Multiplies the leader's city target. Above 1 colonises harder for longer.
		public double ExpansionAppetite { get; set; } = 1.0;

		// Fraction of cities that must be unhappy before dropping everything to
		// consolidate. The AI used a flat "more than half"; this is that number.
		public double UnrestTolerance { get; set; } = 0.5;

		// Research preference, -100..+100. Shifts the tax/science slider target:
		// positive accepts a thinner treasury to keep laboratories funded.
		public int ScienceBias { get; set; }

		// Appetite for opening hostilities. Below 1 needs a clearer advantage
		// before turning aggressive; above 1 goes looking for the fight.
		public double WarAppetite { get; set; } = 1.0;

		// Deterministic per-leader spread, so identically-configured leaders still
		// diverge. Hashed from the name: stable across runs and machines (unlike
		// string.GetHashCode, which is randomised per process since .NET Core).
		private static double Spread(string name, int salt, double magnitude)
		{
			unchecked
			{
				int h = 17 + salt;
				foreach (char c in name ?? string.Empty) h = (h * 31) + c;
				// Map to [-1, 1] then scale.
				double unit = ((h & 0x7FFFFFFF) % 2001 - 1000) / 1000.0;
				return unit * magnitude;
			}
		}

		// Default doctrine for a leader who has not declared one: derived from the
		// existing personality enums so behaviour stays close to what it was, plus
		// the per-leader spread that breaks the chorus.
		public static Doctrine FromTraits(string name, DevelopmentLevel development,
		                                  AggressionLevel aggression, MilitarismLevel militarism)
		{
			double appetite = development switch
			{
				DevelopmentLevel.Expansionistic => 1.15,
				DevelopmentLevel.Perfectionist  => 0.80,
				_                               => 1.0,
			};
			double war = aggression switch
			{
				AggressionLevel.Aggressive => 1.25,
				AggressionLevel.Friendly   => 0.75,
				_                          => 1.0,
			};
			if (militarism == MilitarismLevel.Militaristic) war += 0.15;
			else if (militarism == MilitarismLevel.Civilized) war -= 0.15;

			// A perfectionist tolerates less unrest before consolidating; an
			// expansionist pushes on through it.
			double unrest = development switch
			{
				DevelopmentLevel.Expansionistic => 0.60,
				DevelopmentLevel.Perfectionist  => 0.40,
				_                               => 0.50,
			};

			return new Doctrine
			{
				ExpansionAppetite = appetite + Spread(name, 1, 0.12),
				UnrestTolerance   = unrest   + Spread(name, 2, 0.10),
				WarAppetite       = war      + Spread(name, 3, 0.15),
				ScienceBias       = (int)Spread(name, 4, 10),
			};
		}
	}
}
