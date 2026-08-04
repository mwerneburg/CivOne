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
using CivOne.IO;
using CivOne.Screens;
using CivOne.Units;

namespace CivOne.Tasks
{
	internal class Orders : GameTask
	{
		private enum Order
		{
			None,
			NewCity,
			Sentry,
			Fortify,
			Road,
			Irrigate,
			Mines,
			Fortress,
			CleanPollution,
			Wait,
			Skip,
			Unload,
			Disband
		}

		private City? _city;
		private Player _player = null!;
		private IUnit? _unit = null;
		private int _x, _y;
		private Order _order;

		// Only the owner hears why an order failed. The AI routes FoundCity, BuildRoad,
		// BuildIrrigation and BuildMines through this class, and a failed order raised a
		// popup for the PLAYER regardless of whose unit it was — 178,252 of them in one
		// 750-turn game, the second-largest cost in it. Every other error site in the codebase
		// already guards on ownership; this one was missed because it is one level of
		// indirection away from the message.
		private void Error(string error)
		{
			if (_unit is not null && Human != _unit.Owner) return;
			if (_unit is null && _player is not null && _player != Human) return;
			GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText($"ERROR/{error}")));
		}
		
		private void CityManagerClosed(object sender, EventArgs args)
		{
			if (_unit is not null)
			{
				Game.DisbandUnit(_unit);
			}
			EndTask();
		}

		private void CityViewed(object sender, EventArgs args)
		{
			if (Common.HasScreenType<CityManager>()) return;

			// allowCycle=false: this screen represents a task waiting for a build decision; ← / →
			// would Destroy() and EndTask(), dropping the founding before the player decides.
			CityManager cityManager = new CityManager(_city!, viewCity: false, allowCycle: false);
			cityManager.Closed += CityManagerClosed;
			Common.AddScreen(cityManager);
		}

		private void CityFounded(object sender, EventArgs args)
		{
			// Optional founding art (data/event_art/cityfounded.png) ahead of the
			// city's first view — the founding animation the asset-free rebuild lost.
			Show? founding = Show.CityFounded(_city!);
			if (founding is not null) GameTask.Enqueue(founding);

			CityView cityView = new CityView(_city!, firstView: true);
			cityView.Closed += CityViewed;
			Common.AddScreen(cityView);
		}

		private void CityNameAccept(object sender, EventArgs args)
		{
			int nameId = (sender as CityName)!.NameId;
			Game.CityNames[nameId] = (sender as CityName)!.Value;
			CreateCity(nameId);
			EndTask();
		}

		private void CityNameCancel(object sender, EventArgs args)
		{
			Human.CityNamesSkipped++;
			_unit!.MovesLeft--;
			EndTask();
		}

		private void CreateCity(int nameId)
		{
			_city = Game.AddCity(_player, nameId, _x, _y);
			if (_city is not null)
			{
				if (_player.IsHuman)
				{
					CityManager cityManager = new CityManager(_city!, viewCity: false, allowCycle: false);
					cityManager.Closed += CityManagerClosed;
					Common.AddScreen(cityManager);
					return;
				}
				if (_unit is not null)
				{
					Game.DisbandUnit(_unit);
				}
			}
			Game.UpdateResources(_city!.Tile);
			EndTask();
		}

		private void CreateCity(Player player, int x, int y)
		{
			int nameId = Game.CityNameId(player);
			// Autopilot counts as AI here, matching City.cs:1187, :1479, :2018 and
			// Player.cs:866. Without this the human's own settlers raise a naming dialog
			// that nothing can answer in an unattended run — the one branch of the founding
			// path the AI cannot execute, on the civ the player is watching most closely.
			if (player.IsHuman && !Settings.Instance.Autopilot)
			{
				CityName cityName = new CityName(nameId, Game.CityNames[nameId]);
				cityName.Accept += CityNameAccept;
				cityName.Cancel += CityNameCancel;
				Common.AddScreen(cityName);
				return;
			}
			
			CreateCity(nameId);
		}

		private void CreateCity()
		{
			if (_unit is not null && !(_unit is Settlers) && !(_unit is HydroEngineer))
			{
				Error("SETTLERS");
				EndTask();
				return;
			}

			if (_unit is not null)
			{
				_player = Game.GetPlayer(_unit.Owner);
				_x = _unit.X;
				_y = _unit.Y;
			}

			if (Map[_x, _y].IsOcean && !(_player?.HasAdvance<AquaticColonization>() ?? false))
			{
				EndTask();
				return;
			}

			if (Map[_x, _y].City is not null)
			{
				// There is already a city here
				if (_unit is Settlers)
				{
					if (Map[_x, _y].City.Size >= 10)
					{
						// City is 10 or larger, can not join city
						Error("ADDCITY");
						EndTask();
						return;
					}
					Map[_x, _y].City.Size++;
					Game.DisbandUnit(_unit);
				}
				EndTask();
				return;
			}

			CreateCity(_player, _x, _y);
		}

		private void Irrigate()
		{
			if (!(_unit is Settlers))
			{
				Error("SETTLERS");
				EndTask();
				return;
			}
			(_unit as Settlers)!.BuildIrrigation();
			EndTask();
		}

		private void Mines()
		{
			if (!(_unit is Settlers))
			{
				Error("SETTLERS");
				EndTask();
				return;
			}
			(_unit as Settlers)!.BuildMines();
			EndTask();
		}

		private void Fortress()
		{
			if (!(_unit is Settlers))
			{
				Error("SETTLERS");
				EndTask();
				return;
			}
			if (Game.GetPlayer(_unit.Owner).HasAdvance<Construction>())
			{
				(_unit as Settlers)!.BuildFortress();
			}
			EndTask();
		}

		private void Road()
		{
			if (!(_unit is Settlers))
			{
				Error("SETTLERS");
				EndTask();
				return;
			}
			(_unit as Settlers)!.BuildRoad();
			EndTask();
		}

		private void DoCleanPollution()
		{
			if (!(_unit is Settlers))
			{
				EndTask();
				return;
			}
			(_unit as Settlers)!.CleanPollution();
			EndTask();
		}

		private void UnitWait()
		{
			Game.UnitWait();
			EndTask();
		}

		public override void Run()
		{
			switch (_order)
			{
				case Order.NewCity:
					CreateCity();
					break;
				case Order.Irrigate:
					Irrigate();
					break;
				case Order.Mines:
					Mines();
					break;
				case Order.Fortress:
					Fortress();
					break;
				case Order.Road:
					Road();
					break;
				case Order.CleanPollution:
					DoCleanPollution();
					break;
				case Order.Wait:
					UnitWait();
					break;
				default:
					EndTask();
					break;
			}
		}

		public static Orders FoundCity(IUnit? unit = null)
		{
			return new Orders()
			{
				_unit = unit,
				_order = Order.NewCity
			};
		}

		public static Orders NewCity(Player player, int x, int y)
		{
			return new Orders()
			{
				_player = player,
				_order = Order.NewCity,
				_x = x,
				_y = y
			};
		}

		public static Orders BuildIrrigation(IUnit unit)
		{
			return new Orders()
			{
				_unit = unit,
				_order = Order.Irrigate
			};
		}

		public static Orders BuildMines(IUnit unit)
		{
			return new Orders()
			{
				_unit = unit,
				_order = Order.Mines
			};
		}

		public static Orders BuildFortress(IUnit unit)
		{
			return new Orders()
			{
				_unit = unit,
				_order = Order.Fortress
			};
		}

		public static Orders BuildRoad(IUnit unit)
		{
			return new Orders()
			{
				_unit = unit,
				_order = Order.Road
			};
		}

		public static Orders CleanPollution(IUnit unit)
		{
			return new Orders()
			{
				_unit = unit,
				_order = Order.CleanPollution
			};
		}

		public static Orders Wait(IUnit unit)
		{
			return new Orders()
			{
				_unit = unit,
				_order = Order.Wait
			};
		}

		private Orders()
		{
			
		}
	}
}