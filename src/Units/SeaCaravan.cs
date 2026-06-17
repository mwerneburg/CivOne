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
	// A Caravan that sails. Establishes trade routes / helps wonders just like the
	// land Caravan, but moves over ocean and into coastal cities. Renders with the
	// Sail sprite (see Graphics/Sprites/Unit.GetUnit).
	internal class SeaCaravan : BaseUnitSea, ICaravan
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
						// Human player: pause and let the user pick deliver/help-wonder/move-on.
						// The dialog performs the move itself, so return true without base.MoveTo.
						if (Game.Human == Owner)
						{
							GameTask.Enqueue(Show.CaravanChoice(this, city));
							return true;
						}
						// AI: no dialog — fall through so the city is treated as a waypoint.
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

		public SeaCaravan() : base(5, 0, 1, 3, 1)
		{
			Type = UnitType.SeaCaravan;
			Name = "Sea Caravan";
			RequiredTech = new Trade();
			ObsoleteTech = null;
			SetIcon('B', 1, 1); // Sail's unit-panel icon
		}
	}
}
