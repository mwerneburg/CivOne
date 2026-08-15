// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;

using CivOne.Governments;
using CivOne.Wonders;
using Gov = CivOne.Governments;
using UniversityBuilding = CivOne.Buildings.University;
using ObservatoryBuilding = CivOne.Buildings.Observatory;
using static CivOne.Enums.DevelopmentLevel;

namespace CivOne
{
	internal partial class AI
	{
		// ── strategic stance ───────────────────────────────────────────────────

		private enum StrategyStance { Expand, Develop, Militarize, Consolidate }

		// Test/diagnostic accessor: the stance this civ would take right now.
		internal string CurrentStanceName() => GetStance().ToString();

		// ── victory paths ─────────────────────────────────────────────────────────
		//
		// What this civ is TRYING to do, as opposed to what it is reacting to.
		//
		// Nothing in the AI decided anything before this. StrategyStance is reactive — it
		// answers "are we rioting, are we at war, is there room" — and every long-range build
		// fell out of accident. The spaceship is the clearest case: no code anywhere calls
		// Consider on an SS part, so ship parts were reachable ONLY through the last-resort
		// fallback that rolls over whatever a city has left. Measured over the 2200 AD run,
		// three AI civs launched full ships purely because their big cities had run out of
		// buildings, while the autoplayed human's ninety-five smaller ones always had another
		// Cathedral available and finished the game with four parts.
		//
		// A path is a civ's answer to "how do we intend to be remembered". It is deliberately
		// NOT a permission system: nothing below forbids anything. It reorders.
		internal enum VictoryPath { Endurance, Conquest, Commerce, Culture, Diaspora }

		// Sticky by construction. StrategyStance is re-derived every call and the comments
		// through this file are largely a history of what happens when a long-range signal
		// thrashes or latches. A path is re-examined rarely, and only a real shock — the SETI
		// signal arriving, the last rival falling — should move it before then.
		private const int PathReviewInterval = 50;
		private VictoryPath _path = VictoryPath.Endurance;
		// Not int.MinValue: the review test is `GameTurn - _pathChosenTurn >= interval`, and
		// on turn 0 that subtraction overflowed to a large NEGATIVE number, so the first
		// review never fired and every civ in the game held the default forever. One turn
		// past the interval is the honest way to say "never chosen".
		private int _pathChosenTurn = -PathReviewInterval - 1;
		private bool _pathSignalSeen;

		internal VictoryPath Path
		{
			get
			{
				bool shock = _pathSignalSeen != Game.Instance.SETISignalReceived;
				if (shock || Game.GameTurn - _pathChosenTurn >= PathReviewInterval)
				{
					_pathSignalSeen = Game.Instance.SETISignalReceived;
					_pathChosenTurn = (int)Game.GameTurn;
					_path = ChoosePath();
				}
				return _path;
			}
		}

		// What each civilization is TEMPERAMENTALLY inclined toward, authored rather than
		// derived. Doctrine's numbers gave every large empire the same answer — measured twice,
		// Commerce took four to six civs a run and Culture took NONE — so the world came out
		// uniform. This is the character layer on top.
		//
		// A BIAS, never an override: it is worth a bonus in the scoring below, so a civ still
		// abandons its inclination when circumstances say otherwise (no signal yet, nothing to
		// trade with, an army it cannot feed). Six civilizations are deliberately absent —
		// Egyptian, Ethiopian, Guarani, Lakota, Ottoman, Roman — and stay fully derived, so the
		// world keeps some emergent variety alongside the authored kind.
		//
		// Assignments are the project owner's, and frankly subjective; that is the point of
		// having them rather than a formula.
		private static VictoryPath? PreferredPathFor(ICivilization civ) => civ switch
		{
			Civilizations.Russian or Civilizations.Maori or Civilizations.Greek
				=> VictoryPath.Diaspora,

			Civilizations.Mongol or Civilizations.Zulu or Civilizations.Aztec
				or Civilizations.Haida or Civilizations.English
				=> VictoryPath.Conquest,

			Civilizations.Indian or Civilizations.Japanese or Civilizations.Persian
				or Civilizations.Khmer or Civilizations.Babylonian or Civilizations.Frank
				=> VictoryPath.Culture,

			Civilizations.Malian or Civilizations.Arab or Civilizations.Iroquois
				or Civilizations.Chinese
				=> VictoryPath.Commerce,

			_ => null,
		};

		// ── How big a ship to build ──────────────────────────────────────────────
		//
		// The hull requirement scales with the engines and module sets, so a MAXED ship needs
		// 51 structurals against a minimum ship's 15 — about 10,500 shields against 2,500.
		// Nothing used to choose that: Consider() takes the first entry per type and the
		// Diaspora block lists structural, component, module in that order, so cities built
		// structural to its cap, then components, and only reached modules at the very end —
		// by which point the requirement had climbed to 51. Measured across two finished runs:
		// the Chinese completed a maxed ship on turn 744 of 750, and the Japanese (40 of 51)
		// and Maori (42 of 51) simply ran out of game. A ship one structural short scores
		// exactly what no ship scores.
		//
		// So the civ picks the largest hull it can plausibly FINISH and stops there. Modules
		// stay last in the build order, which means the launch condition is met by the final
		// module and the ship launches at its target size rather than at the bare minimum.
		//
		// The horizon is a first guess. With the SETI storyline active the game has no fixed
		// end turn, so there is no deadline to measure against — 100 turns of current output
		// is a stand-in for "can we see the end of this from here", and wants a run to
		// calibrate like the other numbers in ChoosePath.
		private const int SpaceshipHorizonTurns = 100;

		// ...of which only a share ever reaches the ship. The first version budgeted the
		// empire's ENTIRE shield output for the horizon, which is not what any civ does: a
		// city building a ship part is not building a Factory, a settler or a defender, and
		// the ordinary production chain keeps winning most of those choices. Measured on the
		// run after the target shipped, three civs still aimed at the maximum hull and missed
		// it — Babylonians 48 of 51, Haida 37, Japanese 34 — while only three civs launched
		// at all, down from five. Budgeting a third is a guess in the right direction; the
		// error before was one of kind, not degree.
		private const int SpaceshipOutputShare = 3;

		// Returns the (component, module) hull this civ should aim for. Always at least the
		// launchable minimum: a civ on this path is trying to leave, and the smallest ship
		// that flies beats the biggest one that does not.
		internal (int component, int module) SpaceshipTarget()
		{
			byte me = Game.PlayerNumber(Player);
			int haveStruct = Game.Instance.SpaceshipStructural[me];
			int haveComp   = Game.Instance.SpaceshipComponent[me];
			int haveModule = Game.Instance.SpaceshipModule[me];

			// What the empire can put into a ship between now and the horizon. ShieldIncome is
			// net of unit upkeep and can be negative in a city carrying an army, so the floor
			// keeps one struggling city from cancelling out the rest.
			int perTurn = Player.Cities.Sum(c => Math.Max(0, c.ShieldIncome));
			int budget = perTurn * SpaceshipHorizonTurns / SpaceshipOutputShare;

			int structPrice = new SSStructural().Price * 10;
			int compPrice   = new SSComponent().Price * 10;
			int modPrice    = new SSModule().Price * 10;

			(int component, int module) best = (2, 3);
			for (int engines = 1; engines * 2 <= Game.MAX_SS_COMPONENT; engines++)
			for (int modSets = 1; modSets * 3 <= Game.MAX_SS_MODULE; modSets++)
			{
				int comp = engines * 2, module = modSets * 3;
				int cost = Math.Max(0, Game.SpaceshipStructuresNeeded(comp, module) - haveStruct) * structPrice
				         + Math.Max(0, comp   - haveComp)   * compPrice
				         + Math.Max(0, module - haveModule) * modPrice;
				if (cost > budget) continue;
				// Bigger is better, and modules outrank engines: score is habitation modules
				// times the success chance, so a module set is worth more than an engine.
				if (module * 2 + comp > best.module * 2 + best.component) best = (comp, module);
			}
			return best;
		}

		// Is this ship part still wanted, or is the target hull already that big?
		//
		// Applied to the last-resort production fallback as well as the Diaspora path,
		// because the fallback is how a civ NOT on that path builds a ship at all — a
		// built-out city with nothing left to make picks parts until the hull is maxed.
		// That is how the Chinese, a Commerce civ, completed a 51/16/12 ship on turn 744 of
		// 750. Without this the target binds only on the civs that are already trying.
		private bool WantsSpaceshipPart(IProduction production)
		{
			if (production is not ISpaceShip) return true;
			(int targetComp, int targetModule) = SpaceshipTarget();
			byte me = Game.PlayerNumber(Player);
			if (production is SSStructural)
				return Game.Instance.SpaceshipStructural[me] < Game.SpaceshipStructuresNeeded(targetComp, targetModule);
			if (production is SSComponent) return Game.Instance.SpaceshipComponent[me] < targetComp;
			if (production is SSModule)    return Game.Instance.SpaceshipModule[me] < targetModule;
			return true;
		}

		// Scored from the leader's temperament first and their circumstances second, so two
		// civs in the same position still choose differently — which is the whole point of
		// Doctrine's per-leader spread.
		private VictoryPath ChoosePath()
		{
			Leaders.Doctrine d = Leader.Doctrine;
			int cities = Player.Cities.Length;

			// Rescaled. The first version was WarAppetite * 100 plus up to +80, which put
			// Conquest between 45 and 231 while every other path sat between 30 and 80 —
			// measured across the whole roster at six cities. It was not a competitor, it was
			// a ceiling: any civ with a WarAppetite above ~0.7 chose Conquest whatever else it
			// was good at, and no authored preference could bridge a gap that wide. Same
			// ordering among warlike leaders, now on the same scale as its rivals.
			double conquest = 45 + d.WarAppetite * 25
			                + (Leader.Aggression == AggressionLevel.Aggressive ? 10 : 0)
			                + (Leader.Militarism == MilitarismLevel.Militaristic ? 10 : 0);

			// Space is a science ambition, and it is only a real ambition once the road to it
			// exists: before the signal there is no spaceship to build, so a would-be Diaspora
			// civ that scored highest here would spend the early game acting on a plan it
			// cannot execute. It reads as Endurance until the sky says otherwise.
			//
			// The city term is the correction the first version needed. Without it Diaspora was
			// scored purely on temperament, and it landed on the civs LEAST able to act on it —
			// measured across two runs, on Khmer at 16 cities and Aztecs at 10, while the big
			// empires took Commerce and then built ships by accident anyway. A space programme
			// is the most expensive thing in the game; wanting one is not enough.
			double diaspora = Game.Instance.SETISignalReceived
			                ? 55 + cities * 2 + d.ScienceBias * 1.2
			                : 0;

			// Commerce and Culture are the builder's paths, and both want a going concern
			// rather than a frontier. Weighted by what the civ has actually got, so a
			// ten-city trading nation reaches for Pax Mercatoria and a two-city one does not.
			//
			// They lean on ExpansionAppetite in opposite directions: commerce is a wide
			// empire's game — routes, partners, gross output — while a civ that never wanted to
			// be wide is playing for what it can make of what it holds.
			//
			// The FIRST version made that split too wide, and Culture became unreachable:
			// Commerce beat it unless ExpansionAppetite fell below 0.625, which almost no
			// leader does, and across two full runs Culture was chosen by exactly nobody. The
			// spread is narrower now and the bases are equal, so the authored preference above
			// is what actually decides between them — which is the right place for that choice
			// to live.
			double commerce = 60 + cities * 2 - d.WarAppetite * 30 + d.ExpansionAppetite * 15;
			double culture  = 60 + cities * 2 - d.WarAppetite * 30 + (1.0 - d.ExpansionAppetite) * 15
			                + (d.UnrestTolerance < 0.5 ? 10 : 0);

			// The default, and not a failure state: outlasting everyone to 2200 is a way to
			// win. Set to beat the builder paths for a civ with almost nothing, so nobody
			// commits to a trade empire from two cities in 3000 BC, and to lose to them once
			// there is something to build on.
			double endurance = 70;

			var scores = new (VictoryPath path, double score)[]
			{
				(VictoryPath.Conquest,  conquest),
				(VictoryPath.Diaspora,  diaspora),
				(VictoryPath.Commerce,  commerce),
				(VictoryPath.Culture,   culture),
				(VictoryPath.Endurance, endurance),
			};

			// An authored preference DECIDES, unless the path is structurally unavailable.
			//
			// This started as an additive bonus and it did not survive contact with the
			// numbers: a civ the owner wants playing for culture can have a WarAppetite of
			// 1.51 (the Franks do), and no bonus small enough to be called a bias could beat
			// that. Trying to tune one that could would have meant a number large enough to
			// decide every case anyway — an override wearing a bias costume.
			//
			// So it is honest about being a preference. The escape hatch is structural rather
			// than numerical: a path scoring zero is one the civ CANNOT take, which today
			// means Diaspora before the SETI signal — there is no spaceship to build, and no
			// amount of inclination conjures one. Everything reactive (rioting, at war,
			// under-defended) is StrategyStance's job and is decided long before this.
			//
			// The six unassigned civilizations fall through to the derived scoring below,
			// which is why the Conquest rescale above still matters.
			VictoryPath? prefer = PreferredPathFor(Player.Civilization);
			if (prefer is not null)
			{
				double preferred = scores.First(x => x.path == prefer.Value).score;
				if (preferred > 0) return prefer.Value;
			}

			VictoryPath best = VictoryPath.Endurance;
			double bestScore = double.MinValue;
			foreach ((VictoryPath path, double score) in scores)
				if (score > bestScore) { bestScore = score; best = path; }
			return best;
		}

		private StrategyStance GetStance()
		{
			var cities = Player.Cities;

			// Consolidate: Rep/Dem with unhappy majorities can't sustain expansion
			// Consolidate: a happiness crisis — drop expansion and build Temples/Colosseums/
			// Cathedrals (this stance front-loads them). Republics/Democracies feel unhappiness
			// early, so they consolidate on widespread discontent; the harsher governments only
			// once cities actually tip into disorder. The LuxuriesRate >= 4 clause keeps us in
			// Consolidate while ConsiderSliders is leaning on the luxury slider, so we keep
			// building happiness infrastructure until luxuries can wind back down toward science.
			// Unrest tolerance is per-leader (Doctrine): the flat "more than half"
			// meant every civ crossed into Consolidate within a turn or two of each
			// other once the map filled, so whole fields peaked and crashed together.
			double tolerance = Leader.Doctrine.UnrestTolerance;
			// The disorder test is proportional, with the old flat 2 as a floor. An
			// absolute "2 cities rioting" was written when empires held a handful of
			// cities; at 55 it is met essentially every turn, so every large civ sat in
			// Consolidate permanently — never expanding, never switching research
			// priorities, and building whatever the random fallback handed them. The
			// scoreboard showed it plainly: civs with 54 and 55 cities scoring less than
			// half of the one civ that still reached Expand.
			int disorderLimit = Math.Max(2, (int)(cities.Length * 0.15));
			if (cities.Length > 0 && (
			        (Player.RepublicDemocratic && cities.Count(c => c.UnhappyCitizens > 0) > cities.Length * tolerance)
			        || cities.Count(c => c.IsInDisorder) >= disorderLimit
			        || Player.LuxuriesRate >= 4))
				return StrategyStance.Consolidate;

			// Militarize: already at war.
			//
			// A stance-level "dormant war" cap was tried here — stand down when no enemy is
			// within 12 tiles and no offensive of our own is under way — and MEASURED to be
			// inert: in a completed 750-turn game every one of the nine live wars had an
			// enemy within 12 tiles (most within 1), because on a shared continent a rival's
			// CITY is permanently in range even when nothing is happening. It changed
			// nothing across three harness seeds. The war economy's real damage is done by
			// its consequences, so those are capped individually instead — see the
			// government cap in ConsiderGovernment and the wartime unit ceiling in the
			// production fallback. Do not re-add a proximity test here without new evidence.
			if (Game.Players.Any(p => p != Player && !p.IsDestroyed() && Player.IsAtWar(p)))
				return StrategyStance.Militarize;

			// Reconquest: the horde is sitting in cities that were OURS.
			//
			// Not the same thing as the barbarian-proximity clause that used to live below
			// and was removed — read that note before touching this. Proximity had no
			// expiry: a horde camped ten tiles away kept a civ on a war footing for the rest
			// of the game, and nothing the civ did could end it. THIS ends the moment the
			// cities are retaken or razed, which is the only reason it is safe to have.
			//
			// The gap it closes is a deadlock, not a preference. Attackers are Considered
			// only in Militarize, and attack targets are chosen only in Militarize — but
			// Militarize is reached (below) only by a civ that ALREADY out-guns a neighbour.
			// A civ that loses cities therefore can never build the army that would win them
			// back. Measured at turn 393: the Haudenosaunee down to three cities, holding six
			// Militia and a Phalanx — every one of them attack 1 — while barbarian Legions
			// and Knights sat in three cities whose OriginalOwner was still theirs. They had
			// The Wheel the whole time, so BestAttacker would have handed them a Chariot at
			// attack 4, an even match. They never built one, in any game, ever.
			//
			// Deliberately BARBARIAN-only, which is narrower than "held by anyone". A rival
			// holding one of our cities is already covered while the war lasts, by the clause
			// above; once peace is signed, re-arming forever over a city we have accepted
			// losing is exactly the no-expiry pathology — and AI wars here end in permanent
			// peace, so that state would never clear. Barbarians hold no diplomacy and never
			// sue for peace, so with them there is nothing to accept and no treaty to freeze.
			//
			// Placed ahead of armySaturated on purpose: being unable to afford an army is the
			// condition, so an army cap must not veto the answer.
			byte reconquestOwn = Game.PlayerNumber(Player);
			if (Game.GetCities().Any(c => c.Size > 0 && c.Owner == 0
			                           && c.OriginalOwner == reconquestOwn))
				return StrategyStance.Militarize;

			// Everything below this line is SPECULATIVE militarisation — nobody has
			// actually declared war on us. Those clauses have no expiry of their own: a
			// barbarian city that is never expelled, or a neighbour we merely out-gun,
			// holds a civ on a war footing for the rest of the game. And Militarize is
			// the most expensive stance to be wrong about — it is the only one that never
			// weights growth tech, it sets a tax target of 6 before the broke overlay
			// pushes it to 8, and it caps colonisation hard.
			//
			// So an army that is already large answers the question. Measured on a
			// 751-turn autoplayed island game: Japan sat in Militarize at peace with
			// nobody, holding 36 combat units across 10 cities — 13 of its 27 total
			// shields going to upkeep — on 18 advances, with no Granary or Aqueduct
			// anywhere. Three per city is a garrison plus a field force; past that,
			// another Legion is worth less than the Granary it displaces.
			//
			// A real war still overrides this: the at-war clause above returns before we
			// get here, so a besieged civ arms without limit.
			byte stanceOwn = Game.PlayerNumber(Player);
			bool armySaturated = cities.Length > 0
			    && Game.GetUnits().Count(u => u.Owner == stanceOwn
			        && (u.Role == UnitRole.LandAttack || u.Role == UnitRole.Defense)) > cities.Length * 3;

			// A barbarian city near our empire used to put the whole civ on a war footing.
			// It no longer does. Barbarians are a raiding nuisance, not a rival power: they
			// hold no diplomacy, they never sue for peace, and nothing ever expels them, so
			// the clause had no expiry — a horde camped ten tiles away kept an empire in
			// Militarize for the rest of the game. That is the stance which weights no
			// growth tech, targets a tax rate of 6, caps colonisation, and puts Monarchy at
			// the top of the government table. Japan finished a measured game in Monarchy on
			// 43 advances with Democracy researched, at war with nobody, held there by this.
			//
			// The response to barbarians belongs at the city that can see them, and it
			// already exists: a hostile within 3 tiles earns a second defender
			// (PlanProductionInto, `hostileNear`), stance regardless. Local threat, local
			// answer.

			// Militarize: aggressive/militaristic and at least as strong as a neighbour
			if (!armySaturated
			    && (Leader.Militarism == MilitarismLevel.Militaristic
			     || Leader.Aggression == AggressionLevel.Aggressive))
			{
				// WarAppetite sets how much of an edge this leader wants before turning
				// on a neighbour: below 1 they want a clear advantage, above 1 they
				// will pick a fight from parity or worse.
				int own = MilitaryScore(Player);
				double edge = 1.0 / Math.Max(0.25, Leader.Doctrine.WarAppetite);
				if (own > 0 && Game.Players.Any(p =>
				    p != Player && !p.IsDestroyed()
				    && IsNeighbor(p) && own >= MilitaryScore(p) * edge))
					return StrategyStance.Militarize;
			}

			// Expand: below the leader's preferred city count (scales with difficulty and map size).
			// mapScale uses WIDTH/80 (linear) rather than (W×H)/4000 (area) so Epic 320×200
			// produces scale=4, not 16. The area formula gave Normal-development leaders a
			// 99-city target on Epic — unreachable in practice — so every civ stayed in
			// Expand forever, never flipped research priorities to Trade/Currency/Banking,
			// never reached Republic, never escaped the Despotism tile penalty. The linear
			// scale matches the civ-separation knob in Game.NewGame.cs:32 (same source).
			// Expand only while there is somewhere to expand TO. Being under the city
			// target is not sufficient: a civ hemmed in by water is under it forever, so
			// it sat in Expand for the whole game — and Expand is precisely the wrong
			// posture for the situation. Everything a confined civ ought to be doing is
			// gated on Develop: worker settlers and irrigation (line 2088), the leaner
			// tax target that funds research, and the government preference, which in
			// Expand scores Monarchy 5 and Democracy 2 — actively steering an island
			// nation away from the constitution that suits it. Japan on Epic Earth holds
			// ~10 cities against a target of 26 and never once reached Develop.
			//
			// This changes PRIORITIES only, not permission: MayFoundCities is deliberately
			// independent of stance, so a settler that does find a site still founds on it.
			//
			// Path modulation, and ONLY here. Everything above this line is a civ answering a
			// situation — rioting, at war, out-gunned — and a plan does not get to override
			// any of it: a Diaspora civ whose cities are burning consolidates like anybody
			// else. This last choice is the one place where nothing is actually wrong, which
			// makes it the only place an ambition may legitimately speak.
			//
			// Expand is the frontier posture: settlers, roads, more towns. The builder paths
			// want the opposite — deepen what you hold — and Develop is where the AI funds
			// research, reaches for Republic and stops capping colonisation. So a civ with a
			// plan that lives in its cities skips Expand once it has a real base to build on.
			// Below that threshold it still expands, because four cities is not a trade empire
			// and a plan you cannot fund is not a plan.
			// "A real base" is measured against the civ's OWN target, not a fixed number.
			//
			// This was a constant 5, and a constant does not survive contact with an epic map:
			// CityTarget scales by Map.WIDTH/80, so a Normal leader on 320x200 Earth is aiming
			// at about 26 cities, and 5 is a beachhead rather than a base. The effect was total
			// rather than gradual, because settler production for a civ with three or more
			// cities lives inside `stance == Expand`: a deepening civ that crossed five cities
			// could never enter Expand again, so it never built another settler, so it never
			// grew. The only escape was the last-resort fallback, which fires when a city has
			// run out of everything else — and a large city being actively developed never
			// does.
			//
			// Measured on the Maori across three runs. Before the paths existed: 84 Expand
			// stances and 3 settlers built. After: zero and zero, frozen on 8 cities from turn
			// 313 to 749 while Whanganui grew to size 17 building Library, Aqueduct, Harbour,
			// Observatory, Hospital, Neural Lab, Mass Transit — never once a settler.
			//
			// Half the target keeps what the clause was for: a civ that is genuinely
			// established deepens instead of spreading, which is why the Babylonians build
			// temples at 20 cities rather than more towns. A civ at a third of its target is
			// not established, whatever it intends to be remembered for.
			int pathBaseCities = Math.Max(5, CityTarget() / 2);
			bool deepensRatherThanSpreads = Path is VictoryPath.Diaspora
			                                     or VictoryPath.Commerce
			                                     or VictoryPath.Culture;
			if (deepensRatherThanSpreads && cities.Length >= pathBaseCities)
				return StrategyStance.Develop;

			if (HasExpansionRoom()) return StrategyStance.Expand;

			return StrategyStance.Develop;
		}

		// The leader's preferred city count. Scales with map size and difficulty.
		private int CityTarget()
		{
			int mapScale = Math.Max(1, Map.WIDTH / 80);
			int baseTarget = Leader.Development == Expansionistic ? (9 * mapScale) + Game.Difficulty
			               : Leader.Development == Normal          ? (6 * mapScale) + Game.Difficulty
			               :                                         (4 * mapScale) + Game.Difficulty;
			// Doctrine scales it, so two Expansionistic leaders no longer stop
			// colonising on the same turn.
			return Math.Max(1, (int)Math.Round(baseTarget * Leader.Doctrine.ExpansionAppetite));
		}

		// May our settlers found cities right now?
		//
		// Deliberately NOT "are we in the Expand stance". Expand is the last branch of
		// GetStance, so being at war with anyone — or having two cities in disorder —
		// silently forbids colonisation, and AI civs are at war or unhappy almost
		// permanently. That is why settlers irrigated forever: 989 settlers built over
		// one 736-turn game founded 294 cities and logged 11,338 irrigation orders,
		// while whole continents sat empty. Whether there is somewhere worth settling
		// is a question about the map, not about this turn's mood.
		// Below the leader's city target, expansion is unconditional — that is what
		// broke the AI out of its one-city paralysis and it must not regress.
		//
		// PAST the target, founding becomes conditional on the empire being healthy
		// enough to deserve another city, and the bar rises the wider it already is.
		// Measured over a full game (2079 AD, 10 civs): score rank matched MEAN CITY
		// SIZE almost exactly, and city COUNT hardly mattered —
		//
		//     Germans   42.7 cities  size 7.6  ->  2820   (fewest cities, 1st)
		//     Indians   58.1 cities  size 4.3  ->  1669
		//     Persians  56.1 cities  size 2.5  ->   559   (most cities, 7th)
		//
		// So unrestricted founding was actively losing games. This gate is gradual
		// rather than a cliff: at the target it asks for size 4, at twice the target
		// for size 8. A civ whose cities are thriving keeps colonising; one whose
		// cities are starving stops and — because AI.Move routes settlers to
		// BestImproveSite when founding is off — puts those settlers on irrigation
		// instead, which is precisely what raises the sizes back up.
		internal bool MayFoundCities()
		{
			City[] cities = Player.Cities;
			int target = CityTarget();

			if (cities.Length < target) return true;
			if (!HasExpansionRoom()) return false;

			double meanSize = cities.Average(c => (double)c.Size);
			double over = (cities.Length - target) / (double)Math.Max(1, target);
			double required = 4.0 + (over * 4.0);
			return meanSize >= required;
		}

		// Cheap, per-turn-cached test: is there a foundable unclaimed tile reachable by
		// land near any of our cities? Land only (same continent), habitable, and at
		// least 3 tiles clear of every existing city. Early-exits on the first hit; tiles
		// near cities or on the wrong continent reject fast, so both "has room" and
		// "boxed in" cases stay cheap.
		private int _roomTurn = -1;
		private bool _roomCached;
		private bool HasExpansionRoom()
		{
			if (_roomTurn == (int)Game.GameTurn) return _roomCached;
			_roomTurn = (int)Game.GameTurn;
			_roomCached = false;

			// Reachability is established by walking the land, not by comparing continent
			// IDs. Every small island shares the "misc" bucket 15, so the old ID test told
			// an island civ that islets across open water were part of its own landmass:
			// room was reported forever, BoxedIn (its inverse) never became true, and the
			// Longboat — the one way off an archipelago — was never built. Japan sat on
			// seven cities for 127 turns while the test insisted it had somewhere to go.
			//
			// Flood fill is bounded to the same ±8 window the search already used, and the
			// whole result is cached per turn, so this is a few hundred tile visits per
			// city once a turn.
			int W = Map.WIDTH, H = Map.HEIGHT;
			int WrapDX(int a, int b)
			{
				int d = Math.Abs(a - b);
				return Math.Min(d, W - d);
			}

			foreach (City c in Player.Cities)
			{
				var seen  = new HashSet<int> { c.Y * W + c.X };
				var queue = new Queue<(int X, int Y)>();
				queue.Enqueue((c.X, c.Y));

				while (queue.Count > 0)
				{
					(int cx, int cy) = queue.Dequeue();
					for (int dy = -1; dy <= 1; dy++)
					for (int dx = -1; dx <= 1; dx++)
					{
						if (dx == 0 && dy == 0) continue;
						int tx = (cx + dx + W) % W;
						int ty = cy + dy;
						if (ty < 0 || ty >= H) continue;
						if (Math.Max(WrapDX(tx, c.X), Math.Abs(ty - c.Y)) > 8) continue;
						ITile step = Map[tx, ty];
						if (step is null || step.IsOcean) continue;   // the sea stops the walk
						if (!seen.Add(ty * W + tx)) continue;
						queue.Enqueue((tx, ty));
					}
				}

				foreach (int key in seen)
				{
					int tx = key % W, ty = key / W;
					if (Math.Max(WrapDX(tx, c.X), Math.Abs(ty - c.Y)) < 4) continue; // too close to be a new site
					ITile t = Map[tx, ty];
					if (t is null) continue;
					if (t.Type == Terrain.Mountains || t.Type == Terrain.Arctic) continue;
					if (t.City is not null) continue;
					// Must match BestSettleSite's bar exactly (>= 4 from every city). At < 3
					// this reported "room" on tiles no settler is ever permitted to settle,
					// so BoxedIn stayed false and the Longboat — the only way off an
					// archipelago — was never built.
					if (Game.GetCities().Any(cc => cc.Size > 0 && Common.DistanceToTile(cc.X, cc.Y, tx, ty) < 4)) continue;
					_roomCached = true;
					return true;
				}
			}
			return false;
		}

		// Is anyone in the world flying anything that a SAM Battery would answer?
		//
		// The question a SAM's value actually turns on. BaseUnit.DefendStrength consults the
		// battery in exactly one place — when the ATTACKER is UnitClass.Air — so with no
		// aircraft anywhere the building is inert, and the AI was buying it on Rocketry
		// alone. Deliberately world-wide rather than "a civ I am at war with": aircraft take
		// a long time to reach and wars here start faster than a SAM can be built, so waiting
		// for the war means waiting until it is too late.
		//
		// Own aircraft count. A civ that flies knows what aircraft do, and its rivals are
		// generally a few turns behind it on the same tech.
		//
		// Cached per turn: this scans every unit in the world and is asked once per city per
		// turn, which at 500 cities and 2,000 units is the kind of thing that shows up in the
		// late-game turn timings.
		private int _flyingTurn = -1;
		private bool _flyingCached;
		private bool SomebodyIsFlying()
		{
			if (_flyingTurn == (int)Game.GameTurn) return _flyingCached;
			_flyingTurn = (int)Game.GameTurn;
			_flyingCached = Game.GetUnits().Any(u => u is not null && u.Class == UnitClass.Air);
			return _flyingCached;
		}

		private bool IsNeighbor(Player enemy)
		{
			return Player.Cities.Any(oc =>
			    enemy.Cities.Any(ec =>
			        Common.DistanceToTile(oc.X, oc.Y, ec.X, ec.Y) <= 15));
		}

		// True when the human player has broken away from the pack: 2× the cities or 2× the
		// score of the strongest AI.  8-city floor on the city check so it doesn't fire in
		// the early expansion phase before civs have had a chance to settle.
		// The human the AI plays ASYMMETRICALLY against — null when nobody is at the controls.
		//
		// Three behaviours single the human out: diplomats put their cities at the head of the
		// espionage queue, ConsiderDiplomacy aims every demand (tribute, war threat, "cede this
		// city") at them and at nobody else, and HumanIsDominant steers attacks their way. That
		// is difficulty design for a played game. In an unattended run it means thirteen civs
		// dogpile one, and the human slot was the ONLY civ to collapse in four consecutive
		// autoplay games — 10 cities at turn 100 down to none by turn 700 while every other civ
		// grew monotonically.
		//
		// Read live rather than latched at load: a play-tester who stops autopilot to look
		// around is the human again on the very next decision, and hands back on resume.
		// Settings.Autopilot is session-only and always starts off (Settings.cs:237).
		private static Player? HumanOpponent => Settings.Instance.Autopilot ? null : Human;

		private bool HumanIsDominant()
		{
			Player? human = HumanOpponent;
			if (human is null || human.IsDestroyed()) return false;

			Player[] aiPlayers = Game.Players
			    .Where(p => Game.PlayerNumber(p) != 0 && !p.IsDestroyed() && p != human)
			    .ToArray();
			if (aiPlayers.Length == 0) return false;

			int humanCities = human.Cities.Length;
			int humanScore  = Math.Max(1, human.Score);
			int bestAICities = aiPlayers.Max(p => p.Cities.Length);
			int bestAIScore  = aiPlayers.Max(p => Math.Max(1, p.Score));

			if (humanCities >= 8 && humanCities > bestAICities * 2) return true;
			if (humanScore > bestAIScore * 2) return true;
			return false;
		}

		private int MilitaryScore(Player player)
		{
			byte num = Game.PlayerNumber(player);
			return Game.GetUnits()
			           .Where(u => u.Owner == num && u.Role == UnitRole.LandAttack)
			           .Sum(u => u.Attack + u.Defense);
		}

		// ── tax/science slider management ─────────────────────────────────────

		// The most a civ may put into luxuries, leaving at least this much trade on
		// research. Previously the ceiling was simply "everything that isn't tax", and a
		// large empire spent its whole economy on a slider that plainly wasn't working:
		// at 8/10 luxuries the Romans still had 15 cities in disorder and the Mongols 32.
		// Buying nothing for two points of trade is a poor deal, and stopping research
		// outright is what left the whole field in Despotism and Monarchy in 1890 AD.
		private const int MinScienceTrade = 2;

		// Above this share of cities in disorder the research reserve is waived entirely:
		// the civ is fighting for its life and the luxury slider is the only fast lever
		// it has. Reserving trade for science below the threshold is what stops a large,
		// mostly-content empire from spending its whole economy on a handful of angry
		// cities; applying it during a real crisis is what made small civs stillborn —
		// a one-city nation is at 100% unrest the moment that city riots, and capping it
		// at 5 luxuries left it rioting forever with no way back.
		private const double CrisisUnrest = 0.40;

		// ...but it is never waived to NOTHING. A crisis reserve of 0 let luxuries take
		// the last point of trade, and a civ that reaches science rate 0 stops advancing
		// permanently — which is worse than the riot it was buying off, because the
		// happiness buildings that end the riot for good are themselves behind research.
		// Measured on a turn-630 autoplayed save: Japan sat at tax 2 / luxuries 8 /
		// science 0, and at 2080 AD still had no Pottery, no Granary and no Aqueduct, its
		// largest city pinned at exactly 7 — the Aqueduct ceiling. The Mongols (45
		// cities), the Olvir and the Babylonians were all at zero beakers the same way.
		// One point of trade is a small price for still being in the game.
		private const int CrisisScienceTrade = 1;

		// Hysteresis on the crisis test. With a single threshold the two halves of
		// ConsiderSliders traded the same point back and forth forever: lowering
		// luxuries tipped two more cities into disorder, unrest crossed 0.40, the crisis
		// branch put the point back, and order returned — a stable two-turn cycle with
		// luxuries pinned at 8 and science at 0 for the rest of the game. Entering a
		// crisis is a decision the empire has to hold for a while, so leaving it needs a
		// real improvement (30%), not merely stepping back over the line it came in on.
		private const double CrisisRecovered = 0.30;
		private bool _inCrisis;

		// Latched crisis state — call once per ConsiderSliders pass, before anything reads
		// the thresholds, so every branch in that pass agrees about the situation.
		private bool InCrisis(double unrest)
		{
			if (_inCrisis) _inCrisis = unrest > CrisisRecovered;
			else           _inCrisis = unrest > CrisisUnrest;
			return _inCrisis;
		}

		private int MaxLuxuries(double unrest)
		{
			int reserve = _inCrisis ? CrisisScienceTrade : MinScienceTrade;
			return Math.Max(0, 10 - Player.TaxesRate - reserve);
		}

		// Citizen allocation, the tool the AI never had. See City.AutoAssignCitizens for what
		// it does and why the luxury slider was not enough on its own.
		//
		// Runs from Player.NewTurn, which the turn queue schedules AFTER every one of our
		// cities has taken its turn — so IsInDisorder is this turn's freshly computed answer
		// rather than last turn's. A city fixed here is content when it is evaluated next
		// turn, which resets DisorderTurns before it can reach the 3 that collapses a
		// Republic or Democracy.
		internal void ConsiderCitizens()
		{
			foreach (City city in Player.Cities)
				city.AutoAssignCitizens();
		}

		internal void ConsiderSliders()
		{
			if (Player.IsDestroyed()) return;
			if (Player.Government is Gov.Anarchy) return;

			int rioting   = Player.Cities.Count(c => c.IsInDisorder);
			int cityCount = Math.Max(1, Player.Cities.Length);
			double unrest = rioting / (double)cityCount;
			bool crisis   = InCrisis(unrest);

			// Enforce the ceiling on the way IN, not merely as a target to climb toward.
			// The raise branch below only ever moves the slider upward, so a civ that had
			// already reached 8 luxuries against a tax floor of 2 would sit there
			// permanently however the thresholds changed. One step per turn, and only
			// while the civ is NOT in crisis — clawing trade back from a burning empire
			// is exactly the mistake that froze the small civs.
			//
			// The luxury ceiling is tested FIRST. These two clauses are different goals
			// welded to one else-if: "climb to a tax floor of 3" and "come back inside the
			// luxury ceiling". With the tax test first, a civ parked below the floor never
			// reached the second clause at all — and a civ at tax 2 with luxuries 8 is
			// precisely the one whose research has stopped. Order matters more than which
			// clause wins, since only one step is taken per turn either way.
			if (!crisis)
			{
				if (Player.LuxuriesRate > MaxLuxuries(unrest)) Player.LuxuriesRate--;
				else if (Player.TaxesRate < 3) Player.TaxesRate++;
			}

			// Happiness safety valve: pump luxuries UP while cities are rioting, then wind them
			// back down once order returns. Civil disorder freezes a city's production AND its
			// growth, so with luxuries pinned at 0 and too few happiness buildings, a growing
			// empire tips into the grow→riot→shrink→recover oscillation (the mid-game "wasting
			// illness"). Raising luxuries quells it far faster than waiting for a Temple to
			// finish; it costs science, but GetStance flips to Consolidate to build the
			// happiness infrastructure that lets luxuries fall back toward research.
			// The luxury slider is an EMPIRE-WIDE lever, so it must answer to an
			// empire-wide measure. "Any city rioting" is guaranteed true forever once a
			// civ is large — at 79 cities this branch fired every single turn, took the
			// early return, and the wind-down below was simply never reached. Luxuries
			// ratcheted to 8, taxes pinned at the floor of 2, and science got nothing for
			// the rest of the game: measured at turn 440, the Romans held 79 cities on 30
			// advances and the Mongols 51 on 15. It also locked every large civ in the
			// Consolidate stance (LuxuriesRate >= 4 is a trigger), which is why no civ in
			// a 440-turn game ever reached Develop, Republic or Democracy.
			//
			// Two thresholds with a dead band between them, so the slider settles instead
			// of oscillating: push up above 12% unrest, ease down below 5%, hold in
			// between while happiness buildings do the real work.
			if (unrest > 0.12)
			{
				// Deadlock break: a rioting city earns no gold, so the gold overlay's
				// habit of pinning taxes high when broke only perpetuates the disorder.
				// The tax floor is 3 normally, but 2 in a crisis — the old escape valve.
				int taxFloor = crisis ? 2 : 3;
				if (Player.LuxuriesRate >= MaxLuxuries(unrest) && Player.TaxesRate > taxFloor)
					Player.TaxesRate--;
				int maxLux = MaxLuxuries(unrest);
				if (Player.LuxuriesRate < maxLux)
					Player.LuxuriesRate = Math.Min(maxLux, Player.LuxuriesRate + (rioting >= 3 ? 2 : 1));
				// The reserve is a CEILING, not merely a target to climb toward — and it
				// has to bind inside a crisis too, which is the only time luxuries ever
				// reach 8. Without this the branch above was the sole mover here, the
				// !crisis clause never ran, and a civ over the threshold kept luxuries at
				// 8 and science at 0 for good: the Mongols at 45 cities and 51% disorder
				// had 26 advances at 2080 AD, the Olvir 10.
				else if (Player.LuxuriesRate > maxLux)
					Player.LuxuriesRate--;
				return;
			}
			// Wind luxuries back down only when EVERY city has a real happiness margin
			// (strictly more happy than unhappy). If any city is at the brink (unhappy
			// >= happy but not yet rioting), hold the sliders steady — lowering luxury
			// there instantly re-triggers the disorder we just quelled, producing the
			// clear→riot→clear oscillation. Holding lets the city sit stable at the edge
			// until happiness buildings (Consolidate stance) let luxury fall for real.
			if (Player.LuxuriesRate > 0)
			{
				// Lower on the same empire-wide measure that raises. The old test asked
				// whether any city was near the brink, which for a large empire is always
				// yes — one city in forty is permanently one citizen from unhappy — so
				// the slider could only ever go up. Below 5% unrest the buildings are
				// carrying it and the trade is better spent on research; between 5% and
				// 12% hold, so the two thresholds cannot chase each other.
				if (unrest < 0.05) Player.LuxuriesRate--;
				return;
			}

			StrategyStance stance = GetStance();
			int gold = Player.Gold;
			int tax  = Player.TaxesRate;

			// Base target tax rate by strategic stance.
			int target = stance switch
			{
				StrategyStance.Militarize  => 6, // wars drain gold; lean on taxes
				StrategyStance.Develop     => 4, // peace dividend goes to science
				StrategyStance.Consolidate => 5,
				_                          => 5, // Expand
			};

			// Gold overlay: tighten taxes when broke, ease off when flush.
			if      (gold <  20) target = Math.Max(target, 8);
			else if (gold <  60) target = Math.Max(target, 7);
			else if (gold < 120) target = Math.Max(target, 6);
			else if (gold > 500) target = Math.Min(target, 4);
			else if (gold > 250) target = Math.Min(target, 5);

			// Doctrine: a research-minded leader accepts a thinner treasury to keep
			// the laboratories funded (negative shifts the other way).
			target -= (int)Math.Round(Leader.Doctrine.ScienceBias / 40.0);

			// Keep science in [2, 8] and taxes in [2, 8].
			if (target < 2) target = 2;
			if (target > 8) target = 8;

			// Move one point per turn so the economy shifts smoothly.
			if      (tax < target) Player.TaxesRate = tax + 1;
			else if (tax > target) Player.TaxesRate = tax - 1;
		}

		// ── rush-buy logic ─────────────────────────────────────────────────────

		internal void ConsiderRushBuy()
		{
			if (Player.IsDestroyed()) return;
			if (Player.Government is Gov.Anarchy) return;
			if (Player.Gold < 20) return;

			StrategyStance stance = GetStance();

			foreach (City city in Player.Cities)
			{
				if (city.CurrentProduction is null) continue;
				if (city.IsInDisorder && city.CurrentProduction is IBuilding) continue;

				int fullCost = city.ProductionCost(city.CurrentProduction);
				int gold     = Player.Gold;
				short buy    = city.BuyPrice;
				double done  = (double)city.Shields / fullCost;

				// Desperation: a tiny civ sitting on a big hoard converts gold to tempo.
				// For a 1-3 city civ (often an isolated island start), gold is the only
				// lever it has — 500 unspent gold and a 1-shield city means centuries of
				// nothing. Any completion level, keep a 100g cushion — but only spend on
				// production that breaks the deadlock: population, buildings, or a first
				// defender. The Inca burned ~350g rush-buying queued Diplomats.
				bool worthBuying = city.CurrentProduction is IBuilding
					|| city.CurrentProduction is Settlers
					|| (city.CurrentProduction is IUnit defUnit && defUnit.Role == UnitRole.Defense
					    && !city.Tile.Units.Any(u => u.Role == UnitRole.Defense));
				if (Player.Cities.Length <= 3 && gold >= 200 && buy <= gold - 100 && worthBuying)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				// Idle treasury: past 500 gold, any empire buys buildings outright at any
				// completion level. The Lakota sat on 1,361 gold while four cities
				// hand-built units at 2 shields/turn — the tail-end rules below never
				// fire because nothing reaches 60% done. Buildings only: units are
				// cheap enough to build and wonders keep their 70% clinch rule.
				if (city.CurrentProduction is IBuilding && gold >= 500 && buy <= gold - 200)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				if (city.Shields <= 0) continue; // no tail-end discount without prior investment

				// Emergency: undefended city with an enemy land unit adjacent.
				// Rush the current production if it's a defender, even at low completion.
				if (city.CurrentProduction is IUnit emergUnit
				    && emergUnit.Role == UnitRole.Defense
				    && city.Tile.Units.Count(u => u.Role == UnitRole.Defense) == 0
				    && city.Tile.GetBorderTiles().Any(t =>
				           t.Units.Any(u => u.Owner != city.Owner
				                        && u.Role == UnitRole.LandAttack))
				    && gold >= buy)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				// Wonder: clinch it once > 70 % done — higher reserve to avoid going broke.
				if (city.CurrentProduction is IWonder
				    && done >= 0.7 && buy <= gold / 2 && gold - buy >= 60)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				// Tail-end: > 60 % done, affordable — matches how a human shops.
				// Keep 30g reserve to cover maintenance.
				if (done >= 0.6 && buy <= gold / 3 && gold - buy >= 30)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
					continue;
				}

				// Militarize: more aggressive about completing attackers (> 50 % done).
				if (stance == StrategyStance.Militarize
				    && city.CurrentProduction is IUnit
				    && done >= 0.5 && buy <= gold / 4 && gold - buy >= 30)
				{
					Player.Gold -= buy;
					city.Shields = fullCost;
				}
			}
		}

		// ── garrison upkeep relief ────────────────────────────────────────────

		// A city whose garrison upkeep eats its entire shield output can never
		// complete anything: the Inca deadlock was a size-1 hills city carrying two
		// Militia — 1 shield income, 1 upkeep, net zero, production frozen for
		// centuries. Disband one excess home defender per turn until the city
		// produces again. Never touches the last defender.
		// ── divestment ────────────────────────────────────────────────────────
		//
		// Sell one building a turn while the empire is insolvent, but ONLY buildings that
		// are provably doing nothing where they stand.
		//
		// The gate on construction (EarnsItsKeep) is preventative and cannot reach this:
		// a city that shrinks keeps paying for the infrastructure of the city it used to
		// be. Measured at the end of a 750-turn autoplayed game, Japan took 62 gold a turn
		// and owed 175 — Osaka was a size-1 city carrying 12 buildings at 20 gold of upkeep
		// and producing NO taxes at all. Nothing in the game ever divested, and Player.Gold
		// clamps at zero, so the shortfall was silently absorbed forever while the gold
		// overlay pinned taxes at 8 and research at 2.
		//
		// Deliberately NOT happiness buildings. A Temple in a content city looks redundant
		// but is usually the reason the city is content — selling it starts a riot, and the
		// riot costs more than the gold. Only the two provable cases qualify: a building
		// whose effect is gated on a city size this city no longer has, and a multiplier
		// sitting on a base of zero. Both give back exactly nothing today.
		//
		// One sale per turn empire-wide, so a civ that has a bad decade sheds a little
		// rather than dismantling itself.
		internal void ConsiderDivestment()
		{
			if (Player.IsDestroyed()) return;
			// Anarchy is NOT excluded here, unlike the slider logic: a civ between
			// governments still pays upkeep, and the two deepest deficits in the game
			// measured (Rome at -2289 a turn, Egypt at -667) were both in Anarchy.

			// Only while genuinely insolvent: broke AND spending more than it earns.
			// Matches the gold overlay's threshold in ConsiderSliders, so divestment stops
			// exactly when the tax slider is free to come back down.
			int taxes = Player.Cities.Sum(c => (int)c.Taxes);
			int upkeep = Player.Cities.Sum(c => (int)c.TotalMaintenance);
			if (Player.Gold >= 20 || taxes >= upkeep) return;

			City? worstCity = null;
			IBuilding? worst = null;
			foreach (City city in Player.Cities)
			{
				// A city in disorder produces nothing THIS turn, which would make every
				// multiplier in it look worthless. Skip it rather than gut it.
				if (city.IsInDisorder) continue;
				foreach (IBuilding building in city.Buildings)
				{
					if (building.Maintenance == 0) continue;
					if (!DoesNothingHere(city, building)) continue;
					if (worst is null || building.Maintenance > worst.Maintenance)
					{
						worst = building;
						worstCity = city;
					}
				}
			}

			if (worst is null || worstCity is null) return;
			Log($"{Player.TribeName}: selling {worst.Name} in {worstCity.Name} "
			  + $"(size {worstCity.Size}, upkeep {worst.Maintenance}; empire {taxes} income vs {upkeep} upkeep)");
			worstCity.SellBuilding(worst);
		}

		// Provably inert: the building's whole effect is unavailable in this city right now.
		private static bool DoesNothingHere(City city, IBuilding building) => building switch
		{
			// Growth caps: City.cs:1367-1368 blocks growth at 7 without an Aqueduct and at
			// 12 without a Sewer. The thresholds here leave two sizes of HEADROOM below
			// each cap on purpose — selling a size-6 city's Aqueduct would be technically
			// correct and practically absurd, since it needs one again the moment it grows,
			// and a rebuild costs far more in shields than the upkeep ever saved.
			Aqueduct    => city.Size <= 4,
			SewerSystem => city.Size <= 9,
			// Multipliers on a base of zero. A Library is +50% of the city's science, and
			// half of nothing is nothing.
			Library or UniversityBuilding or ObservatoryBuilding or Xenolab => city.Science == 0,
			MarketPlace or Bank => city.Taxes == 0,
			_ => false
		};

		internal void ConsiderGarrisonUpkeep()
		{
			if (Player.IsDestroyed()) return;
			foreach (City city in Player.Cities)
			{
				if (city.ShieldIncome > 0) continue;
				IUnit[] garrison = city.Units
					.Where(u => u.Role == UnitRole.Defense && u.X == city.X && u.Y == city.Y)
					.ToArray();
				if (garrison.Length <= 1) continue;
				Game.DisbandUnit(garrison.Last());
			}
		}

		// ── government progression ────────────────────────────────────────────

		// How much this civ wants a given government, by strategic posture.
		//
		// The table used to make Monarchy worth 5 in BOTH Expand and Militarize, while
		// the Republic and Democracy could only win inside Develop. Since BestGovernment
		// requires a STRICTLY higher score, that made Monarchy a terminus: a large civ
		// lives in Expand or Militarize, nothing there could beat 5, so the search
		// returned null and no upgrade was ever attempted. Measured at 1973 AD, every
		// surviving civ was in Monarchy — the Lakota on 65 cities and 76 advances.
		//
		// Monarchy now tops the table only in Militarize, where its simplicity under arms
		// genuinely is the point. A large empire at peace — expanding or consolidating —
		// should want the Republic's trade and then Democracy, and now scores them that
		// way. Despotism stays bottom; Anarchy falls through to 0 so anything beats it.
		// How far-flung this empire is: mean distance of its cities from the capital.
		// Government-independent by design — measuring the graft we currently pay is
		// useless for the comparison, because the civs that matter are already in Democracy,
		// which has no distance corruption at all (measured across a finished game: every
		// civ with 20+ cities sat at 0% graft). What decides whether Communism's
		// fixed-distance corruption is a good trade is the SHAPE of the empire, not the
		// bill the current constitution happens to be sending.
		private int EmpireSpread()
		{
			City? capital = Player.Cities.FirstOrDefault(c => c.HasBuilding<Palace>())
			             ?? Player.Cities.FirstOrDefault();
			if (capital is null || Player.Cities.Length == 0) return 0;
			return (int)Player.Cities.Average(c => (double)Common.DistanceToTile(capital.X, capital.Y, c.X, c.Y));
		}

		// Test seam: the scoring is situational now, so a test needs to ask for a score
		// under this civ's actual circumstances rather than reimplement the table.
		internal int GovernmentScoreForTest(IGovernment gov) => GovernmentScore(gov, GetStance());
		internal int EmpireSpreadForTest() => EmpireSpread();

		private int GovernmentScore(IGovernment gov, StrategyStance stance)
		{
			bool war = stance == StrategyStance.Militarize;

			if (gov is Gov.Democracy)
				return war ? 2                                    // war weariness is crippling
				     : stance == StrategyStance.Develop ? 6 : 5;  // best in peace
			if (gov is Gov.Republic)
				return war ? 3
				     : stance == StrategyStance.Develop ? 5 : 4;

			// Communism is the LATE war government, where Monarchy is the early one. Scored
			// flat it was unreachable: second in war behind Monarchy, third in peace behind
			// both republics, and since BestGovernment only moves on a strictly higher score,
			// no civ could adopt it by any path at all.
			//
			// In peace the republics genuinely out-earn it — it has no trade bonus — so they
			// stay ahead. At war it carries everything Monarchy does (martial law, three free
			// unit supports, no war weariness, no collapse into anarchy) and adds +50%
			// science in every city and corruption that stops growing with distance. For a
			// large or far-flung empire that is simply the better war constitution; for a
			// small compact one Monarchy's lower multiplier still wins, so it stays behind.
			if (gov is Gov.Communism)
			{
				if (!war) return stance == StrategyStance.Develop ? 4 : 3;
				int score = 5;
				if (Player.Cities.Length >= 12) score += 1;   // draws level with Monarchy
				if (EmpireSpread() >= 12)       score += 1;   // and passes it when sprawling
				return score;
			}

			if (gov is Gov.Monarchy)
				return war ? 6 : 3;
			if (gov is Gov.Despotism)
				return 1;
			return 0;
		}

		// Test/diagnostic accessor.
		internal string? BestGovernmentName() => BestGovernment()?.GetType().Name;

		private IGovernment BestGovernment()
		{
			StrategyStance stance = GetStance();

			// Same demotion the production and terraforming code uses: a civ formally at
			// war but with no enemy near any of its cities is not on campaign, and should
			// not be choosing its constitution as though it were. Militarize is the one
			// stance whose table puts Monarchy on top, so without this a civ at war —
			// which, since AI wars are rarely concluded, means most civs most of the time —
			// permanently rates Monarchy "already optimal" and never upgrades, however long
			// ago the fighting stopped. Removing the stance veto in ConsiderGovernment was
			// not enough on its own; this is the half that actually held the door shut.
			if (stance == StrategyStance.Militarize
			    && !Player.Cities.Any(c => NearHostiles(c.X, c.Y)))
				stance = StrategyStance.Develop;

			// A civ that has decided it wants a war rates its constitution as a war leader
			// would, which is what puts Monarchy and Communism above the republics. Applied
			// after the demotion above so it cannot be undone by it.
			if (WantsWarFooting) stance = StrategyStance.Militarize;
			int currentScore = GovernmentScore(Player.Government, stance);
			return Player.AvailableGovernments
			             .Where(g => GovernmentScore(g, stance) > currentScore)
			             .OrderByDescending(g => GovernmentScore(g, stance))
			             .FirstOrDefault();
		}

		// The first advance we could research right now that leads toward T. Walks T's
		// prerequisite tree, skipping what we already know, and returns something the
		// player can actually start on this turn. Null when T is already known or its
		// tree is blocked for some other reason.
		private IAdvance? NextStepToward<T>() where T : IAdvance, new()
		{
			if (Player.HasAdvance<T>()) return null;
			IAdvance[] available = Player.AvailableResearch.ToArray();
			if (available.Length == 0) return null;

			var seen = new System.Collections.Generic.HashSet<byte>();
			var queue = new System.Collections.Generic.Queue<IAdvance>();
			queue.Enqueue(new T());

			while (queue.Count > 0)
			{
				IAdvance node = queue.Dequeue();
				if (!seen.Add(node.Id)) continue;
				if (Player.HasAdvance(node)) continue;

				// Researchable now — this is the step to take.
				IAdvance? ready = available.FirstOrDefault(a => a.Id == node.Id);
				if (ready is not null) return ready;

				foreach (IAdvance prereq in node.RequiredTechs)
					queue.Enqueue(prereq);
			}
			return null;
		}

		// Called when anarchy ends: pick the best available government.
		internal void ChooseGovernment()
		{
			Player.Government = BestGovernment() ?? new Gov.Despotism();
		}

		// Called each turn: consider starting a revolution if conditions are good.
		// How many shields a government change would cost us that we cannot pay.
		//
		// Mirrors City.ShieldCosts exactly (same non-combatant exclusions, same
		// free-support semantics including the -1 "free = city size" case) but evaluated
		// against the government we are ABOUT to adopt. Summed only over cities that would
		// actually go short: a city with shields to spare does not offset one that starves,
		// because the disbanding in City.NewTurn is per-city, not empire-wide.
		private int ProjectedSupportDeficit(IGovernment target)
		{
			int deficit = 0;
			foreach (City c in Player.Cities)
			{
				int supported = c.Units.Count(u => !(u is Diplomat) && !(u is ICaravan));
				int free = target.FreeUnitSupport < 0 ? c.Size : target.FreeUnitSupport;
				int cost = Math.Max(0, supported - free);
				deficit += Math.Max(0, cost - c.ShieldTotal);
			}
			return deficit;
		}

		// Disband one unit from the city that would be worst off, choosing the same victim
		// the engine would: furthest from home. A city's last defender is spared — losing
		// the garrison to pay for a constitution is not a trade worth making.
		private void TrimForSupport(IGovernment target)
		{
			City? worst = null;
			int worstShort = 0;
			foreach (City c in Player.Cities)
			{
				int supported = c.Units.Count(u => !(u is Diplomat) && !(u is ICaravan));
				int free = target.FreeUnitSupport < 0 ? c.Size : target.FreeUnitSupport;
				int shortfall = Math.Max(0, Math.Max(0, supported - free) - c.ShieldTotal);
				if (shortfall > worstShort) { worstShort = shortfall; worst = c; }
			}
			if (worst is null) return;

			int defenders = worst.Tile.Units.Count(u => u.Role == UnitRole.Defense);
			IUnit? cull = worst.Units
				.Where(u => !(u is Diplomat) && !(u is ICaravan))
				.Where(u => !(defenders <= 1 && u.Role == UnitRole.Defense
				             && u.X == worst.X && u.Y == worst.Y))
				.OrderByDescending(u => Common.DistanceToTile(worst.X, worst.Y, u.X, u.Y))
				.FirstOrDefault();
			if (cull is null) return;

			Log($"{Player.TribeName}: {worst.Name} cannot carry {cull.GetType().Name} under "
			  + $"{target.Name} (short {worstShort} shields) — disbanding ahead of the revolt");
			Game.DisbandUnit(cull);
		}

		internal void ConsiderGovernment()
		{
			if (Player.Government is Gov.Anarchy) return;
			if (Player.Civilization is TheOthers or TheThing or Skynet) return; // administrations, organisms, and networks do not revolt
			// Raiders do not hold constitutional conventions either. Omitted from this list
			// (and from ChooseResearch) until a turn-750 save showed the horde governed by
			// REPUBLIC with nine advances to its name — the same defect as the captured-city
			// production bug: a non-civilisation routed through civilisation machinery.
			if (Player.Civilization is Civilizations.Barbarian) return;

			// Research lock-in escape. This must live here, on the every-turn path —
			// ChooseResearch only runs when the research slot is empty or a target just
			// completed, never while one is in flight, so an escape placed there is dead
			// code (the English ground Mysticism for a century with Monarchy available).
			// Science points are a player-level pool; switching targets costs nothing.
			if (Player.Government is Gov.Despotism
			    && Player.CurrentResearch is not null
			    && Player.CurrentResearch is not Advances.Monarchy)
			{
				// Steer along the PATH to Monarchy, not merely onto Monarchy once it
				// happens to become available. Waiting for it to appear in
				// AvailableResearch is no help to a civ that lacks its prerequisites:
				// measured at 1940 AD, an autoplayed Japan held 20 cities on EIGHT
				// advances, still in Despotism because it had never researched
				// Ceremonial Burial, so Monarchy was never offered and this escape was
				// dead. Despotism's tile penalty then kept every city tiny, tiny cities
				// made 3 trade a turn between them, and 3 trade a turn never buys the
				// advance that ends it. Nothing else in the AI breaks that circle.
				IAdvance? escape = Player.AvailableResearch.FirstOrDefault(a => a is Advances.Monarchy)
				               ?? NextStepToward<Advances.Monarchy>();
				if (escape is not null)
					Player.CurrentResearch = escape;
			}

			if (BestGovernment() is null) return; // already optimal

			// Escaping Despotism is the single biggest economic win: it lifts the despot
			// tile penalty that suppresses irrigation and keeps cities tiny. Pursue it
			// eagerly — any stance, high chance, even at war — AI wars rarely end (see
			// GetStance), so waiting for peace means staying a despot forever, and that
			// permanent stunting outweighs the anarchy interregnum risk.
			if (Player.Government is Gov.Despotism)
			{
				if (Common.Random.Next(100) < 60)
					Player.Revolt();
				return;
			}

			// Don't revolt while a war is actually being FOUGHT — the anarchy interregnum
			// is genuinely dangerous with an enemy at the gates. But AI wars are rarely
			// concluded, only abandoned, so "at war with anybody" is a near-permanent
			// state and testing for it alone meant no civ ever upgraded past Monarchy.
			// What matters is whether anyone is actually pressing us: a foreign unit
			// belonging to a civ we are at war with, within a few tiles of one of ours.
			bool underThreat = Game.GetUnits().Any(u =>
				u.Owner != Game.PlayerNumber(Player)
				&& !NonCombatant(u)                      // a caravan at the border is not a siege
				&& Player.IsAtWar(Game.GetPlayer(u.Owner))
				&& Player.Cities.Any(c => Common.DistanceToTile(c.X, c.Y, u.X, u.Y) <= 4));
			if (underThreat) return;

			// Shed what the new constitution cannot carry — deliberately, and in advance.
			//
			// FreeUnitSupport is 3 per city under Monarchy and Communism and ZERO under
			// Republic and Democracy. That is not a cap — units past the allowance cost a
			// shield each — but it means a 26-city empire revolting to Democracy suddenly
			// owes 78 shields it did not owe yesterday. Every city whose shields go negative
			// then has City.NewTurn (City.cs:1464) disband its furthest-from-home unit, one
			// per city per turn, with no message for an AI: the army evaporation you can
			// watch in any autoplayed game.
			//
			// Two guards, both learned the hard way. The test is SHIELD INCOME against the
			// new upkeep, not a unit count — reading FreeUnitSupport as a cap demanded a civ
			// disband its entire army before it could reform, which cost half the world's
			// research over 500 turns and killed two civs. And the wait is bounded twice
			// over: a small deficit is tolerated rather than perfected, and after
			// MaxDrawdownTurns the revolt proceeds regardless. Reform must never be
			// blockable indefinitely.
			IGovernment? target = BestGovernment();
			if (target is not null && target.FreeUnitSupport < Player.Government.FreeUnitSupport)
			{
				const int TolerableDeficit  = 2;   // a couple of shields is not worth delaying reform for
				const int MaxDrawdownTurns  = 15;  // hard stop, whatever the books say

				int deficit = ProjectedSupportDeficit(target);
				if (deficit > TolerableDeficit && _govDrawdownTurns < MaxDrawdownTurns)
				{
					_govDrawdownTurns++;
					TrimForSupport(target);
					return;
				}
			}
			_govDrawdownTurns = 0;

			// Further upgrades (Monarchy → Republic/Democracy, etc.). Consolidate is no
			// longer a veto — a civ managing unhappiness is exactly the one that wants the
			// Republic's trade and the Democracy's content citizens, and holding it back
			// until it reaches Develop (1% of decisions) is what pinned the whole field in
			// Monarchy. Militarize still abstains: changing constitution mid-campaign is
			// how you lose the campaign.
			// The Militarize stance no longer vetoes this outright. "Changing constitution
			// mid-campaign loses the campaign" is sound for a campaign actually being
			// fought — which is exactly what the underThreat test above already measures,
			// enemy units within 4 tiles of one of our cities. Testing the STANCE as well
			// was a second, far broader veto: AI wars are rarely concluded, so a civ could
			// hold an advance and never use it. Measured at 1858 in a real game, Japan sat
			// in Monarchy with Democracy researched, at war with two civs it was not
			// fighting, and stayed there until the war happened to end.
			//
			// The narrower guard stands: with an enemy at the gates, underThreat returns
			// above and no revolt happens.
			if (Common.Random.Next(100) < 25)
				Player.Revolt();
		}

		// ── proactive diplomacy ───────────────────────────────────────────────────

		internal List<AIDemand> GenerateDemands(Player human)
		{
			var demands = new List<AIDemand>();
			byte aiNum    = (byte)Game.PlayerNumber(Player);
			byte humanNum = (byte)Game.PlayerNumber(human);
			bool atWar    = Player.IsAtWar(human);

			if (atWar)
			{
				// At war: ask for ONE captured city back in exchange for peace — the most
				// valuable (largest) one. Listing every lost city at once reads ridiculously.
				City? wantBack = Game.GetCities()
					.Where(c => c.Owner == humanNum && c.OriginalOwner == aiNum)
					.OrderByDescending(c => c.Size)
					.FirstOrDefault();
				if (wantBack is not null)
					demands.Add(new AIDemand(AIDemandKind.ReturnCity, city: wantBack, duration: 100));
			}
			else if (Game.GameTurn >= 30)
			{
				// Check for grievance: AI has 2+ cities held by the human.
				City[] capturedByHuman = Game.GetCities()
					.Where(c => c.Owner == humanNum && c.OriginalOwner == aiNum)
					.ToArray();
				if (capturedByHuman.Length >= 2 && Game.GameTurn - LastGrievanceTurn >= 40)
				{
					// Formal grievance: one city back + one tech + gold.
					City wantBack = capturedByHuman.OrderByDescending(c => c.Size).First();

					IAdvance[] wantedTechs = human.Advances.Where(a => !Player.HasAdvance(a)).ToArray();
					IAdvance? wantedTech = wantedTechs.Length >= 1
						? wantedTechs.OrderByDescending(a => AdvanceDemandValue(a)).First()
						: null;

					int goldAmount = human.Gold >= 25 ? Math.Max(25, (int)(human.Gold * 0.2f)) : 0;

					LastGrievanceTurn = Game.GameTurn;
					demands.Add(new AIDemand(AIDemandKind.GrievancePack,
						city: wantBack, advance: wantedTech, amount: goldAmount, duration: 75));
					return demands;
				}

				// At peace: standard extortion for attitude bonus
				if (human.HasNewVisibilityFor(Player))
					demands.Add(new AIDemand(AIDemandKind.GiveMap, duration: 50));

				IAdvance[] techOptions = human.Advances.Where(a => !Player.HasAdvance(a)).ToArray();
				if (techOptions.Length >= 2)
				{
					int topWeight = techOptions.Max(a => AdvanceDemandValue(a));
					IAdvance[] top = techOptions.Where(a => AdvanceDemandValue(a) == topWeight).ToArray();
					demands.Add(new AIDemand(AIDemandKind.GiveTech, advance: top[Common.Random.Next(top.Length)], duration: 50));
				}

				if (human.Gold >= 25)
				{
					int amount = Math.Max(25, (int)(human.Gold * 0.25f));
					demands.Add(new AIDemand(AIDemandKind.GiveMoney, amount: amount, duration: 50));
				}

				City[] humanCities = Game.GetCities().Where(c => c.Owner == humanNum).ToArray();
				if (humanCities.Length >= 3)
				{
					City[] smallCities = humanCities.Where(c => c.Size <= 2 && !c.HasBuilding<Palace>()).ToArray();
					if (smallCities.Length > 0)
					{
						City[] aiCities = Game.GetCities().Where(c => c.Owner == aiNum).ToArray();
						City target = aiCities.Length > 0
							? smallCities.OrderBy(c => aiCities.Min(ac => Common.DistanceToTile(ac.X, ac.Y, c.X, c.Y))).First()
							: smallCities[Common.Random.Next(smallCities.Length)];
						demands.Add(new AIDemand(AIDemandKind.CedeCity, city: target, duration: 50));
					}
				}
			}

			return demands;
		}

		internal void ConsiderDiplomacy()
		{
			if (Game.PlayerNumber(Player) == 0) return;
			if (Player.Civilization is Olvir) return; // refugees seek coexistence, not negotiation
			if (Player.Civilization is TheOthers or TheThing or Skynet) return; // the Registry does not take meetings; the Thing has nothing to say; the network does not answer
			if (Player.Government is Governments.Anarchy) return;
			// Nobody talks to a civ that has just used a nuclear weapon on people. Every
			// channel closes for the duration — no gifts, no tribute, no terms.
			if (Game.Instance.IsNuclearPariah(Player)
			 || Game.Instance.IsNuclearPariah(Game.Instance.HumanPlayer)) return;

			if (Player.IsDestroyed()) return;

			// HumanOpponent, not Human: this whole channel is human-directed (there is no
			// AI-to-AI equivalent), so under autopilot every civ aims its tribute demands and
			// cede-city ultimatums at the one unattended player.
			Player? human = HumanOpponent;
			if (human is null || human == Player || human.IsDestroyed()) return;

			// Only approach if we've spotted at least one of their cities
			if (!Game.GetCities().Any(c => c.Player == human && Player.Visible(c.X, c.Y))) return;

			// Honour active goodwill / peace-treaty windows: no approaches until they expire.
			// The war channel stays open so the AI can still seek peace during a conflict.
			if (!Player.IsAtWar(human) &&
			    (Player.HasAttitudeBonus(human) || Player.HasPeaceTreaty(human)))
				return;

			// Humanitarian plea: a small civ at peace with a starving city begs the human for
			// aid rather than extorting. Far likelier than routine diplomacy so a doomed
			// frontier neighbour reaches out while it still can; the attitude-bonus return
			// above stops it begging again right after a successful airdrop.
			if (!Player.IsAtWar(human) && Player.Cities.Length <= 2)
			{
				City? dying = Player.Cities
					.Where(c => c.Size <= 2 && c.FoodIncome < 0)
					.OrderBy(c => c.Size).ThenBy(c => c.FoodIncome)
					.FirstOrDefault();
				if (dying is not null)
				{
					if (Common.Random.Next(100) >= 40) return;
					var plea = new List<AIDemand> { new AIDemand(AIDemandKind.BegForAid, city: dying, amount: 50, duration: 40) };
					GameTask.Enqueue(Show.MeetKing(Player, aiInitiated: true, demands: plea));
					return;
				}
			}

			// Base ~3 % per turn; personality and war status nudge the odds
			int chance = 3;
			if (Leader.Aggression == AggressionLevel.Aggressive) chance += 4;
			if (Leader.Militarism == MilitarismLevel.Militaristic) chance += 2;
			if (Leader.Aggression == AggressionLevel.Friendly)    chance += 4;
			if (Player.IsAtWar(human))                             chance += 6;

			if (Common.Random.Next(100) >= chance) return;

			List<AIDemand> demands = GenerateDemands(human);
			GameTask.Enqueue(Show.MeetKing(Player, aiInitiated: true, demands: demands));
		}

		// ── background map trading ────────────────────────────────────────────────

		internal void ConsiderMapTrade()
		{
			if (Game.PlayerNumber(Player) == 0) return;
			if (Player.Government is Governments.Anarchy) return;
			if (Player.IsDestroyed()) return;

			// ~3 % chance per turn to consider a map trade
			if (Common.Random.Next(100) >= 3) return;

			// Pick a random non-barbarian, non-hostile AI partner that has an embassy
			Player[] candidates = Game.Players
				.Where(p => p != Player
				         && !p.IsDestroyed()
				         && Game.PlayerNumber(p) != 0   // not barbarians
				         && !p.IsHuman
				         && !Player.IsAtWar(p)
				         && Player.HasEmbassy(p))
				.ToArray();

			if (candidates.Length == 0) return;

			Player partner = candidates[Common.Random.Next(candidates.Length)];

			bool weHaveNew   = Player.HasNewVisibilityFor(partner);
			bool theyHaveNew = partner.HasNewVisibilityFor(Player);
			if (!weHaveNew && !theyHaveNew) return;

			Player.MergeVisibility(partner);
			partner.MergeVisibility(Player);

			// The charts go with the map. Visibility alone buys surprisingly little for
			// colonisation — the site scan reads terrain through fog and is bounded by how
			// far a hull can see, not by what has been explored — so it is the REGISTER that
			// carries the useful part of a map across an ocean: somewhere worth settling
			// that we could never have surveyed ourselves.
			AI partnerAi = AI.Instance(partner);
			MergeColonyRegister(partnerAi);
			partnerAi.MergeColonyRegister(this);
		}

		// ── proactive war declaration ──────────────────────────────────────────

		// A republic eyeing a weaker neighbour. Sets an appetite rather than acting: while it
		// stands, BestGovernment prefers a war government, ConsiderGovernment revolts, and by
		// the time the anarchy clears the civ is a Monarchy or a Communist state that CAN
		// declare war. The appetite decays either way, so a civ that revolts and finds no war
		// worth having climbs back to a republic afterwards.
		//
		// Deliberately hard to trigger: warlike leaders only, no enemy at the gates (tearing
		// up the constitution mid-invasion is suicide), a neighbour we clearly outweigh, and
		// a low roll. A world of civilised leaders still pacifies itself, which is the point
		// of the government ladder — this stops it being the ONLY outcome.
		// The most a commercial relationship can subtract from a war roll. 40 against the -15
		// it replaced: with the three-route cap gone, a genuine trading partnership can now
		// accumulate enough value to matter, and it should — but a war every other factor
		// favours must still be reachable. Compare: a gift buys -15, being notably weaker -20.
		private const int TradeDeterrentMax = 40;

		private void ConsiderWarFooting()
		{
			if (_warAmbition > 0) return;

			bool warMinded = Leader.Militarism == MilitarismLevel.Militaristic
			              || Leader.Aggression == AggressionLevel.Aggressive;
			if (!warMinded) return;

			// Never revolt with hostiles already among our cities — the anarchy interregnum
			// is the same trap ConsiderGovernment guards against.
			if (Player.Cities.Any(c => NearHostiles(c.X, c.Y))) return;

			// There must be a government worth revolting TO: one with no war weariness, that
			// is not Anarchy and not the Despotism we climbed out of.
			if (!Player.AvailableGovernments.Any(g => g.WarWeariness == 0
			        && g is not Gov.Anarchy && g is not Gov.Despotism))
				return;

			// A tempting target. A republic has no army to compare (it never builds
			// attackers), so weigh empires rather than militaries: a neighbour with half our
			// cities or fewer is the one a warmonger talks himself into.
			bool tempting = Game.Players.Any(p => p is not null && p != Player
			        && !p.IsDestroyed() && Game.PlayerNumber(p) != 0
			        && !Player.IsAtWar(p) && IsNeighbor(p)
			        && p.Cities.Length * 2 <= Player.Cities.Length
			        && p.Cities.Length > 0);
			if (!tempting) return;

			if (Common.Random.Next(100) < 4) _warAmbition = 25;
		}

		internal void ConsiderWar()
		{
			// Barbarians use their own logic; governments in revolution are distracted
			if (Game.PlayerNumber(Player) == 0) return;
			if (Player.Civilization is Olvir) return; // refugees do not declare war
			// The Others and the Thing arrive at war with everyone and stay there:
			// no tribute, no peace initiatives, no fresh declarations needed.
			if (Player.Civilization is TheOthers or TheThing or Skynet) return;
			if (Player.Government is Governments.Anarchy) return;

			// ── Track war duration and peacetime city baseline ───────────────────
			bool atWar = Game.Players.Any(p => p != Player && !p.IsDestroyed() && Player.IsAtWar(p));
			if (atWar)
				_turnsAtWar++;
			else
			{
				_turnsAtWar      = 0;
				_peacetimeCities = Player.Cities.Length;
			}

			// ── Tribute pact (Layer 1) ───────────────────────────────────────────
			// A militarily outclassed AI civ at war with an AI neighbour it has an embassy
			// with offers tribute in exchange for peace. The protector accepts (no AI
			// refusal logic in Layer 1; the gold is free protection for them, costless to
			// agree). Tribute is the *better* outcome than the existing make-peace random
			// roll below for clearly losing civs — peace decays but tribute self-renews
			// each turn the gold flows, so the small civ stops bleeding shields on
			// futile attackers.
			if (atWar)
			{
				int ownPower = MilitaryScore(Player);
				Player[] tributeCandidates = Game.Players
				    .Where(p => p != Player && !p.IsDestroyed()
				             && Game.PlayerNumber(p) != 0
				             && !(p.Civilization is TheOthers or TheThing or Skynet) // the Registry takes cities, not gold; the Thing takes people; the network takes all
				             && !Game.Instance.IsNuclearPariah(p)   // you do not buy off a pariah
				             && Player.IsAtWar(p)
				             && Player.HasEmbassy(p)
				             && ownPower * 2 < MilitaryScore(p))
				    .ToArray();
				if (tributeCandidates.Length > 0)
				{
					Player protector = tributeCandidates.OrderByDescending(MilitaryScore).First();
					// Annual tribute scales with player gold income, clamped: 5 gold floor,
					// 25 gold ceiling. The cap matters because a tiny civ shouldn't price
					// itself out of survival, and a runaway civ shouldn't extract everything.
					int annual = Math.Max(5, Math.Min(25, Player.Gold / 20 + 5));
					if (!Player.PaysTributeTo(protector) && Player.Gold >= annual)
					{
						if (protector.IsHuman)
						{
							// The human decides in person: the offer arrives as an audience,
							// rate-limited so a refusal isn't repeated every turn.
							if (Game.GameTurn - _lastTributeOfferTurn >= 20 && Common.Random.Next(100) < 30)
							{
								_lastTributeOfferTurn = (int)Game.GameTurn;
								var offer = new List<AIDemand> { new AIDemand(AIDemandKind.OfferTribute, amount: annual) };
								GameTask.Enqueue(Show.MeetKing(Player, aiInitiated: true, demands: offer));
							}
						}
						else
						{
							Player.EstablishTribute(protector, annual);
							// World news travels along embassy channels.
							if (Human.HasEmbassy(Player) || Human.HasEmbassy(protector))
								GameTask.Enqueue(Message.Newspaper(null!,
									$"{Player.TribeNamePlural} sue for protection!",
									$"${annual}/turn tribute flows",
									$"to the {protector.TribeNamePlural}."));
						}
					}
				}
			}

			// ── AI-vs-AI peace initiatives ───────────────────────────────────────
			if (atWar)
			{
				Player[] aiEnemies = Game.Players
				    .Where(p => p != Player && !p.IsDestroyed() && !p.IsHuman
				             && Game.PlayerNumber(p) != 0 && Player.IsAtWar(p)
				             && !(p.Civilization is TheOthers or TheThing or Skynet) // no peace with the manifest's author, the ice, or the network
				             && !Game.Instance.IsNuclearPariah(p))  // ...nor with a civ under condemnation
				    .ToArray();

				if (aiEnemies.Length > 0)
				{
					// Sustained territory loss: net fewer cities than when the war began.
					bool losingTerritory = Player.Cities.Length < _peacetimeCities
					                    && Player.Cities.Length > 0;

					// War exhaustion: long campaign with an empty treasury.
					bool exhausted = _turnsAtWar > 40 && Player.Gold < 50;

					// Stalemate: a long war in which we haven't lost ground either. Without
					// this exit, two solvent civs in a going-nowhere war stay at war forever
					// (neither loses territory, neither goes broke), which locks both in the
					// Militarize stance for the rest of the game.
					bool stalemate = _turnsAtWar > 30 && Player.Cities.Length >= _peacetimeCities;

					if (losingTerritory || exhausted || stalemate)
					{
						int peaceChance = losingTerritory ? 30 : stalemate ? 25 : 20;
						foreach (Player enemy in aiEnemies)
						{
							if (Common.Random.Next(100) < peaceChance)
							{
								Player.MakePeace(enemy);
								break; // one treaty per turn
							}
						}
					}
				}
			}

			// ── Defense pacts (AI ↔ AI) ──────────────────────────────────────────
			// Blocs form against the local hegemon: a civ at peace that sees a
			// neighbour at least twice its strength seeks a pact with another
			// AI neighbour who shares the threat. The human hegemon is the classic
			// trigger — dominate the continent and watch the alliances form. The
			// human is never auto-signed as a partner; they consent via the console.
			// The random gate comes first: everything below scans every player, and
			// Player.Cities rebuilds an array on each access.
			if (!atWar && Common.Random.Next(100) < 25)
			{
				Player[] alive = Game.Players
					.Where(p => p is not null && !p.IsDestroyed() && Game.PlayerNumber(p) != 0)
					.ToArray();
				// World totals once, not once per candidate.
				int worldCities = alive.Sum(p => p.Cities.Length);
				int worldScore  = alive.Sum(p => p.Score);

				// A civ that has broken away from the entire field, not just from us.
				// The old test asked only "is there a strong neighbour?", so a power
				// dominating the globe from another continent was invisible to every
				// civ it had not yet reached — and it snowballed unopposed while the
				// rest of the world filed no objection. An empire holding a third of
				// the world's cities or score is everyone's problem; distance buys
				// time, not safety.
				bool Global(Player p) =>
					alive.Length >= 3 &&
					((worldCities > 0 && p.Cities.Length * 3 >= worldCities) ||
					 (worldScore  > 0 && p.Score * 3 >= worldScore));

				int myPower = MilitaryScore(Player);
				Player? hegemon = alive
					.Where(p => p != Player && MilitaryScore(p) > myPower * 2
					         && (IsNeighbor(p) || Global(p)))
					.OrderByDescending(MilitaryScore)
					.FirstOrDefault();

				// A strong neighbour is routine caution and keeps its old 1-in-20
				// cadence. A world power gets the full 1-in-4: nobody can afford to
				// deliberate for fifty turns while it eats the map.
				if (hegemon is not null && (Global(hegemon) || Common.Random.Next(5) == 0))
				{
					int threat = MilitaryScore(hegemon);
					// Partners need only be weaker than the hegemon. The old bar —
					// less than half its strength — excluded exactly the mid-tier
					// civs with enough army left to make a bloc worth signing.
					Player? partner = alive
						.Where(p => p != Player && p != hegemon && !p.IsHuman
						         && !(p.Civilization is Olvir or TheOthers or TheThing)
						         && !Player.IsAtWar(p) && Player.HasEmbassy(p)
						         && !Player.HasDefensePact(p)
						         && MilitaryScore(p) < threat)
						.OrderByDescending(MilitaryScore)
						.FirstOrDefault();
					if (partner is not null)
					{
						Player.SetDefensePact(partner, 50);
						partner.SetDefensePact(Player, 50);
						DecisionLogger.LogDefensePact(Player, partner, hegemon, Global(hegemon));
					}
				}
			}

			// ── Normal war logic below ───────────────────────────────────────────

			// Republics and Democracies are blocked by their Senate from starting wars —
			// but a civ that wants one badly enough can trade the Senate for it, which is
			// the escape a human player has always had. See ConsiderWarFooting.
			if (_warAmbition > 0) _warAmbition--;
			if (Player.RepublicDemocratic)
			{
				ConsiderWarFooting();
				return;
			}

			// Civilised non-aggressive leaders don't pick fights
			if (Leader.Militarism == MilitarismLevel.Civilized
			    && Leader.Aggression != AggressionLevel.Aggressive)
				return;

			// Don't pick fights with other civs while barbarians hold a city near our empire
			if (Game.GetCities().Any(c => c.Owner == 0
			    && Player.Cities.Any(oc => Common.DistanceToTile(c.X, c.Y, oc.X, oc.Y) <= 10)
			    && Player.Visible(c.X, c.Y)))
				return;

			int ownScore = MilitaryScore(Player);
			if (ownScore == 0) return; // no army, no war

			// ── Expansion gate ───────────────────────────────────────────────────
			// Civs that still have room to grow prefer settlers to swords.
			// Militaristic/aggressive leaders can still fight but take a penalty;
			// everyone else waits until their empire is built out.
			// Linear map scale — see GetStance (line 71) for the rationale.
			int mapScale  = Math.Max(1, Map.WIDTH / 80);
			int cityTarget = Leader.Development == Expansionistic ? (9 * mapScale) + Game.Difficulty
			               : Leader.Development == Normal          ? (6 * mapScale) + Game.Difficulty
			               :                                         (4 * mapScale) + Game.Difficulty;
			bool stillExpanding = Player.Cities.Length < cityTarget && !atWar;

			bool warMinded = Leader.Militarism == MilitarismLevel.Militaristic
			              || Leader.Aggression == AggressionLevel.Aggressive;
			if (stillExpanding && !warMinded) return;

			foreach (Player enemy in Game.Players)
			{
				if (enemy == Player || enemy.IsDestroyed()) continue;
				if (Player.IsAtWar(enemy)) continue;
				if (!IsNeighbor(enemy)) continue;
				if (enemy.HasWonder<UnitedNations>()) continue;

				int their = MilitaryScore(enemy);

				// Base chance from leader personality + difficulty bonus
				int chance = Game.Difficulty * 3;
				if (Leader.Aggression  == AggressionLevel.Aggressive)    chance += 8;
				if (Leader.Militarism  == MilitarismLevel.Militaristic)   chance += 7;

				// Modifier for relative strength
				if (ownScore > their)             chance += 5;
				if (ownScore > their * 3 / 2)     chance += 5; // notably stronger
				if (their > ownScore * 3 / 2)     chance -= 20; // notably weaker — don't be reckless

				// Expansion penalty: even war-minded leaders are less eager while still settling
				if (stillExpanding) chance -= 10;

				// Trade deterrent: an AI profiting from trade routes with this civ is reluctant
				// to wreck them.
				//
				// Counted on BOTH sides. This used to sum only the deciding AI's own cities,
				// which made a partner's investment invisible to the civ deciding whether to
				// attack it — a player could run the richest trade relationship on the map and
				// have it weigh nothing, because the ledger being read was the other one. What
				// deters a war is the value of the relationship, and a relationship has two
				// ends.
				//
				// Still capped, and deliberately. An uncapped deterrent means a rich enough
				// trading partner can never be attacked by anybody, which does not make the AI
				// peaceful so much as inert. TradeDeterrentMax is the most any commercial tie
				// can be worth against a war that every other factor favours.
				byte enemyNum = (byte)Game.PlayerNumber(enemy);
				byte ownNum   = (byte)Game.PlayerNumber(Player);
				int tradeValue = Player.Cities
				    .SelectMany(c => c.TradeRoutes)
				    .Where(r => r.Partner.Owner == enemyNum)
				    .Sum(r => r.Value)
				  + enemy.Cities
				    .SelectMany(c => c.TradeRoutes)
				    .Where(r => r.Partner.Owner == ownNum)
				    .Sum(r => r.Value);
				if (tradeValue > 0) chance -= Math.Min(TradeDeterrentMax, tradeValue / 2);

				// Goodwill deters aggression: a gift or aid package buys real safety
				// for its duration, not just trade acceptance and quiet borders.
				if (Player.HasAttitudeBonus(enemy)) chance -= 15;

				if (Common.Random.Next(100) < chance)
				{
					Player.DeclareWar(enemy);
					return; // one declaration per turn
				}
			}
		}

		// ── city-site scoring ──────────────────────────────────────────────────

		private int SiteSuitability(ITile center)
		{
			int score = 0;
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;

			// Resource value of every tile in the working diamond.
			// Ocean tiles get a +2 premium for long-term coastal trade potential.
			// Special resource tiles get +3 for improvement headroom (mines, irrigation).
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2) continue;
				int tx = (center.X + dx + mapWidth) % mapWidth;
				int ty = center.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null) continue;
				score += tile.Food + tile.Shield * 2 + tile.Trade;
				if (tile.IsOcean) score += 2;
				if (tile.Special)  score += 3;
			}

			// Immediate neighbours: river adjacency unlocks irrigation chains.
			// Track whether we have both coastal and river neighbours for the
			// river-mouth synergy bonus below.
			bool hasCoastNeighbor = false, hasRiverNeighbor = false;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (center.X + dx + mapWidth) % mapWidth;
				int ty = center.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is River)             { score += 3; hasRiverNeighbor  = true; }
				else if (tile is not null && tile.IsOcean) hasCoastNeighbor = true;
			}

			// The centre tile again, weighted.
			//
			// The radius loop above already counted it, as one of twenty-one — and that is a
			// bad account of what it is. Every other tile in the ring is optional: a citizen
			// works it or does not, and the governor reassigns them as the city grows. The
			// centre is worked from the first turn, for free, forever, and cannot be given
			// up. A site whose ring is excellent and whose centre is barren is not the same
			// proposition as one where the reverse is true, and scoring them alike is how
			// eight Malian cities ended up on Salt Flat.
			//
			// Food carries the heaviest multiplier because it is the only one of the three
			// with no floor: ShieldValue and TradeValue both guarantee the centre at least 1
			// (City.cs), so a barren centre still yields a shield and a coin — it just never
			// yields a bite.
			score += center.Food * 6 + center.Shield * 2 + center.Trade * 2;

			// A river-mouth site combines irrigation, river trade, and ocean trade.
			if (hasCoastNeighbor && hasRiverNeighbor) score += 6;

			// Natural-hazard risk: disasters are more likely on river tiles and mountain-adjacent sites.
			if (center is River) score -= 5;
			if (center.GetBorderTiles().Any(t => t is Mountains)) score -= 3;

			// City proximity penalties
			foreach (City city in Game.GetCities())
			{
				int dist = Common.DistanceToTile(center.X, center.Y, city.X, city.Y);
				if (dist < 4) { score -= 20; continue; } // working-radius overlap
				if (dist < 6) { score -= 5;  continue; }
				// Foreign city in the 6–10 band: contested border risk
				if (city.Player != Player && dist < 10)
					score -= Player.IsAtWar(city.Player) ? 10 : 4;
			}

			// Prefer sites within Chariot-reach (≤5 tiles) of the nearest own city; lone outposts are hard to defend.
			if (Player.Cities.Length > 0)
			{
				int nearestOwn = Player.Cities.Min(c => Common.DistanceToTile(center.X, center.Y, c.X, c.Y));
				if      (nearestOwn <= 5) score += 15;
				else if (nearestOwn <= 7) score += 5;
				else                      score -= 5;
			}

			return score;
		}

		// Can this unit actually WALK to that tile? Continent equality is the cheap
		// test; the "misc" bucket (15) that every small island shares proves nothing,
		// so those fall through to the caller's own handling.
		//
		// Site finders used to skip this entirely: a settler on a coast would pick
		// the better land across a strait, GotoStep would return null, Goto would
		// clear, and it repeated the identical failed A* every turn. Same bug the
		// Explorer had in the Canaries, and ~80 failed searches a turn were still
		// showing in the timing log after that one was fixed.
		// The "misc" bucket every landmass past the 14 named ones is folded into.
		private const byte MISC_CONTINENT = Map.MiscContinent;

		// internal for test: the island cases below are pure logic on two continent ids,
		// and reproducing them through settler targeting would take a whole world.
		internal static bool LandReachable(IUnit unit, ITile tile)
		{
			byte from = unit.Tile?.ContinentId ?? MISC_CONTINENT;
			byte to   = tile.ContinentId;

			// Both on named landmasses: reachable only if it is the SAME one.
			//
			// Map.NamedContinent, not a hardcoded 1-14: the generator now numbers every land
			// region up to 254 (Map.MiscContinent = 255), so two DIFFERENT islands hold
			// distinct ids. Under the old test both failed "known" and fell through to the
			// permissive "both misc, cannot tell" branch below — a land route allowed across
			// open water, using information the map already had.
			bool fromKnown = Map.NamedContinent(from);
			bool toKnown   = Map.NamedContinent(to);
			if (fromKnown && toKnown) return from == to;

			// The misc bucket is genuinely ambiguous only against ITSELF: two different
			// islets both land in it. But a named landmass is a distinct connected
			// component, so named-vs-misc can never be the same ground — and blanket
			// "unknown: allow" is what let a settler on continent 7 be sent to a
			// continent-15 island across open water. GotoStep returned null, Goto
			// cleared, and it re-ran the identical failed search every turn for the rest
			// of the game. That is Japan's four settlers, and a standing share of the
			// failed pathfinds in the timing log.
			if (fromKnown != toKnown) return false;

			return true;   // both misc, or an unexpected id: cannot tell, so allow
		}

		// Is this civ hemmed in — nowhere left to settle by land? Island starts, a
		// civ behind a strait, or one walled off by desert and a neighbour. These
		// are the civs that flatline for a whole game: HasExpansionRoom is land-only
		// by design, so for them it is permanently false and no settler is ever
		// worth building.
		internal bool BoxedIn() => !HasExpansionRoom() && Player.Cities.Length > 0;

		// Somewhere across the water worth crossing for. Scans well beyond the land
		// finders' +-8 window, because the whole point is a long crossing — Japan
		// wants Manchuria, Britain the North European Plain.
		//
		// Searched near first, then wide. 15 tiles finds a neighbouring island or the
		// far side of a strait, which is the common case and stays cheap. But on an
		// Earth map the nearest OTHER continent can be far further off: measured on a
		// real save, Australia's northern coast is ~40 tiles from southern Japan and the
		// island chain above it ~29. At 15 alone a Japanese Longboat found nothing, and
		// AI.cs then sentries a boat with nowhere to go — so every longboat the civ
		// built sat in harbour for the rest of the game. Only a boat that has already
		// failed the cheap search pays for the wide one.
		private const int OverseasRange     = 15;
		private const int OverseasRangeFar  = 45;

		// ── the colony register ───────────────────────────────────────────────
		//
		// What a civ knows about the world beyond its own shores, and the first piece of
		// memory this AI has ever had. Everything else it does is recomputed from nothing
		// every turn: BestOverseasSiteWithin scans up to 91x91 tiles per idle ship, scores
		// every candidate, keeps one, and throws the rest away — then does it again next
		// turn, and again for the next ship, which is also why two hulls would sail for the
		// same beach without either knowing the other had looked.
		//
		// Recording what the scan already discovered turns that work into an asset:
		// exploration accumulates instead of evaporating, ships consult a list rather than
		// re-survey an ocean, and a claim marks a site so the second hull picks another.
		// It is deliberately a plain fact-store, not a plan — no scoring of our own
		// ambitions, no goals. If goal-directed behaviour is ever wanted, it reads this
		// rather than being tangled into it.
		//
		// Not persisted: AI instances are rebuilt at runtime and hold no saved state, so a
		// reloaded game re-surveys. That is consistent with the rest of the class and costs
		// only the first scan back.
		private sealed class ColonySite
		{
			public int X, Y;
			public byte Continent;
			public int Score;
			public int SurveyedTurn;
			public IUnit? Claimant;   // the hull already sailing for it
		}

		private readonly Dictionary<(int X, int Y), ColonySite> _colonyRegister = new();

		// Is this entry still worth having? Sites go stale when somebody founds on or beside
		// them, and claims lapse when the claimant is lost at sea.
		private bool StillViable(ColonySite site)
		{
			ITile t = Map[site.X, site.Y];
			if (t is null || t.IsOcean || t.City is not null) return false;
			if (Game.GetCities().Any(c => c.Size > 0 && Common.DistanceToTile(c.X, c.Y, site.X, site.Y) < 4))
				return false;
			if (site.Claimant is not null && !Game.GetUnits().Contains(site.Claimant))
				site.Claimant = null;
			return true;
		}

		// Everything the register knows, freshest survey first, minus the stale entries.
		internal int KnownColonySites()
		{
			foreach (var key in _colonyRegister.Keys.ToArray())
				if (!StillViable(_colonyRegister[key])) _colonyRegister.Remove(key);
			return _colonyRegister.Count;
		}

		private void RecordColonySite(ITile tile, int score)
		{
			var key = (tile.X, tile.Y);
			if (_colonyRegister.TryGetValue(key, out ColonySite? existing))
			{
				existing.Score = score;
				existing.SurveyedTurn = (int)Game.GameTurn;
				return;
			}
			_colonyRegister[key] = new ColonySite
			{
				X = tile.X, Y = tile.Y,
				Continent = tile.ContinentId,
				Score = score,
				SurveyedTurn = (int)Game.GameTurn
			};
		}

		// Fold another civ's colony register into ours. This is what a map is worth: the
		// scan that builds a register only ever sees within 45 tiles of one of OUR hulls, so
		// a coast on the far side of the world is unreachable knowledge no amount of our own
		// surveying can produce. A partner who has sailed there can simply tell us.
		//
		// Claims are not copied — their hull's business is theirs. Staleness is not filtered
		// here either; BestOverseasSite prunes the whole register on every call, so a site
		// they knew about and somebody has since settled drops out on first use.
		internal void MergeColonyRegister(AI other)
		{
			int gained = 0;
			foreach (var kv in other._colonyRegister)
			{
				if (_colonyRegister.ContainsKey(kv.Key)) continue;
				ColonySite src = kv.Value;
				_colonyRegister[kv.Key] = new ColonySite
				{
					X = src.X, Y = src.Y,
					Continent = src.Continent,
					Score = src.Score,
					SurveyedTurn = src.SurveyedTurn,
					Claimant = null
				};
				gained++;
			}
			if (gained > 0)
				Log($"{Player.TribeName}: charts from {other.Player.TribeName} add {gained} colony sites");
		}

		// Best unclaimed site this hull could sail to, from memory alone.
		private ITile? RegisteredSite(IUnit boat)
		{
			byte from = boat.Tile?.ContinentId ?? MISC_CONTINENT;
			foreach (ColonySite site in _colonyRegister.Values
				.Where(s => s.Claimant is null || s.Claimant == boat)
				.OrderByDescending(s => s.Score - Common.DistanceToTile(boat.X, boat.Y, s.X, s.Y)))
			{
				if (!StillViable(site)) continue;
				// Somewhere we could not simply have walked.
				if (Map.NamedContinent(from) && site.Continent == from) continue;
				if (Common.GotoStep(boat, site.X, site.Y) is null) continue;   // no sailable route
				site.Claimant = boat;
				return Map[site.X, site.Y];
			}
			return null;
		}

		internal ITile? BestOverseasSite(IUnit boat)
		{
			// Memory first. A hull that already knows where the good coast is does not need
			// to survey the ocean again, and the claim keeps the next hull off the same beach.
			foreach (var key in _colonyRegister.Keys.ToArray())
				if (!StillViable(_colonyRegister[key])) _colonyRegister.Remove(key);
			if (RegisteredSite(boat) is ITile remembered) return remembered;

			ITile? site = BestOverseasSiteWithin(boat, OverseasRange)
			           ?? BestOverseasSiteWithin(boat, OverseasRangeFar);

			// One path probe on the winner. A coast 45 tiles off may sit in a different
			// ocean basin with no sailable route, and without this the boat would set
			// Goto, fail the step, clear Goto and re-pick the same tile every turn. The
			// probe runs only when an idle boat chooses a target, not per turn — Goto
			// persists across the crossing. Land is impassable to a ship, but GotoStep
			// exempts the goal tile, so a coastal target resolves correctly.
			if (site is not null && Common.GotoStep(boat, site.X, site.Y) is null)
				return null;

			if (site is not null && _colonyRegister.TryGetValue((site.X, site.Y), out ColonySite? chosen))
				chosen.Claimant = boat;
			return site;
		}

		private ITile? BestOverseasSiteWithin(IUnit boat, int range)
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			ITile? best = null;
			int bestScore = int.MinValue;
			byte from = boat.Tile?.ContinentId ?? MISC_CONTINENT;

			for (int dy = -range; dy <= range; dy++)
			for (int dx = -range; dx <= range; dx++)
			{
				int tx = (boat.X + dx + w) % w;
				int ty = boat.Y + dy;
				if (ty < 0 || ty >= h) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean || tile.City is not null) continue;
				if (tile is Arctic || tile is Mountains) continue;
				// Must be coast — the boat has to reach it.
				if (!tile.GetBorderTiles().Any(b => b is not null && b.IsOcean)) continue;
				// Somewhere we could not already have walked.
				if (Map.NamedContinent(from) && tile.ContinentId == from) continue;
				if (Game.GetCities().Any(c => c.Size > 0 && Common.DistanceToTile(c.X, c.Y, tx, ty) < 4)) continue;

				// Every viable candidate goes into the register, not just the winner — the
				// scan has already paid for the survey, and next turn's ship should not
				// have to repeat it.
				int suitability = SiteSuitability(tile);
				RecordColonySite(tile, suitability);

				int score = suitability - Common.DistanceToTile(boat.X, boat.Y, tx, ty);
				if (score > bestScore) { bestScore = score; best = tile; }
			}
			return best;
		}

		// Nearest site worth founding on. Searched at ±8 first; if that finds nothing, the
		// search widens once to ±16 before the settler gives up and drifts home.
		//
		// The narrow window is right for a civ with room at its elbow, and it keeps the
		// per-settler scan small. But a settler that finds nothing simply returns to its
		// nearest city and waits — so any gap further out than 8 tiles is invisible
		// forever, however long the civ stands there. Japan's city chain runs 22 tiles
		// north to south with its settlers clustered at the southern end: the northern
		// gaps could never be seen, the chain could never fill, and BoxedIn (which gates
		// the Longboat) could never become true. Only civs that have already failed the
		// cheap search pay for the wide one.
		// TEMPORARY probe (2026-08-02) — see TurnMetrics.AddBucket. This one scans
		// 13x13 and then, on failure, 33x33 = 1089 tiles with a SiteSuitability call
		// each, so it is the leading suspect for the 25 ms unit move.
		// ONE definition of where a city may stand, shared by the founder (AI.MoveInner) and
		// by the site scan below — for exactly the reason WorkAvailable exists. They stated
		// it separately and disagreed: the scan rejected only ocean and occupied tiles, while
		// the founder also refuses Arctic and Mountains. So the scan could route settlers to
		// a mountain the founder would refuse forever.
		//
		// Observed at 1804 AD in a 377-turn game: SIX Malian settlers, at (167,107) through
		// (168,110), every one of them targeting (167,111) — Mountains. They converged,
		// could not found, lost their Goto, drifted home and re-targeted the same peak the
		// next turn. The one already standing on it scored its own tile, which the caller
		// discards as "nowhere to go", so it did nothing at all. None of them logged a single
		// settler action, which is why the decision log looked healthy throughout.
		internal bool CanFoundOn(ITile tile)
			=> tile is not null && tile.City is null && (
				(!tile.IsOcean && !(tile is Arctic) && !(tile is Mountains)) ||
				(tile.IsOcean && Player.HasAdvance<AquaticColonization>()));

		// Can the tile under the city ever produce food?
		//
		// The centre is the one tile a city is guaranteed to work, from its first turn, for
		// free, forever — and it is the only tile it can never reassign. ShieldValue and
		// TradeValue floor it at 1 apiece, but there is NO food floor, so a city centred on
		// Salt Flat begins at zero food from its own tile and stays there: Salt Flat yields 0
		// food and is not irrigable at any point in the game.
		//
		// Measured across two 2200 AD runs, the Malians founded 1 city on Salt Flat and then
		// 8 — the rise almost certainly ours, since routing settlers off Mountains sent that
		// traffic somewhere and Salt Flat was already legal.
		//
		// Tested on the terrain TYPE, deliberately, and not on IrrigationFoodBonus: Desert
		// reports -2 there and irrigates perfectly well (Desert.cs:22 — Food becomes 1 once
		// irrigated). The sign is a yield modifier on some terrain and a "never" flag on
		// others, so it cannot carry this question. The type list is AllowIrrigation's.
		internal static bool CentreCanFeed(ITile tile)
		{
			if (tile is null) return false;
			if (tile.Food > 0) return true;
			return tile is Desert or Grassland or Hills or Plains or River;
		}

		internal ITile? BestSettleSite(IUnit settlers)
			=> BestSettleSiteWithin(settlers, 8) ?? BestSettleSiteWithin(settlers, 16);

		private ITile? BestSettleSiteWithin(IUnit settlers, int radius)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile? best = null;
			int bestScore = int.MinValue;

			byte ownId = Game.PlayerNumber(Player);
			var claimedGotos = new System.Collections.Generic.HashSet<(int, int)>(
				Game.GetUnits().OfType<Settlers>()
				    .Where(s => s != settlers && s.Owner == ownId && !s.Goto.IsEmpty)
				    .Select(s => (s.Goto.X, s.Goto.Y)));

			for (int dy = -radius; dy <= radius; dy++)
			for (int dx = -radius; dx <= radius; dx++)
			{
				int tx = (settlers.X + dx + mapWidth) % mapWidth;
				int ty = settlers.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				// The IsOcean test stays alongside CanFoundOn rather than folded into it: a
				// settler on foot cannot walk to water even where AquaticColonization makes
				// it foundable, and floating cities are placed by their own path.
				if (tile is null || tile.IsOcean || !CanFoundOn(tile)) continue;
				// ...and it has to be able to feed itself. See CentreCanFeed.
				if (!CentreCanFeed(tile)) continue;
				if (!LandReachable(settlers, tile)) continue;
				if (Game.GetCities().Any(c => Common.DistanceToTile(c.X, c.Y, tx, ty) < 4)) continue;
				if (claimedGotos.Contains((tx, ty))) continue;
				int score = SiteSuitability(tile);
				if (score > bestScore) { bestScore = score; best = tile; }
			}
			return best;
		}

		// Nearest tile worth terraforming near our own cities — an un-irrigated farm tile
		// next to fresh water, inside a city's work radius. A built-out empire sends its
		// settlers here to raise city food (irrigation) instead of founding ever-smaller
		// towns; this is the fix for AI cities stalling at ~+0.8 food/turn. Null when there's
		// nothing useful to improve nearby.
		// Farm first, then mines, then the rail upgrade — food leads, and each pass runs only
		// when the one before it found nothing in range.
		// ── settler work eligibility ──────────────────────────────────────────────
		//
		// ONE definition of what work a tile can take, shared by both halves of the settler
		// AI. They used to state it independently and drifted apart three times, each time
		// the same way: ChooseSettlerImprovement was willing to do work that BestImproveSite
		// would never route a settler TO, so the work only ever happened where a settler
		// already happened to be standing.
		//
		//   rails      a full 750-turn game ended with SEVEN railed tiles in the world
		//   mines      Hills are never `farmable`, so mine sites were unreachable
		//   converts   a drained swamp kept a stale ContinentId and left the routable world
		//              altogether (fixed in Map.ChangeTileType, +16pp improved land)
		//
		// The division of labour, which is what stops this recurring:
		//   WorkAvailable          — what CAN be done on this tile. Eligibility, nothing else.
		//   ChooseSettlerImprovement — which of those to do, given stance and government.
		//   BestImproveSite        — which tile is worth WALKING to.
		//
		// The measurements behind the current ordering live at ChooseSettlerImprovement, and
		// those behind the worker quota at the settler production rule. Both are negative
		// results — approaches that looked right and measured worse. Do not re-derive them
		// from the code; the code only records the winner.
		internal readonly struct TileWork
		{
			public readonly bool Irrigation, Conversion, Mine, NewRoad, RoadUpgrade;
			// The dry-ground pair. Both add food where irrigation cannot reach, because
			// irrigation needs fresh water in the cross and continental interiors have none.
			public readonly bool Terrace, MoistureFarm;
			public TileWork(bool irrigation, bool conversion, bool mine, bool newRoad, bool roadUpgrade,
			                bool terrace = false, bool moistureFarm = false)
			{
				Irrigation = irrigation; Conversion = conversion; Mine = mine;
				NewRoad = newRoad; RoadUpgrade = roadUpgrade;
				Terrace = terrace; MoistureFarm = moistureFarm;
			}
			public bool Road => NewRoad || RoadUpgrade;
		}

		internal TileWork WorkAvailable(ITile? tile)
		{
			if (tile is null || tile.City is not null) return default;

			// Draining a swamp and clearing a jungle or forest are IRRIGATE orders in Civ 1,
			// and Settlers.BuildIrrigation (Settlers.cs:438) implements all three — 4 turns,
			// no water source needed, converting the tile to open ground. The AI could not
			// reach any of it: swamp, jungle and forest appeared only as a water SOURCE for a
			// neighbouring farm tile, never as a target.
			//
			// That left terrain the AI treated as permanently worthless. Measured on a
			// turn-578 save, 30% of Japan's worked land was swamp, jungle or forest —
			// Kamakura worked 6 swamp tiles out of 13 and Kagoshima sat at -1 food — and no
			// settler would ever touch one. Swamp yields 1 food; drained it is grassland at
			// 2, and can then be irrigated again for 3.
			bool conversion = tile is Swamp || tile is Jungle || tile is Forest;
			// `x.City is null` — the missing clause, and the third time this file's own
			// warning has come true: the rule was stated here independently of the order
			// that enforces it (Settlers.BuildIrrigation:476) and drifted.
			//
			// A CITY TILE IS NOT A WATER SOURCE. The order refuses one; this predicate
			// accepted one. So every tile whose only cardinal water was a city — a city
			// founded on a river, or on ground that was irrigated before it was built —
			// looked farmable to the AI and was rejected on arrival. On a developed map
			// that is a large share of the countryside: the human sees "This tile has no
			// water source" dozens of times a round, and an AI settler, which gets no
			// message at all, silently burns its turn and comes back tomorrow.
			//
			// HILLS ARE FARMABLE, and the AI could not see it. Settlers.BuildIrrigation
			// accepts Hills, Hills.Food reads Irrigation (1 -> 2), the human's auto-improve
			// admits them and TileExtensions.AllowIrrigation lists them — every path but this
			// one, which is the only path an AI settler takes. So the AI could MINE a hill
			// (mine, below, has always included them) and never farm one.
			//
			// Harmless while hills were 6.9% of land. After the relief-based Earth map made
			// them 26.5%, it decided games. A hill yields 1 food and a citizen eats 2, so a
			// city on hills is stuck the moment it outgrows its own centre tile: centre
			// (auto-irrigated at founding, 2 food) plus two hills at 1 is 4 food for 4
			// mouths, and it never grows again. Measured over 750 turns on the new map, the
			// Khmer held 2 cities at mean size 1.9, the Japanese 1, the Persians none —
			// while the Lakota and Russians, on flat ground, grew as they always had.
			//
			// The water-source requirement is kept and is what stops this going too far: a
			// dry upland has no cardinal water and is still mine country. Only hills beside a
			// river, lake or an already-irrigated tile become farmland — which is where
			// people actually farm hills.
			//
			// DespotBlocksIrrigation already exempts Hills (their bonus is in ITile.Food, not
			// the government-gated branch), so this works in the early game, which is the only
			// part of it the strangled civs ever reached.
			bool irrigation = !tile.Mine && !tile.Irrigation
				&& (conversion
				    || ((tile is Grassland || tile is River || tile is Plains || tile is Desert
				         || tile is Hills)
				        && tile.HasIrrigationSource()));
			bool mine = (tile is Mountains || tile is Hills) && !tile.Mine && !tile.Irrigation;

			// Mirror Settlers.BuildRoad's eligibility checks: a brand-new road on a River tile
			// requires Bridge Building. Without this guard the AI loops indefinitely
			// (road looks valid → enqueue BuildRoad → silent fail → SkipTurn → repeat).
			bool newRoad = !tile.Road && !tile.RailRoad
				&& (!(tile is River) || Player.HasAdvance<BridgeBuilding>());
			// Rail is the end of the land upgrade path. The tube used to sit above it here, but
			// tubes are sea-only now (see Settlers.BuildRoad) — and leaving the clause would
			// have routed settlers to railroads to start a tube BuildRoad refuses, which is the
			// scan-and-gate disagreement that has already cost this project four bugs.
			bool upgrade = tile.Road && !tile.RailRoad && Player.HasAdvance<RailRoad>();
			if (tile.TransportTube) { newRoad = false; upgrade = false; }

			// Terracing and moisture farming. Both mirror Settlers.BuildTerrace /
			// BuildMoistureFarm exactly — terrain, tech and "not already there" — because a
			// scan that offers work the builder refuses is the disagreement this file has
			// four separate bug comments about. Salt Flat appears in neither: it is meant to
			// stay good for nothing.
			bool terrace = tile is Hills && !tile.Terrace && Player.HasAdvance<Masonry>();
			bool moisture = tile is Desert && !tile.MoistureFarm && Player.HasAdvance<Refining>();

			return new TileWork(irrigation, conversion, mine, newRoad, upgrade, terrace, moisture);
		}

		// Under Despotism and Anarchy the tile penalty claws back any yield above 2, so
		// irrigation pays on some terrain and not others — and that is a statement about SOME
		// terrain, not all of it. Blanket-skipping irrigation under Despotism froze whole
		// civs solid: measured at 1900 AD, the English held seven size-7 cities on a fully
		// roaded island with four settlers doing literally nothing, and finished on six
		// advances, unable to research their way out of the government freezing them.
		//
		// Which terrain pays follows from City.FoodValue. Plains and Hills carry their
		// irrigation bonus in ITile.Food itself (1 -> 2), unconditionally, and Desert
		// likewise (0 -> 1) — none exceeds 2, so the penalty never bites. Grassland and River
		// are flat 2 and take their bonus from the government-gated branch, so irrigating
		// those really does yield nothing until Monarchy. Conversion is exempt for the same
		// reason: it moves 1 -> 2.
		internal bool DespotBlocksIrrigation(ITile? tile)
			=> (Player.Government is Gov.Despotism || Player.Government is Gov.Anarchy)
			   && !(tile is Swamp || tile is Jungle || tile is Forest
			        || tile is Plains || tile is Hills || tile is Desert);

		// ── camp-seeking ──────────────────────────────────────────────────────────
		//
		// Camps were opportunistic only: a settler that happened to be standing on an
		// unclaimed deposit claimed it, and nothing ever walked to one. Measured over a
		// finished 750-turn game that came to 0.7% of all AI tile work, so a civ with no iron
		// in any city radius simply paid the penalty for the rest of the game.
		//
		// The payoff is specific and worth stating, because it decides the whole design:
		// Game.HasResource is satisfied by a city working the deposit OR a camp on it, and
		// lacking a resource costs +50% shields on anything that needs it
		// (City.ProductionCost). So a camp is worth walking to ONLY for a resource this civ
		// does not already hold — a second iron deposit removes no penalty. The shields a
		// remote camp ships home are real but small, and chasing them would send settlers
		// across the map for a couple of hammers, which is the settler-shuttle bug again.
		private const int CampSearchRange = 12;

		// Which materials are we paying the penalty on?
		private StrategicResource[] MissingResources()
		{
			var missing = new List<StrategicResource>();
			foreach (StrategicResource r in new[] { StrategicResource.Iron, StrategicResource.Coal, StrategicResource.Oil })
				if (!Game.Instance.HasResource(Player, r)) missing.Add(r);
			return missing.ToArray();
		}

		// The nearest deposit worth claiming, or null. Shares Settlers.CanCampOn with the
		// builder so the scan cannot route a settler to a tile BuildCamp will refuse.
		internal ITile? BestCampSite(IUnit settler)
		{
			StrategicResource[] wanted = MissingResources();
			if (wanted.Length == 0) return null;

			int w = Map.WIDTH, h = Map.HEIGHT;
			byte me = Game.PlayerNumber(Player);
			byte from = settler.Tile?.ContinentId ?? MISC_CONTINENT;
			ITile? best = null;
			int bestDistance = int.MaxValue;

			for (int dy = -CampSearchRange; dy <= CampSearchRange; dy++)
			for (int dx = -CampSearchRange; dx <= CampSearchRange; dx++)
			{
				int tx = (settler.X + dx + w) % w;
				int ty = settler.Y + dy;
				if (ty < 0 || ty >= h) continue;
				ITile tile = Map[tx, ty];
				if (!Settlers.CanCampOn(tile)) continue;
				if (!wanted.Contains(Game.ResourceAt(tile))) continue;
				// Walkable from here. A deposit across water needs a boat, and a settler that
				// targets one paces the shoreline instead of working.
				if (Map.NamedContinent(from) && tile.ContinentId != from) continue;
				// One settler per deposit: without this every idle settler in the empire
				// converges on the same hill — the exact pathology six Malian settlers showed
				// against a mountain, and the reason BestSettleSite claims its sites.
				if (Game.GetUnits().Any(u => u is Settlers && u != settler && u.Owner == me
				                          && !u.Goto.IsEmpty && u.Goto.X == tx && u.Goto.Y == ty))
					continue;

				int distance = Common.DistanceToTile(settler.X, settler.Y, tx, ty);
				if (distance < bestDistance) { bestDistance = distance; best = tile; }
			}
			return best;
		}

		private enum Pass { Farm, Mine, Rail }

		internal ITile? BestImproveSite(IUnit settlers) => BestImproveSiteInner(settlers);

		private ITile? BestImproveSiteInner(IUnit settlers)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			byte ownId = Game.PlayerNumber(Player);
			// Claimed is Settlers.IsTileClaimed's rule, and it has to be BOTH halves of it or
			// this scan sends settlers to tiles the work gate will refuse.
			//
			// A settler that has ARRIVED and is building has an empty Goto — Goto clears on
			// arrival — and it is Busy for the two to five turns the job takes. The old set
			// only looked at Goto, so that tile stayed invisible here while IsTileClaimed
			// (checked at AI.cs:376, and it tests `s.X == tx && s.Y == ty && s.Busy`) refused
			// it. Meanwhile the tile still reads as work available, because irrigation does not
			// land on the tile until the build completes.
			//
			// So every idle settler within six tiles was routed to ground already being worked,
			// arrived, was refused, found the same tile is the nearest site again — now at
			// distance 0, which the caller discards — and drifted home to start over. That is
			// the pair racing between Byblos and Leeds, and it repeats for as long as the build
			// lasts, which is why it is always several settlers at once.
			var claimed = new System.Collections.Generic.HashSet<(int, int)>();
			foreach (Settlers other in Game.GetUnits().OfType<Settlers>()
			                               .Where(s => s != settlers && s.Owner == ownId))
			{
				if (!other.Goto.IsEmpty) claimed.Add((other.Goto.X, other.Goto.Y));
				if (other.Busy) claimed.Add(((int)other.X, (int)other.Y));
			}

			// The city-proximity test below was an O(cities) scan run per tile, per pass —
			// 169 tiles x 3 passes x every city the player owns, for every settler, every
			// turn. At 2147 AD with 257 cities in the world that is the settler AI's single
			// hottest loop, and raising the worker quota multiplies it.
			//
			// Hoisted, and exactly: DistanceToTile is Chebyshev (Common.cs:240), so for any
			// tile in the 6-tile scan box, d(city, settler) <= d(city, tile) + d(tile, settler)
			// <= 2 + 6. A city more than 8 away therefore cannot be within 2 of anything
			// scanned. Same answers, one pass over the city list instead of 507.
			City[] nearCities = Player.Cities
				.Where(c => Common.DistanceToTile(c.X, c.Y, settlers.X, settlers.Y) <= 8)
				.ToArray();

			ITile? best = null;
			int bestDist = int.MaxValue;
			Scan(Pass.Farm);
			// Mines had the same missing routing path railroads did: ChooseSettlerImprovement is
			// willing to mine a hill, but the site scan below demands `farmable`, and Hills and
			// Mountains are never farmable — so a settler only ever mined a tile it was already
			// standing on. Worst under Despotism, where the farm pass ALSO skips grassland and
			// river (the tile penalty makes irrigating them worthless): a despot civ on grass
			// and hills had no routable work of any kind, which is the "settlers standing
			// still" symptom logged against the English at 1900 AD.
			//
			// Terrain-dependent, and honestly modest: measured over 400 turns on seeds
			// 101/202/303, mined tiles went 2/4/1 -> 2/23/1. Two of the three maps have no
			// hills inside a city radius that a settler ever runs out of farm work beside,
			// so nothing changed there at all; the third quintupled. No seed regressed.
			if (best is null) Scan(Pass.Mine);
			// A rail upgrade had NO ROUTING PATH. The tile that wants a railroad is by
			// definition already irrigated or mined, and the farm scan rejects exactly those,
			// so a settler only ever railed a tile it happened to be standing on already —
			// the last full game ended with SEVEN railed tiles in the entire world, and every
			// civ holding Railroad finished at 6-9% of its worked land improved.
			//
			// This does not reorder anything: ChooseSettlerImprovement still puts food first,
			// and this pass runs only when there is no farm work left within reach.
			if (best is null && Player.HasAdvance<RailRoad>()) Scan(Pass.Rail);
			return best;

			void Scan(Pass pass)
			{
			for (int dy = -6; dy <= 6; dy++)
			for (int dx = -6; dx <= 6; dx++)
			{
				int tx = (settlers.X + dx + mapWidth) % mapWidth;
				int ty = settlers.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean || tile.City is not null) continue;
				if (!LandReachable(settlers, tile)) continue;
				// Eligibility is WorkAvailable's, not restated here. What IS this method's
				// own judgement is the despot rule: a settler routed to grassland it cannot
				// usefully irrigate is a settler sent to stand still.
				TileWork work = WorkAvailable(tile);
				bool wanted = pass switch
				{
					Pass.Mine => work.Mine,
					Pass.Rail => work.RoadUpgrade,
					// Terrace and moisture farm ride the FARM pass rather than getting one of
					// their own: they are food work, they sit on the same tiles the farm pass
					// already walks, and a separate pass would only run after farming found
					// nothing — which on dry ground is exactly when they are needed most.
					// Neither is subject to DespotBlocksIrrigation: that rule is about the
					// despotic penalty clawing back irrigation's third point of food, and
					// these two are adding a first or second one.
					_         => (work.Irrigation && !DespotBlocksIrrigation(tile))
					             || work.Terrace || work.MoistureFarm
				};
				if (!wanted) continue;
				if (claimed.Contains((tx, ty))) continue;
				if (!nearCities.Any(c => Common.DistanceToTile(c.X, c.Y, tx, ty) <= 2)) continue;
				int d = Common.DistanceToTile(settlers.X, settlers.Y, tx, ty);
				// Ties broken by what the finished tile is worth, not by scan order.
				//
				// This started mattering the moment hills became farm work: an irrigated hill
				// is 2 food and NO shields, an irrigated plain is 2 food and 1 shield, so at
				// equal distance the plain is strictly better. Distance alone let the hill win
				// on nothing but its position in the row-major scan — the same trap as the
				// Arctic harvester landings and the north-west harvester march.
				if (d < bestDist || (d == bestDist && best is not null && tile.Shield > best.Shield))
				{ bestDist = d; best = tile; }
			}
			}
		}

		// ── unit mission assignment ────────────────────────────────────────────
		// Sets unit.Goto; leaves it empty if no useful mission is found.

		// ── attack staging ────────────────────────────────────────────────────────

		// ── hunting loose monsters ────────────────────────────────────────────────
		//
		// PickAttackTarget returns a CITY, and nothing anywhere in the AI says "there is a
		// thing standing in our fields, go kill it". The only response to barbarians was
		// defensive — a hostile within 3 tiles of a city earns a second defender — so
		// barbarian-owned megafauna were untouchable by anybody but a human player. In the
		// 1892 AD run six Scavenger harvesters drank a world dry across 120 turns and not one
		// AI civ moved on them; the same immunity covered the kaiju and the Henge Guardian.
		//
		// Local threat, local answer. This deliberately does NOT put the empire on a war
		// footing (barbarians still don't count in NearHostiles, for the reasons written
		// there) and it is stance-independent: a civ clears monsters off its own ground
		// whatever its grand strategy says.
		private const int HuntRadius = 8;

		// The barbarian-owned things that are worth walking out to kill. All of them are
		// unbuildable and answer to nobody's diplomacy, which is what separates them from an
		// ordinary raiding party — a raid moves on, these do not.
		internal static bool IsMegafauna(IUnit u)
			=> u is Units.Harvester || u is Units.Gozira
			|| u is Units.Leviathan || u is Units.HengeGuardian;

		// A fortified unit is never offered to AI.Move at all — the unit-selection loop in
		// Game.cs skips anything with Sentry or Fortify set — so it never reaches AssignMission
		// and never reaches HuntQuarry. The AI fortifies attackers routinely: on a failed
		// advance (HandleMovementFailure -> DisembarkAndFortify) and whenever an idle one is
		// parked in an under-defended city.
		//
		// That is why the 1872 AD harvest went unopposed. The civs held 337 Armor — attack 10
		// against a defence-6 harvester, every one an eligible hunter — and 39% of the damage
		// fell within the hunt radius of a city. The hunt was not wrong; the hunters were
		// asleep, and a sleeping unit cannot decide anything.
		//
		// So wake them, narrowly: only LandAttack units, only when there is something in reach
		// they can actually beat. The general fortify behaviour is untouched, and a woken unit
		// that finds nothing to do simply fortifies again.
		// Once per player turn, before the unit loop runs.
		internal void TurnStart()
		{
			// _settlerMisses keys are unit references, and settlers die. Drop the dead so a
			// 750-turn game does not accumulate them.
			if (_settlerMisses.Count > 0)
			{
				var live = new System.Collections.Generic.HashSet<IUnit>(Game.Instance.GetUnits());
				foreach (IUnit gone in _settlerMisses.Keys.Where(u => !live.Contains(u)).ToArray())
					_settlerMisses.Remove(gone);
			}

			WakeHunters();
		}

		internal void WakeHunters()
		{
			// Cheap exit for the ordinary case: no monsters anywhere, which is most of a game.
			if (!Game.Instance.GetUnits().Any(u => u.Owner == 0 && IsMegafauna(u))) return;

			byte own = Game.PlayerNumber(Player);
			// Any land unit, not just the ones filed as attackers — see HuntQuarry for why.
			foreach (IUnit unit in Game.Instance.GetUnits()
				.Where(u => u.Owner == own && u.Class == UnitClass.Land && (u.Fortify || u.Sentry))
				.ToArray())
			{
				if (HuntQuarry(unit) is null) continue;
				// Busy, not Fortify = false. `Fortify` reads `_fortify || FortifyActive` but its
				// setter only clears `_fortify`, so assigning false to a unit the AI fortified
				// (which sets FortifyActive) leaves it fortified and the wake-up does nothing.
				// The Busy setter is the one that clears all three flags.
				unit.Busy = false;
				unit.Goto = Point.Empty;   // AssignMission only runs on an idle unit
			}
		}

		// Eligibility is CAPABILITY, not job title. It used to be Role == LandAttack, which is
		// how a civ could own the answer and never be offered it: in the 1804 AD Scavenger
		// game the Lakota held 8 MechInf — attack 6, exactly enough for a defence-6 harvester
		// — with ten of their cities inside the hunt radius of one, and not one was ever
		// considered, because MechInf is filed Role.Defense. Their other 104 units were
		// Musketeers and Riflemen, and the Haida's entire 61-unit army topped out at attack 3.
		// Forty-six harvesters drank that world with nineteen of them parked next to a city.
		//
		// The attack >= defence test below already refuses anything that cannot win, so the
		// role test was contributing nothing except the exclusion.
		//
		// (It does not fix a civ that owns nothing capable — the Haida need to BUILD something,
		// which is the production-priority half and is not this.)
		internal IUnit? HuntQuarry(IUnit hunter)
		{
			if (hunter.Attack == 0) return null;
			if (hunter.Class != UnitClass.Land) return null;
			// Widening past the attack roles means garrisons are now eligible, so the garrison
			// has to be protected explicitly: never send the last unit holding a city. Marching
			// the sole defender out to kill a harvester and losing the city to the next raider
			// is a worse trade than the drained tiles.
			if (IsSoleGarrison(hunter)) return null;

			City[] cities = Player.Cities;
			if (cities.Length == 0) return null;

			return Game.GetUnits()
				.Where(u => u.Owner == 0 && IsMegafauna(u))
				// Only what this unit can plausibly beat. Feeding Legions one at a time to a
				// defence-6 harvester is worse for the civ than ignoring it, and it is how an
				// AI empties its army into a wall. Gozira (defence 24) is nobody's quarry,
				// which is correct: it is a catastrophe, not a boss fight.
				.Where(u => hunter.Attack >= u.Defense)
				// A land unit cannot reach something standing in the sea, and cannot walk to
				// another continent — the engine has no naval transport AI.
				.Where(u => !Map.Instance[u.X, u.Y].IsOcean
				         && LandReachable(hunter, Map.Instance[u.X, u.Y]))
				// Our ground, not a crusade: something has to be near a city of ours.
				.Where(u => cities.Any(c => Common.DistanceToTile(c.X, c.Y, u.X, u.Y) <= HuntRadius))
				.OrderBy(u => Common.DistanceToTile(hunter.X, hunter.Y, u.X, u.Y))
				.FirstOrDefault();
		}

		// Is this unit the only thing standing between one of our cities and whoever walks up
		// next? Counts anything with a defence value on the city tile, not just Role.Defense —
		// a stack of Armor holds a city perfectly well.
		private static bool IsSoleGarrison(IUnit unit)
		{
			City? city = Map.Instance[unit.X, unit.Y]?.City;
			if (city is null || city.Owner != unit.Owner) return false;
			return !Game.Instance.GetUnits().Any(u => u != unit && u.Owner == unit.Owner
				&& u.X == unit.X && u.Y == unit.Y && u.Class == UnitClass.Land && u.Defense > 0);
		}

		private City PickAttackTarget()
		{
			// Prefer the weakest (fewest defenders) visible enemy city closest to our empire.
			// Barbarians (P0) are treated as always hostile even without a formal war state.
			// Same-continent filter: we have to walk attackers to the target, and the engine
			// has no naval transport AI yet — picking an off-continent target wedges every
			// attacker on the staging tile (GotoStep returns null for cross-continent paths).
			// "Same continent" = at least one of our cities shares a ContinentId with the target.
			var ownContinents = new HashSet<byte>(Player.Cities
			    .Where(oc => oc.Tile is not null)
			    .Select(oc => oc.Tile.ContinentId)
			    .Where(Map.NamedContinent));
			bool reachable(City c) => ownContinents.Count == 0
			    || (c.Tile is not null
			        && Map.NamedContinent(c.Tile.ContinentId)
			        && ownContinents.Contains(c.Tile.ContinentId));

			var candidates = Game.GetCities()
			    .Where(c => c.Player != Player
			             && (Player.IsAtWar(c.Player) || c.Owner == 0)
			             && Player.Visible(c.X, c.Y)
			             && reachable(c));

			// When the human is dominant and we're at war with them, hit their cities first.
			Player? human = HumanOpponent;
			if (HumanIsDominant() && human is not null && Player.IsAtWar(human))
			{
				City humanCity = candidates
				    .Where(c => c.Player == human)
				    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
				    .ThenBy(c => Player.Cities.Min(oc => Common.DistanceToTile(oc.X, oc.Y, c.X, c.Y)))
				    .FirstOrDefault();
				if (humanCity is not null) return humanCity;
			}

			return candidates
			    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
			    .ThenBy(c => Player.Cities.Min(oc => Common.DistanceToTile(oc.X, oc.Y, c.X, c.Y)))
			    .FirstOrDefault();
		}

		// Where to mass for an assault: the neighbouring tile of `target` that already holds
		// the most of our attackers and none of theirs.
		//
		// ONE pass over the unit list, not sixteen. `ITile.Units` is Game.GetUnits(x, y),
		// which scans EVERY unit in the game, sorts the matches and allocates an array — and
		// the old version called it twice for each of eight neighbours, so choosing a staging
		// square cost 16 full scans, 16 sorts and 16 allocations. Every attacking unit does
		// this on every move.
		//
		// Measured at turn 655 of a live 2,045-unit game: Armor alone spent 55.4 seconds
		// across 3,628 moves in a single turn — 84% of all unit-movement time — at roughly
		// 15 ms a move, against a 66-second turn. The cost is (attackers x all units), so it
		// grew as the world filled: 59s at turn 650, 66s five turns later.
		//
		// The same fix, for the same reason, is already in Common.GotoStepInner (a per-search
		// occupancy bitmask instead of ITile.Units per neighbour, once worth "~100ms per path
		// and 80% of the late-game turn") and in Map.NumberWaterBodies. This is the third
		// place it was needed.
		//
		// Behaviour is deliberately unchanged, including the tie-break: candidates are visited
		// in the same dy/dx order and the comparison is still strictly-greater, so an equal
		// count keeps the earlier tile.
		private ITile? StagingTile(City target)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			byte own = Game.PlayerNumber(Player);

			// The eligible neighbours, in the original scan order.
			var candidates = new List<ITile>(8);
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (target.X + dx + mapWidth) % mapWidth;
				int ty = target.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean) continue;
				candidates.Add(tile);
			}
			if (candidates.Count == 0) return null;

			// One walk of the units, filling both answers at once. No allocation per tile, no
			// sorting, and the inner loop is at most eight integer comparisons.
			int n = candidates.Count;
			bool[] hostile = new bool[n];
			int[] friendly = new int[n];
			foreach (IUnit u in Game.GetUnits())
			{
				if (u is null) continue;
				for (int i = 0; i < n; i++)
				{
					if (u.X != candidates[i].X || u.Y != candidates[i].Y) continue;
					if (u.Owner != own) hostile[i] = true;
					else if (u.Role == UnitRole.LandAttack) friendly[i]++;
					break;
				}
			}

			ITile? best = null;
			int bestCount = -1;
			for (int i = 0; i < n; i++)
			{
				if (hostile[i]) continue;   // don't stage on a tile already holding enemies
				if (best is null || friendly[i] > bestCount) { best = candidates[i]; bestCount = friendly[i]; }
			}
			return best;
		}

		// ── naval transport helpers ───────────────────────────────────────────────

		// Ocean tile adjacent to a city — where a transport can drop troops.
		private ITile? LandingTile(City target)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (target.X + dx + mapWidth) % mapWidth;
				int ty = target.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is not null && tile.IsOcean) return tile;
			}
			return null;
		}

		// Where a landlocked-out settler can catch a boat: an empty-ish transport of ours
		// parked in or beside one of our coastal cities. Returns its tile, which the settler
		// walks onto to board. Nothing else in the AI ever put a settler on a ship, which is
		// why overseas colonisation only happened via the Longboat and therefore never.
		private ITile? BoardingTile(IUnit settler) => BoardingTileInner(settler);

		private ITile? BoardingTileInner(IUnit settler)
		{
			byte own = Game.PlayerNumber(Player);
			var berths = Game.GetUnits()
				.Where(u => u.Owner == own && u is IBoardable
				         && u.Tile is not null
				         && u.Tile.Units.Count(p => p.Class == UnitClass.Land) < ((IBoardable)u).Cargo)
				.Where(u => Player.Cities.Any(c => Common.DistanceToTile(c.X, c.Y, u.X, u.Y) <= 1))
				.ToArray();
			if (berths.Length == 0) return null;

			// Nearest berth within a sensible walk. No path probe: the berth is an OCEAN
			// tile, and GotoStep refuses those for a land unit — boarding is precisely the
			// move that is allowed to break that rule, so probing rejects every real berth.
			// A settler that cannot reach the coast simply fails to close the distance and
			// re-decides next turn.
			const int MaxWalkToPort = 8;
			return berths
				.Where(u => Common.DistanceToTile(settler.X, settler.Y, u.X, u.Y) <= MaxWalkToPort)
				.OrderBy(u => Common.DistanceToTile(settler.X, settler.Y, u.X, u.Y))
				.Select(u => u.Tile)
				.FirstOrDefault();
		}

		// True when an idle non-combat unit should NOT re-target this turn.
		//
		// This replaces a Sentry that looked right and was wrong. Diplomats and Caravans
		// with no reachable target were sentried, on the Explorer's precedent and its
		// comment about waking "if something changes around it". That wake does not exist
		// for AI units: BaseUnit.cs:604 gates it on `Human == u.Owner`, so an AI unit that
		// sentries is parked for the rest of the game. A 2104 AD map had idle caravans
		// standing about everywhere, and they were mine.
		//
		// Sentry IS right for the Explorer, and the difference is worth stating: exploration
		// is monotonic — once the map is charted it stays charted, so there is nothing to
		// wake for. A caravan's target set is not monotonic. Wars end, blocking stacks move
		// on, cities are founded; a route that was shut reopens.
		//
		// So: retry, but not every turn — re-probing every turn for every idle unit is the
		// cost the Sentry was trying to avoid, and it was a real cost (4600 pathfinds a
		// turn). A parked unit does not move, so (X + Y) is a stable per-unit stagger:
		// each one looks again on its own turn in eight, and the fleet spreads its probing
		// across the cycle instead of all re-deciding at once.
		// Test seam: IdleRetryTurn is private and the stagger is the only thing worth
		// asserting on. Returns true when the unit is deferred this turn.
		internal bool TestIdleRetryDeferred(IUnit unit) => IdleRetryTurn(unit);

		private bool IdleRetryTurn(IUnit unit)
		{
			if (!unit.Goto.IsEmpty) return false;

			// Never defer a unit whose mission is one step away. A Caravan beside a foreign
			// city is about to deliver its trade route and a Diplomat beside one is about to
			// run its op — both re-target through this path, so making them wait their turn
			// in eight delays every delivery in the empire by up to seven turns. Eight tile
			// reads to check, against a full A* probe to skip.
			if (unit.Tile is not null && unit.Tile.GetBorderTiles()
			        .Any(t => t is not null && t.City is not null && t.City.Owner != unit.Owner))
				return false;

			// Stagger per UNIT, not per tile. (X + Y) looked like a per-unit key but it is a
			// per-TILE one: every idle unit parked in the same city shares it, so a city
			// holding thirty idle caravans re-probed all thirty on the same turn. Measured at
			// turn 708 of the 2026-08-03 run — 14.3s of an 18.5s turn was pathfinding, and
			// every spike landed on GameTurn & 7 == 4 while the other seven residues ran
			// 0.4-0.6s. The identity hash is stable for the unit's lifetime and spreads
			// evenly however the fleet is stacked. It does not survive save/load, which only
			// means a unit re-draws its slot in the cycle — harmless either way.
			return (System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(unit) & 7) != (Game.GameTurn & 7);
		}

		// ── colonisation ──────────────────────────────────────────────────────
		//
		// Whether this civ should be putting a settler on a boat right now.
		//
		// Colonisation used to be the LAST fallback. AI.cs reached the boarding branch
		// only when BestSettleSite AND BestImproveSite both came back empty — and
		// BestImproveSite returns any tile within 6 that wants irrigation, a mine or a
		// rail near one of our cities. A measured game finishes at 42-45% improved land,
		// so that scan is never empty, the boarding branch never runs, and an entire New
		// World sits unoccupied for the length of a game. For a settler to go to sea its
		// civ first had to improve every reachable tile around every city. Nobody clears
		// that bar.
		//
		// Same defect as the mines and the stance gate: a correct mechanism placed where
		// nothing reaches it. So colonists are DESIGNATED rather than left over — a small
		// standing quota that outranks local gardening, which is exactly the "settlers
		// explicitly assigned ... not a diversion applied to every settler that walks past
		// a bare tile" prescription the comment in AI.cs already wrote for the worker
		// problem.
		internal bool WantsColonist()
		{
			// Not at the expense of a young civ: a colonist costs a citizen and a hull
			// berth, and four cities is where that stops being the whole empire.
			if (Player.Cities.Length < 4) return false;
			// Nowhere charted to go. The register is filled by ships surveying
			// (RecordColonySite), so this also sequences the whole thing: hull first,
			// survey second, colonists third.
			if (KnownColonySites() == 0) return false;

			byte own = Game.PlayerNumber(Player);
			// Committed: settlers already aboard a hull (a land unit standing on ocean),
			// PLUS settlers still walking to one (Goto pointing at an ocean tile).
			//
			// The walkers matter. BoardingTile only rejects a berth once the hull is
			// physically full, and a settler three tiles from the coast has not boarded
			// anything yet — so without counting them, an epic-map civ with twenty
			// settlers could send half of them marching at one Trireme and stop improving
			// its land, which is the exact failure this change exists to fix, inverted.
			int committed = Game.GetUnits().Count(u => u.Owner == own && u is Settlers
				&& ((u.Tile is not null && u.Tile.IsOcean)
				    || (!u.Goto.IsEmpty && Map[u.Goto.X, u.Goto.Y] is ITile g && g.IsOcean)));
			// Two crossings at a time. Each costs a citizen, and the escort costs a unit
			// on top, so this is not a cheap standing programme.
			return committed < 2;
		}

		// The port's parting gift: one defender sails with the colonist.
		//
		// A colony that lands undefended is a colony that gets eaten, which is a fair part
		// of why colonisation looked like a waste of a settler. Donated only at the moment
		// the colonist actually steps aboard — the berth has to be adjacent, so the settler
		// boards this turn and the hull never sails carrying an escort and no one to escort.
		//
		// Unsupported (Home null) on purpose: the new town should not be born owing upkeep
		// on a unit it did not build. Same precedent as the Olvir's free HydroEngineer.
		private void DonatePortEscort(IUnit settler, ITile berth)
		{
			if (Common.DistanceToTile(settler.X, settler.Y, berth.X, berth.Y) > 1) return;

			byte own = Game.PlayerNumber(Player);
			IUnit? hull = berth.Units.FirstOrDefault(u => u.Owner == own && u is IBoardable);
			if (hull is null) return;
			// Room for the colonist AND the escort, or the gift costs the crossing.
			if (berth.Units.Count(u => u.Class == UnitClass.Land) + 2 > ((IBoardable)hull).Cargo) return;
			// One escort per crossing.
			if (berth.Units.Any(u => u.Owner == own && u.Role == UnitRole.Defense)) return;

			IUnit? escort = Game.CreateUnit(((IUnit)BestDefender()).Type, berth.X, berth.Y, own);
			if (escort is null) return;
			escort.SetHome(null);
			escort.Sentry = true;   // aboard: cargo is the land units sharing the hull's tile
		}

		// Own coastal city that has land attackers waiting for a ride.
		private City EmbarkationCity()
		{
			byte own = Game.PlayerNumber(Player);
			return Player.Cities
			             .Where(c => c.Tile.GetBorderTiles().Any(t => t.IsOcean)
			                      && c.Tile.Units.Any(u => u.Owner == own && u.Role == UnitRole.LandAttack))
			             .OrderByDescending(c => c.Tile.Units.Count(u => u.Owner == own && u.Role == UnitRole.LandAttack))
			             .FirstOrDefault();
		}

		// Ocean tile adjacent to the given city where a transport can wait.
		private ITile EmbarkationTile(City city)
		{
			byte own = Game.PlayerNumber(Player);
			return city.Tile.GetBorderTiles()
			           .Where(t => t is not null && t.IsOcean)
			           .OrderByDescending(t => t.Units.Count(u => u.Owner == own && u is IBoardable))
			           .FirstOrDefault();
		}

		private void AssignMission(IUnit unit)
		{
			StrategyStance stance = GetStance();

			// Naval units
			if (unit.Class == UnitClass.Water)
			{
				if (unit is IBoardable)
				{
					byte own = Game.PlayerNumber(Player);
					bool hasPassengers = unit.Tile.Units.Any(u => u.Owner == own && u.Class == UnitClass.Land);

					// Colonisation run. A transport carrying settlers is not an invasion
					// fleet: it is looking for empty coast, and it outranks the wait-for-
					// troops branch below. Overseas settlement previously existed ONLY on the
					// Longboat, which is gated on being boxed in — so in a measured 750-turn
					// game not one Longboat was ever built and, apart from the Arabs, every
					// civ died on the continent it started on.
					bool carriesSettlers = unit.Tile.Units.Any(u => u.Owner == own && u is Settlers);
					if (carriesSettlers)
					{
						// Already beside somewhere worth landing? Put them ashore; the settler's
						// own logic founds from there (AI.cs: validCity && nearestCity > 3).
						ITile? beach = unit.Tile.GetBorderTiles()
							.FirstOrDefault(t => t is not null && !t.IsOcean && t.City is null
							                  && t is not Arctic && t is not Mountains
							                  && !Game.GetCities().Any(c => c.Size > 0
							                       && Common.DistanceToTile(c.X, c.Y, t.X, t.Y) < 4));
						if (beach is not null)
						{
							(unit as BaseUnitSea)!.Unload();
							return;
						}

						ITile? shore = BestOverseasSite(unit);
						if (shore is not null)
						{
							unit.Goto = new Point(shore.X, shore.Y);
							return;
						}
						// Nowhere to take them: fall through and let the ship do something useful.
					}

					if (hasPassengers && _attackTarget is not null)
					{
						ITile? landing = LandingTile(_attackTarget);
						if (landing is not null)
						{
							// Already at the landing zone — unload so troops can storm the beach
							if (Common.DistanceToTile(unit.X, unit.Y, _attackTarget.X, _attackTarget.Y) <= 2)
							{
								(unit as BaseUnitSea)!.Unload();
								return;
							}
							unit.Goto = new Point(landing.X, landing.Y);
							return;
						}
					}

					// Wait at a coastal city for troops — but ONLY when there is actually an
					// invasion to ferry. This branch used to return unconditionally, and an
					// embarkation city always exists, so every empty transport parked at a
					// pier for the rest of the game and the sea-exploration code below was
					// unreachable for the only ship class most civs ever build. Measured at
					// 1900 AD: Japan held three Triremes and had charted 1% of the world's
					// land, while the English — with no ships at all — could not see the
					// continent across the water.
					if (hasPassengers || _attackTarget is not null)
					{
						City embark = EmbarkationCity();
						if (embark is not null)
						{
							ITile pier = EmbarkationTile(embark);
							if (pier is not null) { unit.Goto = new Point(pier.X, pier.Y); return; }
						}
					}
				}

				// Nothing to ferry and no war to fight: go and chart something. Preferred
				// over the harbour patrol below, which is what every idle ship used to do
				// for the entire game.
				if (Player.ExploredLandFraction < 0.70)
				{
					ITile? seaDest = BestSeaExploreTile(unit);
					if (seaDest is not null)
					{
						unit.Goto = new Point(seaDest.X, seaDest.Y);
						return;
					}
				}

				// Warships and fallback: patrol nearest own city
				City port = Player.Cities
				    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault();
				if (port is not null) unit.Goto = new Point(port.X, port.Y);
				return;
			}

			// Explorers: head for the nearest unseen tile
			if (unit is Explorer)
			{
				ITile? dest = BestExploreTile(unit);
				if (dest is not null)
				{
					unit.Goto = new Point(dest.X, dest.Y);
					return;
				}
				// Nothing reachable left to scout — a stranded or done explorer.
				// Sentry rather than re-deciding this every turn for the rest of
				// the game; it wakes if something changes around it.
				unit.Sentry = true;
				return;
			}

			// Diplomats: prefer the human player's cities (steal tech / sabotage), then nearest
			// other foreign city. Only consider cities reachable by land from the diplomat's
			// current tile — same ContinentId means a 4-connected land path exists. Without
			// this filter the diplomat ends up walking forever toward an unreachable target.
			//
			// First-step reachability mirrors the Caravan fix immediately below: skip any
			// candidate whose first step is peaceful-blocked or wedged by pathfinding, so
			// multiple diplomats don't all queue the same unreachable target and burn the
			// AI loop's same-unit circuit breaker turn after turn.
			if (unit is Diplomat)
			{
				if (IdleRetryTurn(unit)) { unit.SkipTurn(); return; }
				byte myContinent = unit.Tile?.ContinentId ?? MISC_CONTINENT;
				bool sameContinent(City c) => Map.NamedContinent(myContinent) && c.Tile is not null && c.Tile.ContinentId == myContinent;

				bool FirstStepReachable(City c)
				{
					ITile? step = Common.GotoStep(unit, c.X, c.Y);
					if (step is null) return false;
					// When the first step IS the target city, the Diplomat is adjacent and the step
					// is its spy mission (steal / incite / sabotage) — not a blocked path. Allow it;
					// AI.Move grants the matching exemption so the unit actually enters.
					if (step.X == c.X && step.Y == c.Y) return true;
					if (step.Units.Any(u => u.Owner != unit.Owner && u.Owner != 0
					                     && Game.GetPlayer(u.Owner) is Player pu
					                     && !Player.IsAtWar(pu))) return false;
					if (step.City is not null && step.City.Owner != unit.Owner && step.City.Owner != 0
					    && Game.GetPlayer(step.City.Owner) is Player pc
					    && pc.Civilization is not CivOne.Civilizations.Barbarian
					    && !Player.IsAtWar(pc)) return false;
					return true;
				}

				// HumanOpponent, not Human: with nobody at the controls this clause yields
				// nothing and the search falls straight through to the nearest foreign city,
				// so espionage spreads across the world instead of converging on one civ.
				Player? human = HumanOpponent;
				// FirstStepReachable runs a full A* per candidate, and a same-continent target
				// with no real path (imperfect ContinentId, a chokepoint held by a peaceful
				// neighbour) makes that A* explore the whole landmass before failing. Probing
				// EVERY foreign city this way was the ~5s late-game diplomat spike (a boxed-in
				// diplomat pathfound to all of them, finding none reachable). Cap the probes at
				// the nearest few — a diplomat only wants a near target, and if the closest
				// handful are all unreachable, farther ones on the same blocked landmass are too.
				const int MaxProbes = 4;
				City target =
					// Espionage priority: the human's cities — but NEVER our own. When the
					// human is on Autopilot the acting Player IS the human, so without the
					// `c.Player != Player` guard a diplomat targets its own cities and shuffles
					// between them forever (you cannot spy on yourself). This clause then yields
					// nothing for an autopiloted human and the search falls through to foreign
					// cities below, which is the correct target.
					Game.GetCities()
					    .Where(c => c.Player == human && c.Player != Player && Player.Visible(c.X, c.Y) && sameContinent(c))
					    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
					    .Take(MaxProbes)
					    .FirstOrDefault(FirstStepReachable)
					??
					Game.GetCities()
					    .Where(c => c.Player != Player && Player.Visible(c.X, c.Y) && sameContinent(c))
					    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
					    .Take(MaxProbes)
					    .FirstOrDefault(FirstStepReachable);
				if (target is not null) unit.Goto = new Point(target.X, target.Y);
				// Sentry rather than SkipTurn, for the reason spelled out at the Caravan
				// below: a diplomat with no reachable target re-ran four full A* searches
				// every turn for the rest of the game.
				else unit.SkipTurn();
				return;
			}

			// Caravans: head for the nearest worthwhile foreign city (trade route gold), but
			// only among cities on the same continent so we don't dispatch the unit on an
			// impossible walk across the ocean.
			if (unit is Caravan)
			{
				if (IdleRetryTurn(unit)) { unit.SkipTurn(); return; }
				// Caravans deliver trade-route gold by entering a foreign city
				// (Caravan.cs:127, EstablishTradeRoute). Target the nearest reachable foreign
				// city (see the targeting note below for why nearest, not most-distant), and
				// verify the first step is actually reachable — otherwise a Caravan can commit
				// to a target whose path is wedged by a peaceful neighbour, loop in AI.Move
				// until the circuit breaker fires, and waste turn budget.
				//
				// No own-city fallback: walking an AI Caravan into its own city does nothing
				// (CaravanChoice is human-only at Caravan.cs:100-103); the unit would idle
				// on arrival and block its build slot. SkipTurn at home is better.
				byte myContinent = unit.Tile?.ContinentId ?? MISC_CONTINENT;
				bool sameContinent(City c) => Map.NamedContinent(myContinent) && c.Tile is not null && c.Tile.ContinentId == myContinent;

				bool FirstStepReachable(City c)
				{
					ITile? step = Common.GotoStep(unit, c.X, c.Y);
					if (step is null) return false;
					// When the first step IS the target city, the Caravan is adjacent and the step
					// is its trade-route delivery — not a blocked path. Allow it; AI.Move grants the
					// matching exemption so the unit actually enters instead of shuttling on the rails.
					if (step.X == c.X && step.Y == c.Y) return true;
					// Peaceful-block: AI.Move at line ~343 refuses the step if the next tile
					// holds a non-warring player's unit, or is a non-Barbarian city at peace
					// with us. Mirror that here so we don't commit to a target we'd refuse
					// to step toward.
					if (step.Units.Any(u => u.Owner != unit.Owner && u.Owner != 0
					                     && Game.GetPlayer(u.Owner) is Player pu
					                     && !Player.IsAtWar(pu))) return false;
					if (step.City is not null && step.City.Owner != unit.Owner && step.City.Owner != 0
					    && Game.GetPlayer(step.City.Owner) is Player pc
					    && pc.Civilization is not CivOne.Civilizations.Barbarian
					    && !Player.IsAtWar(pc)) return false;
					return true;
				}

				// Deliver to the NEAREST reachable foreign city, not the most distant. "Most
				// distant" maximised the gold bonus on paper but was an unstable target: each
				// time the caravan crossed the midpoint between two far cities the ranking
				// flipped and it doubled back, so it shuttled along the rails forever and never
				// delivered. Nearest stays nearest as the caravan approaches, so it commits and
				// pays out. Prefer a city of real size (a worthwhile route, per play-test note);
				// fall back to the nearest city of any size so the unit always delivers rather
				// than idling for endless turns at its owner's upkeep.
				const int popFloor = 3;
				// Probe cap, exactly the one the Diplomat above already has — the comment
				// there calls this "the Caravan fix immediately below", but the cap itself
				// was never applied here. Unbounded, this ran a full A* per foreign city on
				// the continent, and then a SECOND full pass for the no-size-floor fallback,
				// for every caravan, every turn. FirstStepReachable is expensive precisely
				// for these units: a Caravan is nonCombat, so in the pathfinder EVERY
				// foreign unit blocks its tile, and a boxed-in one explores the whole
				// landmass before failing.
				//
				// Measured on the turn-310..365 stretch of a peaceful epic game: 4600
				// pathfinds per turn at 17 ms each — 80 seconds of a 100-second turn, about
				// three quarters of the entire game clock. A world at peace is the worst
				// case, because at peace every foreign unit blocks rather than being a
				// target to attack through.
				//
				// Filtering once instead of twice keeps it to at most MaxProbes searches.
				// Taking the nearest few before the size floor can skip a big city just
				// outside the window; that is the same trade the "nearest, not most distant"
				// note above already argues for.
				const int MaxProbes = 4;
				var reachable = Game.GetCities()
				    .Where(c => c.Player != Player && sameContinent(c))
				    .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .Take(MaxProbes)
				    .Where(FirstStepReachable)
				    .ToList();
				City target = reachable.FirstOrDefault(c => c.Size >= popFloor)
				           ?? reachable.FirstOrDefault();

				if (target is not null) unit.Goto = new Point(target.X, target.Y);
				// SkipTurn, not Sentry — an AI unit that sentries never wakes. See
				// IdleRetryTurn, which is what keeps this from re-probing every turn.
				else unit.SkipTurn();
				return;
			}

			// Hunt loose monsters before considering anybody's cities. See HuntQuarry: the
			// offensive AI targets cities and nothing else, so barbarian megafauna standing in
			// a civ's own fields were untouchable by anyone but a human.
			//
			// Deliberately OUTSIDE the LandAttack branch below. Inside it, a civ that builds
			// only defenders — which is what a developed peaceful AI does — could never hunt
			// anything, however capable its garrison. HuntQuarry vets the unit itself
			// (capability, land, not the sole garrison), so everything that falls through to
			// here is safe to offer.
			IUnit? quarry = HuntQuarry(unit);
			if (quarry is not null)
			{
				unit.Goto = new Point(quarry.X, quarry.Y);
				return;
			}

			// Offensive land units
			if (unit.Role == UnitRole.LandAttack)
			{
				if (stance == StrategyStance.Militarize)
				{
					// Validate or refresh the civ-wide attack target.
					// Barbarian cities stay valid until captured; non-barbarian targets
					// are dropped when the war ends.
					// Same-continent staleness check: an attacker whose target is on a different
					// continent can't path there. Cached _attackTarget held over from before the
					// fix (or picked when our continents shifted) needs invalidating so the
					// strategy code re-picks a reachable target.
					byte ownPN = Game.PlayerNumber(Player);
					// Tile can be null when a city is mid-capture / in transient sentinel state
					// (X==Y==255). Guard both the cached target and each iterated own-city so a
					// ghost reference doesn't NRE the strategy and stall the turn.
					bool targetOffContinent = _attackTarget is not null
					    && _attackTarget.Tile is not null
					    && !Player.Cities.Any(oc => oc.Tile is not null
					                              && oc.Tile.ContinentId == _attackTarget.Tile.ContinentId
					                              && Map.NamedContinent(oc.Tile.ContinentId));
					bool targetStale = _attackTarget is null
					    || _attackTarget.Tile is null
					    || _attackTarget.Size <= 0
					    || !Game.GetCities().Contains(_attackTarget)
					    || _attackTarget.Player == Player
					    || (_attackTarget.Owner != 0 && !Player.IsAtWar(_attackTarget.Player))
					    || targetOffContinent;
					if (targetStale)
						_attackTarget = PickAttackTarget();

					if (_attackTarget is not null)
					{
						ITile? staging = StagingTile(_attackTarget);
						byte own = Game.PlayerNumber(Player);

						// How many attackers are already at the staging tile?
						int staged = staging?.Units.Count(u =>
						    u.Owner == own && u.Role == UnitRole.LandAttack) ?? 0;

						// Commit when we have enough force; be generous if we outbuilt the defense
						int defenders = _attackTarget!.Tile!.Units.Count(u => u.Role == UnitRole.Defense);
						int threshold = Math.Max(2, defenders + 1);

						Point dest = (staged >= threshold || staging is null)
						    ? new Point(_attackTarget.X, _attackTarget.Y)
						    : new Point(staging!.X, staging!.Y);
						unit.Goto = dest;
						return;
					}
				}

				// Default: reinforce the most under-defended own city. If every own city is
				// already garrisoned (>= 2 defenders) and the attacker still has nothing to do,
				// fall back to the nearest own city anyway — better to pile up there and fortify
				// than to sit in open terrain getting nothing done turn after turn.
				City needsHelp = Player.Cities
				    .Where(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense) < 2)
				    .OrderBy(c => c.Tile.Units.Count(u => u.Role == UnitRole.Defense))
				    .ThenBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				    .FirstOrDefault()
				    ?? Player.Cities
				       .OrderBy(c => Common.DistanceToTile(unit.X, unit.Y, c.X, c.Y))
				       .FirstOrDefault();
				if (needsHelp is not null && (needsHelp.X != unit.X || needsHelp.Y != unit.Y))
					unit.Goto = new Point(needsHelp.X, needsHelp.Y);
				else if (needsHelp is null)
					unit.Fortify = true;
				// (already at the only fallback city — leave Goto empty, let next turn pick again
				// without thrashing; the unit just sits but won't waste enqueue cycles)
			}
		}

		// ── research weights ──────────────────────────────────────────────────

		// Returns how much the AI values acquiring a given advance right now.
		// Used by the King screen to pick the advance it demands in a trade.
		internal int AdvanceDemandValue(IAdvance a) => AdvanceWeight(a, GetStance(), Path);

		private static int AdvanceWeight(IAdvance a, StrategyStance stance, VictoryPath path)
		{
			int weight = 1; // baseline: every advance can be chosen

			switch (stance)
			{
				case StrategyStance.Militarize:
					if (a is BronzeWorking)      weight += 7;
					if (a is IronWorking)         weight += 7;
					if (a is TheWheel)            weight += 6;
					if (a is HorsebackRiding)     weight += 7;
					if (a is Feudalism)           weight += 5;
					if (a is Chivalry)            weight += 7;
					if (a is Gunpowder)           weight += 8;
					if (a is Mathematics)         weight += 4;
					if (a is Physics)             weight += 5;
					if (a is Chemistry)           weight += 5;
					if (a is Metallurgy)          weight += 7;
					if (a is Engineering)         weight += 5;
					if (a is SteamEngine)         weight += 5;
					if (a is Industrialization)   weight += 6;
					if (a is Conscription)        weight += 8;
					if (a is Automobile)          weight += 8;
					if (a is LaborUnion)          weight += 8;
					if (a is Masonry)             weight += 4; // gateway to Construction -> Aqueduct (growth)
					// A war economy still has to eat, and a city that cannot pass size 7
					// cannot pay for the war either. Masonry was the only nod to growth in
					// this branch, and its two payoffs sat at weight 0 — so a civ latched
					// in Militarize never researched them at all. The autoplayed Japan's
					// 18 advances are this list read off in order (Bronze Working, Iron
					// Working, Mathematics, Physics, Magnetism) with Pottery still missing
					// after 751 turns. Weighted below the weapons, above nothing.
					if (a is Pottery)             weight += 6; // Granary
					if (a is Construction)        weight += 6; // Aqueduct: the size-7 ceiling
					break;

				case StrategyStance.Develop:
					if (a is Alphabet)            weight += 7;
					if (a is Writing)             weight += 8;
					if (a is Literacy)            weight += 6;
					if (a is CodeOfLaws)          weight += 6;
					if (a is TheRepublic)         weight += 7;
					if (a is Advances.Democracy)  weight += 6;
					if (a is Pottery)             weight += 6;
					if (a is Trade)               weight += 8;
					if (a is Currency)            weight += 7;
					if (a is Banking)             weight += 7;
					if (a is TheCorporation)      weight += 6;
					if (a is Philosophy)          weight += 5;
					if (a is Advances.University)  weight += 7;
					if (a is Invention)           weight += 6;
					if (a is TheoryOfGravity)     weight += 6;
					if (a is Masonry)             weight += 5;
					if (a is Construction)        weight += 5;
					if (a is CeremonialBurial)    weight += 5;
					if (a is Mysticism)           weight += 4;
					if (a is Religion)            weight += 5;
					break;

				case StrategyStance.Consolidate:
					if (a is CeremonialBurial)    weight += 9; // Temple
					if (a is Mysticism)           weight += 8; // doubles Temple
					if (a is Philosophy)          weight += 6;
					if (a is Religion)            weight += 8; // Cathedral
					if (a is Construction)        weight += 8; // Colosseum
					if (a is Pottery)             weight += 7; // Granary
					if (a is Trade)               weight += 6;
					if (a is Currency)            weight += 6;
					if (a is Banking)             weight += 5;
					if (a is Writing)             weight += 5;
					break;

				case StrategyStance.Expand:
					if (a is Pottery)             weight += 8; // Granary feeds growth
					if (a is BridgeBuilding)      weight += 7; // roads cross rivers
					if (a is RailRoad)            weight += 7; // fast movement
					if (a is Masonry)             weight += 6;
					if (a is MapMaking)           weight += 5; // explore coasts
					if (a is Alphabet)            weight += 5;
					if (a is Writing)             weight += 5;
					if (a is Trade)               weight += 5;
					if (a is TheWheel)            weight += 5;
					if (a is HorsebackRiding)     weight += 5;
					if (a is AquaticColonization) weight += 6; // new city sites
					if (a is TransitConduit)      weight += 5; // fast movement upgrade
					break;
			}

			// Post-contact advances — useful in all stances once available.
			if (a is Xenobiology)           weight += 6; // gifted free, but may need to be researched
			if (a is Gravitics)             weight += 7; // gateway to sea + tubes
			if (a is SyntheticEcology)      weight += 6; // tile yield improvements
			if (a is MemeticProtocols)      weight += 5; // happiness/diplomacy
			if (a is AquaticColonization)   weight += 5;
			if (a is TransitConduit)        weight += 6;
			if (a is BioplexEngineering)    weight += 5;
			if (a is CanopyCultivation)     weight += 5;
			if (a is NeuralInterface)       weight += 5;
			if (a is GravitonEngineering)   weight += 4;
			if (a is PlanetaryStewardship)  weight += 4;
			if (a is CollectiveMemory)      weight += 4;

			// The path's own second axis, ADDED to the stance weights rather than replacing
			// them: a civ reaching for the stars still needs Pottery and Construction, and the
			// stance lists are where those live. Without this a Diaspora civ arrived at Space
			// Flight by drift, if at all — the stance tables have no entry for it anywhere, so
			// it was competing at weight 1 against a dozen weighted alternatives.
			switch (path)
			{
				case VictoryPath.Diaspora:
					if (a is SpaceFlight)         weight += 12;   // Mission Control, and the ship
					if (a is Computers)           weight += 8;    // gateway to SpaceFlight
					if (a is NuclearFission)      weight += 6;
					if (a is Astronomy)           weight += 5;
					if (a is Advances.University) weight += 5;
					if (a is Writing)             weight += 4;
					break;

				case VictoryPath.Commerce:
					if (a is Trade)               weight += 9;    // Caravans, and trade routes
					if (a is Currency)            weight += 7;
					if (a is Banking)             weight += 8;    // Pax Mercatoria requires it
					if (a is TheRepublic)         weight += 6;
					break;

				case VictoryPath.Culture:
					if (a is Philosophy)          weight += 9;    // Cultural Ascendancy requires it
					if (a is CeremonialBurial)    weight += 6;
					if (a is Religion)            weight += 7;
					if (a is Literacy)            weight += 5;
					break;

				case VictoryPath.Conquest:
					// The Militarize stance list already is this, in detail. Doubling it here
					// would just deepen a rut the stance is better placed to judge, since it
					// can see whether a war is actually on.
					break;

				case VictoryPath.Endurance:
					break;
			}

			return weight;
		}

		// ── production helpers ─────────────────────────────────────────────────

		private IProduction BestDefender()
		{
			if (Player.HasAdvance<LaborUnion>())    return new MechInf();
			if (Player.HasAdvance<Conscription>())  return new Riflemen();
			if (Player.HasAdvance<Gunpowder>())     return new Musketeers();
			if (Player.HasAdvance<BronzeWorking>()) return new Phalanx();
			return new Militia();
		}

		private IProduction BestAttacker()
		{
			if (Player.HasAdvance<Automobile>())       return new Armor();
			if (Player.HasAdvance<Metallurgy>())       return new Cannon();
			if (Player.HasAdvance<Chivalry>())         return new Knights();
			if (Player.HasAdvance<TheWheel>())         return new Chariot();
			if (Player.HasAdvance<HorsebackRiding>())  return new Cavalry();
			if (Player.HasAdvance<IronWorking>())      return new Legion();
			return new Militia();
		}

		// ── wonder selection ───────────────────────────────────────────────────

		// Only the single highest-production city should chase a wonder.
		// Ties are broken by map position for stability across turns.
		private bool IsTopProductionCity(City city)
		{
			City[] cities = Player.Cities;
			if (cities.Length == 0) return false;
			int maxShields = cities.Max(c => c.ShieldIncome);
			if (city.ShieldIncome < maxShields) return false;
			return cities.Where(c => c.ShieldIncome == maxShields)
			             .OrderBy(c => c.X).ThenBy(c => c.Y)
			             .First() == city;
		}

		private IWonder? SelectWonder(City city, StrategyStance stance)
		{
			if (!IsTopProductionCity(city)) return null;

			// Prioritise dome component(s) assigned to this civilisation, if any
			foreach (var wonderId in Game.Instance.GetDomeAssignments(Player))
			{
				IWonder assigned = Reflect.GetWonders().FirstOrDefault(w => w.Id == (byte)wonderId);
				if (assigned is not null && !Game.WonderBuilt(assigned) && Player.ProductionAvailable(assigned))
					return assigned;
			}

			// Never start a wonder that is already built or already obsolete — an
			// obsolete wonder is pure shield waste, and the stance lists below used
			// to allow it.
			bool Buildable(IWonder w) =>
				!Game.WonderBuilt(w) && !Game.WonderObsolete(w) && Player.ProductionAvailable(w);

			// Catch-up: a civ well behind the tech leader takes the Great Library
			// first — its entire effect (free advances known by two other civs) is
			// catch-up, so it's worth the most to exactly the civs doing the worst.
			int leaderTech = Game.Players
			    .Where(p => p != Player && !p.IsDestroyed() && Game.PlayerNumber(p) != 0)
			    .Select(p => p.Advances.Length)
			    .DefaultIfEmpty(0).Max();
			if (leaderTech - Player.Advances.Length >= 4)
			{
				IWonder library = new GreatLibrary();
				if (Buildable(library)) return library;
			}

			// Power tier: empire-wide passives whose value scales with city count and
			// needs no follow-up decisions. Any civ with a wonder-capable city should
			// take these before the stance-flavoured picks. Happiness first — the
			// grow→riot→shrink cycle is what actually kills AI empires.
			var power = new List<IWonder>
			{
				new HangingGardens(),        // +1 happy in every city
				new MichelangelosChapel(),   // continent-wide Cathedral effect
				new JSBachsCathedral(),      // -2 unhappy, all continent cities
				new LeonardosWorkshop(),     // free unit upgrades
				new Pyramids(),              // government flexibility
				new AdamSmithsTradingHouse(),// building upkeep relief
				new HooverDam(),             // industry without pollution plants
				new CureForCancer()          // +1 happy in every city
			};
			if (Player.RepublicDemocratic) power.Insert(3, new WomensSuffrage());
			IWonder? pick = power.FirstOrDefault(Buildable);
			if (pick is not null) return pick;

			IWonder[] preferred;
			if (stance == StrategyStance.Militarize)
			{
				preferred = new IWonder[]
				{
					new GreatWall(), new Colossus(), new MichelangelosChapel(),
					new SunTzusWarAcademy(), new LeonardosWorkshop()
				};
			}
			else if (stance == StrategyStance.Consolidate)
			{
				preferred = new IWonder[]
				{
					new ShakespearesTheatre(), new JSBachsCathedral(),
					new HangingGardens(), new MichelangelosChapel(), new Oracle(),
					new AdamSmithsTradingHouse()
				};
			}
			else
			{
				preferred = new IWonder[]
				{
					new Pyramids(), new ShakespearesTheatre(), new IsaacNewtonsCollege(),
					new JSBachsCathedral(), new HangingGardens(), new Oracle(),
					new GreatLibrary(), new DarwinsVoyage(), new CopernicusObservatory(),
					new Colossus(), new Lighthouse(), new MagellansExpedition(),
					new LeonardosWorkshop(), new SunTzusWarAcademy(),
					new AdamSmithsTradingHouse(),
					new MarcoPoloVoyage(), new ZhengHeVoyage()
				};
			}

			return preferred.FirstOrDefault(Buildable);
		}

		// ── full production plan for a city ────────────────────────────────────

		// A settler costs one POPULATION when it completes (City.cs:1524) and then 2
		// food per turn — 1 under Despotism/Anarchy — from its home city for as long
		// as it lives (City.FoodCosts). Both land AFTER this check, so the old
		// "FoodIncome >= 0" test cleared cities that go hungry the moment the settler
		// exists, and the old "Size >= 2" gate shipped settlers out of size-2 towns
		// that dropped straight back to 1.
		//
		// That is the wasting disease: a size-2 city ships a settler, falls to size 1,
		// regrows to 2, ships another. It never accumulates, and every settler it
		// produces keeps drawing food from it forever. Requiring size 3 leaves the
		// city at 2 after the build; requiring FoodIncome >= upkeep means it can still
		// feed itself afterwards.
		private bool CanAffordSettler(City city, int minSize)
		{
			int upkeep = (Player.Government is Gov.Anarchy || Player.Government is Gov.Despotism) ? 1 : 2;
			return city.Size >= minSize && city.FoodIncome >= upkeep;
		}

		// The escape hatch for a civ whose city cannot reach the size the rule above wants.
		//
		// A settler needs size 3; size 3 needs a food surplus; the surplus needs irrigation;
		// the irrigation needs a settler. For a civ with one or two cities on ground that
		// tops out at size 2, that loop never opens, and the gate meant to prevent the
		// size-2 settler-shipping churn becomes a life sentence instead. Measured over a
		// 590-turn game: the Lakota held ONE size-2 city in a wide-open continent with no
		// enemy within ten tiles, made fifteen production picks in the whole game, and never
		// built a single settler — their only settler action was founding the capital on
		// turn 0. The Greeks and the Arabs ended identically.
		//
		// Shipping the settler costs a population point, and that is the point: at size 1
		// the city feeds two fewer mouths, so its surplus goes POSITIVE while the settler
		// irrigates the ground that lifts the ceiling. Guards keep it narrow — a tiny
		// empire, a city genuinely stalled rather than merely slow, no settler already in
		// the field, and somewhere for this one to actually work.
		private bool SettlerIsTheOnlyWayOut(City city)
		{
			if (Player.Cities.Length > 2) return false;
			if (city.Size < 2) return false;
			if (city.FoodIncome > 1) return false;   // still growing on its own — normal rules apply
			byte own = Game.PlayerNumber(Player);
			if (Game.GetUnits().Any(u => u.Owner == own && u is Settlers)) return false;
			return HasImprovableLandNear(city);
		}

		// Is there a tile this city works — or could work — that a settler could actually
		// improve? Mirrors the irrigation predicates in AI.cs so the settler is never built
		// for work that does not exist.
		private bool HasImprovableLandNear(City city)
		{
			for (int dy = -2; dy <= 2; dy++)
			for (int dx = -2; dx <= 2; dx++)
			{
				if (Math.Abs(dx) == 2 && Math.Abs(dy) == 2) continue;
				int ty = city.Y + dy;
				if (ty < 0 || ty >= Map.HEIGHT) continue;
				ITile t = Map[(city.X + dx + Map.WIDTH) % Map.WIDTH, ty];
				if (t is null || t.IsOcean || t.City is not null) continue;
				if (t.Irrigation || t.Mine) continue;
				if (t is Swamp || t is Jungle || t is Forest) return true;   // conversion: no water source needed
				if ((t is Grassland || t is River || t is Plains || t is Desert)
				    && t.HasIrrigationSource())
					return true;
			}
			return false;
		}

		private List<IProduction> PlanProduction(City city, StrategyStance stance)
		{
			return PlanProductionInto(new List<IProduction>(), city, stance);
		}

		// Everything the city would consider this turn, in preference order. A test seam:
		// asserting on CurrentProduction alone proves nothing about what a faction is WILLING
		// to build, because a garrison outranks a wonder and hides it.
		internal IProduction[] ProductionPlan(City city) => PlanProduction(city, GetStance()).ToArray();

		// Does this building earn its upkeep in THIS city?
		//
		// Nothing in the AI consulted maintenance before this: grep TotalMaintenance
		// across AI*.cs found no hits. The obvious rule — "don't build what the treasury
		// cannot fund" — turns out to be unshippable, because measured across a turn-616
		// save EVERY civ runs a permanent gold deficit (the Russians take 214 gold a turn
		// against 686 of upkeep) that Player.Gold silently absorbs by clamping to zero.
		// A solvency test would therefore stop the whole world building anything, so the
		// rule here is about need, not affordability, and it is deliberately narrow.
		private bool EarnsItsKeep(IBuilding building, City city)
		{
			// Happiness buildings only pay for themselves where there is unhappiness to
			// quell. The autoplayed Japan held NINE Temples across ten cities that each
			// showed happy 0 / unhappy 0 — content at size 2 — for 9 gold a turn out of
			// the 29 it could not afford.
			//
			// Gated on the luxury slider being idle, which is the whole subtlety: a city
			// can read as content precisely BECAUSE luxuries are cranked to 8, and
			// refusing it a Temple then would pin the slider up forever and re-create the
			// research shutdown from the other direction. While luxuries are doing the
			// work, keep building the thing that lets them stop.
			if (building is Temple or Colosseum or Cathedral
			    or Hospital or ExchangeCenter or NeuralLab
			    && city.UnhappyCitizens == 0 && Player.LuxuriesRate == 0)
				return false;

			// Multipliers need something to multiply.
			//
			// Every building below adds a PERCENTAGE of what the city already produces:
			// Library, University, Observatory and Xenolab scale science, MarketPlace and
			// Bank scale taxes and luxuries. All of it comes off TradeTotal, so on a size-3
			// desert town producing 2 trade a Library returns 1 — for permanent upkeep.
			//
			// Measured over the 2200 AD run: of 2,075 production decisions taken by AI cities
			// of size <= 6 with a food income of zero or less, Observatory was 7%, MarketPlace
			// 6%, Neural Lab 4%, Xenolab 4% and Library 4%. Those cities had already built
			// every food building available to them (86% held a Granary) — they were not
			// choosing these instead of growing, they were choosing them instead of nothing,
			// and paying gold a turn for the privilege.
			//
			// The floor is the building's own maintenance, doubled: a 50% multiplier returns
			// half the base, so base >= 2 x upkeep is exactly the break-even. It scales itself
			// per building rather than freezing a size threshold that would be wrong for
			// half the roster, and a city that grows into it becomes eligible on its own.
			if (building is Library or UniversityBuilding or ObservatoryBuilding or Xenolab
			    or MarketPlace or Bank
			    && city.TradeTotal < 2 * building.Maintenance)
				return false;

			return true;
		}

		private List<IProduction> PlanProductionInto(List<IProduction> plan, City city, StrategyStance stance)
		{
			void Consider(IProduction p)
			{
				// Wonders are exempt — they carry no maintenance and are the one build
				// worth going hungry for.
				if (p is IBuilding b && p is not IWonder && !EarnsItsKeep(b, city)) return;
				if (plan.All(x => x.GetType() != p.GetType())) plan.Add(p);
			}

			int defenders = city.Tile.Units.Count(u => u.Role == UnitRole.Defense);

			// The Others: an occupation, not a civilisation. Cephalopods incapable of sympathy
			// or of recognising their captives as anything but stock — so they build nothing
			// that serves a population. No temples, no aqueducts, no marketplaces, and above
			// all no wonders: they did not come here to develop the place.
			//
			// They were running the ordinary planner, and it showed. Over 1255 production
			// decisions between turns 370 and 749 of a 2200 AD game they built 97
			// Observatories, 95 Cathedrals, 89 Colosseums, 66 Hospitals and 51 Marketplaces,
			// against 131 military units — about a tenth of their output. The invasion landed
			// and then spent five centuries on civic architecture while every human power
			// grew around it. They held 51 cities and finished sixth on score.
			//
			// Two things only: armour that runs everything down, and agents to sabotage what
			// is not worth occupying. The warheads are the ARRIVAL, not the occupation —
			// ExecuteOwnersLanding nukes each civilisation's capital before the seizure, and
			// that is the whole of it. Producing more would be stockpiling a weapon this AI
			// has no doctrine for firing, which is the 84-Diplomat mistake in a costlier form.
			//
			// And no defenders, deliberately. They do not care enough about what they take to
			// garrison it: this is a smash-and-grab for livestock, to be over before the
			// humans work out how to upload a virus or some microbe gets in. The practical
			// consequence is that occupied cities are cheap to retake, which is the arc doing
			// what it should — the occupation has to be broken, not out-produced.
			//
			// ProductionAvailable is the gate rather than a hand-written tech list because
			// HoverTank needs the occupier's own Fusion Core, which a HasAdvance check would
			// miss. Armor is the guaranteed fallback: the landing grants every advance bar
			// FutureTech (Game.ExecuteOwnersLanding), so it can never be unavailable to them.
			if (Player.Civilization is TheOthers)
			{
				byte thId = Game.PlayerNumber(Player);
				int agents = Game.GetUnits().Count(u => u.Owner == thId && u is Diplomat);

				if (Player.ProductionAvailable(new HoverTank())) Consider(new HoverTank());
				// Sabotage is a tool, not a doctrine — one agent per two cities is plenty.
				// They were building them regardless of use: 84 Diplomats produced by a
				// faction that returns early from every diplomatic path (AI.Strategy:1072),
				// which is 84 units that could never do anything at all.
				if (agents < Math.Max(1, Player.Cities.Length / 2)
				    && Player.ProductionAvailable(new Diplomat()))
					Consider(new Diplomat());
				if (plan.Count == 0) Consider(new Armor());
				return plan;
			}

			// The Thing builds nothing civic. It had been falling through to the general path,
			// which meant its top-production city chased the ordinary wonder list — an
			// assimilating organism commissioning J.S. Bach's Cathedral, which is what a
			// turn-750 save actually showed. It makes bodies, and in the third act, The Vessel.
			if (Player.Civilization is TheThing)
			{
				// The Vessel is set directly on the shipyard city by Game.ProcessThingAscension,
				// which picks it empire-wide; honour it here so the plan never overwrites it.
				if (city.CurrentProduction is Wonders.TheVessel) { Consider(new Wonders.TheVessel()); return plan; }

				if (Player.ProductionAvailable(new MechInf())) Consider(new MechInf());
				if (Player.ProductionAvailable(new Armor()))   Consider(new Armor());
				if (plan.Count == 0) Consider(new Militia());
				return plan;
			}

			// Skynet researches — it is the one faction that ought to out-tech the world, and
			// ChooseResearch deliberately leaves it alone. Its WONDERS are another matter: the
			// general list had the network building cathedrals and suffrage movements. Only
			// machine work, and only from the top production city as for anyone else.
			if (Player.Civilization is Skynet)
			{
				// The Reprocessor first: it is the mission, everything else is materiel.
				IWonder[] machineWork =
				{
					new Wonders.TheReprocessor(),
					new NanobotFactory(), new ManhattanProject(), new FusionCore(),
					new InterstellarProbe(), new HumanGenomeProject(),
				};
				if (IsTopProductionCity(city))
					foreach (IWonder w in machineWork)
						if (!Game.WonderBuilt(w) && Player.ProductionAvailable(w)) { Consider(w); break; }

				if (Player.ProductionAvailable(new HoverTank())) Consider(new HoverTank());
				if (Player.ProductionAvailable(new Armor()))     Consider(new Armor());
				if (plan.Count == 0) Consider(new Militia());
				return plan;
			}

			// Olvir production: refugee fleet prioritises rapid expansion and sea infrastructure
			// over military and normal civic buildings. They don't build wonders or advanced
			// civic buildings — just the minimum garrison, food storage, and settlers.
			if (Player.Civilization is Olvir)
			{
				byte oid = Game.PlayerNumber(Player);
				int olvCities = Player.Cities.Length;
				int olvSettlers = Game.GetUnits().Count(u => u.Owner == oid && u is Settlers);
				// One defender per city — they're pacifist, not helpless.
				if (defenders < 1) Consider(BestDefender());
				// Granary feeds growth (Pottery always known for Olvir — late-era arrival).
				if (!city.HasBuilding<Granary>()) Consider(new Granary());
				// Keep one settler per city when below the expansion cap (30 cities).
				// Size 3 and food to spare: at size 2 the completed settler drops the
				// city to 1 (see CanAffordSettler).
				if (CanAffordSettler(city, 3) && olvSettlers < Math.Max(olvCities, 4)
				    && !city.Units.Any(u => u is Settlers) && olvCities < 30)
					Consider(new Settlers());
				// HydroEngineer for ocean/floating-city founding.
				if (Player.HasAdvance<AquaticColonization>()
				    && Game.GetUnits().Count(u => u.Owner == oid && u is HydroEngineer) < olvCities / 3 + 1)
					Consider(new HydroEngineer());
				// The spawning rite: a shrine in every colony, and in the older ones a
				// human cathedral plumbed into a cascade. This is the only culture the
				// Olvir can earn — nothing else on their production list scores any.
				if (!city.HasBuilding<BreedingShrine>()) Consider(new BreedingShrine());
				if (city.HasBuilding<BreedingShrine>() && city.Size >= 6
				    && !city.HasBuilding<CascadeCathedral>()) Consider(new CascadeCathedral());
				// Post-contact buildings that are thematically Olvir.
				if (Player.HasAdvance<Xenobiology>()        && !city.HasBuilding<Xenolab>())        Consider(new Xenolab());
				if (Player.HasAdvance<AquaticColonization>() && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean)
				                                              && !city.HasBuilding<SeaPlatform>())   Consider(new SeaPlatform());
				// Fallback to another defender so the city produces something.
				if (plan.Count == 0 || (plan.Count == 1 && plan[0] is IUnit)) Consider(BestDefender());
				return plan;
			}

			// Per-city threat: one border war shouldn't militarize the whole empire.
			// A city with no hostile unit or enemy city within 8 tiles builds like a
			// developing city even while the empire is at war — only frontline cities
			// pay the war tax. GetStance() stays empire-wide for research, sliders and
			// diplomacy; this demotion is production-only.
			if (stance == StrategyStance.Militarize && !NearHostiles(city.X, city.Y))
				stance = StrategyStance.Develop;

			// Universal first: garrison before barracks so a city isn't left naked while building.
			if (defenders < 1)                Consider(BestDefender());

			// Difficulty 0 (Chieftain): front-load a militia screen, an early settler and a
			// Temple ahead of the standard infrastructure chain. (Folded in from a former
			// stand-alone PlanChieftain method that only ever added these three then ran this
			// same plan — Consider() dedupes by type, matching its old plan.All(...) guards.)
			if (Game.Difficulty == 0)
			{
				byte chId       = Game.PlayerNumber(Player);
				int  chCities   = Player.Cities.Length;
				int  chMilitia  = Game.GetUnits().Count(u => u.Owner == chId && u is Militia);
				int  chSettlers = Game.GetUnits().Count(u => u.Owner == chId && u is Settlers);
				if (chMilitia < chCities * 4) Consider(new Militia());
				if (CanAffordSettler(city, 4) && chSettlers < Math.Max(1, chCities / 2)) Consider(new Settlers());
				if (!city.HasBuilding<Temple>()) Consider(new Temple());
			}

			// Garrison a flexible second defender when a hostile unit is actually close — a
			// barbarian raid or a war party within 3 tiles. We deliberately do NOT build City
			// Walls (here or in the standard chain below): per play-testing they are slow to
			// build, drain upkeep, and fold to Catapults, Knights, and bribing Diplomats. A
			// mobile second defender you can re-station or disband on a shield crunch is the
			// better, more flexible insurance. (Barbarians, owner 0, spawn in human-unexplored
			// areas — i.e. on top of AI civs — so this often decides whether an early city survives.)
			byte threatOwnId = Game.PlayerNumber(Player);
			bool hostileNear = Game.GetUnits().Any(u => u.Owner != threatOwnId
				&& (u.Owner == 0 || Player.IsAtWar(Game.GetPlayer(u.Owner)))
				&& Common.DistanceToTile(u.X, u.Y, city.X, city.Y) <= 3);
			// Second defender capped by city size: a size-1 city under Despotism supports
			// exactly one unit free, so a second garrison eats the city-center shield and
			// deadlocks production forever (the Inca case).
			if (hostileNear && defenders < Math.Min(2, (int)city.Size)) Consider(BestDefender());

			// A hull, for a civ with nowhere left to walk — placed HIGH, which is the whole
			// change. Both boat rules already existed near the bottom of this method and both
			// were being reached: BoxedIn() was true on 35 of the Maori's 63 logged production
			// decisions, so a Longboat went into the plan and stayed there. Consider() keeps
			// the first entry per type and CityProduction builds plan[0], so position IS
			// priority, and a city with any building left to make never got that far. Measured
			// across three complete runs the Maori built not one boat of any kind, sat on the
			// same 8 cities from turn 313 to 749, and finished with two idle settlers and an
			// island with nothing left to improve.
			//
			// This is the same failure the spaceship had before the Diaspora path entry: a
			// correct rule in a position nothing ever reaches.
			//
			// Narrow on purpose. Boxed in (no legal land site in reach), below the leader's
			// own city target, coastal, and able to spare the population point — a civ with
			// room to walk into does not need a navy, and the two-hull cap below keeps this
			// from becoming one.
			if (Player.HasAdvance<MapMaking>()
			    && BoxedIn()
			    && Player.Cities.Length < CityTarget()
			    && city.Tile is not null
			    // SALT water, not merely water. IsFreshwaterAt is true for every body that is
			    // not the main ocean (Map.cs:80), which is exactly "an enclosed lake" — and the
			    // Harbour rule has always asked this question. This one asked only `IsOcean`,
			    // so a city on a pond qualified as a port.
			    //
			    // Not hypothetical: the Maori built two Longboats at (316,162), whose adjacent
			    // water is a FOUR-TILE landlocked lake. Both boats sat in it at full movement
			    // for the rest of the game. Zero of the 277 legal colony sites within the
			    // search window touched that water. The crossing logic was never at fault —
			    // the boats were in a puddle.
			    && city.Tile.GetBorderTiles().Any(t => t is not null && t.IsOcean
			                                       && !Map.Instance.IsFreshwaterAt(t.X, t.Y))
			    && CanAffordSettler(city, 3))
			{
				byte boxedId = Game.PlayerNumber(Player);
				// Longboats counted explicitly. The Longboat is NOT IBoardable — it carries no
				// passenger, it IS the colonist, spending a population point and consuming
				// itself ashore as a land Settlers would. Counting only IBoardable hulls, as
				// the Trireme rule below does, left this cap counting everything except the
				// boat it caps.
				int afloat = Game.GetUnits().Count(u => u.Owner == boxedId
				                                     && (u is Longboat || u is IBoardable));
				// Two crossings in flight, not one and not unbounded. The rule this replaces
				// was uncapped, on the reasoning that "one at sea at a time" colonises a new
				// world at one city per crossing — true, but it was written for a rule sitting
				// at the bottom of the plan where it was never reached anyway. At the TOP,
				// uncapped means a boxed-in civ builds boats every turn from every coastal
				// city, at a population point each. A Longboat consumes itself founding, so
				// the count falls as crossings land and the yard reopens on its own.
				if (afloat < 2) Consider(new Longboat());
			}

			// What this civ is TRYING to do. See VictoryPath.
			//
			// Placed after the garrison and before everything else: a civ pursuing a plan
			// still defends its cities first, but its plan outranks the generic
			// infrastructure chain that would otherwise consume every shield it has.
			//
			// Only the Diaspora entry is unconditional-by-availability; the rest lean on
			// items the ordinary chain already offers, so this changes WHEN they are built
			// rather than introducing anything new. Consider() keeps the first entry per
			// type, so position here is priority.
			switch (Path)
			{
				case VictoryPath.Diaspora:
					// The reason this exists. Nothing else in the AI ever asks for a ship
					// part: before this, SS parts were reachable only through the last-resort
					// fallback at the bottom of this method, which fires when a city has run
					// out of everything else. Three civs launched in the 2200 AD run purely
					// because their largest cities had exhausted the building list.
					//
					// ProductionAvailable carries the real gates — signal received, Apollo
					// built, not already launched, per-part maximums (Player.cs:615) — so a
					// civ that cannot build these simply adds nothing here.
					//
					// Built to a TARGET hull rather than to the per-part maximums — see
					// SpaceshipTarget. The order is deliberate and unchanged: structural,
					// then components, then modules. Modules last means the launch condition
					// (2 components, 3 modules, structure to match) is completed by the final
					// module, so the ship launches at its target size instead of at the bare
					// minimum the moment it becomes airworthy.
					(int targetComp, int targetModule) = SpaceshipTarget();
					byte ssMe = Game.PlayerNumber(Player);
					if (Game.Instance.SpaceshipStructural[ssMe] < Game.SpaceshipStructuresNeeded(targetComp, targetModule)
					    && Player.ProductionAvailable(new SSStructural()))
						Consider(new SSStructural());
					if (Game.Instance.SpaceshipComponent[ssMe] < targetComp
					    && Player.ProductionAvailable(new SSComponent()))
						Consider(new SSComponent());
					if (Game.Instance.SpaceshipModule[ssMe] < targetModule
					    && Player.ProductionAvailable(new SSModule()))
						Consider(new SSModule());
					// The lifeline the colony is flown from, and a city a rival can take.
					if (Player.ProductionAvailable(new MissionControl())
					    && !Player.Cities.Any(x => x.HasBuilding<MissionControl>()))
						Consider(new MissionControl());
					break;

				case VictoryPath.Conquest:
					// A conqueror builds the army before the aqueduct. RepublicDemocratic
					// civs cannot field attackers at all, so they get nothing here and fall
					// through to the ordinary chain.
					//
					// Subject to the SAME unit ceiling the production fallback applies at the
					// bottom of this method, and for the same reason: an ambition to conquer
					// is not a licence to keep building spearmen a one-city civ cannot feed.
					// Without this, the path entry ran ahead of the ceiling and re-created the
					// exact pathology the glut guard was written for — measured as France's
					// 236 Militia in 241 production decisions, and caught here by the test
					// that guards it. A civ actually at war still arms without limit: the
					// Militarize stance branch below is reached before the ceiling applies.
					int conqUnits = Game.GetUnits().Count(u => u.Owner == Game.PlayerNumber(Player));
					if (!Player.RepublicDemocratic && conqUnits <= Player.Cities.Length * 3)
						Consider(BestAttacker());
					break;

				case VictoryPath.Commerce:
					// Pax Mercatoria is measured in gross output and in rivals economically
					// bound to you, and the Caravan is the only unit that does both — a trade
					// route raises output at both ends and counts as a binding.
					if (Player.HasAdvance<Trade>()) Consider(new Caravan());
					break;

				case VictoryPath.Culture:
					// Culture accrues from buildings and wonders (Player.Culture), so the
					// cultural path is the ordinary builder's chain run harder. EarnsItsKeep
					// still refuses these where there is no unhappiness to quell, which is
					// correct: a Temple nobody needs generates upkeep, not admiration.
					if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>()) Consider(new Temple());
					if (Player.HasAdvance<Religion>() && !city.HasBuilding<Cathedral>())      Consider(new Cathedral());
					break;

				case VictoryPath.Endurance:
					// Outlast everyone. That is the existing chain, unmodified.
					break;
			}

			// Food first, for a city that has actually stopped.
			//
			// Measured over the last 202 turns of the 2200 AD run: 1,011 production decisions
			// were taken by AI cities of size <= 6 with a food income of zero or less — 285 of
			// them size 2 — and the ranking was Caravan 14%, Diplomat 12%, Colosseum 10%,
			// Observatory 7%, SAM Battery 7%. Granary was 2%, Aqueduct 1%, and the Harbour,
			// which exists precisely to feed these cities, was chosen by exactly none of them
			// (26 times in 7,475 decisions empire-wide).
			//
			// The growth block further down was never reached, because a starving city is
			// usually also an unhappy or an under-garrisoned one, and both of those come first.
			// So the food case gets its own entry ahead of them — but only when the city has
			// genuinely stopped AND could still grow if fed.
			//
			// Disorder still outranks this: a rioting city produces nothing at all, so the
			// happiness chain below keeps its priority in that case. This is for the quiet
			// city that simply sits at size 2 forever.
			if (city.FoodIncome <= 0 && !city.GrowthBlocked && !city.IsInDisorder
			    && Player.HasAdvance<Pottery>())
			{
				// Harbour before Granary, and not interchangeable: a Granary halves the food
				// already coming in, which is nothing when the income is nothing. The Harbour
				// is what makes any come in at all on a city ringed by water.
				if (!city.HasBuilding<Harbour>()
				    && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean && !Map.Instance.IsFreshwaterAt(t.X, t.Y)))
					Consider(new Harbour());
				if (!city.HasBuilding<Granary>()) Consider(new Granary());
			}

			// Preventive happiness: a city on the verge of disorder — unhappy citizens no
			// longer outweighed by happy ones — builds a Temple (then Colosseum, then
			// Cathedral) NOW, ahead of growth, military and settlers. Stance-independent and
			// high priority because a rioting city produces nothing: getting ahead of the
			// happiness ceiling breaks the grow→riot→luxury-quell→grow sawtooth that was
			// leaving the AIs relying on the reactive luxury valve instead of infrastructure.
			// Marketplace sits after Temple: 80 shields vs the Colosseum's 100, pays gold
			// upkeep instead of draining it, and multiplies the luxury slider that
			// ConsiderSliders pumps during riots. Colosseum is gated on size 4 so a
			// 2-shield town doesn't spend 50 turns on it.
			if (city.UnhappyCitizens > 0 && city.UnhappyCitizens >= city.HappyCitizens)
			{
				if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>())      Consider(new Temple());
				if (Player.HasAdvance<Currency>()         && !city.HasBuilding<MarketPlace>()) Consider(new MarketPlace());
				if (Player.HasAdvance<Construction>()     && city.Size >= 4 && !city.HasBuilding<Colosseum>()) Consider(new Colosseum());
				if (Player.HasAdvance<Religion>()         && !city.HasBuilding<Cathedral>())   Consider(new Cathedral());
				if (Player.HasAdvance<Medicine>()          && !city.HasBuilding<Hospital>())    Consider(new Hospital());
			}

			// Growth-first: Granary before Barracks/Settlers when Pottery is known.
			// Without this, tiny AI civs build Militia → Barracks → Settlers → ship
			// settler → city drops to size 1 → cycle repeats, and the city never
			// accumulates food past size 2 because Granary stays buried at the bottom
			// of the standard infrastructure chain.
			if (Player.HasAdvance<Pottery>() && !city.HasBuilding<Granary>()) Consider(new Granary());

			// ...and the Harbour beside it, for the same reason and from the same advance. A
			// coastal city working ocean tiles cannot grow without one, so for those cities
			// this IS the growth building — a Granary only halves the food already coming in,
			// while the Harbour is what makes any come in at all.
			if (Player.HasAdvance<Pottery>() && !city.HasBuilding<Harbour>()
			    && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean && !Map.Instance.IsFreshwaterAt(t.X, t.Y)))
				Consider(new Harbour());

			// Barracks is deliberately NOT considered here. It only makes future units
			// veteran — no growth, no expansion, no immediate defense — yet it used to sit
			// at slot #4 ahead of Settlers and infrastructure, so tiny AI cities burned
			// their early shields on it (Barracks was the single most-built early item in
			// the decision logs). It is now built only in the Militarize stance.

			int ownCities = Player.Cities.Length;
			// Match the city target used by GetStance (line 70-73) so that the Settler-cap and
			// the Expand→Develop transition agree. Previous hard caps of 13/10/7 caused Epic-map
			// civs to stop founding cities long before hitting the stance target, leaving them
			// stuck in Expand stance forever (no research weight shift to Trade/Currency/Banking
			// → never reaches Republic → permanent Despotism tile penalty → cities stay tiny).
			// Linear map scale — see GetStance (line 71) for why area-based was wrong.
			int maxCities = CityTarget();

			// Empire-wide settler ceiling: two per three cities. Every settler rule below
			// has its own local condition, but nothing counted the TOTAL, so several could
			// fire across different cities in the same turn — 16th-century logs showed
			// settlers idling near home with nothing left to do while their cities kept
			// building more. A settler costs a population point, so an unused one is a
			// citizen deleted. Longboats are deliberately NOT counted: a hemmed-in civ's
			// boats are its only expansion and are capped by BoxedIn and cost instead.
			byte settlerCapId = Game.PlayerNumber(Player);
			int liveSettlers  = Game.GetUnits().Count(u => u.Owner == settlerCapId && u is Settlers);
			bool settlerBudget = liveSettlers < Math.Max(1, ownCities * 2 / 3);

			// Tiny-empire settlers: < 3 cities → skip Explorer, build settlers
			// once the city has actual mass to spend. Requiring size >= 3 (and Granary
			// where Pottery is researched) breaks the "size-1 cycle" where AI civs
			// repeatedly ship a settler and revert to size 1, never accumulating food.
			// Never build settlers from a starving city — that accelerates population loss.
			// Runs in every stance: for a 1-2 city civ, expansion IS survival, and shipping
			// a settler out of a size-3 city even relieves a happiness crisis (fewer mouths,
			// fewer malcontents). Stances reorder priorities; they don't veto survival needs.
			if (ownCities < 3)
			{
				bool granaryReady = !Player.HasAdvance<Pottery>() || city.HasBuilding<Granary>();
				// ...or the city is stalled below that size for good, in which case the
				// settler is the only thing that can lift it (see SettlerIsTheOnlyWayOut).
				bool canBuild = CanAffordSettler(city, 3) || SettlerIsTheOnlyWayOut(city);
				if (granaryReady && settlerBudget && canBuild && !city.Units.Any(x => x is Settlers) && ownCities < maxCities)
					Consider(new Settlers());
			}

			// Explorer: one per 3 cities while the map still has meaningful fog. Stop
			// queueing once the player has revealed > 70% of the world's land — late-game
			// Explorer builds just churn shields with nothing useful to scout. Analytics
			// (2026-06-06) showed 8.8% of late-game builds were Explorers, with civs of
			// 30+ cities still pumping them out.
			//
			// The world-wide test was the wrong measure: explorers are LAND units, so a
			// civ on one continent of an Earth map can never walk to 70% of the world's
			// land, the gate never closes, and it builds explorers forever. What matters
			// is whether its own continent still holds fog — which on a small continent
			// can be answered in the first few dozen turns. AI civs do not chase every
			// last goody hut, so once home is charted there is nothing left to scout.
			if (Player.ExploredHomeContinentFraction < 0.95 && Player.ExploredLandFraction < 0.70)
			{
				byte ownId = Game.PlayerNumber(Player);
				int ownExplorers = Game.GetUnits().Count(u => u.Owner == ownId && u is Explorer);
				// Two per civilization, flat. Scaling with city count meant a 40-city
				// empire fielded a dozen scouts; through most of history even ONE
				// expedition in the field was a national undertaking. The fog tests
				// above already stop the queue once home is charted.
				if (ownExplorers < 2) Consider(new Explorer());
			}

			// Consolidate: happiness and growth buildings first
			if (stance == StrategyStance.Consolidate)
			{
				if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>())    Consider(new Temple());
				if (Player.HasAdvance<Construction>()     && !city.HasBuilding<Colosseum>()) Consider(new Colosseum());
				if (Player.HasAdvance<Religion>()         && !city.HasBuilding<Cathedral>()) Consider(new Cathedral());
				if (Player.HasAdvance<Medicine>()          && !city.HasBuilding<Hospital>())    Consider(new Hospital());
				if (Player.HasAdvance<Pottery>()          && !city.HasBuilding<Granary>())   Consider(new Granary());
			}

			// Militarize: garrison up to 2, barracks for veterans, then attackers.
			// One worker even at war: AI wars rarely end (GetStance flips to Militarize
			// whenever at war with anyone), so a civ that loses its last settler mid-war
			// would otherwise never improve another tile — the "zero improvements" wasting
			// disease. Roads serve troop movement anyway.
			if (stance == StrategyStance.Militarize)
			{
				if (defenders < Math.Min(2, (int)city.Size)) Consider(BestDefender());
				byte mzId = Game.PlayerNumber(Player);
				// One per four cities, not one empire-wide. A single settler cannot be in
				// two places, and on an archipelago it is stranded wherever it happens to
				// stand: the autoplayed Japan held a mainland beachhead at Kagoshima with
				// ~30 unclaimed prime sites 4-8 tiles out (grassland and plains, 21 land
				// tiles in the working diamond) and took none of them for 751 turns,
				// because its one permitted settler was 20 tiles south on the home island
				// filling a swamp gap and no city was allowed to build a second. The
				// empire-wide settlerBudget above (2 per 3 cities) is still the real
				// ceiling; this only stops a war footing from meaning "never colonise".
				// One per three cities, floor of two. The previous ownCities/4 with a floor of
				// ONE was too tight at the small end, which is exactly where it matters: a
				// 7-city Japan got a single settler for its whole territory, and one settler
				// cannot road and drain an empire. Measured at turn 578, its two settlers stood
				// on already-improved tiles with nothing in reach. The empire-wide settlerBudget
				// (2 per 3 cities) is still the real ceiling.
				int mzSettlers = Game.GetUnits().Count(u => u.Owner == mzId && u is Settlers);
				if (settlerBudget && CanAffordSettler(city, 3) && !city.Units.Any(u => u is Settlers)
				    && mzSettlers < Math.Max(2, ownCities / 3))
					Consider(new Settlers());
				if (!city.HasBuilding<Barracks>()) Consider(new Barracks());
				if (!Player.RepublicDemocratic) Consider(BestAttacker());

				// Unmanned strike. BestAttacker returns land units only, so before this the
				// AI never deliberately built ANY aircraft — every Fighter and Bomber in a
				// finished game came from the built-out-city fallback. These two are the
				// reason a republic can fight at all without paying war weariness for it
				// (City.ComputeCitizens exempts both), which is exactly why the
				// RepublicDemocratic guard above does not apply here.
				//
				// Capped at one per three cities between them: a missile is spent on use, so
				// an uncapped rule is an empire that builds nothing else.
				byte airId = Game.PlayerNumber(Player);
				int unmanned = Game.GetUnits().Count(u => u.Owner == airId
				                                       && (u is CruiseMissile || u is ReaperDrone));
				if (unmanned < Math.Max(1, ownCities / 3))
				{
					if (Player.HasAdvance<Rocketry>()) Consider(new CruiseMissile());
					if (Player.HasAdvance<Robotics>()) Consider(new ReaperDrone());
				}
			}

			// Expand: infrastructure before settlers, then settlers when city is large enough.
			// Granary goes first so food investment lands before population is spent.
			// minSize raised so cities consolidate at size 3+ before spawning settlers.
			if (stance == StrategyStance.Expand && ownCities >= 3)
			{
				if (Player.HasAdvance<Pottery>() && !city.HasBuilding<Granary>()) Consider(new Granary());

			// ...and the Harbour beside it, for the same reason and from the same advance. A
			// coastal city working ocean tiles cannot grow without one, so for those cities
			// this IS the growth building — a Granary only halves the food already coming in,
			// while the Harbour is what makes any come in at all.
			if (Player.HasAdvance<Pottery>() && !city.HasBuilding<Harbour>()
			    && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean && !Map.Instance.IsFreshwaterAt(t.X, t.Y)))
				Consider(new Harbour());
				if (Player.HasAdvance<CeremonialBurial>() && !city.HasBuilding<Temple>()) Consider(new Temple());
				int minSize = Leader.Development == Expansionistic ? 3
				            : Leader.Development == Normal          ? 4 : 4;
				// Past the fixed cap, keep founding while reachable land remains — GetStance
				// only holds us in Expand here because HasExpansionRoom() is already true.
				if (settlerBudget && CanAffordSettler(city, minSize) && !city.Units.Any(x => x is Settlers)
				    && (ownCities < maxCities || HasExpansionRoom()))
					Consider(new Settlers());
			}

			// Worker settlers: a built-out empire (Develop/Consolidate) keeps a few settlers
			// terraforming — irrigating the tiles its cities work — so cities reach the food
			// surplus to grow past size 3 instead of stalling. Capped at ~1 per 4 cities so it
			// doesn't crowd out economy, and only from healthy cities with mass to spend.
			// AI.Move routes these to BestImproveSite() rather than founding new towns.
			// Size gate is 3, not 4: size 4 is exactly what the worker exists to reach,
			// so an unirrigated city stuck at 3 must still be able to build its way out.
			// It is not 2 — a size-2 city drops to 1 the moment the settler completes.
			//
			// The 1-per-4 ratio was tested against 1-per-2 and 1-per-6, three seeds each,
			// 400 turns (2026-08-01), totalled across seeds:
			//
			//              improved land   advances   mean city size
			//     1 per 6       126           443          5.0
			//     1 per 4       126           444          5.1     <- unchanged
			//     1 per 2       130           414          4.7
			//
			// Halving the ratio changes nothing and doubling it BUYS 3% more improved land
			// for 7% fewer advances and smaller cities — each settler costs a population
			// point, and past this point that outweighs the terraforming. The quota is not
			// the binding constraint; improved land pins near 42% whichever way it moves,
			// because settlers run out of work BestImproveSite will route them to, not out
			// of settlers. Do not raise this without fixing that first.
			//
			// That last conclusion was drawn in the wrong regime and is false at scale. The
			// harness's civs hold about five cities each, where the quota resolves to ONE
			// settler and that settler costs a big share of a small civ's population — which
			// is why 1-per-2 measured worse there. A turn-551 epic game is a different world:
			// 257 cities, 80 settlers, and inside the city radii 2432 tiles of outstanding
			// routable farm work against 2985 land tiles. 81% of all worked land unimproved,
			// with the settlers nowhere near running out of things to do. A 47-city civ was
			// allowed eleven workers for roughly 560 workable tiles.
			//
			// So the ratio is not wrong, its constancy is: a settler is a big cost to a
			// 5-city civ and a rounding error to a 40-city one. Large empires get 1-per-2.
			// Deliberately gated well above anything the harness reaches (its biggest civ
			// runs 11-13 cities) so the measured small-empire behaviour is untouched —
			// verified by the three seeds coming back byte-identical.
			//
			// The epic run measured that change and it did NOTHING: worked land went from 81%
			// unimproved to 86%, settlers from 0.31 to 0.35 per city. The quota was never the
			// constraint. The STANCE GATE was.
			//
			// Develop is the fallback at the bottom of GetStance — reached only when a civ is
			// not consolidating, NOT AT WAR, not speculatively militarising, and out of
			// expansion room. Across 8758 production decisions in that game: Militarize 1592,
			// Expand 703, Consolidate 522, and Develop exactly ONE. 79% of all decisions were
			// taken at war. On an epic map with room left, Develop is unreachable, so the
			// terraforming branch behind it may as well not exist — which is the whole of the
			// "threadbare countryside" symptom that started this.
			//
			// So the gate goes — but only for large empires, which is where the evidence is.
			// Dropping it outright was A/B'd on seeds 101/202/303: one seed inert, one better
			// (29->33 cities, improved land 42->45%), one materially worse (29->20 cities, 122
			// ->99 advances). Aggregate 88->83. That is the regime problem again, and the
			// harness civs are the small regime: for a five-city civ at war a worker settler
			// really is worse than a defender, because it costs a population point and does
			// not hold the line. The stance gate is right for them.
			//
			// It is wrong for a thirty-city civ that has been nominally at war for four
			// centuries, where irrigation is what feeds the cities through the war and the
			// population point is a rounding error. Above the threshold, war and expansion
			// stop disqualifying terraforming; below it nothing changes at all — the harness
			// tops out at 11-13 cities, so all three seeds return byte-identical.
			//
			// The remaining limits still bind in both regimes: the empire-wide settlerBudget
			// (2 per 3 cities), the quota below, CanAffordSettler's size floor, and one settler
			// in build per city at a time.
			const int LargeEmpire = 16;
			bool stanceAllows = ownCities >= LargeEmpire
			                 || stance == StrategyStance.Develop
			                 || stance == StrategyStance.Consolidate;
			if (stanceAllows && settlerBudget && CanAffordSettler(city, 3) && !city.Units.Any(x => x is Settlers))
			{
				byte wsId = Game.PlayerNumber(Player);
				int workers = Game.GetUnits().Count(u => u.Owner == wsId && u is Settlers);
				int quota = ownCities >= LargeEmpire ? ownCities / 2 : ownCities / 4;
				if (workers < Math.Max(1, quota))
					Consider(new Settlers());
			}

			// The Longboat rule used to live HERE, at the bottom of the method, and that is
			// exactly why no civ ever built one — see the entry near the top, beside the
			// garrison. Stated once, up there, where a city can reach it.

			// A hull, for a civ that has run out of world to walk to.
			//
			// The Longboat above is gated on BoxedIn(), which means "no legal land site
			// anywhere in reach" — a bar so high that across a measured 750-turn game not a
			// single Longboat was built by anybody, and every civ but one finished on the
			// continent it started on. This is the softer condition that actually fires: the
			// home continent is charted, most of the WORLD is not, and we have a coast. A
			// transport is worth building then whether or not the last inland gap has been
			// filled — it carries settlers out (AssignMission's colonisation run) and charts
			// the sea lanes on the way.
			//
			// Two per civ. These are expensive hulls, not a navy, and the transport that
			// exists is usually idle rather than full.
			// `ExploredLandFraction < 0.60` was a window that CLOSED PERMANENTLY. It reads
			// as "there is unknown world out there", but once a civ has charted 60% of the
			// world's land it can never build another hull again — including at the exact
			// moment its ships have finally found somewhere worth going. The proxy was for
			// unknown world; the thing actually wanted is unclaimed coast.
			//
			// So: still exploring, OR we have surveyed a colony site and have not settled
			// it yet. KnownColonySites prunes the register as it reads, so a site that has
			// since been taken does not keep the yard open.
			if (Player.HasAdvance<MapMaking>()
			    && Player.ExploredHomeContinentFraction > 0.90
			    && (Player.ExploredLandFraction < 0.60 || KnownColonySites() > 0)
			    && city.Tile is not null
			    // Salt water, for the same reason as the Longboat rule above: a yard on an
			    // enclosed lake launches a hull that can never leave it.
			    && city.Tile.GetBorderTiles().Any(t => t is not null && t.IsOcean
			                                       && !Map.Instance.IsFreshwaterAt(t.X, t.Y)))
			{
				byte hullId = Game.PlayerNumber(Player);
				int hulls = Game.GetUnits().Count(u => u.Owner == hullId && u is IBoardable);
				if (hulls < 2) Consider(new Trireme());
			}

			// Pollution control. A city past the tolerated smog level (City.cs:1193 gives
			// the first 20 units free) pays unhappiness for it and rolls for a new polluted
			// tile every single turn, and enough polluted tiles trigger global warming for
			// the whole world. The AI considered none of this, so its industrial cities
			// smoked unchecked for the rest of the game. Ordered ahead of the general
			// chain deliberately — this list is a priority order, and a city already over
			// the line should fix that before it builds another Library.
			// Start the cleanup BEFORE the city crosses the line, not after. SmokeStacks is
			// post-tolerance, so waiting for it means the city has already been rolling for
			// polluted tiles for as long as the Mass Transit takes to build — and polluted
			// tiles are what drive global warming, which is irreversible and rewrites the
			// map for everyone. Measured over one 750-turn game: FIVE warming events, ending
			// with 24% of all land turned to swamp and rivers wiped out entirely, driven by a
			// single industrial superpower that built its 28 Mass Transits and 29 Recycling
			// Centers only after the damage was done.
			//
			// 15 of the 20 tolerated units is the trigger: close enough that the build
			// finishes near the crossing, far enough that a merely large city is not
			// retooled for nothing.
			const int PollutionActionPoint = 15;
			if (city.SmokeStacks > 0 || city.PollutionPressure >= PollutionActionPoint)
			{
				// Mass Transit zeroes population pollution outright; Recycling Center
				// thirds the industrial side. Hydro Plant only halves it, so it comes
				// last and only where no plant exists yet.
				if (Player.HasAdvance<MassProduction>() && !city.HasBuilding<MassTransit>())
					Consider(new MassTransit());
				if (Player.HasAdvance<Recycling>()      && !city.HasBuilding<RecyclingCenter>())
					Consider(new RecyclingCenter());
				if (Player.HasAdvance<Electronics>()     && !city.HasBuilding<HydroPlant>()
				    && !city.HasBuilding<NuclearPlant>() && !city.HasBuilding<PowerPlant>())
					Consider(new HydroPlant());
			}

			// Standard infrastructure chain (all stances)
			if (Player.HasAdvance<Pottery>()           && !city.HasBuilding<Granary>())      Consider(new Granary());
			// Aqueduct: unlocks growth past size 6 (City.cs:1187). Build when the
			// city is approaching the cap (size 5+) so shields aren't wasted in
			// tiny cities; without this the AI's entire empire stalls at size 6.
			if (Player.HasAdvance<Construction>()      && city.Size >= 5  && !city.HasBuilding<Aqueduct>())   Consider(new Aqueduct());
			if (Player.HasAdvance<CeremonialBurial>()  && !city.HasBuilding<Temple>())        Consider(new Temple());
			if (Player.HasAdvance<Writing>()           && !city.HasBuilding<Library>())       Consider(new Library());
			if (Player.HasAdvance<Currency>()          && !city.HasBuilding<MarketPlace>())   Consider(new MarketPlace());
			// SAM Battery, gated on somebody actually flying — see SomebodyIsFlying. It is
			// the one building with no city-output basis at all, so EarnsItsKeep has no
			// honest break-even to compute for it and waved it through on Rocketry alone.
			// Air defence in a world with no aircraft is 150 shields and 3 gold a turn spent
			// against nothing.
			if (Player.HasAdvance<Rocketry>() && !city.HasBuilding<SamBattery>()
			    && SomebodyIsFlying())                                                        Consider(new SamBattery());
			if (Player.HasAdvance<Construction>()      && !city.HasBuilding<Colosseum>())     Consider(new Colosseum());
			if (Player.HasAdvance<Religion>()          && !city.HasBuilding<Cathedral>())     Consider(new Cathedral());
			if (Player.HasAdvance<Medicine>()          && !city.HasBuilding<Hospital>())    Consider(new Hospital());
			if (Player.HasAdvance<Computers>()         && !city.HasBuilding<Observatory>())   Consider(new Observatory());
			// Sewer System: unlocks growth past size 12 (City.cs:1188). Same
			// pattern — only consider once the city is closing on the cap.
			if (Player.HasAdvance<Engineering>()       && city.Size >= 10 && !city.HasBuilding<SewerSystem>()) Consider(new SewerSystem());

			// Post-contact buildings
			if (Player.HasAdvance<Xenobiology>()        && !city.HasBuilding<Xenolab>())        Consider(new Xenolab());
			if (Player.HasAdvance<MemeticProtocols>()   && !city.HasBuilding<ExchangeCenter>()) Consider(new ExchangeCenter());
			if (Player.HasAdvance<NeuralInterface>()    && !city.HasBuilding<NeuralLab>())      Consider(new NeuralLab());
			if (Player.HasAdvance<AquaticColonization>() && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean) && !city.HasBuilding<SeaPlatform>()) Consider(new SeaPlatform());

			// Hydro Engineer: build one per ~4 coastal cities so the AI can colonize ocean tiles.
			// Skips if the city is starving or has no population to spare.
			if (Player.HasAdvance<AquaticColonization>() && Map[city.X, city.Y].GetBorderTiles().Any(t => t.IsOcean))
			{
				byte ownIdH = Game.PlayerNumber(Player);
				int ownHydro = Game.GetUnits().Count(u => u.Owner == ownIdH && u is HydroEngineer);
				int coastalCities = Player.Cities.Count(c => Map[c.X, c.Y].GetBorderTiles().Any(t => t.IsOcean));
				int hydroCap = Math.Max(1, coastalCities / 4);
				if (ownHydro < hydroCap && city.Size >= 2 && city.FoodIncome >= 0 && !city.Units.Any(u => u is HydroEngineer))
					Consider(new HydroEngineer());
			}

			// Wonder: only for the empire's top production city
			IWonder? wonder = SelectWonder(city, stance);
			if (wonder is not null) Consider(wonder);

			// Second defender once infrastructure is underway
			if (defenders < 2) Consider(BestDefender());

			// Soft units by government / stance
			if (stance == StrategyStance.Militarize && !Player.RepublicDemocratic)
			{
				Consider(BestAttacker());
			}

			// Diplomats: useful under every stance (espionage, sabotage, incite revolt).
			// Previously gated to non-Militarize, which is why no civ ever built one in heavy
			// fighting eras. One per 2 cities, minimum 3 empire-wide — espionage (especially
			// tech theft, now repeatable via the TechStolen cooldown) is high-value, and
			// diplomats are consumed on use, so a larger steady-state pool keeps spies in play.
			if (Player.HasAdvance<Writing>())
			{
				byte ownId2 = Game.PlayerNumber(Player);
				int ownDiplomats = Game.GetUnits().Count(u => u.Owner == ownId2 && u is Diplomat);
				// Floor capped by city count: the old Max(3, …) floor made a 1-city civ
				// owe 3 Diplomats — consumed on use, rebuilt forever — which made Diplomat
				// the #1 AI production item ahead of Settlers. Tiny civs have better uses
				// for their shields than espionage.
				//
				// CEILING added 2026-08-02. One per two cities is a pool that scales with the
				// empire, and the whole premise of that — "diplomats are consumed on use, so
				// keep a larger steady-state pool" — is contradicted by the saves. They are
				// NOT being consumed: a 2200 AD game held 190 alive world-wide (against 199
				// Armor), and the current game spent 123 production decisions, 4.7% of all
				// output, on them. A 30-city civ owed 15 agents and simply accumulated them.
				//
				// Espionage is opportunistic and local — an agent is only worth anything near
				// a foreign city it can reach — so the useful pool is a handful, not a fixed
				// fraction of the empire. Six is already generous against a consumption rate
				// of approximately zero.
				//
				// A/B'd on seeds 101/202/303. The DIVISOR is the whole effect and it is a step,
				// not a dial: /4-capped-6 and /3-capped-8 are byte-identical, and /2-capped-12
				// is byte-identical to the old /2-capped-by-city-count. So anything at or below
				// cities/3 buys the change and anything at cities/2 does not.
				//
				// What the change buys, aggregated over the three seeds: mined tiles 29 -> 58,
				// overseas cities 1 -> 5, hulls 3 -> 8, cities 88 -> 89, advances 456 -> 458.
				// What it costs: improved land 43% -> 38% average, almost all of it seed 303
				// (42 -> 31), as the freed shields go to hulls and expansion rather than
				// settlers (20 -> 12 on seed 101). That is a genuine trade and not a free win —
				// it converts idle espionage into ships and overseas cities at the price of
				// some irrigation. Taken deliberately, because colonies were the thing missing.
				// If a long run shows the countryside going threadbare again, this divisor is
				// the first thing to put back to /2.
				// Ceiling lowered 6 -> 3 (2026-08-06). At 6 apiece across sixteen civs the world
				// carried ~96 agents, and a diplomat is CONSUMED by its mission, so they were
				// rebuilt and respent continuously: 48,643 diplomat moves in one 520-turn run,
				// 35,434 of them ending in a sabotage of a human city — 68 per turn. That is not
				// a spy game, it is a city being dismantled a building at a time by software.
				// The Police Station is the counterplay; this is the volume.
				int diplomatCap  = Math.Min(Player.Cities.Length,
				                            Math.Max(1, Math.Min(3, Player.Cities.Length / 6)));
				if (ownDiplomats < diplomatCap)
					Consider(new Diplomat());
			}

			// Caravans: trade-route gold once Trade is researched. Capped empire-wide so
			// the planner doesn't queue Caravan after Caravan once Trade lands. Caravans are
			// one-shot (consumed on delivery at Caravan.cs:77), so the cap counts in-flight
			// units, not lifetime production. /6 keeps the queue flowing for a typical empire
			// without crowding out science/military builds — see the Diplomat cap above for
			// the same shape applied to a persistent unit.
			if (Player.HasAdvance<Trade>())
			{
				byte ownId3 = Game.PlayerNumber(Player);
				int ownCaravans = Game.GetUnits().Count(u => u.Owner == ownId3 && u is Caravan);
				int caravanCap  = Math.Max(2, Player.Cities.Length / 6);
				if (ownCaravans < caravanCap)
					Consider(new Caravan());
			}

			// Fallback: nothing useful left to build, so pick a random available item — but
			// NEVER the Palace when the civ already has a capital. Building a Palace just
			// relocates the capital (City.cs:1412), so a random pick here had built-out AI civs
			// forever shuffling their seat of government. A capital-less civ (lost its capital in
			// war) is still allowed one, so it can re-establish a corruption-free centre.
			if (plan.Count == 0)
			{
				bool hasCapital = Player.Cities.Any(c => c.HasBuilding<Palace>());

				// This is a safety net for a city with nothing sensible to build, and
				// it picks at RANDOM from everything available — so it must not be
				// allowed to reach for anything that costs POPULATION or that only
				// makes sense in a specific situation. A size-4 city was rolling a
				// Longboat and losing a citizen for a boat it had no use for; every
				// Longboat the AI ever built came from here, never from the rule that
				// is supposed to gate them on being hemmed in.
				// The deliberate rules above cap explorers at two per civ; this fallback
				// picked at random from everything available and Explorer was not excluded,
				// so it minted them without limit — measured at turn 590, Japan had 21 alive
				// and the Mongols 13, against a cap of 2. Any cap the considered rules
				// enforce has to bind here as well, or the fallback quietly overrides it.
				int fallbackExplorers = Game.GetUnits().Count(u => u.Owner == Game.PlayerNumber(Player) && u is Explorer);

				bool Situational(IProduction p) =>
					p is Settlers || p is Longboat || p is HydroEngineer   // cost a citizen
					|| p is ICaravan                                        // needs a destination
					|| p is Diplomat                                        // needs a target
					|| (p is Explorer && fallbackExplorers >= 2);           // already at the cap

				// Unit glut guard. A built-out city reaching this fallback rolled uniformly
				// over everything available, and for a technologically stalled civ most of
				// that list is ancient military — which is how Militia, Chariot and Legion
				// became the most-produced items in the world in the 22nd century, with the
				// unit count climbing ~11/turn while city count stayed flat. Those units
				// then dominated turn time in pathfinding. Past three units per city the
				// civ has enough; let this fallback reach for infrastructure instead, and
				// only return to units when there is genuinely nothing else to build.
				byte glutId = Game.PlayerNumber(Player);
				int ownUnitCount = Game.GetUnits().Count(u => u.Owner == glutId);

				// A nation under attack does not retool its foundries for pure research,
				// and an army fighting for its life is not a "glut". Both guards are
				// suspended in wartime — without this the Lakota spent an entire alien
				// invasion issuing Research Grants in 79% of their cities while The
				// Others took the map, because a built-out city's only civic option IS
				// the grant, so it won every roll.
				bool fighting = stance == StrategyStance.Militarize
				    || Game.Players.Any(p => p is not null && p != Player
				                          && !p.IsDestroyed() && Player.IsAtWar(p));

				// The glut ceiling is RAISED in wartime, not switched off. Suspending it
				// outright made this fallback the empire's production policy for any civ at
				// war — and AI wars are rarely concluded, only abandoned, so that is most
				// civs for most of the late game. Measured over a 590-turn game: Japan's 192
				// production picks were 67 Militia and 63 Explorers, 68% of everything it
				// built, while it sat on 14 advances.
				bool unitGlut = ownUnitCount > Player.Cities.Length * (fighting ? 6 : 3);

				IProduction[] items = city.AvailableProduction
				    .Where(p => !hasCapital || !(p is Palace))
				    .Where(p => !Situational(p))
				    .Where(p => !fighting || p is not ResearchGrant)
			    .Where(WantsSpaceshipPart)
				    // Apply the upkeep rule HERE rather than letting Consider drop the pick
				    // silently: this branch is the last one that adds anything, so a
				    // dropped pick leaves the plan empty and CityProduction (AI.cs:674)
				    // indexes plan[0]. That crash is not hypothetical — it fired on the
				    // first run of this gate, via the ResearchGrant path below.
				    .Where(p => p is not IBuilding b || p is IWonder || EarnsItsKeep(b, city))
				    .ToArray();
				if (unitGlut)
				{
					IProduction[] civic = items.Where(p => p is not IUnit).ToArray();
					if (civic.Length > 0) items = civic;
					// The cap counts settlers ALIVE, not settlers homed here: city.Units is the
					// home roster, so a single idle settler homed to the capital blocked this
					// branch for the rest of the game — which is exactly what happened to
					// France, 20 Militia and one settler across 149 turns.
					else if (ownCities < maxCities && CanAffordSettler(city, 3)
					         && Game.GetUnits().Count(u => u.Owner == Game.PlayerNumber(Player)
					                                   && u is Settlers) < ownCities + 1)
					{
						// Over the unit ceiling with nothing civic left to build. This is the
						// state a poor, hemmed-in civ lives in: too few advances to unlock a
						// single building, so every fallback roll is another spearman it has
						// to feed. Measured over one 597-turn game, France made 241 production
						// decisions and 236 of them were Militia, from a size-7 capital that
						// held one city from the first turn to the last.
						//
						// A settler is the only thing that can change the situation — it can
						// found if an opening appears, and terraform if none does. The
						// empire-wide settlerBudget is deliberately bypassed here: its job is
						// to stop a civ drowning in settlers, and a civ whose alternative is a
						// two-hundredth Militia is not that civ.
						Consider(new Settlers());
						return plan;
					}
				}

				// At peace with every building already raised, the only civic item left is
				// the grant itself. Take it deliberately rather than rolling the dice —
				// otherwise the random pick lands on another obsolete spearman, which is
				// the case this whole fallback kept getting wrong.
				if (!fighting && Player.HasAdvance<Writing>()
				    && !items.Any(p => p is not IUnit && p is not ResearchGrant))
				{
					Consider(new ResearchGrant());
					return plan;
				}
				// Last resort: if filtering left nothing, fall back to a defender
				// rather than to a random population-costing unit.
				if (items.Length == 0)
				{
					Consider(BestDefender());
					return plan;
				}
				Consider(items[Common.Random.Next(items.Length)]);
			}

			// The plan must never come back empty — CityProduction takes plan[0]. Every
			// path above can now decline a candidate on upkeep grounds, so this backstop
			// adds a defender DIRECTLY rather than through Consider, which could decline
			// it too. A unit carries no maintenance, so it is always affordable.
			if (plan.Count == 0) plan.Add(BestDefender());

			return plan;
		}

		// ── exploration helpers ───────────────────────────────────────────────

		internal ITile? BestExploreTile(IUnit unit) => BestExploreTileInner(unit);

		private ITile? BestExploreTileInner(IUnit unit)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile? best = null;
			int bestScore = 0; // only move if it adds value
			var ownCities = Player.Cities;

			// Only consider land we can actually walk to, using the SHARED rule
			// (LandReachable) rather than a private copy of it. The copy that used to
			// live here treated "candidate is in the misc bucket 15" as acceptable, so
			// an explorer standing on a named continent would happily target an islet
			// across open water — and the reachability probe below only fires when the
			// EXPLORER's own continent is unknown, so that case was never checked. The
			// unit set Goto, GotoStep returned null, Goto cleared, and it re-picked the
			// identical tile every turn: an explorer that races to the coast facing the
			// islet and then appears to park there forever.
			byte myContinent = unit.Tile?.ContinentId ?? MISC_CONTINENT;
			bool KnownContinent(byte id) => Map.NamedContinent(id);

			for (int dy = -8; dy <= 8; dy++)
			for (int dx = -8; dx <= 8; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (unit.X + dx + mapWidth) % mapWidth;
				int ty = unit.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean) continue;
				if (!LandReachable(unit, tile)) continue;
				int dist = Common.DistanceToTile(unit.X, unit.Y, tx, ty);

				// Hut bias: BaseUnitLand.TribalHut (case 0/3) rolls Barbarians when
				// NearestCity >= 4 AND the player has cities — ~25% of outcomes there
				// spawn hostile units, which is a real risk to a lone Explorer. So
				// weight close-to-home huts highly and decay the bonus past distance 3.
				// Direct-hit (tile.Hut) > adjacent (will step onto it next turn).
				int hutBonus = tile.Hut ? 12
				             : tile.GetBorderTiles().Any(bt => bt is not null && bt.Hut) ? 8
				             : 0;
				if (hutBonus > 0 && ownCities.Length > 0)
				{
					int homeDist = ownCities.Min(c => Common.DistanceToTile(c.X, c.Y, tx, ty));
					hutBonus = Math.Max(0, hutBonus - Math.Max(0, homeDist - 3));
				}

				int score = CountUnseenTiles(tx, ty) - dist + hutBonus;
				if (score > bestScore) { bestScore = score; best = tile; }
			}

			// Misc-continent tiles (islands) can still be unreachable, so confirm the
			// winner with a single path probe rather than discovering it every turn.
			if (best is not null && !KnownContinent(myContinent)
			    && Common.GotoStep(unit, best.X, best.Y) is null)
				return null;

			return best;
		}

		// Where a ship should sail to chart the world. The naval AI had exactly two
		// behaviours — ferry troops to an invasion, or patrol the nearest home port — so
		// every warship and every idle transport loitered in harbour for the whole game
		// while the map stayed dark. That is fatal for an island civ in particular: its
		// Explorers are land units that can never leave home, so nothing it owns is
		// capable of finding another continent.
		//
		// Targets are water (the ship has to be able to float there) chosen for how much
		// fog they would lift. Coast counts double: a ship that reaches an unknown
		// shoreline reveals a landmass, which is the thing worth knowing.
		internal ITile? BestSeaExploreTile(IUnit unit) => BestSeaExploreTileInner(unit);

		private ITile? BestSeaExploreTileInner(IUnit unit)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile? best = null;
			int bestScore = 0;   // only move if it actually adds something

			for (int dy = -12; dy <= 12; dy++)
			for (int dx = -12; dx <= 12; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (unit.X + dx + mapWidth) % mapWidth;
				int ty = unit.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				// Sea units move through ocean and their own ports only.
				if (tile is null || (!tile.IsOcean && tile.City is null)) continue;

				int unseen = CountUnseenTiles(tx, ty);
				if (unseen == 0) continue;

				bool coastal = tile.GetBorderTiles().Any(b => b is not null && !b.IsOcean);
				int score = unseen * (coastal ? 2 : 1) - Common.DistanceToTile(unit.X, unit.Y, tx, ty);
				if (score > bestScore) { bestScore = score; best = tile; }
			}

			// Confirm the winner is actually sailable to. Enclosed seas and lakes are
			// common on the Earth maps, and without this a ship re-runs the identical
			// failed search every turn — the same loop the land Explorer had.
			if (best is not null && Common.GotoStep(unit, best.X, best.Y) is null)
				return null;

			return best;
		}

		// Ocean-tile target finder for Hydro Engineer: prefers open ocean far from any city
		// (a candidate floating-city site) over tiles already inside a city's working radius.
		// Where a Hydro Engineer should head: open water, as far from anybody's city as it can
		// get without a long swim. Scored `nearestCity - dist`, so distance from civilisation
		// is worth having and distance from the engineer is worth avoiding.
		//
		// bestScore starts at int.MinValue, NOT 0, and that is the whole of a bug that killed
		// the aquatic game outright.
		//
		// Requiring a POSITIVE score means requiring a tile further from every city than from
		// the engineer — and while the engineer stands IN a city that is arithmetically
		// impossible: nearestCity(tile) <= distance(tile, thisCity) = dist, for every
		// candidate, so the best score attainable is zero. The function returned null, Goto
		// stayed empty, the engineer skipped its turn, and did so again the next turn forever.
		// It could never take the first step out of the port it was built in.
		//
		// Measured at turn 750 of a live game: 69 Hydro Engineers alive across eleven civs,
		// every single one standing in a city, zero transport tubes on the map, and not one
		// `found-floating`, `sea-tube` or `sea-aquafarm` action in the entire decision log.
		// The floating cities, the sea farms and the tube network had never run at all.
		//
		// Taking the best tile available rather than only a strictly-improving one cannot
		// wedge: once the engineer is actually at sea the sea-work block above acts before
		// this is consulted, and with no ocean in range this still returns null.
		internal ITile? BestFloatingSite(IUnit unit)
		{
			int mapWidth = Map.WIDTH, mapHeight = Map.HEIGHT;
			ITile? best = null;
			int bestScore = int.MinValue;
			City[] cities = Game.GetCities();

			for (int dy = -8; dy <= 8; dy++)
			for (int dx = -8; dx <= 8; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (unit.X + dx + mapWidth) % mapWidth;
				int ty = unit.Y + dy;
				if (ty < 0 || ty >= mapHeight) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || !tile.IsOcean || tile.City is not null) continue;
				if (tile.Units.Any()) continue;
				int dist = Common.DistanceToTile(unit.X, unit.Y, tx, ty);
				int nearestCity = cities.Any() ? cities.Min(c => Common.DistanceToTile(c.X, c.Y, tx, ty)) : 255;
				int score = nearestCity - dist;
				if (score > bestScore) { bestScore = score; best = tile; }
			}
			return best;
		}

	private int CountUnseenTiles(int x, int y)
		{
			int count = 0;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				int tx = (x + dx + Map.WIDTH) % Map.WIDTH;
				int ty = y + dy;
				if (ty < 0 || ty >= Map.HEIGHT) continue;
				if (!Player.Visible(tx, ty)) count++;
			}
			return count;
		}

		// ── settler improvement selection ──────────────────────────────────────

		private enum SettlerImprovement { Road, Irrigation, Mine, None }

		// Is there a hostile unit or an enemy city within `radius` of this spot? Barbarians
		// (owner 0) always count. Shared by the two places that ask "is this the front, or
		// just a map square inside a country that happens to be at war".
		// A unit that cannot take or hold ground is not a threat, whoever owns it. Settlers,
		// Diplomats, Caravans, Explorers and Hydro Engineers are all things a rival parks on
		// a border in the ordinary course of play — and since the threat tests below decide
		// whether an empire stays on a war footing, counting a trade caravan as an enemy
		// army would keep a civ mobilised and its constitution frozen on the strength of
		// somebody's freight. (At peace none of them count anyway: Hostile() requires a
		// declared war or a barbarian. This matters once a war IS on the books.)
		private static bool NonCombatant(IUnit u)
			=> u is Settlers || u is Diplomat || u is ICaravan || u is Explorer || u is HydroEngineer;

		// Barbarians (owner 0) are deliberately NOT hostile here. This test answers "are we
		// on campaign" — it decides whether a city builds for war, whether a settler roads
		// instead of irrigating, and whether the constitution is frozen. A raiding party is
		// none of those things, and since barbarians never make peace, counting them meant
		// those answers could never revert. Defending against a raid is a local matter and
		// is handled where it belongs, at the threatened city.
		private bool NearHostiles(int x, int y, int radius = 8)
		{
			byte own = Game.PlayerNumber(Player);
			bool Hostile(byte owner) => owner != own && owner != 0
				&& Player.IsAtWar(Game.GetPlayer(owner));
			// A barbarian city that used to be OURS counts as hostile, and is the one
			// barbarian thing that does.
			//
			// `owner != 0` above excludes the horde deliberately — a wandering raider must
			// not put a city on a war footing, which is the same judgement the stance code
			// makes. But this demotion is what decides whether a FRONTLINE city arms at all,
			// and with the horde excluded a civ whose towns were taken was demoted straight
			// back to Develop even while GetStance had it in Militarize for reconquest. The
			// attacker was never built. Same narrow test as the stance clause, and the same
			// expiry: retake the city or lose it for good and this stops being true.
			bool OccupiedOwnCity(City c) => c.Owner == 0 && c.Size > 0 && c.OriginalOwner == own;
			return Game.GetUnits().Any(u => Hostile(u.Owner) && !NonCombatant(u)
			                             && Common.DistanceToTile(u.X, u.Y, x, y) <= radius)
			    || Game.GetCities().Any(c => (Hostile(c.Owner) || OccupiedOwnCity(c))
			                              && Common.DistanceToTile(c.X, c.Y, x, y) <= radius);
		}

		// `conversion` marks an irrigate order that CHANGES THE TERRAIN — draining swamp,
		// clearing jungle or forest — rather than adding a water channel to open ground.
		// The distinction matters because of the despot rule below.
		// `newRoad` distinguishes laying the FIRST road on a bare tile from upgrading one
		// that already exists (road → railroad → transport tube). See the ordering below.
		// Test seam: the irrigate-vs-mine arbitration is the half of hill farming that decides
		// whether a starving city gets food or shields, and asserting on the choice beats
		// inferring it from whichever order happened to be queued.
		internal string TestSettlerPlanAt(IUnit unit)
		{
			TileWork work = WorkAvailable(unit.Tile);
			int nearestOwn = Player.Cities.Length == 0 ? 255
				: Player.Cities.Min(c => Common.DistanceToTile(c.X, c.Y, unit.X, unit.Y));
			return ChooseSettlerImprovement(unit, work.Road, work.Irrigation, work.Mine,
				nearestOwn, work.Conversion, work.NewRoad).ToString();
		}

		private SettlerImprovement ChooseSettlerImprovement(
		    IUnit unit, bool validRoad, bool validIrrigation, bool validMine, int nearestOwnCity,
		    bool conversion = false, bool newRoad = true)
		{
		    StrategyStance stance = GetStance();

		    // Same per-city demotion production uses: a settler working ground with no
		    // hostile within 8 tiles terraforms like a peacetime one, even while the empire
		    // is at war somewhere. Without this the stance is empire-wide, and since AI wars
		    // are rarely concluded — only abandoned — most civs sit in Militarize for the
		    // whole late game, where roads outrank irrigation. Measured at turn 578 with ten
		    // of twelve civs in Militarize: world irrigation stood at 5-11% of worked land.
		    // The frontier still builds roads for troop movement; the interior farms.
		    if (stance == StrategyStance.Militarize && !NearHostiles(unit.X, unit.Y))
		        stance = StrategyStance.Develop;

		    // The despot tile penalty decides whether irrigation is worth anything here — see
		    // DespotBlocksIrrigation, which owns that rule and the measurement behind it.
		    bool preMonarchy = DespotBlocksIrrigation(unit.Tile);

		    // A ROAD UPGRADE never outranks food.
		    //
		    // validRoad counts railroading an existing road, and later tubing an existing
		    // railroad (AI.cs:220). So the moment Railroad is discovered, every roaded tile
		    // in the empire becomes "available road work" again — and with roads placed
		    // unconditionally first, the AI re-swept its whole network instead of farming
		    // it. The decision log shows the collapse precisely: irrigation as a share of
		    // settler work, by era, for the four largest empires —
		    //
		    //     turn 150+   300+    450+    600+
		    //         93%     37%      2%      0%   Babylonians
		    //         87%     88%     30%      0%   Chinese
		    //          0%      2%     21%      0%   Egyptians
		    //         94%     96%     87%     41%   Mongols
		    //
		    // Every civ holding Railroad finished the game at 6-9% of its worked land
		    // irrigated; Japan and the French, who never got it, were at 26% and 7%.
		    //
		    // A FIRST road still comes first — that is connectivity, and it is finite work.
		    // The upgrade is the tail option, taken only when there is nothing left to farm.
		    bool roadFirst = validRoad && newRoad;

		    // Expansion phase: roads first; skip irrigation under Despotism
		    if (stance == StrategyStance.Expand)
		        return roadFirst ? SettlerImprovement.Road :
		               (!preMonarchy && validIrrigation) ? SettlerImprovement.Irrigation :
		               validMine ? SettlerImprovement.Mine :
		               validRoad ? SettlerImprovement.Road : SettlerImprovement.None;

		    // Consolidation: irrigation → growth (roads first under Despotism)
		    if (stance == StrategyStance.Consolidate)
		        return (!preMonarchy && validIrrigation) ? SettlerImprovement.Irrigation :
		               roadFirst ? SettlerImprovement.Road :
		               validMine ? SettlerImprovement.Mine :
		               validIrrigation ? SettlerImprovement.Irrigation :
		               validRoad ? SettlerImprovement.Road : SettlerImprovement.None;

		    // Militarization: roads first for rapid troop movement, and here that DOES include
		    // the rail upgrade — this stance now only applies to settlers working within 8
		    // tiles of a hostile, where moving troops twice as fast is worth more than a
		    // wheat field. Behind the lines the demotion above has already made this Develop.
		    if (stance == StrategyStance.Militarize)
		        return validRoad ? SettlerImprovement.Road :
		               (conversion && validIrrigation) ? SettlerImprovement.Irrigation :
		               validMine ? SettlerImprovement.Mine :
		               validIrrigation ? SettlerImprovement.Irrigation : SettlerImprovement.None;

		    // Default (Develop). The two halves are the government's, not the stance's:
		    //
		    //   Despot / Anarchy — the tile penalty claws back anything above 2, so the
		    //     things worth doing are roads (trade, movement, never penalised) and mines
		    //     (hills 0 -> 2 shields, also under the line). Irrigation is already handled
		    //     terrain-by-terrain above: Plains/Hills/Desert pay, Grassland/River do not.
		    //
		    //   Republic / Democracy — the penalty is gone, so food leads, and the ROAD
		    //     UPGRADE finally earns its place: a railroad adds half again to a worked
		    //     tile's yield and doubles movement. It sits above mines and below
		    //     irrigation, deliberately — putting rail above food is the mistake that
		    //     stalled every large empire's countryside for a whole game.
		    if (preMonarchy)
		        return roadFirst ? SettlerImprovement.Road :
		               validMine ? SettlerImprovement.Mine :
		               validRoad ? SettlerImprovement.Road : SettlerImprovement.None;

		    return validIrrigation ? SettlerImprovement.Irrigation :
		           validRoad ? SettlerImprovement.Road :      // first road, then the rail upgrade
		           validMine ? SettlerImprovement.Mine : SettlerImprovement.None;
		}

		// ── Olvir improvement helpers ─────────────────────────────────────────

		// Pick the Olvir improvement type that best suits a given tile.
		internal static OlvirImprovementType OlvirImprovementFor(ITile tile)
		{
			if (tile.GetBorderTiles().Any(b => b.IsOcean))        return OlvirImprovementType.Aquafarm;
			if (tile is Forest || tile is Jungle)                  return OlvirImprovementType.CanopyArray;
			if (tile is Hills  || tile is Mountains)               return OlvirImprovementType.RepairBay;
			return (tile.X + tile.Y) % 2 == 0
				? OlvirImprovementType.ExchangeNode
				: OlvirImprovementType.BiofilterWall;
		}

		// Find the nearest unimproved land tile within the working radius of any
		// Olvir city.  Returns null if everything reachable is already developed.
		internal ITile? BestOlvirImproveSite(IUnit settler)
		{
			byte ownId = Game.PlayerNumber(Player);
			City[] ownCities = Game.GetCities().Where(c => c.Owner == ownId).ToArray();
			if (ownCities.Length == 0) return null;

			ITile? best = null;
			int bestDist = int.MaxValue;

			foreach (City city in ownCities)
			{
				for (int dy = -2; dy <= 2; dy++)
				for (int dx = -2; dx <= 2; dx++)
				{
					if ((dx == -2 || dx == 2) && (dy == -2 || dy == 2)) continue; // skip corners (match CityRadius)
					int tx = (city.X + dx + Map.WIDTH) % Map.WIDTH;
					int ty = city.Y + dy;
					if (ty < 0 || ty >= Map.HEIGHT) continue;
					ITile tile = Map[tx, ty];
					if (tile is not null && !LandReachable(settler, tile)) continue;
				if (tile is null || tile.IsOcean || tile.City is not null) continue;
					if (Game.Instance.OlvirImprovements.ContainsKey((tx, ty))) continue;
					int dist = Common.DistanceToTile(city.X, city.Y, tx, ty);
					if (dist < bestDist) { bestDist = dist; best = tile; }
				}
			}
			return best;
		}

		// Find the best tile for the Olvir to found a new city on. Prefers ocean tiles
		// (if AquaticColonization is known) and coastal land, targeting "disused" space
		// — tiles not yet claimed by any city. Minimum separation from any existing city
		// is 4 (relaxed from the 8-tile spawn spread) so they pack in more densely over time.
		internal ITile? BestOlvirSettleSite(IUnit settler)
		{
			bool hasAquatic = Player.HasAdvance<AquaticColonization>();
			int half = Map.HEIGHT / 10;

			return Enumerable.Range(0, Map.WIDTH)
				.SelectMany(x => Enumerable.Range(1, Map.HEIGHT - 2).Select(y => (x, y)))
				.Where(t =>
				{
					ITile tile = Map[t.x, t.y];
					if (tile is null || tile is Arctic || tile is Mountains) return false;
					if (t.y <= half || t.y >= Map.HEIGHT - half) return false;
					if (tile.City is not null) return false;
					if (tile.IsOcean && !hasAquatic) return false;
					// Land targets must be walkable from here; ocean targets are left
					// alone, since aquatic colonisation is how the Olvir reach those.
					if (!tile.IsOcean && !LandReachable(settler, tile)) return false;
					// Must be at least 4 tiles from any existing city.
					if (!Game.GetCities().All(c => Common.DistanceToTile(c.X, c.Y, t.x, t.y) >= 4)) return false;
					return true;
				})
				// Prefer ocean first (Olvir affinity), then coastal, then anywhere habitable.
				.OrderByDescending(t => Map[t.x, t.y].IsOcean ? 2 : Map[t.x, t.y].GetBorderTiles().Any(b => b.IsOcean) ? 1 : 0)
				.ThenBy(_ => Common.Random.Next(1000))
				.Select(t => Map[t.x, t.y])
				.FirstOrDefault();
		}
	}
}