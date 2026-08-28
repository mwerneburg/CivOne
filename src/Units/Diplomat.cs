// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Tiles;
using System.Linq;
using CivOne.Buildings;
using System.Collections.Generic;

namespace CivOne.Units
{
	internal class Diplomat : BaseUnitLand
	{
		public static bool CanIncite(City cityToIncice, int gold)
		{
			return gold >= InciteCost(cityToIncice) && !cityToIncice.HasBuilding<Palace>();
		}

		// What a city is worth to the civ that would lose it. Civ 1 priced a size-1 hamlet and
		// a wonder-bearing metropolis identically: only the OWNER'S TREASURY entered, and an AI
		// treasury is usually near empty, so any distant city went for (0 + 1000) / 19 = 52 gold
		// regardless of what had been built in it. These put the city itself in the price.
		private const int WorthPerCitizen  = 200;
		private const int WorthPerBuilding = 300;
		private const int WorthPerWonder   = 2000;

		// How firmly the owner holds the place, as a percentage on top of that worth. Two things
		// tighten a grip: proximity to the seat of government, and an empire substantial enough
		// that defecting means joining a rival rather than escaping a doomed one.
		//
		// Distance modulates the GRIP, not the worth. Civ 1 divided the entire price by distance,
		// which is why a remote city was pocket change — but a wonder twenty tiles from the
		// palace is still a wonder. The frontier should be easier to turn, not worthless.
		private const int GripAtTheGates   = 600;   // divided by (distance + 3)
		private const int GripPerCity      = 5;
		private const int GripMaxFromCities = 200;

		public static int InciteCost(City cityToIncite)
		{
			Player owner = cityToIncite.Player;
			City[] cities = owner.Cities;
			City capital = cities.Where(c => c.HasBuilding(new Palace())).FirstOrDefault();

			int distance = capital is null ? 16 : cityToIncite.Tile.DistanceTo(capital);

			// A quarter of the treasury: Civ 1 used the owner's gold as the whole price, which
			// meant a broke AI sold anything. It belongs in the reckoning, but not as the bulk.
			int worth = (WorthPerCitizen  * cityToIncite.Size)
			          + (WorthPerBuilding * cityToIncite.Buildings.Length)
			          + (WorthPerWonder   * cityToIncite.Wonders.Length)
			          + (owner.Gold / 4);

			int grip = 100
			         + (GripAtTheGates / (distance + 3))
			         + Math.Min(GripMaxFromCities, GripPerCity * cities.Length);

			int cost = worth * grip / 100;

			// A city already rioting is halfway to revolt — the player's lever for making an
			// expensive target affordable, and the reason unrest is worth engineering first.
			if (cityToIncite.IsInDisorder) cost /= 2;
			return Math.Max(1, cost);
		}

		public IAdvance? GetAdvanceToSteal(Player victim)
		{
			IList<IAdvance> possible = victim.Advances.Where(p => !Player.Advances.Any(p2 => p2.Id == p.Id)).ToList();

			if (!possible.Any())
				return null;

			return possible[Common.Random.Next(possible.Count)];
		}

		// True when this city cannot be sabotaged at all. Checked at both call sites — the AI's
		// mission dispatch in Confront and the player's own DiplomatSabotage screen — so the
		// rule reads the same whichever direction the agent is travelling.
		internal static bool SabotageProof(City city) => city.HasBuilding<Buildings.PoliceStation>();

		public string Sabotage(City city)
		{
			Game.DisbandUnit(this);

			// The police got there first. The agent is spent either way.
			if (SabotageProof(city)) return $"agent held by {city.Name} police";

			IList<IBuilding> buildings = city.Buildings.Where(b => (b.GetType() != typeof(Buildings.Palace))).ToList();

			// buildings.Count + 1 outcomes: sabotage any one building, or (the last
			// value) halt production. Next's upper bound is exclusive, so the +1 is
			// what makes the production-sabotage branch reachable.
			int random = Common.Random.Next(0, buildings.Count + 1);

			if (random == buildings.Count)
			{
				city.Shields = (ushort)0;
				string? production = (city.CurrentProduction as ICivilopedia)?.Name;
				return $"{production} production sabotaged";
			}
			else
			{
				// sabotage a building
				city.RemoveBuilding(buildings[random]);
				return $"{buildings[random].Name} sabotaged";
			}
		}

		protected override bool Confront(int relX, int relY)
		{
			ITile moveTarget = Map[X, Y][relX, relY];

			if (moveTarget.City is not null)
			{
				// Under autopilot the human still OWNS the unit but is not playing it —
				// prompting here stops the whole run dead waiting for a click that never
				// comes. Fall through to the AI branch, which decides for itself.
				if (Human == Owner && !Settings.Instance.Autopilot)
				{
					GameTask.Enqueue(Show.DiplomatCity(moveTarget.City, this));
					return true;
				}
				else
				{
					City target = moveTarget.City;

					// Counter-espionage: a resident Diplomat has a 50 % chance of catching the spy
					if (target.Tile.Units.Any(u => u.Owner == target.Owner && u is Diplomat)
					    && Common.Random.Next(2) == 0)
					{
						Game.DisbandUnit(this);
						if (target.Player == Human || Player == Human)
							GameTask.Insert(Message.Spy("Spies report:", $"{Player.TribeName} spy caught", $"in {target.Name}!"));
						return true;
					}

					// A Police Station catches saboteurs outright — ordinary police work, which
					// is what actually caught spies through the Cold War. Not a dice roll: it is
					// the counterplay to a campaign of sabotage, and it has to be dependable
					// enough to be worth building. The spy is lost, so a civ that keeps sending
					// them keeps paying for them.
					if (target.HasBuilding<Buildings.PoliceStation>())
					{
						Game.DisbandUnit(this);
						if (target.Player == Human || Player == Human)
							GameTask.Insert(Message.Spy("Spies report:", $"{Player.TribeName} agent held", $"by police in {target.Name}."));
						return true;
					}

					// Incite revolt: when affordable and the target isn't a capital, flip the
					// city. Cheaper than a military campaign and gives us the cake fully baked.
					// Skips size-1 starter towns (the conversion isn't worth the diplomat).
					if (CanIncite(target, Player.Gold) && target.Size >= 2)
					{
						int inciteCost = InciteCost(target);
						byte oldOwner = target.Owner;
						Player oldOwnerPlayer = Game.GetPlayer(oldOwner);

						// Disband resident units and a random ~half of the buildings.
						foreach (IUnit u in target.Units.Concat(target.Tile.Units.Where(u => u.Owner == oldOwner)).Distinct().ToArray())
							Game.DisbandUnit(u);
						foreach (IBuilding b in target.Buildings.Where(b => Common.Random.Next(0, 2) == 0).ToList())
							target.RemoveBuilding(b);

						target.Owner       = this.Owner;
						target.TechStolen  = false;
						Player.Gold       -= (short)inciteCost;
						Game.DisbandUnit(this);
						oldOwnerPlayer?.IsDestroyed();
						bool humanVictim = oldOwnerPlayer == Human;

						// Use oldOwnerPlayer / humanVictim, NOT target.Player: target.Owner was
						// reassigned to the inciter above, so target.Player == Human is always
						// false for the victim and the human never heard of the loss.
						//
						// Under an elected government (Republic/Democracy) the affronted Senate
						// convenes an interactive response — recommend war, with the player free
						// to do nothing, declare war, or (with an embassy) strong-arm the city
						// back. Under authoritarian rule there is no Senate: just the spy report.
						// Inserted FIRST so the rebellion art (inserted next, thus on top of the
						// LIFO queue) plays before it.
						// Inciting a city counts toward the same tally: it is the gravest act a
						// diplomat can commit, and a civ that does it repeatedly should stop
						// being shielded by our Senate. The hearing below is the incite-specific
						// one, so no second dialog is convened here.
						if (humanVictim) Game.RecordProvocation(Player);

						if (humanVictim && Human.RepublicDemocratic && System.Linq.Enumerable.Contains(Game.GetCities(), target))
							GameTask.Insert(Show.IncitedCityResponse(target, Player));
						else if (humanVictim || Player == Human)
							GameTask.Insert(Message.Spy("Spies report:", $"{Player.TribeName} incite", $"revolt in {target.Name}!"));

						// Human's city was incited away — show the rebellion art.
						if (Game.Animations && humanVictim)
						{
							string? artPath = CivOne.Screens.ImprovementArtScreen.FindArtPath("Incite Rebellion", "event_art");
							if (artPath is not null)
								GameTask.Insert(Show.Screen(new CivOne.Screens.ImprovementArtScreen(artPath, "Incite Rebellion", target.Name)));
						}
						return true;
					}

					IAdvance? advance = !target.TechStolen ? GetAdvanceToSteal(target.Player) : null;

					if (advance is not null)
					{
						// Steal technology — notify if the human is involved on either side
						GameTask task = new Tasks.GetAdvance(Player, advance);
						task.Done += (s, a) =>
						{
							target.TechStolen = true;
							Game.DisbandUnit(this);
							if (target.Player == Human || Player == Human)
								GameTask.Insert(Message.Spy("Spies report:", $"{Player.TribeName} steal", $"{advance.Name}"));
						};
						GameTask.Enqueue(task);
					}
					else if (target.Player == Human)
					{
						GameTask.Enqueue(Tasks.Show.DiplomatSabotage(target, this));
						// A single sabotage is an incident; a pattern of them is a campaign,
						// and under an elected government the human cannot answer a campaign
						// without the Senate. See Game.RecordProvocation.
						if (Game.RecordProvocation(Player) && Human.RepublicDemocratic)
							GameTask.Enqueue(Tasks.Show.SenateGrievanceResponse(Player));
					}
					else
					{
						Sabotage(target);
					}

					return true;
				}
			}

			IUnit[] enemies = moveTarget.Units.Where(u => u.Owner != Owner).ToArray();

			if (enemies.Length > 0)
			{
				if (Human == Owner && enemies.Length == 1 && enemies[0] is BaseUnitLand)
					GameTask.Enqueue(Show.DiplomatBribe((enemies[0] as BaseUnitLand)!, this));
				return false;
			}

			MovementTo(relX, relY);
			return true;
		}

		internal void KeepMoving(IUnit unit) => MovementTo(unit.X - X, unit.Y - Y);
		
		private static readonly string[] _page1 =
		{
			"A DIPLOMAT can ESTABLISH an",
			"EMBASSY, INVESTIGATE a city, STEAL",
			"an ADVANCE, SABOTAGE production,",
			"INCITE a revolt or BRIBE a unit.",
			"",
			"Most of these consume the",
			"diplomat.",
		};

		private static readonly string[] _page2 =
		{
			"Requires WRITING.",
			"",
			"Inciting a city costs gold that",
			"rises with the owner's treasury",
			"and falls with distance from their",
			"capital. A city in DISORDER costs",
			"half. A CAPITAL cannot be bought",
			"at any price.",
			"",
			"A diplomat in a city may block an",
			"enemy's attempt.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Diplomat() : base(3, 0, 0, 2)
		{
			Type = UnitType.Diplomat;
			Name = "Diplomat";
			RequiredTech = new Writing();
			ObsoleteTech = null;
			SetIcon('C', 1, 0);
		}
	}
}