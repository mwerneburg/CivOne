#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Wonders;

namespace CivOne.Units
{
	internal class Caravan : BaseUnitLand
	{
		private static string[] WARES = ["Silk", "Silver", "Wine", "Copper", "Gems", "Dye", "Salt", "Spice"];

		private int TradeGoldBonus(City targetCity)
		{
			// This formula is not confirmed... but it seems close enough

			if (targetCity is null) return 0; // this should not happen

			// set default values if home city is NONE
			int distance = Common.DistanceToTile(-1, -1, targetCity.X, targetCity.Y);
			int tradeHome = 12;

			if (Home is not null)
			{
				distance = Home.Tile.DistanceTo(targetCity);
				tradeHome = Home.TradeTotal;
			}

			int tradeTarget = targetCity.TradeTotal;

			float multiplier = 1;
			if (Home is not null && Home.Tile.ContinentId == targetCity.Tile.ContinentId) multiplier *= 0.5F;
			if (Owner == targetCity.Owner) multiplier *= 0.5F;
			if (Game.GetPlayer(Owner).HasAdvance<RailRoad>() && Game.GetPlayer(targetCity.Owner).HasAdvance<RailRoad>()) multiplier *= 0.66F;
			if (Game.GetPlayer(Owner).HasAdvance<Flight>() && Game.GetPlayer(targetCity.Owner).HasAdvance<Flight>()) multiplier *= 0.66F;

			return (int)(multiplier * (float)((distance + 10) * (tradeHome + tradeTarget) / 24));
		}

		private static bool HasUnbuiltDomeAssignment(int ownerIndex)
		{
			var player = Game.GetPlayer((byte)ownerIndex);
			if (player is null) return false;
			return Game.Instance.GetDomeAssignments(player)
				.Any(w => !Game.Instance.WonderBuilt(
					Game.DomeFiveComponents.First(c => (Wonder)c.Id == w)));
		}

		internal void KeepMoving(City city) => MovementTo(city.X - X, city.Y - Y);

		internal void EstablishTradeRoute(City city)
		{
			string homeName = Home?.Name ?? "NONE";
			string ware = WARES[Common.Random.Next(8)];
			int revenue = TradeGoldBonus(city);
			if (revenue <= 0) revenue = 1;

			Home?.AddTradeRoute(city, ware);
			city.AddTradeRoute(Home, ware);

			GameTask.Insert(Message.General(
				$"{ware} caravan from {homeName}",
				$"arrives in {city.Name}",
				"Trade route established",
				$"Revenue: ${revenue}."));
			Game.GetPlayer(Owner).Gold += (short)revenue;
			Game.DisbandUnit(this);
		}

		internal void HelpBuildWonder(City city)
		{
			city.Shields += 50;
			Game.DisbandUnit(this);
		}

		public override bool MoveTo(int relX, int relY)
		{
			ITile moveTarget = Map[X, Y][relX, relY];
			if (moveTarget is null) return false;

			City city = moveTarget.City;
			if (city is not null && city != Home)
			{
				if (city.Owner == Owner)
				{
					bool tooClose = Home is not null && moveTarget.DistanceTo(Home) < 10;
					bool buildingWonder = city.CurrentProduction is IWonder;
					if (!tooClose || buildingWonder)
					{
						// Human player: pause and let the user pick deliver/help-wonder/move-on
						// via the CaravanChoice dialog. The dialog handles the move itself, so
						// returning true here without invoking base.MoveTo is correct for humans.
						if (Game.Human == Owner)
						{
							GameTask.Enqueue(Show.CaravanChoice(this, city));
							return true;
						}
						// AI player: no dialog exists. Previously this also returned true,
						// which the AI loop interpreted as "move succeeded" — but the unit
						// hadn't actually moved, so it stuck on the same tile turn after turn
						// until the circuit breaker fired. Fall through to base.MoveTo so the
						// AI Caravan treats its own city as a normal waypoint en route to a
						// foreign trade target.
					}
				}
				else if (Game.Human == Owner && HasUnbuiltDomeAssignment(city.Owner))
				{
					GameTask.Enqueue(Show.CaravanChoice(this, city));
					return true;
				}
			}

			return base.MoveTo(relX, relY);
		}

		protected override bool Confront(int relX, int relY)
		{
			ITile moveTarget = Map[X, Y][relX, relY];
			if (moveTarget is null) return false;
			City city = moveTarget.City;

			// No city to trade with — caravan has no attack, refuse the move.
			if (city is null) return false;

			// Foreign city: deliver the caravan as a trade route.
			if (city.Owner != Owner)
			{
				EstablishTradeRoute(city);
				return true;
			}

			return true;
		}

		public Caravan() : base(5, 0, 1, 2)
		{
			Type = UnitType.Caravan;
			Name = "Caravan";
			RequiredTech = new Trade();
			ObsoleteTech = null;
			SetIcon('E', 0, 1);
		}
	}
}