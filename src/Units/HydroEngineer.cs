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
		private static readonly string[] _page1 =
		{
			"The HYDRO ENGINEER is the ocean's",
			"answer to the Settler. Built only",
			"in coastal cities, it operates on",
			"open water and lays the foundation",
			"for life beyond the shoreline.",
			"",
			"It can FOUND a floating city on",
			"ocean, lay TRANSPORT TUBES across",
			"the sea bed, build an AQUAFARM",
			"on a worked ocean tile, and",
			"RECLAIM a coastal tile, turning",
			"it into PLAINS adjoining land.",
		};

		private static readonly string[] _page2 =
		{
			"Reclaiming ocean to land can",
			"sever an inland bay from the open",
			"sea — the enclosed water is then",
			"reclassified as a freshwater lake,",
			"enabling irrigation and rivers.",
			"",
			"Transport tubes function like rail",
			"on the sea: connected tube tiles",
			"and floating cities form a free-",
			"movement corridor for both land",
			"and sea units.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

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
				else
				{
					Map.ChangeTileType(X, Y, Terrain.Plains);
					Game.InvalidateCitiesAt(X, Y);
					// Reclaim is the only in-game op that converts Ocean→land. If this tile
					// (or a chain of them) severs a bay from the global ocean, the cut-off
					// region becomes a freshwater lake — but only after we rerun the flood-fill.
					Map.Instance.ComputeFreshwaterLakes();
				}
				return;
			}
		}

		private MenuItem<int> MenuFoundCity() => MenuItem<int>
			.Create("Found Floating City")
			.SetShortcut("b")
			.OnSelect((s, a) => FoundFloatingCity());

		// 'r', not 't'. The tube is the sea's road, so it takes the key a player already
		// builds roads and rail with on land — and 't' never worked anyway: GameMap.KeyDown
		// claims 'T' for the terrain view and returns before the active unit's menu is ever
		// consulted, so this order was reachable by mouse alone.
		private MenuItem<int> MenuBuildTube() => MenuItem<int>
			.Create("Build Sea Tube")
			.SetShortcut("r")
			.OnSelect((s, a) => BuildSeaTube());

		private MenuItem<int> MenuBuildAquafarm() => MenuItem<int>
			.Create("Build Aquafarm")
			.SetShortcut("a")
			.OnSelect((s, a) => BuildSeaAquafarm());

		// Moved off 'r' to make room for the tube. 'l' for Land, and it costs nothing: every
		// letter is already spoken for at the map layer, but shortcuts only need to be unique
		// within ONE unit's menu — GameMap forwards 'L' to whatever the active unit calls 'l',
		// which is "Lower to Plains" on a Settlers and this on a Hydro Engineer.
		//
		// 'r' did not work here either. GameMap's case 'R' went straight to Orders.BuildRoad,
		// whose Road() refuses anything that is not a Settlers and pops a "SETTLERS" error, so
		// pressing it on a Hydro Engineer produced a complaint rather than an order.
		private MenuItem<int> MenuReclaimLand() => MenuItem<int>
			.Create("Reclaim Land")
			.SetShortcut("l")
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

				yield return null!; // menu separator
				yield return MenuDisbandUnit();
			}
		}

		public HydroEngineer() : base(4, 0, 1, 6)
		{
			Type = UnitType.HydroEngineer;
			Name = "Hydro Engineer";
			RequiredTech = new AquaticColonization();
			ObsoleteTech = null;
			SetIcon('A', 0, 0);
		}
	}
}
