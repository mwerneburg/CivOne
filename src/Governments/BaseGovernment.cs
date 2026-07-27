// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Concepts;
using CivOne.Governments;

namespace CivOne.Governments
{
	internal abstract class BaseGovernment : BaseConcept, IGovernment
	{
		public byte Id { get; private set; }
		public string NameAdjective { get; private set; }
		public IAdvance? RequiredTech { get; private set; }
		public int CorruptionMultiplier { get; protected set; }

		// ── government rules as data ─────────────────────────────────────────────
		// These were `government is Communism` / `Player.RepublicDemocratic` style
		// type tests scattered through City.cs and Player.cs. Expressing them as
		// properties means a new government — or a rebalanced existing one — is a
		// matter of setting numbers here rather than hunting down every special
		// case, and it lets the AI reason about a government instead of matching
		// on its type. Defaults reproduce the pre-refactor behaviour.

		// Tile output: Anarchy and Despotism dock 1 from any tile producing 3+.
		public bool TilePenalty { get; protected set; }

		// Extra trade on roaded grass/plains, ocean and river (Republic, Democracy).
		public int TradeBonus { get; protected set; }

		// Extra trade from special resources on Jungle/Mountains: 0 under Anarchy
		// and Despotism, 1 under Monarchy and Communism, 2 under Republic and
		// Democracy — the old MonarchyCommunist / RepublicDemocratic split.
		public int SpecialResourceTradeBonus { get; protected set; }

		// Food per settler per turn. Cheaper under the authoritarian governments.
		public int SettlerFoodCost { get; protected set; } = 2;

		// Units a city supports free of shield upkeep. -1 means "as many as the city
		// is large", the Anarchy/Despotism model.
		//
		// This was a cliff: the primitive governments supported SIZE units free and
		// every later government supported NONE, so a size-8 city went from 8 free
		// units to 0 the turn it changed government. Shield income turns negative,
		// and City.NewTurn disbands the furthest unit every turn until it balances —
		// so a civ with an army in the field loses that army for modernising. Civ 1
		// grants Monarchy and Communism 3 free units each; that is restored here.
		public int FreeUnitSupport { get; protected set; }

		// Garrisoned military units keep citizens content (max 3). The classic tool
		// for holding a large authoritarian city together.
		public bool MartialLaw { get; protected set; } = true;

		// Unhappiness per military unit away from home: 0 none, 1 Republic,
		// 2 Democracy. Mutually exclusive with MartialLaw in practice.
		public int WarWeariness { get; protected set; }

		// Percentage bonus to a city's SCIENCE output. Communism's distinguishing
		// feature: it is excluded from the trade bonus the later governments get
		// (TradeBonus stays 0), and instead converts what commerce it has into
		// research far more efficiently — a state that pours everything into
		// science and pays for it in commerce.
		public int ScienceBonus { get; protected set; }

		// Sustained civil disorder topples the government into Anarchy (Republic,
		// Democracy). The authoritarian governments simply endure the riot.
		public bool CollapsesInDisorder { get; protected set; }

		// "We Love the King Day" grants a free growth (or a Caravan when the city
		// cannot grow) rather than the celebration's default effect.
		public bool CelebrationGrowsCity { get; protected set; }

		// Communism: corruption uses a flat distance instead of real distance from
		// the palace, and a Palace halves it outright.
		public int? FixedCorruptionDistance { get; protected set; }
		public bool PalaceHalvesCorruption { get; protected set; }

		internal BaseGovernment(byte id, string name, IAdvance? requiredTech = null)
		{
			Id = id;
			Name = name;
			NameAdjective = name;
			RequiredTech = requiredTech;
		}
		
		internal BaseGovernment(byte id, string name, string nameAdjective, IAdvance? requiredTech = null)
		{
			Id = id;
			Name = name;
			NameAdjective = nameAdjective;
			RequiredTech = requiredTech;
		}
	}
}