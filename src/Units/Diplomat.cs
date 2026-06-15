#nullable enable
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
		public static bool CanIncite(City cityToIncice, short gold)
		{
			return gold >= InciteCost(cityToIncice) && !cityToIncice.HasBuilding<Palace>();
		}

		public static int InciteCost(City cityToIncite)
		{
			City capital = cityToIncite.Player.Cities.Where(c => c.HasBuilding(new Palace())).FirstOrDefault();

			int distance = capital is null ? 16 : cityToIncite.Tile.DistanceTo(capital);
			
			int cost = (cityToIncite.Player.Gold + 1000) / (distance + 3);

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

		public string Sabotage(City city)
		{
			Game.DisbandUnit(this);

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
				if (Human == Owner)
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
						// Human's city was incited away — show the rebellion art.
						if (Game.Animations && Human == oldOwnerPlayer)
						{
							string? artPath = CivOne.Screens.ImprovementArtScreen.FindArtPath("Incite Rebellion", "event_art");
							if (artPath is not null)
								GameTask.Insert(Show.Screen(new CivOne.Screens.ImprovementArtScreen(artPath, "Incite Rebellion", target.Name)));
						}
						if (target.Player == Human || Player == Human)
							GameTask.Insert(Message.Spy("Spies report:", $"{Player.TribeName} incite", $"revolt in {target.Name}!"));
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
					GameTask.Enqueue(Show.DiplomatBribe(enemies[0] as BaseUnitLand, this));
				return false;
			}

			MovementTo(relX, relY);
			return true;
		}

		internal void KeepMoving(IUnit unit) => MovementTo(unit.X - X, unit.Y - Y);
		
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