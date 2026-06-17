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
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Wonders;

namespace CivOne.Units
{
	internal class Caravan : BaseUnitLand, ICaravan
	{
		public void KeepMoving(City city) => MovementTo(city.X - X, city.Y - Y);

		public void EstablishTradeRoute(City city) => CaravanActions.EstablishTradeRoute(this, city);

		public void HelpBuildWonder(City city) => CaravanActions.HelpBuildWonder(this, city);

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
				else if (Game.Human == Owner && CaravanActions.HasUnbuiltDomeAssignment(city.Owner))
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
