# Strategic Resources — Design & Status

Materials gate industry — softly. Iron, Coal, and Oil exist on the map,
can be claimed by remote camps far outside any city's working radius, and
missing one makes the affected production cost +50% shields. Never a wall,
always a price.

## The model (implemented July 2026)

- **Deposits are derived, not placed.** `Game.ResourceAt(tile)` reads the
  map's existing special tiles: Mountains special = **Iron**, Hills special
  = **Coal**, Desert/Swamp special = **Oil**. No new map state, no mapgen
  changes; distribution follows the classic special pattern.
- **Possession** = any of your cities *works* the deposit tile, **or** you
  hold a **camp** on it (`Game.ResourceCamps`, tile → owner, .cos-saved).
- **Camps**: a Settlers order ("Build <Resource> Camp", 3 turns) on any
  unclaimed deposit outside a city — including deep wilderness. Renders as
  a fortified mine. A city founded over a camp absorbs it.
- **Capture by occupation**: any unit standing on a rival's camp at turn's
  end takes it — flags on mines, not ashes (`ProcessResourceCamps`).
  Barbarians count: a barbarian-held camp yields to nobody until retaken.
  Grey goo destroys camps outright (the goo does not mine; it only takes).
- **The soft gate** (`City.ProductionCost`): +50% shields without the
  material, applied to the completion target, rush-buy pricing, AI
  rush-buy math, the bond pool cap, and every city-screen progress display
  — the higher target is *visible*, not hidden.
- **The industrial tier only** — ancient/medieval play is untouched:
  - **Iron**: Cannon, Artillery, Ironclad
  - **Coal**: Factory, Power Plant
  - **Oil**: Armor, Fighter, Bomber, Cruiser, Battleship, Submarine,
    Carrier, Transport
  - Fusion-era units (HoverTank, FusionInf) need nothing — that's the point
    of fusion.
- **AI**: production penalties apply automatically (same cost path); AI
  settlers claim deposits opportunistically when standing on one.
  Dedicated camp-*seeking* AI is deferred pending an autoplay read.

## Deliberately deferred

- Road/rail connectivity requirements for camps (assume wagons).
- Resource depletion.
- Resource trading via the diplomacy console — the tribute machinery
  ("$N/turn while the deal holds") is the natural carrier when wanted.
- Camp yields (camps gate; they don't produce).

## Planned expansion (user roadmap)

- **Copper** — required by the electronics tier (Observatory-era buildings,
  late signal-tech), per the user's Civ III mod heritage.
- **Luxury resources** — happiness-side twins of the strategic trio.
- **Salt** — the user's open question: luxury or mineral? The historically
  correct answer is *both*, which suggests Salt as the bridge resource
  between the strategic and luxury systems when both exist: a mineral that
  trades like a luxury and preserved the food of every empire that ever
  moved an army. Millennia of caravans agree.
