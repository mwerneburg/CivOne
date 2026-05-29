// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.UserInterface;

namespace CivOne.Units
{
	internal class HydroEngineer : BaseUnitSea
	{
		public int BuildingTube { get; internal set; }
		public int BuildingAquafarm { get; internal set; }
		public int BuildingReclaim { get; internal set; }

		public override bool Busy
		{
			get
			{
				return (base.Busy || BuildingTube > 0 || BuildingAquafarm > 0 || BuildingReclaim > 0);
			}
			set
			{
				base.Busy = value;
				BuildingTube = 0;
				BuildingAquafarm = 0;
				BuildingReclaim = 0;
			}
		}

		public bool FoundFloatingCity()
		{
			if (!Player.HasAdvance<AquaticColonization>()) return false;
			if (!Map[X, Y].IsOcean) return false;
			if (Map[X, Y].City is not null) return false;
			GameTask.Enqueue(Orders.FoundCity(this));
			return true;
		}

		public bool BuildSeaTube()
		{
			if (!Map[X, Y].IsOcean) return false;
			if (Map[X, Y].TransportTube) return false;
			if (Map[X, Y].City is not null) return false;
			BuildingTube = 3;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildSeaAquafarm()
		{
			if (!Player.HasAdvance<BioplexEngineering>()) return false;
			if (!Map[X, Y].IsOcean) return false;
			if (Game.OlvirImprovements.ContainsKey((X, Y))) return false;
			BuildingAquafarm = 4;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool ReclaimLand()
		{
			if (!Player.HasAdvance<Hydroengineering>()) return false;
			if (!Map[X, Y].IsOcean) return false;
			if (!Map[X, Y].GetBorderTiles().Any(t => !t.IsOcean)) return false;
			BuildingReclaim = 8;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public override void NewTurn()
		{
			base.NewTurn();

			if (BuildingTube > 0)
			{
				BuildingTube--;
				if (BuildingTube > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Map[X, Y].TransportTube = true; Game.InvalidateCitiesAt(X, Y); }
				return;
			}

			if (BuildingAquafarm > 0)
			{
				BuildingAquafarm--;
				if (BuildingAquafarm > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Game.OlvirImprovements[(X, Y)] = OlvirImprovementType.Aquafarm; Game.InvalidateCitiesAt(X, Y); }
				return;
			}

			if (BuildingReclaim > 0)
			{
				BuildingReclaim--;
				if (BuildingReclaim > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Map.ChangeTileType(X, Y, Terrain.Plains); Game.InvalidateCitiesAt(X, Y); }
				return;
			}
		}

		private MenuItem<int> MenuFoundCity() => MenuItem<int>
			.Create("Found Floating City")
			.SetShortcut("b")
			.OnSelect((s, a) => FoundFloatingCity());

		private MenuItem<int> MenuBuildTube() => MenuItem<int>
			.Create("Build Sea Tube")
			.SetShortcut("t")
			.OnSelect((s, a) => BuildSeaTube());

		private MenuItem<int> MenuBuildAquafarm() => MenuItem<int>
			.Create("Build Aquafarm")
			.SetShortcut("a")
			.OnSelect((s, a) => BuildSeaAquafarm());

		private MenuItem<int> MenuReclaimLand() => MenuItem<int>
			.Create("Reclaim Land")
			.SetShortcut("r")
			.OnSelect((s, a) => ReclaimLand());

		public override IEnumerable<MenuItem<int>> MenuItems
		{
			get
			{
				ITile tile = Map[X, Y];

				yield return MenuNoOrders();
				yield return MenuWait();
				yield return MenuSentry();
				yield return MenuGoTo();

				if (tile.IsOcean && tile.City is null)
				{
					yield return MenuFoundCity();
					if (!tile.TransportTube)
						yield return MenuBuildTube();
					if (Player.HasAdvance<BioplexEngineering>() && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)))
						yield return MenuBuildAquafarm();
					if (Player.HasAdvance<Hydroengineering>() && tile.GetBorderTiles().Any(t => !t.IsOcean))
						yield return MenuReclaimLand();
				}

				if (tile.City is not null)
				{
					yield return MenuHomeCity();
				}

				yield return null;
				yield return MenuDisbandUnit();
			}
		}

		public HydroEngineer() : base(4, 0, 1, 3)
		{
			Type = UnitType.HydroEngineer;
			Name = "Hydro Engineer";
			RequiredTech = new AquaticColonization();
			ObsoleteTech = null;
			SetIcon('A', 0, 0);
		}
	}
}
