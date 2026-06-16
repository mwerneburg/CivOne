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
	// Trade behaviour shared by Caravan (land) and SeaCaravan (sea). These units
	// can't share a base class, so the common logic lives here.
	internal static class CaravanActions
	{
		private static readonly string[] WARES = ["Silk", "Silver", "Wine", "Copper", "Gems", "Dye", "Salt", "Spice"];

		public static int TradeGoldBonus(IUnit unit, City targetCity)
		{
			// This formula is not confirmed... but it seems close enough

			if (targetCity is null) return 0; // this should not happen

			// set default values if home city is NONE
			int distance = Common.DistanceToTile(-1, -1, targetCity.X, targetCity.Y);
			int tradeHome = 12;

			if (unit.Home is not null)
			{
				distance = unit.Home.Tile.DistanceTo(targetCity);
				tradeHome = unit.Home.TradeTotal;
			}

			int tradeTarget = targetCity.TradeTotal;

			float multiplier = 1;
			if (unit.Home is not null && unit.Home.Tile.ContinentId == targetCity.Tile.ContinentId) multiplier *= 0.5F;
			if (unit.Owner == targetCity.Owner) multiplier *= 0.5F;
			if (Game.Instance.GetPlayer(unit.Owner).HasAdvance<RailRoad>() && Game.Instance.GetPlayer(targetCity.Owner).HasAdvance<RailRoad>()) multiplier *= 0.66F;
			if (Game.Instance.GetPlayer(unit.Owner).HasAdvance<Flight>() && Game.Instance.GetPlayer(targetCity.Owner).HasAdvance<Flight>()) multiplier *= 0.66F;

			return (int)(multiplier * (float)((distance + 10) * (tradeHome + tradeTarget) / 24));
		}

		public static bool HasUnbuiltDomeAssignment(int ownerIndex)
		{
			var player = Game.Instance.GetPlayer((byte)ownerIndex);
			if (player is null) return false;
			return Game.Instance.GetDomeAssignments(player)
				.Any(w => !Game.Instance.WonderBuilt(
					Game.DomeFiveComponents.First(c => (Wonder)c.Id == w)));
		}

		public static void EstablishTradeRoute(IUnit unit, City city)
		{
			string homeName = unit.Home?.Name ?? "NONE";
			string ware = WARES[Common.Random.Next(8)];
			int revenue = TradeGoldBonus(unit, city);
			if (revenue <= 0) revenue = 1;

			unit.Home?.AddTradeRoute(city, ware);
			if (unit.Home is not null)
				city.AddTradeRoute(unit.Home, ware);

			GameTask.Insert(Message.General(
				$"{ware} caravan from {homeName}",
				$"arrives in {city.Name}",
				"Trade route established",
				$"Revenue: ${revenue}."));
			Game.Instance.GetPlayer(unit.Owner).Gold += (short)revenue;
			Game.Instance.DisbandUnit(unit);
		}

		public static void HelpBuildWonder(IUnit unit, City city)
		{
			city.Shields += 50;
			Game.Instance.DisbandUnit(unit);
		}
	}
}
