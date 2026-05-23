// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.IO;
using CivOne.Screens;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.UserInterface;

namespace CivOne.Units
{
	internal class Settlers : BaseUnitLand
	{
		public override bool Busy
		{
			get
			{
				return (base.Busy || BuildingRoad > 0 || BuildingIrrigation > 0 || BuildingMine > 0 || BuildingFortress > 0 || BuildingCleanPollution > 0 || BuildingCanopyArray > 0 || BuildingAquafarm > 0);
			}
			set
			{
				base.Busy = false;
				BuildingRoad = 0;
				BuildingIrrigation = 0;
				BuildingMine = 0;
				BuildingFortress = 0;
				BuildingCleanPollution = 0;
				AutoClean = false;
				RoadTo = Point.Empty;
			}
		}
		public Point RoadTo { get; set; } = Point.Empty;
		public int BuildingRoad { get; private set; }
		private bool _buildingTube;
		public int BuildingCanopyArray { get; private set; }
		public int BuildingAquafarm { get; private set; }
		public int BuildingIrrigation { get; private set; }
		public int BuildingMine { get; private set; }
		public int BuildingFortress { get; private set; }
		public int BuildingCleanPollution { get; private set; }
		public bool AutoClean { get; private set; }

		internal bool IsTileClaimed(int tx, int ty) =>
			Game.GetUnits().OfType<Settlers>().Any(s =>
				s != this && s.Owner == Owner &&
				((!s.Goto.IsEmpty && s.Goto.X == tx && s.Goto.Y == ty) ||
				 (s.X == tx && s.Y == ty && s.Busy)));

		private ITile FindNearestCityPollution()
		{
			ITile best = null;
			int bestDist = int.MaxValue;
			foreach (ITile t in Map.AllTiles())
			{
				if (!t.Pollution) continue;
				if (IsTileClaimed(t.X, t.Y)) continue;
				bool nearCity = Game.GetCities().Any(c => c.Owner == Owner && Common.DistanceToTile(c.X, c.Y, t.X, t.Y) <= 3);
				if (!nearCity) continue;
				int d = Common.DistanceToTile(X, Y, t.X, t.Y);
				if (d < bestDist) { bestDist = d; best = t; }
			}
			return best;
		}

		protected override void MovementDone(ITile previousTile)
		{
			base.MovementDone(previousTile);
			if (AutoClean && Map[X, Y].Pollution)
				CleanPollution();
			if (!RoadTo.IsEmpty)
			{
				Log($"[Settlers.MovementDone] RoadTo=({RoadTo.X},{RoadTo.Y}) now at ({X},{Y}) ML={MovesLeft}");
				if (X == RoadTo.X && Y == RoadTo.Y)
				{
					Log("[Settlers.MovementDone] Reached destination, clearing RoadTo+Goto");
					RoadTo = Point.Empty;
					Goto = Point.Empty;
				}
				else
				{
					ITile tile = Map[X, Y];
					bool needsRoad = !tile.Road && !tile.RailRoad && tile.City is null && !tile.IsOcean;
					Log($"[Settlers.MovementDone] needsRoad={needsRoad} (road={tile.Road} rail={tile.RailRoad} city={tile.City is not null} ocean={tile.IsOcean})");
					if (needsRoad)
					{
						BuildRoad();
						Log($"[Settlers.MovementDone] After BuildRoad: Goto cleared, BuildingRoad={BuildingRoad}");
						Goto = Point.Empty;
					}
					else
					{
						Log($"[Settlers.MovementDone] No road needed, keeping Goto=({Goto.X},{Goto.Y})");
					}
				}
			}
		}

		internal void SetBuildProgress(int road, int irrigation, int mine, int fortress)
		{
			BuildingRoad = road;
			BuildingIrrigation = irrigation;
			BuildingMine = mine;
			BuildingFortress = fortress;
		}

		internal void SetStatus(bool[] bits)
		{
			BuildingRoad = (bits[1] && !bits[6] && !bits[7]) ? 2 : 0;
			BuildingIrrigation = (!bits[1] && bits[6] && !bits[7]) ? 3 : 0;
			BuildingMine = (!bits[1] && !bits[6] && bits[7]) ? 4 : 0;
			BuildingFortress = (!bits[1] && bits[6] && bits[7]) ? 5 : 0;
		}

		public bool BuildRoad()
		{
			ITile tile = Map[X, Y];
			Log($"[BuildRoad] ({X},{Y}) road={tile.Road} rail={tile.RailRoad} tube={tile.TransportTube} ocean={tile.IsOcean} city={tile.City is not null} ML={MovesLeft}");

			if (tile.TransportTube) { Log("[BuildRoad] -> false (tube exists)"); return false; }

			if (tile.RailRoad && Game.CurrentPlayer.HasAdvance<TransitConduit>() && tile.City is null)
			{
				_buildingTube = true;
				BuildingRoad = 3;
				MovesLeft = 0; PartMoves = 0;
				Log("[BuildRoad] -> true (tube building started)");
				return true;
			}

			if (tile.RailRoad) { Log("[BuildRoad] -> false (railroad exists)"); return false; }

			if (!tile.IsOcean && !tile.Road && tile.City is null)
			{
				if ((tile is River) && !Game.CurrentPlayer.HasAdvance<BridgeBuilding>()) { Log("[BuildRoad] -> false (river, no BridgeBuilding)"); return false; }
				if (tile is Plains || tile is Grassland) tile.Road = true;
				else BuildingRoad = 1;
				MovesLeft = 0; PartMoves = 0;
				Log($"[BuildRoad] -> true (road/instant, BuildingRoad={BuildingRoad})");
				return true;
			}
			else if (Game.CurrentPlayer.HasAdvance<RailRoad>() && !tile.IsOcean && tile.Road && tile.City is null)
			{
				BuildingRoad = 2;
				MovesLeft = 0; PartMoves = 0;
				Log("[BuildRoad] -> true (railroad building started)");
				return true;
			}
			Log("[BuildRoad] -> false (no applicable case)");
			return false;
		}

		public bool BuildCanopyArray()
		{
			if (!Game.CurrentPlayer.HasAdvance<CanopyCultivation>()) return false;
			ITile tile = Map[X, Y];
			if (!(tile is Forest || tile is Jungle)) return false;
			if (Game.OlvirImprovements.ContainsKey((tile.X, tile.Y))) return false;
			BuildingCanopyArray = 4;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildAquafarm()
		{
			if (!Game.CurrentPlayer.HasAdvance<BioplexEngineering>()) return false;
			ITile tile = Map[X, Y];
			bool coastal = !tile.IsOcean && tile.GetBorderTiles().Any(t => t.IsOcean);
			bool onOcean = tile.IsOcean;
			if (!coastal && !onOcean) return false;
			if (Game.OlvirImprovements.ContainsKey((tile.X, tile.Y))) return false;
			BuildingAquafarm = 4;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildIrrigation()
		{
			ITile tile = Map[X, Y];
			if (tile.Irrigation)
			{
				// Tile already irrigated, ignore
				return false;
			}

			if ((tile is Forest) || (tile is Jungle) || (tile is Swamp))
			{
				BuildingIrrigation = 4;
				MovesLeft = 0;
				PartMoves = 0;
				return true;
			}
			else if ((tile.GetBorderTiles().Any(t => (t.X == X || t.Y == Y) && (t.City is null)
				&& (t.Irrigation || (t is River) || (t is Swamp)
				    || (t.IsOcean && Map.Instance.IsFreshwaterAt(t.X, t.Y))))) || (tile is River))
			{
				if (!tile.IsOcean && !(tile.Irrigation) && ((tile is Desert) || (tile is Grassland) || (tile is Hills) || (tile is Plains) || (tile is River)))
				{
					BuildingIrrigation = (tile is Plains || tile is Grassland) ? 2 : 3;
					MovesLeft = 0;
					PartMoves = 0;
					return true;
				}
				if (Human == Owner)
					GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText("ERROR/NOIRR")));
				return false;
			}
			else
			{
				if (((tile is Desert) || (tile is Grassland) || (tile is Hills) || (tile is Plains) || (tile is River)) && tile.City is null)
				{
					if (Human == Owner)
						GameTask.Enqueue(Message.Error("-- Civilization Note --",
						"This tile has no water source.",
						"Needs a neighboring river, lake,",
						"swamp, or irrigated tile."));
					return true;
				}
				if (Human == Owner)
					GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText("ERROR/NOIRR")));
			}
			return false;
		}

		public bool BuildMines()
		{
			ITile tile = Map[X, Y];
			if (!tile.IsOcean && !(tile.Mine) && ((tile is Desert) || (tile is Hills) || (tile is Mountains) || (tile is Jungle) || (tile is Grassland) || (tile is Plains) || (tile is Swamp)))
			{
				BuildingMine = 4;
				MovesLeft = 0;
				PartMoves = 0;
				return true;
			}
			return false;
		}

		public bool BuildFortress()
		{
			if (!Game.CurrentPlayer.HasAdvance<Construction>())
				return false;

			ITile tile = Map[X, Y];
			if (!tile.IsOcean && !(tile.Fortress) && tile.City is null)
			{
				BuildingFortress = 5;
				MovesLeft = 0;
				PartMoves = 0;
				return true;
			}
			return false;
		}

		public bool CleanPollution()
		{
			ITile tile = Map[X, Y];
			if (!tile.Pollution) return false;
			BuildingCleanPollution = 2;
			MovesLeft = 0;
			PartMoves = 0;
			return true;
		}

		public override void NewTurn()
		{
			base.NewTurn();
			if (Map[X, Y].IsOcean)
			{
				BuildingRoad = BuildingIrrigation = BuildingMine = BuildingFortress = 0;
				BuildingCanopyArray = BuildingAquafarm = 0;
				_buildingTube = false;
				return;
			}
			if (BuildingRoad > 0)
			{
				BuildingRoad--;
				if (_buildingTube)
				{
					if (BuildingRoad == 0) { Map[X, Y].TransportTube = true; _buildingTube = false; }
					else { MovesLeft = 0; PartMoves = 0; }
				}
				else
				{
					if (Map[X, Y].Road)
					{
						if (Human.HasAdvance<RailRoad>())
							Map[X, Y].RailRoad = true;
						else if (BuildingRoad > 0)
							foreach (Settlers settlers in Map[X, Y].Units.Where(u => (u is Settlers) && (u as Settlers).BuildingRoad > 0).Select(u => (u as Settlers)))
								settlers.BuildingRoad = 0;
					}
					Map[X, Y].Road = true;
					if (BuildingRoad > 0) { MovesLeft = 0; PartMoves = 0; }
				}
			}
			else if (BuildingIrrigation > 0)
			{
				BuildingIrrigation--;
				if (BuildingIrrigation > 0)
				{
					MovesLeft = 0;
					PartMoves = 0;
				}
				else if (Map[X, Y] is Forest)
				{
					Map[X, Y].Irrigation = false;
					Map[X, Y].Mine = false;
					Map.ChangeTileType(X, Y, Terrain.Plains);
				}
				else if ((Map[X, Y] is Jungle) || (Map[X, Y] is Swamp))
				{
					Map[X, Y].Irrigation = false;
					Map[X, Y].Mine = false;
					Map.ChangeTileType(X, Y, Terrain.Grassland1);
				}
				else
				{
					Map[X, Y].Irrigation = true;
					Map[X, Y].Mine = false;
				}
			}
			else if (BuildingMine > 0)
			{
				BuildingMine--;
				if (BuildingMine > 0)
				{
					MovesLeft = 0;
					PartMoves = 0;
				}
				else if ((Map[X, Y] is Jungle) || (Map[X, Y] is Grassland) || (Map[X, Y] is Plains) || (Map[X, Y] is Swamp))
				{
					Map[X, Y].Irrigation = false;
					Map[X, Y].Mine = false;
					Map.ChangeTileType(X, Y, Terrain.Forest);
				}
				else
				{
					Map[X, Y].Irrigation = false;
					Map[X, Y].Mine = true;
				}
			}
			else if (BuildingFortress > 0)
			{
				BuildingFortress--;
				if (BuildingFortress > 0)
				{
					MovesLeft = 0;
					PartMoves = 0;
				}
				else
				{
					Map[X, Y].Fortress = true;
				}
			}
			else if (BuildingCleanPollution > 0)
			{
				BuildingCleanPollution--;
				if (BuildingCleanPollution > 0) { MovesLeft = 0; PartMoves = 0; }
				else Map[X, Y].Pollution = false;
			}
			else if (BuildingCanopyArray > 0)
			{
				BuildingCanopyArray--;
				if (BuildingCanopyArray > 0) { MovesLeft = 0; PartMoves = 0; }
				else Game.OlvirImprovements[(X, Y)] = OlvirImprovementType.CanopyArray;
			}
			else if (BuildingAquafarm > 0)
			{
				BuildingAquafarm--;
				if (BuildingAquafarm > 0) { MovesLeft = 0; PartMoves = 0; }
				else Game.OlvirImprovements[(X, Y)] = OlvirImprovementType.Aquafarm;
			}

			if (AutoClean && BuildingRoad == 0 && BuildingIrrigation == 0 && BuildingMine == 0 && BuildingFortress == 0 && BuildingCleanPollution == 0 && BuildingCanopyArray == 0 && BuildingAquafarm == 0)
			{
				if (Map[X, Y].Pollution && Game.GetCities().Any(c => c.Owner == Owner && Common.DistanceToTile(c.X, c.Y, X, Y) <= 3))
				{
					CleanPollution();
				}
				else
				{
					ITile target = FindNearestCityPollution();
					if (target is not null)
						Goto = new Point(target.X, target.Y);
					else
						AutoClean = false;
				}
			}

			if (!RoadTo.IsEmpty && BuildingRoad == 0 && BuildingIrrigation == 0 && BuildingMine == 0 && BuildingFortress == 0 && BuildingCleanPollution == 0 && BuildingCanopyArray == 0 && BuildingAquafarm == 0)
			{
				if (X == RoadTo.X && Y == RoadTo.Y)
				{
					RoadTo = Point.Empty;
				}
				else
				{
					ITile tile = Map[X, Y];
					bool needsRoad = !tile.Road && !tile.RailRoad && tile.City is null && !tile.IsOcean;
					if (needsRoad)
						BuildRoad();
					else
						Goto = new Point(RoadTo.X, RoadTo.Y);
				}
			}
		}

		private MenuItem<int> MenuFoundCity() => MenuItem<int>
			.Create((Map[X, Y].City is null) ? "Found New City" : "Add to City")
			.SetShortcut("b")
			.OnSelect((s, a) => GameTask.Enqueue(Orders.FoundCity(this)));

		private MenuItem<int> MenuBuildRoad()
		{
			ITile t = Map[X, Y];
			string label = (t.RailRoad && Human.HasAdvance<TransitConduit>()) ? "Build Transport Tube"
			             : t.Road ? "Build RailRoad"
			             : "Build Road";
			return MenuItem<int>.Create(label).SetShortcut("r").OnSelect((s, a) => BuildRoad());
		}

		private MenuItem<int> MenuBuildCanopyArray() => MenuItem<int>
			.Create("Build Canopy Array")
			.SetShortcut("q")
			.OnSelect((s, a) => BuildCanopyArray());

		private MenuItem<int> MenuBuildAquafarm() => MenuItem<int>
			.Create("Build Aquafarm")
			.SetShortcut("a")
			.OnSelect((s, a) => BuildAquafarm());

		private MenuItem<int> MenuBuildRoadTo() => MenuItem<int>
			.Create("Build Road To...")
			.SetShortcut("t")
			.OnSelect((s, a) => GameTask.Enqueue(Show.RoadTo));

		private MenuItem<int> MenuBuildIrrigation() => MenuItem<int>
			.Create((Map[X, Y] is Forest) ? "Change to Plains" :
					((Map[X, Y] is Jungle) || (Map[X, Y] is Swamp)) ? "Change to Grassland" :
					"Build Irrigation")
			.SetShortcut("i")
			.SetEnabled(Map[X, Y].AllowIrrigation() || Map[X, Y].AllowChangeTerrain())
			.OnSelect((s, a) => GameTask.Enqueue(Orders.BuildIrrigation(this)));

		private MenuItem<int> MenuBuildMines() => MenuItem<int>
			.Create(((Map[X, Y] is Jungle) || (Map[X, Y] is Grassland) || (Map[X, Y] is Plains) || (Map[X, Y] is Swamp)) ?
					"Change to Forest" : "Build Mines")
			.SetShortcut("m")
			.OnSelect((s, a) => GameTask.Enqueue(Orders.BuildMines(this)));

		private MenuItem<int> MenuBuildFortress() => MenuItem<int>
			.Create("Build fortress")
			.SetShortcut("f")
			.SetEnabled(Game.CurrentPlayer.HasAdvance<Construction>())
			.OnSelect((s, a) => GameTask.Enqueue(Orders.BuildFortress(this)));

		private MenuItem<int> MenuCleanPollution() => MenuItem<int>
			.Create("Clean Pollution")
			.SetShortcut("p")
			.OnSelect((s, a) => GameTask.Enqueue(Orders.CleanPollution(this)));

		private MenuItem<int> MenuAutoCleanPollution() => MenuItem<int>
			.Create("Auto-Clean Pollution")
			.SetShortcut("c")
			.OnSelect((s, a) =>
			{
				AutoClean = true;
				Goto = Point.Empty;
				ITile target = FindNearestCityPollution();
				if (target is not null)
				{
					if (target.X == X && target.Y == Y)
						CleanPollution();
					else
						Goto = new Point(target.X, target.Y);
				}
				else
				{
					AutoClean = false;
				}
			});
		
		public override IEnumerable<MenuItem<int>> MenuItems
		{
			get
			{
				ITile tile = Map[X, Y];

				yield return MenuNoOrders();
				if (!tile.IsOcean || Human.HasAdvance<AquaticColonization>())
					yield return MenuFoundCity();
				{
					bool noInfra     = !tile.Road && !tile.RailRoad && !tile.TransportTube;
					bool canRailroad = tile.Road  && Human.HasAdvance<RailRoad>()       && !tile.RailRoad && !tile.TransportTube;
					bool canTube     = tile.RailRoad && Human.HasAdvance<TransitConduit>() && !tile.TransportTube;
					if (!tile.IsOcean && (noInfra || canRailroad || canTube))
						yield return MenuBuildRoad();
				}
				if (!tile.IsOcean && !tile.TransportTube)
					yield return MenuBuildRoadTo();
				if (!tile.Irrigation && ((tile is Desert) || (tile is Grassland) || (tile is Hills) || (tile is Plains) || (tile is River) || (tile is Forest) || (tile is Jungle) || (tile is Swamp)))
					yield return MenuBuildIrrigation();
				if (!tile.Mine && ((tile is Desert) || (tile is Hills) || (tile is Mountains) || (tile is Jungle) || (tile is Grassland) || (tile is Plains) || (tile is Swamp)))
					yield return MenuBuildMines();
				if (!tile.IsOcean && !tile.Fortress)
					yield return MenuBuildFortress();
				if (Human.HasAdvance<CanopyCultivation>() && (tile is Forest || tile is Jungle) && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)))
					yield return MenuBuildCanopyArray();
				if (Human.HasAdvance<BioplexEngineering>() && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)) && (!tile.IsOcean && tile.GetBorderTiles().Any(t => t.IsOcean) || tile.IsOcean))
					yield return MenuBuildAquafarm();
				if (tile.Pollution)
				{
					yield return MenuCleanPollution();
				}
				if (!AutoClean && FindNearestCityPollution() is not null)
				{
					yield return MenuAutoCleanPollution();
				}
				//
				yield return MenuWait();
				yield return MenuSentry();
				yield return MenuGoTo();
				if (tile.Irrigation || tile.Mine || tile.Road || tile.RailRoad || tile.TransportTube)
				{
					yield return MenuPillage();
				}
				if (tile.City is not null)
				{
					yield return MenuHomeCity();
				}
				yield return null;
				yield return MenuDisbandUnit();
			}
		}

		public Settlers() : base(4, 0, 1, 1)
		{
			Type = UnitType.Settlers;
			Name = "Settlers";
			RequiredTech = null;
			ObsoleteTech = null;
			SetIcon('D', 1, 1);
		}
	}
}