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
using CivOne.Enums;
using CivOne.Governments;
using CivOne.Screens;
using CivOne.Screens.Dialogs;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Tasks
{
	internal class Show : GameTask
	{
		private readonly IScreen _screen;

		public void Closed(object sender, EventArgs args) => EndTask();

		public override void Run()
		{
			_screen.Closed += Closed;
			Common.AddScreen(_screen);
		}

		internal static void DropAllWeLovePresidentDay()
		{
			RemoveQueued(t => t is Show s && s._screen is WeLovePresidentDayScreen);
		}

		public static Show Empty => new Show(Overlay.Empty);

		public static Show InterfaceHelp => new Show(Overlay.InterfaceHelp);

		public static Show Terrain
		{
			get
			{
				GamePlay gamePlay = (GamePlay)Common.Screens.First(s => (s is GamePlay));
				return new Show(Overlay.TerrainView(gamePlay.X, gamePlay.Y));
			}
		}

		private static Point NearestLandTile(int x, int y)
		{
			// Spiral outward from (x,y) until we find a non-ocean, non-arctic tile.
			for (int radius = 1; radius < Map.WIDTH / 2; radius++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				for (int dx = -radius; dx <= radius; dx++)
				{
					if (Math.Abs(dx) != radius && Math.Abs(dy) != radius) continue;
					ITile t = Map[(x + dx + Map.WIDTH) % Map.WIDTH, y + dy];
					if (t is null || t.IsOcean || t.Type == CivOne.Enums.Terrain.Arctic) continue;
					return new Point(t.X, t.Y);
				}
			}
			return new Point(x, y);
		}

		public static Show Goto
		{
			get
			{
				GamePlay gamePlay = (GamePlay)Common.Screens.First(s => (s is GamePlay));
				Goto gotoScreen = new Goto(gamePlay.X, gamePlay.Y);
				gotoScreen.Closed += (s, a) =>
				{
					if (Human != Game.CurrentPlayer) return;
					if (Game.ActiveUnit is null) return;
					if (gotoScreen.X == -1 || gotoScreen.Y == -1) return;
					int gx = gotoScreen.X, gy = gotoScreen.Y;
					if (Game.ActiveUnit.Class == UnitClass.Land && Map[gx, gy] is not null && Map[gx, gy].IsOcean)
					{
						Point land = NearestLandTile(gx, gy);
						gx = land.X;
						gy = land.Y;
					}
					Game.ActiveUnit.Goto = new Point(gx, gy);
				};
				return new Show(gotoScreen);
			}
		}

		public static Show TaxRate => new Show(SetRate.Taxes);

		public static Show LuxuryRate => new Show(SetRate.Luxuries);

		public static Show CityManager(City city) => new Show(new CityManager(city));

		public static Show ViewCity(City city) => new Show(new CityManager(city, true));

		public static Show UnitStack(int x, int y) => new Show(new UnitStack(x, y));

		public static Show Search
		{
			get
			{
				Search search = new Search();
				search.Accept += (s, a) =>
				{
					City city = (s as Search).City;
					if (city is null) return;
					GamePlay gamePlay = (GamePlay)Common.Screens.First(x => x.GetType() == typeof(GamePlay));
					gamePlay.CenterOnPoint(city.X, city.Y);
				};
				return new Show(search);
			}
		}

		public static Show ChooseGovernment
		{
			get
			{
				ChooseGovernment chooseGovernment = new ChooseGovernment();
				chooseGovernment.Closed += (s, a) => {
					IGovernment result = (s as ChooseGovernment).Result;
					if (result is null) return;
					Human.Government = result;
					GameTask.Insert(Message.NewGoverment(null, $"{Human.TribeName} government", $"changed to {Human.Government.Name}!"));
				};
				return new Show(chooseGovernment);
			}
		}

		public static Show Nuke(int x, int y) => new Show(new Nuke(x, y));

		public static Show DestroyUnit(IUnit unit, bool stack) => new Show(new DestroyUnit(unit, stack));

		public static Show CaptureCity(City city) => new Show(CityView.Capture(city));

		public static Show DisorderCity(City city) => new Show(CityView.Disorder(city));

		public static Show WeLovePresidentDayCity(City city) => new Show(new WeLovePresidentDayScreen(city));

		public static Show CaravanChoice(Caravan unit, City city) => new Show(new CaravanChoice(unit, city));

		public static Show DiplomatBribe(BaseUnitLand unitToBribe, Diplomat diplomat) => new Show(new DiplomatBribe(unitToBribe, diplomat));

		public static Show DiplomatCity(City enemyCity, Diplomat diplomat) => new Show(new DiplomatCity(enemyCity, diplomat));

		public static Show DiplomatIncite(City enemyCity, Diplomat diplomat) => new Show(new DiplomatIncite(enemyCity, diplomat));

		public static Show DiplomatSabotage(City enemyCity, Diplomat diplomat) => new Show(new DiplomatSabotage(enemyCity, diplomat));
		
		public static Show SelectAdvanceAfterCityCapture(Player player, IList<IAdvance> advances) => new Show(new SelectAdvanceAfterCityCapture(player, advances));

		public static Show MeetKing(Player player, bool aiInitiated = false) => new Show(new King(player, aiInitiated));

		public static Show Screen<T>() where T : IScreen, new() => new Show(new T());

		public static Show Screen(Type type)
		{
			if (!typeof(IScreen).IsAssignableFrom(type)) return null;
			return new Show((IScreen)Activator.CreateInstance(type));
		}

		public static Show Screens(IEnumerable<Type> types)
		{
			Queue<Type> screenTypeQueue = new Queue<Type>(types.Where(x => typeof(IScreen).IsAssignableFrom(x)));
			if (screenTypeQueue.Count == 0) return null;
			Func<Show> nextTask = null;
			nextTask = () =>
			{
				if (screenTypeQueue.Count == 0) return null;
				Show showScreen = Show.Screen(screenTypeQueue.Dequeue());
				showScreen.Done += (s, a) => GameTask.Insert(nextTask());
				return showScreen;
			};
			return nextTask();
		}

		public static Show Screens(params Type[] types) => Screens(types.ToList());

		public static Show Screen(IScreen screen) => new Show(screen);

		private Show(IScreen screen)
		{
			_screen = screen;
		}
	}
}