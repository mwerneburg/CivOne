// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Buildings;
using CivOne.Screens;
using CivOne.Units;
using CivOne.Wonders;

namespace CivOne.Tasks
{
	internal class ImprovementBuilt : GameTask
	{
		private readonly City _city;
		private readonly IProduction? _improvement;
		private readonly string? _unitName;

		private void ClosedCityView(object sender, EventArgs args)
		{
			if (Common.HasScreenType<CityManager>())
			{
				// A CityManager was opened outside the task (e.g., player clicked a city
				// during the Turn.End countdown). The production is complete; just end
				// the task so the queue doesn't deadlock.
				EndTask();
				return;
			}

			// Quiet Build Queue: when the city's production queue already holds the
			// next item, there is no decision to make — skip the City Manager popup.
			if (Settings.Instance.QuietBuilds && _city.ProductionQueue.Count > 0)
			{
				EndTask();
				return;
			}

			// allowCycle=false: ← / → must not navigate away. Destroy() fires Closed → EndTask,
			// which would advance the news queue past the city the player is supposed to decide on.
			CityManager cityManager = new CityManager(_city, viewCity: false, allowCycle: false);
			cityManager.Closed += (s, a) => EndTask();
			Common.AddScreen(cityManager);
		}

		public override void Run()
		{
			string name = _unitName ?? (_improvement as ICivilopedia)?.Name ?? "";

			if (Human != _city.Owner)
			{
				Log($"{_city.Name} builds {name}.");

				// A rival completing a WONDER is world news — it is the one foreign build
				// worth interrupting for, and the thing you most want to know about, since
				// each wonder can only be built once. Ordinary buildings stay silent.
				if (_improvement is IWonder)
				{
					// Never the city view for a foreign city: Newspaper(city) draws the
					// place, which would hand the player a look at a city they may not have
					// discovered. The art screen shows the wonder and names its city
					// without touching the map, so it is safe either way.
					string? artPath = Game.Animations ? ImprovementArtScreen.FindArtPath(name) : null;
					IScreen screen = artPath is not null
						? new ImprovementArtScreen(artPath, name, _city.Name)
						: new Newspaper(null, [$"{name} completed", $"in {_city.Name}."], showGovernment: false);
					screen.Closed += (s, a) => EndTask();
					Common.AddScreen(screen);
					return;
				}

				EndTask();
				return;
			}

			// With animations OFF, an ordinary build is not news — it is bookkeeping, and one
			// screen per completed building across a large empire is the bulk of what makes an
			// autoplayed game unwatchable. Report it only when the player actually has
			// something to decide or something to fix:
			//
			//   nothing queued        — the city is about to idle and wants an order
			//   building a duplicate  — it has queued something it already owns, which is
			//                           either a misclick or an AI bug, and is worth seeing
			//
			// WONDERS are exempt and always announced: they can be built once in the whole
			// world, and they are the event a watcher most wants to catch.
			bool isWonder = _improvement is IWonder;
			if (!Game.Animations && !isWonder && !NeedsAnOrder())
			{
				EndTask();
				return;
			}

			IScreen cityView;
			if (_unitName is not null || !Game.Animations)
			{
				cityView = new Newspaper(_city, [$"{_city.Name} builds", $"{name}."], showGovernment: false);
			}
			else
			{
				string? artPath = ImprovementArtScreen.FindArtPath(name);
				if (artPath is not null)
				{
					cityView = new ImprovementArtScreen(artPath, name, _city.Name);
				}
				else if (_improvement is IBuilding)
				{
					cityView = new CityView(_city, production: (_improvement as IBuilding));
				}
				else if (_improvement is IWonder)
				{
					cityView = new CityView(_city, production: (_improvement as IWonder));
				}
				else
				{
					EndTask();
					return;
				}
			}
			cityView.Closed += ClosedCityView;
			Common.AddScreen(cityView);
		}

		// True when the completed build leaves the city needing the player's attention.
		private bool NeedsAnOrder()
		{
			if (_city.ProductionQueue.Count == 0) return true;
			// Queued something it already has: a duplicate building, or a wonder that exists
			// somewhere in the world. Either way the shields are about to be wasted.
			IProduction next = _city.CurrentProduction;
			if (next is IBuilding b && _city.HasBuilding(b.GetType())) return true;
			if (next is IWonder w && Game.WonderBuilt(w)) return true;
			return false;
		}

		public ImprovementBuilt(City city, IBuilding building)
		{
			_city = city;
			_improvement = building;
		}

		public ImprovementBuilt(City city, IWonder wonder)
		{
			_city = city;
			_improvement = wonder;
		}

		public ImprovementBuilt(City city, IUnit unit)
		{
			_city = city;
			_unitName = (unit as ICivilopedia)?.Name ?? unit.GetType().Name;
		}
	}
}