# Cursed Wonders — Design Roster

Sci-fi hijinks in the register of the South Pole Expedition curse. A cursed
wonder advertises a blessing and *usually* delivers it — but sometimes the
ice thaws, the play is the wrong play, the factory doesn't stop.

## Design rules

1. **Budget.** Not every wonder rolls. The roster below is the whole list;
   every other wonder is trustworthy. If wonders become slot machines,
   players stop building them.
2. **Playable crisis, never a cutscene punishment.** Every curse has
   counterplay and an end state, like the Thing's five-turn clocks.
3. **Weight by conduct where thematic** (the archetype-draw philosophy:
   your character changes the odds, never the outcome). Pure 1/4 dice
   otherwise.
4. **Foreshadow in flavor text.** Every cursed wonder's civilopedia entry
   carries one quiet warning sentence, so the curse is retrospectively fair.
5. **Story wonders are exempt.** SETI, Interstellar Probe, Apollo, the five
   Dome components, Fusion Core, and South Pole Expedition are load-bearing
   for the Tau Ceti arc. No double-cursing. (Apollo gates South Pole, so its
   chain is already shadowed.)
6. **One curse active per game?** Open question. Recommend a soft cap:
   at most two cursed outcomes per game; later rolls auto-bless. Shock
   loses value with repetition.

Implementation molds already in the codebase:

| Mold | Proven by |
|------|-----------|
| Hostile pseudo-faction, mid-game join | TheThing, TheOthers |
| City-halving strike + building loss | ExecuteOwnersStrike |
| Faction seizure / city transfer | ExecuteOwnersLanding |
| Per-city parasite/overlay keyed by tile | OlvirImprovements dict |
| Recurring low-probability scoped event | Hurricane checks |
| Barbarian super-unit spawn (land/sea) | Barbarian spawn machinery |
| Contagion along trade routes | TradeRoutes + culture defection |
| Countdown clocks saved in .cos | ThingOutbreaks |
| Event art + TerminalScreen reveal | EventArtScreen / TerminalScreen |

---

## The roster

### 1. Manhattan Project — *Gozira* (existing wonder)

- **Blessing:** vanilla (enables Nuclear units).
- **Trigger:** not a build-time roll. The wonder plants the egg; **the first
  nuclear detonation by anyone wakes it**. Conduct-weighted in the purest way.
- **Curse:** a kaiju (barbarian super-unit: massive attack/defense/HP, move 1,
  shrugs off one nuke) surfaces off the coast of the detonator's largest port
  city and walks inland, attacking cities in its path.
- **Counterplay:** concentrated conventional forces; it can be killed. Ends
  when dead.
- **Art:** `Gozira.png` — it rising behind the harbor. Caption:
  "AWAKENED — COURSE: {CITY}".

### 2. The Internet — *the Splinter Republic* (new wonder, late)

- **Tech:** post-contact era or Computers-tier.
- **Blessing:** +science and +culture empire-wide (exact numbers at build time).
- **Curse (1/4):** an outbreak of Social Media. Half the builder's cities
  (round down, capital exempt, most-distant first) secede as a new civ on an
  unused extended slot — full tech copy, immediate rival, at peace (for now).
  Cities in disorder afterwards defect *to the splinter* via the existing
  culture-defection check.
- **Counterplay:** reconquest (they're your own cities back), or diplomacy —
  the splinter can be tributed, pacted, gifted like anyone else.
- **Mold:** ExecuteOwnersLanding faction creation + city transfer.
- **Art:** `SplinterRepublic.png`.

### 3. The Portal — *the Greys* (new wonder, late)

- **Tech:** post-contact tier (fits "extra-planar" ambitions).
- **Blessing (3/4):** enlightened beings counsel the world: global peace —
  `MakePeace` between all civs + long peace treaties + attitude bonuses all
  around.
- **Curse (1/4):** loafing Greys move into the wonder city and spread to one
  new city every ~10 turns (per-city parasite flag, Olvir-improvements mold):
  each infested city gets +corruption and one permanently unhappy citizen.
  They don't fight. They just... stay. Watching television.
- **Counterplay:** they leave a city that starves for one turn (they hate
  effort) — deliberate austerity evicts them. MIB energy.
- **Art:** `TheGreys.png` — them on a couch in front of the Palace.

### 4. Pyramids — *the Visitations* (existing wonder)

- **Blessing:** vanilla.
- **Curse (1/4):** the alignment is a beacon. For the next 4000 years
  (~all game), the wonder city rolls a small per-turn chance (hurricane
  mold) of a visitation: a citizen vanishes (-1 pop), a tile is scorched,
  or — rarely — "recovered debris" grants +science. Mostly harmless,
  permanently unsettling.
- **Counterplay:** none, and that's fine — it's the mildest curse, an
  ambient haunting rather than a crisis. The story is the point.
- **Art:** `Visitations.png` — **medieval tapestry, deliberately broken
  perspective, saucer above the ziggurat.** (User's brief; the best art
  prompt in the project.)

### 5. Stonehenge — *the Door* (new wonder, ancient)

- **Tech:** Mysticism-tier, ancient era.
- **Blessing (3/4):** a free Temple in every city, present and future
  (Michelangelo-style ongoing effect).
- **Curse (1/4):** the circle is a door. Something comes through: the wonder
  city is halved (ExecuteOwnersStrike mold — population, buildings) and a
  hostile guardian super-unit stands in the stones until killed. The free
  temples still arrive — the druids got *that* part right.
- **Counterplay:** kill the guardian; ancient-era armies make this a real
  early-game war effort.
- **Art:** `TheDoor.png` — the trilithon glowing from inside.

### 6. Shakespeare's Theatre — *The King in Yellow* (existing wonder)

- **Blessing:** vanilla (content citizens in the city).
- **Curse (1/4):** the debut play is the wrong play. The wonder city gains
  **permanent unrest** (vanilla effect inverted), and the madness spreads
  along **trade routes**: each turn, any city holding a route to an afflicted
  city has a small chance to catch it.
- **Counterplay:** cancel routes to quarantine (cut the tour); afflicted
  cities are cured by building a Cathedral (a stronger faith than the play).
- **Mold:** trade-route graph + per-city flag. Weird-fiction pedigree sits
  exactly beside The Thing.
- **Art:** `KingInYellow.png` — empty stage, one pale mask on the boards.

### 7. Isaac Newton's College — *the Alchemist's Success* (existing wonder)

- **Blessing:** vanilla (+science in city).
- **Curse (1/4):** Newton's *other* research works. A temporal anomaly
  settles on the city; each turn it rolls once: free advance / lost advance /
  a unit from the past (Knights in 1900) / a unit from the future (one
  HoverTank in 1400, unsupported). Bounded chaos, one city.
- **Counterplay:** none needed — it's symmetric chaos; average value ≈ 0
  but variance is the story. Ends if the city is lost or (option) after
  ~50 turns "the equations balance."
- **Art:** `Anomaly.png`.

### 8. The Lighthouse — *the Leviathan* (existing wonder)

- **Blessing:** vanilla (veteran sea units).
- **Curse (1/4):** the light carries farther than intended. A leviathan
  (barbarian sea super-unit) circles the builder's coasts, attacking ships
  and coastal cities' harbors. Ancient-era Jaws.
- **Counterplay:** hunt it down; killing it grants a milestone score bonus
  and a newspaper worth framing.
- **Art:** `Leviathan.png` — tentacles around a trireme, lighthouse beam.

### 9. Great Wall — *What the Wall Was For* (existing wonder)

- **Blessing:** vanilla.
- **Curse (1/4):** the old builders knew something. Barbarian spawn rate
  near the builder's continent **doubles for one era** — the wall wasn't to
  keep them out; it was to keep something's attention elsewhere, and you
  just rebuilt the beacon.
- **Counterplay:** the wall itself (vanilla defense bonus) — the curse makes
  the blessing necessary. Self-balancing.
- **Mold:** one multiplier on the existing spawn roll + a dated newspaper.

### 10. Cure for Cancer — *the Overflowing Cup* (existing wonder)

- **Blessing:** vanilla (+1 content citizen everywhere).
- **Curse (1/4):** it cures slightly more than cancer. Every city +2
  population immediately; granaries empty; the happiness bonus becomes a
  food crisis in every jungle/desert city. A blessing that plays as a
  disaster — no faction, no units, pure economics.
- **Counterplay:** it *is* population — a civ with food infrastructure
  converts the curse into the strongest outcome on this list. Punishes
  hollow empires, rewards developed ones.

### 11. The Oracle — *the Other Voice* (existing wonder)

- **Blessing:** vanilla (doubled temple effects).
- **Curse (1/4):** the Oracle answers, and it is not Apollo. The advisor
  begins delivering **true hidden information** — AI war planning, tribute
  pacts you can't see, the visitor-archetype leaning — while empire-wide
  happiness sags one step under the dread of prophecy. Information as a
  cursed resource.
- **Counterplay:** you can brick it up (sell the wonder's city improvement?
  raze option in city screen): silence the voice, lose the intel, restore
  the peace of ignorance.
- **Art:** `OtherVoice.png`.

### 12. Nanobot Factory — *Grey Goo* (new wonder, late — user's brief)

- **Tech:** post-contact tier (Fabricator-adjacent).
- **Blessing (3/4):** late-game Leonardo's Workshop — free unit upgrades
  each turn to current-era equivalents.
- **Curse (1/4):** the factory doesn't stop at your units. **Grey goo**: the
  tile under the wonder city converts to Goo (dead terrain: no food, no
  shields, no trade) and the front **doubles every 5 turns** (Thing-clock
  mold, applied to terrain) — 1 tile, 2, 4, 8 — consuming improvements,
  Olvir overlays, and any unit that ends its turn on it. It cannot cross
  ocean. The wonder city itself is consumed at the second doubling if the
  goo isn't stopped.
- **Counterplay:** Settlers/Engineers cleanse frontier Goo tiles like slow
  pollution (2 turns each); **nuking a goo region sterilizes it entirely**
  — the one time the game *rewards* a nuclear strike on your own land, with
  all the fallout/warming consequences that implies. Fire for the Thing,
  atom for the goo.
- **Art:** `GreyGoo.png` — a silver tide with a half-dissolved city wall.

---

## Status

**All twelve implemented** (July 2026). Outstanding: event art for several
(see generate_improvement_art.sh — missing sources are skipped harmlessly),
and bespoke unit sprites for Gozira / Leviathan / Henge Guardian (all three
wear placeholder sprites).

## Suggested build order

1. **Gozira** — self-contained, maximum spectacle, detonation trigger is the
   showcase for conduct-weighted curses.
2. **The Portal** — new wonder, both outcomes cheap, comic relief.
3. **Grey Goo** — terrain-eater; the Thing's sibling, mostly existing molds.
4. **The Internet** — biggest payoff, most new machinery (civil split).
5. **King in Yellow** — trade-route contagion; small, literary, chilling.
6. Remainder in any order; Pyramids whenever the tapestry gets painted.

## Persistence notes

Each curse needs at most: one flag/dict in Game (Cos-saved, following
ThingOutbreaks' `List<int[]>` pattern), art keys via `EventArtScreen.FindPath`
(exact-case), and entries in `generate_improvement_art.sh`. New wonders need
enum + class + civilopedia text + art in the improvement pipeline.
