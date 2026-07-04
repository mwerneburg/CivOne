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