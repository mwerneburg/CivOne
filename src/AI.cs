// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CivOne.Advances;
using CivOne.Buildings;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Leaders;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;

using Democratic = CivOne.Governments.Democracy;

using static CivOne.Enums.DevelopmentLevel;

namespace CivOne
{
	internal partial class AI : BaseInstance
	{
		public Player Player { get; }
		public ILeader Leader => Player.Civilization.Leader;

		// The city this civ is currently marshalling forces to attack.
		private City? _attackTarget;

		// Consecutive blocked steps per settler. See ResolveMovementFailure: a settler holds
		// its target across a few failures rather than re-choosing from where it is standing,
		// which is what stopped the shuttling. Entries are dropped when the unit gives up or
		// dies; nothing here is persisted.
		private const int SettlerRetries = 4;
		private readonly System.Collections.Generic.Dictionary<IUnit, int> _settlerMisses = new();

		// Where each unit has already stood this turn, so it cannot be walked back onto a tile
		// it has left.
		//
		// A step between a railed tile and a city costs NOTHING — cities are rail waypoints,
		// which is the rule as intended and as the original game had it (BaseUnitLand
		// .MovementDone). A unit that oscillates across such a step therefore never runs out of
		// moves, and the turn never ends. Measured in a 17-civ run that stopped at turn 487
		// (1937 AD) with the process at 100% CPU: an Ottoman Knights shuttled between (176,27)
		// and its own coastal city of Quierzy 144,164 times in seventy seconds, MovesLeft
		// pinned at 2 the whole way, with two more units behind it doing the same.
		//
		// The cheap fixes are both wrong. Charging for the step would rewrite the movement
		// economy for the human as well; a step budget per unit would cut short the long rail
		// journeys the network exists for. A unit going somewhere never revisits a tile, so
		// only the oscillation is refused.
		private uint _steppedTurn;
		private readonly System.Collections.Generic.Dictionary<IUnit,
			System.Collections.Generic.HashSet<(int, int)>> _stepped = new();

		// True when this unit has already occupied (x,y) earlier in this same turn.
		private bool AlreadyStoodOn(IUnit unit, int x, int y)
		{
			if (_steppedTurn != Game.GameTurn) { _stepped.Clear(); _steppedTurn = Game.GameTurn; }
			return _stepped.TryGetValue(unit, out var tiles) && tiles.Contains((x, y));
		}

		private void RecordStep(IUnit unit, int x, int y)
		{
			if (_steppedTurn != Game.GameTurn) { _stepped.Clear(); _steppedTurn = Game.GameTurn; }
			if (!_stepped.TryGetValue(unit, out var tiles))
				_stepped[unit] = tiles = new System.Collections.Generic.HashSet<(int, int)>();
			tiles.Add((x, y));
		}

		// War-state tracking for peace initiatives.
		// Turns of appetite for a war the constitution forbids. A Republic or Democracy
		// cannot declare war (AI.Strategy.ConsiderWar), cannot build attackers and cannot
		// militarise — so a civ that climbs the government ladder is disarmed for good, and
		// a world that develops peacefully stays that way. Measured across six games: three
		// flatlined to 0% at-war by turn 300 and never recovered.
		//
		// This is the escape a human player has always had — revolt to a war government when
		// you want a war. While it is set, BestGovernment scores against Militarize, which
		// puts Monarchy and Communism above the republics, and the existing revolt logic does
		// the rest. It decays, so the civ climbs back afterwards.
		private int _warAmbition     = 0;
		internal bool WantsWarFooting => _warAmbition > 0;

		private int _turnsAtWar      = 0;
		private int _peacetimeCities = 0; // city count when we were last at peace
		private int _lastTributeOfferTurn = -100; // turn of the last tribute offer to the human

		// Grievance-demand cooldown: turn on which the last GrievancePack was issued.
		internal int LastGrievanceTurn = -50;

		// Consecutive turns spent shedding units before a constitutional change. Bounded —
		// see ConsiderGovernment; an unbounded version of this cost half the world's
		// research in testing because it could block reform forever.
		private int _govDrawdownTurns;

		// Polluted tiles within 3 of one of our own cities, computed once per turn.
		//
		// This WAS `Map.AllTiles().Count(t => t.Pollution && Player.Cities.Any(...))`,
		// evaluated per settler, per turn: 64000 tiles x up to 30 cities, about two
		// million distance calls, for every settler that took a turn. The move_split
		// probe measured settler moves at 77 ms each with the site scans accounting for
		// almost none of it, and this is the only full-map operation in that path.
		//
		// Two changes, and neither alters the answer:
		//   - Pollution only ever lands inside a city's working radius. Every source
		//     places it there: City.ExecutePollution and ExecuteMeltdown use CityTiles,
		//     and the Owners' arrival strike uses a 5x5 around a capital. So a 7x7 box
		//     around each of OUR cities covers every tile the old predicate could match
		//     (it kept tiles within Chebyshev 3 of an own city — exactly this box).
		//     ~49 x cities reads instead of 64000, deduped for overlapping radii.
		//   - Cached per turn. Every settler asked the same question and got the same
		//     answer; now the first one pays and the rest read it.
		private int _pollutionBacklog;
		private int _pollutionBacklogTurn = -1;

		internal int PollutionBacklog()
		{
			if (_pollutionBacklogTurn == Game.GameTurn) return _pollutionBacklog;

			const int R = 3;                       // must match the old predicate's <= 3
			var seen = new HashSet<int>();
			int n = 0;
			foreach (City c in Player.Cities)
			for (int dy = -R; dy <= R; dy++)
			for (int dx = -R; dx <= R; dx++)
			{
				int ty = c.Y + dy;
				if (ty < 0 || ty >= Map.HEIGHT) continue;
				int tx = (c.X + dx + Map.WIDTH) % Map.WIDTH;
				if (!seen.Add(ty * Map.WIDTH + tx)) continue;
				ITile t = Map[tx, ty];
				if (t is not null && t.Pollution) n++;
			}

			_pollutionBacklogTurn = Game.GameTurn;
			_pollutionBacklog = n;
			return n;
		}

		internal void Move(IUnit unit)
		{
			if (Player != unit.Owner) return;
			long __m0 = TurnMetrics.Now;
			try { MoveInner(unit); }
			finally
			{
				TurnMetrics.AddAiMove(__m0);
				// Which KIND of unit is spending the 25 ms. Temporary — see TurnMetrics.AddBucket.
				TurnMetrics.AddBucket("unit:" + unit.GetType().Name, __m0);
			}
		}

		private void MoveInner(IUnit unit)
		{

			string gotoStr = unit.Goto.IsEmpty ? "empty" : $"({unit.Goto.X},{unit.Goto.Y})";
			//Log($"[AI.Move] {Player.LeaderName}(P{Game.PlayerNumber(Player)}) {unit.GetType().Name} ({unit.X},{unit.Y}) ML={unit.MovesLeft} PM={unit.PartMoves} Moving={unit.Moving} Goto={gotoStr}");

			if (unit.Owner == 0)
			{
				BarbarianMove(unit);
				return;
			}

			if (unit is Settlers && Player.Civilization is Olvir)
			{
				ITile tile = unit.Tile;
				int olvCities = Player.Cities.Length;
				bool shouldExpand = olvCities < 30;

				// Expansion phase: try to found a new city on the current tile or navigate
				// toward the best settle site. Olvir pack more densely than normal civs
				// (minimum 4-tile separation vs. the standard 3+) and prefer ocean/coastal.
				if (shouldExpand)
				{
					bool validCity = tile.City is null
					    && !(tile is Arctic) && !(tile is Mountains)
					    && (tile.IsOcean ? Player.HasAdvance<AquaticColonization>() : true);
					int nearestCity = Game.GetCities().Any()
					    ? Game.GetCities().Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y))
					    : 255;

					if (unit.Goto.IsEmpty)
					{
						if (validCity && nearestCity >= 4)
						{
							DecisionLogger.LogSettlerAction(unit, "olvir-found");
							GameTask.Enqueue(Orders.FoundCity(unit as Settlers));
							unit.SkipTurn();
							return;
						}
						ITile? site = BestOlvirSettleSite(unit);
						if (site is not null && (site.X != tile.X || site.Y != tile.Y))
							unit.Goto = new System.Drawing.Point(site.X, site.Y);
					}

					if (!unit.Goto.IsEmpty)
					{
						ITile? step = Common.GotoStep(unit);
						if (step is null) { unit.Goto = System.Drawing.Point.Empty; unit.SkipTurn(); return; }
						if (!unit.MoveTo(step.X - unit.X, step.Y - unit.Y))
						{
							unit.Goto = System.Drawing.Point.Empty;
							unit.SkipTurn();
						}
						return;
					}
				}

				// Improvement phase (empire mature or expansion blocked): place Olvir
				// improvements near existing cities.
				if (!tile.IsOcean && tile.City is null
				    && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)))
				{
					Game.OlvirImprovements[(tile.X, tile.Y)] = OlvirImprovementFor(tile);
					unit.Goto = System.Drawing.Point.Empty;
					unit.SkipTurn();
					return;
				}

				if (unit.Goto.IsEmpty)
				{
					ITile? next = BestOlvirImproveSite(unit);
					if (next is not null && (next.X != tile.X || next.Y != tile.Y))
						unit.Goto = new System.Drawing.Point(next.X, next.Y);
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? step = Common.GotoStep(unit);
					if (step is null) { unit.Goto = System.Drawing.Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(step.X - unit.X, step.Y - unit.Y))
					{
						unit.Goto = System.Drawing.Point.Empty;
						unit.SkipTurn();
					}
					return;
				}
				unit.SkipTurn();
				return;
			}

			// Longboat: sail to the chosen coast, then put ashore and found. Handled
			// before the land-settler branch because it is a sea unit and shares none
			// of that logic.
			if (unit is Longboat boat)
			{
				// Adjacent to somewhere worth landing? Go ashore now — the crossing
				// is the expensive part and a founded city beats a better one later.
				if (boat.LandingSite() is not null && boat.GoAshore()) return;

				if (unit.Goto.IsEmpty)
				{
					ITile? target = BestOverseasSite(unit);
					if (target is not null) unit.Goto = new Point(target.X, target.Y);
				}
				if (!unit.Goto.IsEmpty)
				{
					ITile? step = Common.GotoStep(unit);
					if (step is null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(step.X - unit.X, step.Y - unit.Y)) unit.Goto = Point.Empty;
					return;
				}
				// Nowhere to go: wait rather than re-deciding every turn.
				//
				// ...but never asleep on open water with no hull under it. That unit is
				// standing on a tube, where sentry means "blocks this tile forever" — the
				// selection loop skips sentried units, so it would never wake on its own.
				// SkipTurn rests it for this turn only. See AI.Strategy.WakeSeaSleepers,
				// which cleans up the ones already asleep out there.
				if (unit.Tile is not null && unit.Tile.IsOcean && unit.Tile.City is null
				    && !unit.Tile.Units.Any(x => x.Class == UnitClass.Water))
				{
					unit.SkipTurn();
					return;
				}
				unit.Sentry = true;
				return;
			}

			if (unit is Settlers)
			{
				ITile tile = unit.Tile;

				// Pollution duty. The AI had no pollution behaviour whatsoever — it never
				// built a mitigation building and never cleaned a tile, so its smog piled
				// up until global warming rewrote the map for everyone, the human included.
				// Settlers already know how to do this: AutoClean routes them to the
				// nearest polluted tile near one of our cities and switches itself back off
				// when there is nothing left to clean. Nobody was turning it on.
				// Gated on SmokeStacks rather than a map scan — FindNearestCityPollution
				// walks every tile, so only settlers actually on duty pay that cost.
				//
				// The crew SIZE follows the backlog rather than a flat share. A fixed third
				// meant one cleaner for most civs (they run 1-6 settlers all game), and one
				// settler clears roughly one tile every few turns including travel — so a
				// single industrial city could out-produce its whole cleanup crew, and the
				// polluted tiles that drive global warming simply accumulated. One cleaner
				// per outstanding polluted tile, never more than half the workforce, so
				// terraforming does not stop while the smog is dealt with.
				//
				// Note the trigger stays reactive, and deliberately: an enrolled settler with
				// nothing to clean switches its own flag straight back off (Settlers.cs:706),
				// so enrolling before the first tile smokes achieves nothing. The response
				// lags the first polluted tile by one turn, which is cheap; it was the crew
				// size that was letting the backlog grow.
				// The gate was `Player.Pollution > 0` — CURRENT EMISSIONS (Player.cs:243 sums
				// SmokeStacks), not smog on the ground. So a civ that cleaned up its cities
				// stopped cleaning up its land: Moscow with Mass Transit and a Recycling
				// Center, emitting zero, sat ringed by tiles nobody would ever be sent to.
				// The greener the cities, the less the countryside got cleaned, and the
				// legacy pollution still fed global warming for everyone.
				//
				// The gate is now ground truth. It could not be before because the backlog
				// was a full-map scan and this runs per settler per turn; PollutionBacklog
				// makes it cheap enough to ask honestly.
				if (unit is Settlers cleaner && !cleaner.AutoClean && PollutionBacklog() > 0)
				{
					byte pollId = Game.PlayerNumber(Player);
					Settlers[] crew = Game.GetUnits().OfType<Settlers>()
						.Where(u => u.Owner == pollId).ToArray();

					int backlog = PollutionBacklog();
					int wanted = System.Math.Max(1,
						System.Math.Min(System.Math.Max(1, crew.Length / 2), backlog));

					if (crew.Count(u => u.AutoClean) < wanted)
					{
						DecisionLogger.LogSettlerAction(unit, "autoclean");
						cleaner.AutoClean = true;
					}
				}

				// Resource camp: a settler standing on an unclaimed Iron/Coal/Oil deposit
				// outside a city claims it before doing anything else.
				//
				// This is also the ARRIVAL half of camp-seeking — it sits ahead of the Goto
				// logic below, so a settler that walked here on a BestCampSite target claims
				// the deposit the turn it arrives without any further routing.
				if (Game.ResourceAt(tile) != StrategicResource.None
				    && tile.City is null && !Game.ResourceCamps.ContainsKey((tile.X, tile.Y))
				    && (unit as Settlers)!.BuildCamp())
				{
					DecisionLogger.LogSettlerAction(unit, "camp");
					unit.SkipTurn();
					return;
				}

				// Any habitable land tile is a valid city site — Desert, Hills, Jungle etc.
				// are all legal in Civ 1. Restricting to Grassland/Plains was causing settlers
				// to mill endlessly after the new arid-interior map generation.
				//
				// The rule itself now lives in AI.Strategy.CanFoundOn, shared with the site
				// scan that routes settlers here. Stating it in both places is what put six
				// settlers on a mountain; see the comment there.
				bool validCity = CanFoundOn(tile);
				// What this tile can take. Defined once, in AI.Strategy.WorkAvailable, so that
				// this half of the settler AI and BestImproveSite cannot disagree about it —
				// three separate bugs came from them doing exactly that.
				TileWork work = WorkAvailable(tile);
				bool convertible      = work.Conversion;
				bool validIrrigation  = work.Irrigation;
				bool validMine        = work.Mine;
				bool canNewRoadHere   = work.NewRoad;
				bool validRoad        = work.Road;
				bool validCanopy = Player.HasAdvance<CanopyCultivation>() && (tile is Forest || tile is Jungle) && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y));
				bool validAquafarm = Player.HasAdvance<BioplexEngineering>() && !tile.IsOcean && tile.GetBorderTiles().Any(t => t.IsOcean) && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y));
				int nearestCity = 255;
				int nearestOwnCity = 255;

				City[] cities = Game.GetCities();
				if (cities.Any()) nearestCity = cities.Min(x => Common.DistanceToTile(x.X, x.Y, tile.X, tile.Y));
				City[] ownCities = cities.Where(x => x.Owner == unit.Owner).ToArray();
				if (ownCities.Any()) nearestOwnCity = ownCities.Min(x => Common.DistanceToTile(x.X, x.Y, tile.X, tile.Y));

				// If Goto is already set the AI previously committed to a better site — honour
				// that commitment and don't detour to found a city at the current tile.
				if (unit.Goto.IsEmpty)
				{
					// Found new cities whenever the map still has room for them — see
					// MayFoundCities. This used to read GetStance() == Expand, which meant a
					// single war anywhere (or two cities in disorder) suspended colonisation
					// entirely; since AI civs are nearly always at one or the other, their
					// settlers spent whole games irrigating the same few tiles. Tiles within
					// 3 of an existing city are still improved rather than settled, so the
					// terraforming workers around a city keep doing their job.
					bool expanding = MayFoundCities();
					// A civ with no cities left founds where it stands, subject only to the
					// tile being legal. Both gates below assume a player that already has
					// somewhere to live: on a crowded late map `nearestCity > 3` is satisfied
					// nowhere, and MayFoundCities asks whether the world has room — the wrong
					// question for a player whose alternative is not existing.
					//
					// Without this a cityless civ becomes a permanent zombie, because
					// Player.IsDestroyed (Player.cs:662) keeps a player alive while it holds
					// one unsupported Settlers. It neither founds nor dies. Measured at 1888 AD
					// in a 438-turn game: the Aztecs on 0 cities, score 20, and not a single AI
					// decision of any kind logged for them in the entire game.
					//
					// Barbarians and the story factions are excluded — the horde does not
					// re-found itself, and an occupation that loses everything it seized is
					// meant to end (Repossession), not squat on a spare tile.
					bool lastChance = ownCities.Length == 0
					               && Game.PlayerNumber(Player) != 0
					               && Player.Civilization is not (TheOthers or TheThing or Skynet)
					               && Player.Civilization is not Civilizations.Barbarian;
					// CentreCanFeed is deliberately NOT part of lastChance. A civ down to its
					// final settler is choosing between a poor city and not existing, and the
					// note above is explicit that the ordinary questions are the wrong ones to
					// ask it. Everyone else has to found somewhere the city can eat.
					if (validCity && (lastChance || (nearestCity > 3 && expanding && CentreCanFeed(tile))))
					{
						DecisionLogger.LogSettlerAction(unit, "found");
						GameTask.Enqueue(Orders.FoundCity(unit as Settlers));
						unit.SkipTurn();
						return;
					}
					// Expand improvement radius: secondary cities surrounded by desert can't
					// get irrigation chains started unless settlers work tiles up to 5 tiles out.
					else if (nearestOwnCity <= 5)
					{
						bool tileAlreadyClaimed = (unit as Settlers)?.IsTileClaimed(tile.X, tile.Y) ?? false;
						if (!tileAlreadyClaimed)
						{
							// Post-contact improvements take priority when available.
						if (validCanopy)  { DecisionLogger.LogSettlerAction(unit, "canopy");   (unit as Settlers)?.BuildCanopyArray(); unit.SkipTurn(); return; }
						if (validAquafarm) { DecisionLogger.LogSettlerAction(unit, "aquafarm"); (unit as Settlers)?.BuildAquafarm();  unit.SkipTurn(); return; }

						// Dry-ground food, ahead of the ordinary improvement choice. Both only
						// ever fire on terrain where irrigation has nothing to offer — Hills
						// without water in the cross, Desert likewise — so they cannot crowd
						// out farming that would have been better. Eligibility is
						// WorkAvailable's, so the scan that routed the settler here and the
						// order it now gives cannot disagree.
						TileWork dry = WorkAvailable(tile);
						if (dry.Terrace)
						{
							DecisionLogger.LogSettlerAction(unit, "terrace");
							GameTask.Enqueue(Orders.BuildTerrace(unit)); unit.SkipTurn(); return;
						}
						if (dry.MoistureFarm)
						{
							DecisionLogger.LogSettlerAction(unit, "moisture-farm");
							GameTask.Enqueue(Orders.BuildMoistureFarm(unit)); unit.SkipTurn(); return;
						}

						var improvementChoice = ChooseSettlerImprovement(unit, validRoad, validIrrigation, validMine, nearestOwnCity, convertible, canNewRoadHere);
							switch (improvementChoice)
							{
								case SettlerImprovement.Road:
									if (validRoad) { DecisionLogger.LogSettlerAction(unit, "road"); GameTask.Enqueue(Orders.BuildRoad(unit)); unit.SkipTurn(); return; }
									break;
								case SettlerImprovement.Irrigation:
									if (validIrrigation) { DecisionLogger.LogSettlerAction(unit, "irrigate"); GameTask.Enqueue(Orders.BuildIrrigation(unit)); unit.SkipTurn(); return; }
									break;
								case SettlerImprovement.Mine:
									if (validMine) { DecisionLogger.LogSettlerAction(unit, "mine"); GameTask.Enqueue(Orders.BuildMines(unit)); unit.SkipTurn(); return; }
									break;
							}
						}
					}

					// Built-out empires head for a tile to irrigate; expanding ones for a new city
					// site. When not expanding we don't fall back to a settle site — founding is
					// gated on `expanding`, so a settler sent there couldn't act and would just mill;
					// a null here drifts it home (below) to wait for terraforming work instead.
					//
					// The reverse fallback DOES apply: a civ that wants to expand but has nowhere
					// to go should terraform what it already holds rather than idle. On an Epic map
					// the city target is ~26, so a boxed-in 7-city Japan counts as "expanding" for
					// the entire game — its settlers went looking for a settle site, found none,
					// drifted home and sat on the city tile, where no improvement is legal. Two
					// settlers doing nothing for centuries beside 24 drainable swamp and forest
					// tiles inside their own city radii.
					// Work at hand beats a journey. Routing "settle first, improve only if
					// there is nowhere to settle" meant that while ANY site remained anywhere
					// in range, no settler ever improved anything — and on a big continent
					// that is the whole game. Measured at 1900 AD, the Lakota led the world
					// with 48 cities and 49 advances, and Rosebud — size 18 — worked 21 tiles
					// of which ONE was roaded and ONE irrigated.
					//
					// So: unimproved ground within a short walk is done first, and the settler
					// heads off to found a city only once its own neighbourhood is in order.
					// Self-limiting by construction — BestImproveSite only returns tiles
					// within 2 of one of our own cities, so once local work runs out the
					// settler goes back to expanding on its own.
					// Expansion first, improvement only when there is nowhere left to settle.
					//
					// Prioritising nearby improvement over founding was tried and measured, and
					// it is a trap: improvable ground is always within reach of home, so
					// settlers garden instead of expanding. Guarding it on "3+ cities and a
					// neglected countryside" was not enough either — past 3 cities the
					// countryside is ALWAYS neglected, so the rule simply latched. Headless
					// autoplay over 200 turns, same map, with and without it:
					//
					//     with:    7 cities, biggest civ 2, 68 advances, 9% of land improved
					//     without: 16 cities, biggest civ 5, 87 advances, 10% improved
					//
					// It halves the world and buys no extra terraforming. If the bare
					// countryside is worth attacking again, it needs a dedicated worker quota
					// — settlers explicitly assigned to improvement and capped per empire —
					// not a diversion applied to every settler that walks past a bare tile.
					// Colonist call-up, deliberately AHEAD of local work — see WantsColonist
					// for why. A designated colonist walks to the boat; every other settler
					// gardens exactly as before, so this changes nothing for a civ that has
					// no hull, no charted site, or a crossing already under way.
					ITile? port = WantsColonist() ? BoardingTile(unit) : null;
					if (port is not null) DonatePortEscort(unit, port);

					// Camp-seeking, between founding and gardening. A civ short of iron pays
					// +50% shields on everything that needs it (City.ProductionCost), which
					// is worth more than any single irrigated tile — but a new CITY is worth
					// more than either, and BestCampSite returns null the moment the civ
					// holds all three materials, so this is inert for most civs most of the
					// game. See AI.Strategy.BestCampSite.
					ITile? camp = BestCampSite(unit);

					ITile? best = port
						?? (expanding
							? (BestSettleSite(unit) ?? camp ?? BestImproveSite(unit))
							: (camp ?? BestImproveSite(unit)));
					if (best is not null && (best.X != unit.X || best.Y != unit.Y))
					{
						unit.Goto = new Point(best.X, best.Y);
					}
					// Note the condition: not just "no site found", but also "the only site is
					// where I already stand" — the improvement branch above has already had
					// its chance at this tile, so falling through here means there is nothing
					// left to do on foot either way.
					else if (ownCities.Any())
					{
						// Nothing left to reach on foot. Before drifting home, look for a ride:
						// a transport sitting in one of our coastal cities is a way to ground
						// this continent does not have. Walking onto its tile boards it
						// (MovementStart sentries the passenger), and the ship's own logic
						// carries it to an overseas shore and unloads.
						ITile? berth = BoardingTile(unit);
						if (berth is not null)
						{
							unit.Goto = new Point(berth.X, berth.Y);
						}
						else
						{
							// Nothing to found, nothing to improve, no ride out. Fold the
							// settler back into a city instead of pacing the countryside
							// with it for the rest of the game.
							//
							// "Add to City" has always existed — Orders.CreateCity turns a
							// settler standing on a city tile into a population point, capped
							// at size 10 (Civ 1's rule) — but only the human menu ever issued
							// it. Nothing in the AI did, so a stranded settler was simply
							// stranded. The case is the island civ: measured on the Maori at
							// 2200 AD, two idle Settlers, no boat, and an island with nothing
							// left to irrigate.
							//
							// Reached only after BOTH site searches and the boarding check
							// have come back empty, so this never dissolves a settler that
							// had somewhere to be.
							City? host = ownCities
								.Where(c => c.Size > 0 && c.Size < MaxJoinCitySize)
								.OrderBy(c => Common.DistanceToTile(c.X, c.Y, unit.X, unit.Y))
								.FirstOrDefault();
							if (host is not null && host.X == unit.X && host.Y == unit.Y)
							{
								DecisionLogger.LogSettlerAction(unit, "join");
								GameTask.Enqueue(Orders.FoundCity(unit as Settlers));
								return;
							}
							// Drift toward a city that could take us — or, if every city is
							// already at the cap, toward the nearest one anyway rather than
							// milling in empty terrain indefinitely.
							City drift = host ?? ownCities
								.OrderBy(c => Common.DistanceToTile(c.X, c.Y, unit.X, unit.Y)).First();
							if (Common.DistanceToTile(drift.X, drift.Y, unit.X, unit.Y) > (host is null ? 2 : 0))
								unit.Goto = new Point(drift.X, drift.Y);
						}
					}
				}

				// Road as you go. A settler crossing its own ground to found a city lays road
				// under itself first: it pre-seeds the trade bonus on the corridor, lets the
				// capital rush units up to the new town, and speeds every settler that
				// follows. It costs two turns per tile, so it is bounded to the corridor near
				// home rather than paving the whole wilderness.
				const int RoadCorridor = 8;
				if (!unit.Goto.IsEmpty && canNewRoadHere && nearestOwnCity <= RoadCorridor)
				{
					DecisionLogger.LogSettlerAction(unit, "road");
					GameTask.Enqueue(Orders.BuildRoad(unit));
					unit.SkipTurn();
					return;
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
					{
						unit.Goto = Point.Empty;
						unit.SkipTurn();
					}
					return;
				}
				unit.SkipTurn();
				return;
			}

			// Olvir Hydro Engineers thread transport-tube lines between the colony's
			// cities: lay a tube wherever they stand on bare ocean, and shuttle from
			// city to city so their wake becomes a corridor. No reachable target →
			// fall through to the generic hydro behaviour below.
			if (unit is HydroEngineer olvirHydro && Player.Civilization is Olvir)
			{
				ITile here = unit.Tile;
				if (here.IsOcean && here.City is null && !here.TransportTube)
				{
					DecisionLogger.LogSettlerAction(unit, "olvir-tube");
					olvirHydro.BuildSeaTube();
					unit.SkipTurn();
					return;
				}

				if (unit.Goto.IsEmpty)
				{
					// Pick among the three nearest coastal/ocean sister cities at random,
					// so a fully-tubed corridor doesn't trap the engineer in a shuttle loop.
					City[] targets = Player.Cities
						.Where(c => Common.DistanceToTile(c.X, c.Y, unit.X, unit.Y) >= 4
						         && (Map[c.X, c.Y].IsOcean || Map[c.X, c.Y].GetBorderTiles().Any(t => t.IsOcean)))
						.OrderBy(c => Common.DistanceToTile(c.X, c.Y, unit.X, unit.Y))
						.Take(3)
						.ToArray();
					if (targets.Length > 0)
					{
						City target = targets[Common.Random.Next(targets.Length)];
						unit.Goto = new Point(target.X, target.Y);
					}
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? step = Common.GotoStep(unit);
					if (step is null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(step.X - unit.X, step.Y - unit.Y))
					{
						unit.Goto = Point.Empty;
						unit.SkipTurn();
					}
					return;
				}
			}

			if (unit is HydroEngineer)
			{
				ITile tile = unit.Tile;
				HydroEngineer hydro = (HydroEngineer)unit;

				if (tile.IsOcean && tile.City is null)
				{
					int nearestCity = 255;
					City[] allCities = Game.GetCities();
					if (allCities.Any()) nearestCity = allCities.Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y));

					int nearestOwnCity = 255;
					City[] ownCities = allCities.Where(c => c.Owner == unit.Owner).ToArray();
					if (ownCities.Any()) nearestOwnCity = ownCities.Min(c => Common.DistanceToTile(c.X, c.Y, tile.X, tile.Y));

					if (nearestCity > 4)
					{
						DecisionLogger.LogSettlerAction(unit, "found-floating");
						hydro.FoundFloatingCity();
						unit.SkipTurn();
						return;
					}

					if (nearestOwnCity <= 3 && Player.HasAdvance<BioplexEngineering>() && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)))
					{
						DecisionLogger.LogSettlerAction(unit, "sea-aquafarm");
						hydro.BuildSeaAquafarm();
						unit.SkipTurn();
						return;
					}

					if (nearestOwnCity <= 3 && !tile.TransportTube)
					{
						DecisionLogger.LogSettlerAction(unit, "sea-tube");
						hydro.BuildSeaTube();
						unit.SkipTurn();
						return;
					}
				}

				// Drift toward open ocean: find a deep ocean tile beyond any city's working radius.
				if (unit.Goto.IsEmpty)
				{
					ITile? dest = BestFloatingSite(unit);
					if (dest is not null) unit.Goto = new Point(dest.X, dest.Y);
				}

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null) { unit.Goto = Point.Empty; unit.SkipTurn(); return; }
					if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
					{
						unit.Goto = Point.Empty;
						unit.SkipTurn();
					}
					return;
				}
				unit.SkipTurn();
				return;
			}

			if (unit is Militia || unit is Phalanx || unit is Musketeers || unit is Riflemen || unit is MechInf)
			{
				// Trim excess defenders in cities (per-city cap of 4)
				while (unit.Tile.City is not null && unit.Tile.Units.Count(x => x is Militia || x is Phalanx || x is Musketeers || x is Riflemen || x is MechInf) > 4)
				{
					IUnit? disband = null;
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Militia)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Phalanx)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Musketeers)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is Riflemen)) is not null) { Game.DisbandUnit(disband); continue; }
					if ((disband = unit.Tile.Units.FirstOrDefault(x => x != unit && x is MechInf)) is not null) { Game.DisbandUnit(disband); continue; }
					break; // unit itself is the only remaining candidate — leave it alone
				}

				// Chieftain: militia explore toward fog-of-war instead of fortifying immediately,
				// but only if the city still has another defender (or the unit is already in the field).
				bool lastCityDefender = unit.Tile.City is not null
					&& unit.Tile.Units.Count(u => u.Role == UnitRole.Defense) <= 1;
				if (Game.Difficulty == 0 && unit is Militia && !lastCityDefender)
				{
					if (unit.Goto.IsEmpty)
					{
						ITile? dest = BestExploreTile(unit);
						if (dest is not null) unit.Goto = new Point(dest.X, dest.Y);
					}
					if (!unit.Goto.IsEmpty)
					{
						ITile? next = Common.GotoStep(unit);
						if (next is null) { unit.Goto = Point.Empty; unit.Fortify = true; return; }
						if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y)) unit.SkipTurn();
						return;
					}
				}

				unit.Fortify = true;
			}
			else
			{
				// Stale-Goto guard for Caravans and Diplomats. If the unit was saved with
				// a target that is now unreachable (peaceful neighbour moved in, war state
				// changed, etc.), AssignMission's FirstStepReachable check wouldn't re-fire
				// because Goto is non-empty — the unit would loop in MoveTo failures until
				// the circuit breaker. Clear the cached target here so AssignMission below
				// re-picks via FirstStepReachable on this very tick.
				if ((unit is Caravan || unit is Diplomat) && !unit.Goto.IsEmpty)
				{
					ITile? step = Common.GotoStep(unit, unit.Goto.X, unit.Goto.Y);
					bool reachable = step is not null;
					// A Caravan or Diplomat's step into a foreign city is its purpose, not a blocked path.
					bool civilianCityEntry = (unit is Caravan || unit is Diplomat) && step?.City is not null && step.City.Owner != unit.Owner && step.City.Owner != 0;
					if (reachable && !civilianCityEntry && step!.Units.Any(u => u.Owner != unit.Owner && u.Owner != 0
					                                  && Game.GetPlayer(u.Owner) is Player puStale
					                                  && !Player.IsAtWar(puStale))) reachable = false;
					if (reachable && !civilianCityEntry && step!.City is not null && step!.City.Owner != unit.Owner && step!.City.Owner != 0
					    && Game.GetPlayer(step.City.Owner) is Player pcStale
					    && pcStale.Civilization is not Civilizations.Barbarian
					    && !Player.IsAtWar(pcStale)) reachable = false;
					if (!reachable) unit.Goto = Point.Empty;
				}

				// Land unit just disembarked — still on an ocean tile. Step straight to land.
				//
				// ...unless the water it is standing on holds a CITY or a transport tube. A
				// coastal city can sit on an ocean tile, and a land unit standing in one has
				// not disembarked anywhere: it is the garrison. This rule marched it out of its
				// own city, AssignMission sent it back in, and because a step between a railed
				// tile and a city costs nothing (BaseUnitLand.MovementDone — cities are rail
				// waypoints), neither leg spent a movement point. The turn then never ended.
				//
				// Measured in a 17-civ run stopped at turn 487 (1937 AD), 100% CPU and a window
				// that could not repaint: one Ottoman Knights bounced between (176,27) and its
				// own city of Quierzy 288,641 times in eighty seconds with MovesLeft pinned at
				// 2, a Riflemen behind it doing the same.
				//
				// The predicate is BaseUnitLand's own ShipWater — ocean, no tube, no city —
				// which already draws this line for movement cost. The AI simply was not asking
				// the same question.
				if (unit.Class == UnitClass.Land && unit.Tile.IsOcean
				    && unit.Tile.City is null && !unit.Tile.TransportTube)
				{
					ITile land = unit.Tile.GetBorderTiles()
					    .Where(t => t is not null && !t.IsOcean && !t.Units.Any(u => u.Owner != unit.Owner))
					    .OrderBy(t => t.Units.Any() ? 0 : 1) // prefer our own units already there
					    .FirstOrDefault()
					    ?? unit.Tile.GetBorderTiles().FirstOrDefault(t => t is not null && !t.IsOcean);
					if (land is not null)
						unit.MoveTo(land.X - unit.X, land.Y - unit.Y);
					else
						unit.SkipTurn();
					return;
				}

				// Assign a mission if the unit is idle (sets unit.Goto)
				if (unit.Goto.IsEmpty) AssignMission(unit);

				if (!unit.Goto.IsEmpty)
				{
					ITile? next = Common.GotoStep(unit);
					if (next is null)
					{
						// No land path — try boarding an adjacent friendly transport
						if (unit.Role == UnitRole.LandAttack)
						{
							byte own = (byte)Game.PlayerNumber(Player);
							ITile boardTile = unit.Tile.GetBorderTiles()
							    .FirstOrDefault(t => t is not null && t.IsOcean
							        && t.Units.Any(u => u.Owner == own && u is IBoardable)
							        && t.Units.Where(u => u is IBoardable).Sum(u => (u as IBoardable)!.Cargo)
							           > t.Units.Count(u => u.Class == UnitClass.Land));
							if (boardTile is not null)
							{
								if (unit.MoveTo(boardTile.X - unit.X, boardTile.Y - unit.Y))
								{
									// Sentry the passenger. Without this the unit is
									// reconsidered while sitting on the boat's ocean tile —
									// GotoStep cannot route a land unit through water, so it
									// steps ashore, finds no land path, and boards again.
									// Board, disembark, board, several times a second.
									// BaseUnitSea wakes cargo when the ship arrives.
									unit.Sentry = true;
									unit.Goto = Point.Empty;
								}
								else unit.SkipTurn();
								return;
							}
						}
						unit.Goto = Point.Empty;
						unit.SkipTurn();
						return;
					}

					// Don't let a GoTo move initiate war with a civilization at peace. Barbarians
					// (Owner 0) are always implicitly hostile — never in _warWith but always
					// fair game. Without the explicit exemption a single Barbarian Settler can
					// freeze an attack stack of dozens, since each unit treats the Settler as
					// "peaceful blocked" and skips turn.
					// Hoisted out of the block below because the SECOND refusal check (foreign
					// units on the tile) needs it too — see the note there.
					bool civilianCityEntry = (unit is Caravan || unit is Diplomat)
					                      && next.City is not null && next.City.Owner != unit.Owner;
					{
						Player? nextCityOwner = (next.City is not null && next.City.Owner != unit.Owner) ? Game.GetPlayer(next.City.Owner) : null;
						// A Caravan or Diplomat stepping onto a foreign city is its purpose, not an
						// act of war (Caravan.Confront → trade route; Diplomat.Confront → steal /
						// incite / sabotage). Without this exemption the peaceful-city block clears
						// the unit's Goto on the final step into its target, so it never enters — the
						// Caravan just shuttles between cities on the rails, the Diplomat never spies.
						bool peacefulBlock =
							next.Units.Any(u => {
								if (u.Owner == unit.Owner) return false;
								if (u.Owner == 0) return false;            // Barbarian unit: always attackable
								Player p = Game.GetPlayer(u.Owner);
								return p is not null && !Player.IsAtWar(p);
							})
							|| (nextCityOwner is not null && nextCityOwner.Civilization is not Civilizations.Barbarian && !Player.IsAtWar(nextCityOwner));
						if (peacefulBlock && !civilianCityEntry)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}
					}

					if (next.Units.Any(x => x.Owner != unit.Owner))
					{
						// ...and the same exemption again, which is where it was being lost. The
						// peaceful-city check above lets a Caravan or Diplomat step into its target,
						// and then this blanket rule took it straight back: EVERY city carries a
						// garrison, so `next.Units.Any(foreign)` is true for every city worth
						// visiting, and a Civilian-role unit refused the move.
						//
						// The result was a caravan that walks to its target, is refused, clears its
						// Goto, re-targets the same city next turn and walks up again — parked on
						// the doorstep for the rest of the game. That is the pile-up of idle
						// caravans beside foreign cities, and because a foreign unit blocks a
						// Settler from entering a tile, the ones that parked on polluted ground
						// stopped that city cleaning it (Nagasaki, 2200 AD).
						//
						// Confront handles what happens on arrival — trade route or spy mission —
						// so the garrison is not this rule's business.
						if ((unit.Role == UnitRole.Civilian || unit.Role == UnitRole.Settler)
						    && !civilianCityEntry)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}

						if (unit.Role == UnitRole.Transport && Common.Random.Next(0, 100) < 67)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}

						// Staged assault: units committed to a designated target don't back down.
						bool stagedAssault = _attackTarget is not null && next.City == _attackTarget;
						if (!stagedAssault
						    && unit.Attack < next.Units.Select(x => x.Defense).Max()
						    && Common.Random.Next(0, 100) < 50)
						{
							unit.Goto = Point.Empty;
							unit.SkipTurn();
							return;
						}
					}

					// Refuse a step back onto ground this unit has already left this turn. See
					// _stepped: a rail-to-city step is free, so an oscillation across one costs
					// nothing and runs until the process is killed.
					if (AlreadyStoodOn(unit, next.X, next.Y))
					{
						Log($"[AI] {unit.GetType().Name} P{unit.Owner} at ({unit.X},{unit.Y}) "
						  + $"would step back to ({next.X},{next.Y}), already held this turn — stopping it here");
						unit.Goto = Point.Empty;
						unit.SkipTurn();
						return;
					}
					RecordStep(unit, unit.X, unit.Y);

                    if (!unit.MoveTo(next.X - unit.X, next.Y - unit.Y))
                    {
                        HandleMovementFailure(unit, next);
                        return;
                    }
					return;
				}
				unit.SkipTurn();
				return;
			}
		}

		internal void ChooseResearch()
		{
			if (Player.CurrentResearch is not null) return;
			// The horde has no laboratories. See ConsiderGovernment for the companion gate.
			if (Player.Civilization is Civilizations.Barbarian) return;

			// The organism does not study. Every advance it holds was taken off a city it
			// assimilated (Game.InfectCity), which is the only route by which it ever reaches
			// Space Flight and therefore The Vessel.
			if (Player.Civilization is Civilizations.TheThing) return;

			// The Registry landed knowing everything bar FutureTech (Game.ExecuteOwnersLanding),
			// so this only ever picked the one advance left. Nothing to choose.
			if (Player.Civilization is Civilizations.TheOthers) return;

			IAdvance[] available = Player.AvailableResearch.ToArray();
			if (available.Length == 0) return;

			StrategyStance stance = GetStance();
			int[] weights = available.Select(a => AdvanceWeight(a, stance, Path)).ToArray();

			// Government escape: no stance list weights Monarchy, so a despot civ kept
			// rolling weight-1 odds against 5-9 weighted picks and stayed in Despotism
			// for centuries — tile penalty intact, cities stuck at size 1-2, even with
			// both prerequisites long since researched. While stuck in Despotism or
			// Anarchy, any advance that unlocks a better government dominates the roll.
			if (Player.Government is Despotism || Player.Government is Anarchy)
			{
				for (int i = 0; i < available.Length; i++)
				{
					if (available[i] is Advances.Monarchy || available[i] is TheRepublic)
						weights[i] += 20;
					// Pull civs missing the prerequisites toward Monarchy too — the boost
					// above is useless to a despot who never researched Code of Laws or
					// Ceremonial Burial in the first place.
					if (available[i] is CodeOfLaws || available[i] is CeremonialBurial)
						weights[i] += 10;
				}
			}

			// Seafaring escape: a civ that has charted its whole home continent but almost
			// none of the world is on an island, and every scout it can build is a land unit
			// that will never leave. Nothing in the stance tables values the advances that
			// float a hull, so an island civ could sit on its rock indefinitely — measured at
			// 1900 AD, the English held seven size-7 cities, 100% of home explored, 0% of the
			// world, no Map Making and not a single ship.
			if (Player.ExploredHomeContinentFraction > 0.95 && Player.ExploredLandFraction < 0.50)
			{
				for (int i = 0; i < available.Length; i++)
				{
					if (available[i] is MapMaking)  weights[i] += 15;   // Trireme: the way off
					if (available[i] is Astronomy)  weights[i] += 5;    // ...and the way onward
					if (available[i] is Navigation) weights[i] += 8;
				}
			}

			int total = weights.Sum();

			int roll = Common.Random.Next(total);
			int cumulative = 0;
			for (int i = 0; i < available.Length; i++)
			{
				cumulative += weights[i];
				if (roll < cumulative)
				{
					Player.CurrentResearch = available[i];
					Log($"AI: {Player.LeaderName} of the {Player.TribeNamePlural} starts researching {Player.CurrentResearch.Name}.");
					return;
				}
			}

			// Fallback (weights should always sum > 0, but be safe)
			Player.CurrentResearch = available[Common.Random.Next(available.Length)];
			Log($"AI: {Player.LeaderName} of the {Player.TribeNamePlural} starts researching {Player.CurrentResearch.Name}.");
		}

		internal void CityProduction(City city)
		{
			if (city is null || city.Size == 0 || city.Tile is null || Player != city.Owner) return;
			long __t0 = TurnMetrics.Now;
			try {

			// Barbarians garrison what they take; they do not run a civilisation out of it.
			// A captured city put them through the ordinary production planner, and because
			// they held exactly one city they landed in the tiny-empire branch
			// (AI.Strategy.cs:2854), whose premise — "for a 1-2 city civ, expansion IS
			// survival" — is written about civilisations. Measured in a turn-219 game: the
			// horde took Tenochtitlan from the Aztecs on turn ~140 and thereafter built a
			// Settlers (turn 145) and then alternated Explorer and Militia to the end. An
			// Explorer is useless to a player with no research, no diplomacy and no map
			// trading, and the Settlers turns the raiders into a fifteenth expanding civ
			// that can never be made peace with. The Aztecs finished on one city, alive only
			// because BaseUnit.cs:127 makes a civ's last city unattackable by barbarians.
			//
			// Defenders only, and no queue: units built here count against the 30-unit horde
			// cap in Game.cs, so a garrisoned city suppresses new raids rather than adding to
			// them. Recapture stays possible, which is the Civ 1 behaviour.
			if (Player.Civilization is Civilizations.Barbarian)
			{
				city.ClearProductionQueue();
				city.SetProduction(BestDefender());
				return;
			}

			// Stalled city: no net production. Rerunning the full plan every turn just
			// thrashes the queue and spams the journal. Ensure a cheap defender exists
			// and leave everything else alone until income recovers.
			if (city.ShieldIncome <= 0)
			{
				int defenders = city.Tile.Units.Count(u => u.Role == UnitRole.Defense);
				if (defenders < 1)
					city.SetProduction(BestDefender());
				return;
			}


			city.ClearProductionQueue();
			var stance = GetStance();
			var plan = PlanProduction(city, stance);
			city.SetProduction(plan[0]);
			DecisionLogger.LogCityProduction(city, plan[0], stance.ToString(), hasRoom: HasExpansionRoom());
			for (int i = 1; i < plan.Count; i++)
				city.EnqueueProduction(plan[i]);
			} finally { TurnMetrics.AddAiProduction(__t0); }
		}

		// A settler may join a city below this size, adding a population point and being
		// disbanded. Mirrors the cap enforced in Orders.CreateCity ("ADDCITY"): stated here
		// so the AI does not walk a settler across the map to a city that will refuse it.
		private const int MaxJoinCitySize = 10;

		// Keyed by Player — and Player.GetHashCode is Game.PlayerNumber(this), i.e. the SLOT
		// INDEX IN THE CURRENT GAME (Player.cs:1107), with Equals to match. So a Player object
		// from a previous game hashes to the same bucket as whoever now holds that slot, and a
		// stale entry is returned in preference to building a fresh one: every AI decision then
		// reads the OLD game's advances, cities and gold.
		//
		// Nothing cleared this. Starting a new game or loading a save without restarting the
		// process left every AI reasoning about the game before it. Found via a test that
		// passed alone and failed in the full suite — by the fortieth Sim.NewGame the cache was
		// handing back AIs bound to long-dead players, and the tech test in WorkAvailable read
		// false for an advance the live player definitely had.
		private static Dictionary<Player, AI> _instances = new();

		// Called wherever the Game singleton is replaced. See above for why this is not
		// optional.
		internal static void ResetInstances() => _instances.Clear();
		internal static AI Instance(Player player)
		{
			if (_instances.ContainsKey(player))
				return _instances[player];
			_instances.Add(player, new AI(player));
			return _instances[player];
		}

		private AI(Player player)
		{
			Player = player;
		}
	}
}