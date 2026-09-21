// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Wonders
{
	// The orbital that takes the Interstellar Probe's place at SPACE FLIGHT.
	//
	// NOT a cursed wonder: there is no Settings.CursedWonders roll and no one-in-four.
	// Starlab's quality is DRAWN FROM THE BUILDER, by the same rubric the visitors are
	// judged on (Game.AssessCharacter) — a free, peaceful, unpolluted civilization gets
	// the station its founders described, and a retrograde one gets a free port. That is
	// why page 1's warning is about the builders rather than about luck, and why page 2
	// states the rule outright: an outcome you can steer is only interesting if you know
	// the wheel is there.
	internal class Starlab : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"STARLAB is a city in the sky: a ring",
			"of glass and light where no nation's",
			"law runs and every hand is equal.",
			"",
			"Its telescopes read the deep sky. Its",
			"weather eyes read the Earth, and warn",
			"the coasts days before a storm turns",
			"toward them.",
			"",
			"A station is built by the people who",
			"build it, and it keeps their habits",
			"long after the ribbon is cut.",
		};

		private static readonly string[] _page2 =
		{
			"Requires SPACE FLIGHT.",
			"",
			"Trade rises in every city you hold,",
			"and your science with it.",
			"",
			"Its forecasts blunt the worst storms",
			"empire-wide: a CATASTROPHIC hurricane",
			"lands as a MAJOR one.",
			"",
			"What is built depends on who builds",
			"it. A free, peaceful people breathing",
			"clean air raise the station they",
			"intended. A polluted despotism at war",
			"raises a free port instead: casinos,",
			"smugglers, and CORRUPTION in every",
			"city it holds.",
		};

		// What the intended station is worth, empire-wide. Both are percentages rather than
		// the Colossus's flat +1 a tile: that is a one-city wonder, and the same rule applied
		// to every city a civilization holds would dwarf everything else on the board.
		//
		// Trade lands on RawTrade, BEFORE corruption, so the free port's graft eats into the
		// same money the good station would have made. Science is the Internet's 25% — the
		// station is a telescope before it is anything else.
		internal const double TradeBonus   = 0.15;
		internal const double ScienceBonus = 0.25;

		// And what the free port costs, in every city its owner holds. Half the Greys' rate
		// (RawTrade / 5, one city) because this one is empire-wide and permanent: there is no
		// evicting it, the station is yours for the rest of the game.
		internal const int FreePortSkimDivisor = 10;

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Starlab() : base(50)
		{
			Name         = "Starlab";
			RequiredTech = new SpaceFlight();
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.Starlab;
		}
	}
}
