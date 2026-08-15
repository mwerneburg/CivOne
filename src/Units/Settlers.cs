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
				return (base.Busy || BuildingRoad > 0 || BuildingIrrigation > 0 || BuildingMine > 0 || BuildingFortress > 0 || BuildingCleanPollution > 0 || BuildingCanopyArray > 0 || BuildingAquafarm > 0 || BuildingCamp > 0
					|| BuildingLowerTerrain > 0 || BuildingRaiseTerrain > 0 || BuildingPlantForest > 0 || BuildingPlantJungle > 0 || BuildingThawTundra > 0 || BuildingAddRiver > 0
					|| BuildingTerrace > 0 || BuildingMoistureFarm > 0);
			}
			set
			{
				base.Busy = false;
				BuildingRoad = 0;
				BuildingIrrigation = 0;
				BuildingMine = 0;
				BuildingFortress = 0;
				BuildingCleanPollution = 0;
				BuildingCanopyArray = 0;
				BuildingAquafarm = 0;
				BuildingCamp = 0;
				BuildingLowerTerrain = 0;
				BuildingRaiseTerrain = 0;
				BuildingPlantForest = 0;
				BuildingPlantJungle = 0;
				BuildingThawTundra = 0;
				BuildingAddRiver = 0;
				BuildingTerrace = 0;
				BuildingMoistureFarm = 0;
				AutoClean = false;
				AutoImprove = false;
				RoadTo = Point.Empty;
			}
		}
		public Point RoadTo { get; set; } = Point.Empty;
		public int BuildingRoad { get; private set; }
		public int BuildingCanopyArray { get; internal set; }
		public int BuildingAquafarm { get; internal set; }
		public int BuildingIrrigation { get; private set; }
		public int BuildingMine { get; private set; }
		public int BuildingFortress { get; private set; }
		public int BuildingCleanPollution { get; private set; }
		public int BuildingLowerTerrain { get; internal set; }
		public int BuildingRaiseTerrain { get; internal set; }
		public int BuildingPlantForest { get; internal set; }
		public int BuildingPlantJungle { get; internal set; }
		public int BuildingThawTundra { get; internal set; }
		public int BuildingAddRiver { get; internal set; }
		public int BuildingCamp { get; private set; }
		public int BuildingTerrace { get; private set; }
		public int BuildingMoistureFarm { get; private set; }
		// internal set: the AI turns this on for its own workers (AI.cs). The NewTurn
		// machinery below already routes an auto-cleaning settler to the nearest
		// polluted tile and switches the flag back off when the map is clean.
		public bool AutoClean { get; internal set; }
		public bool AutoImprove { get; private set; }

		// Test seam: auto-improve is switched on from the unit menu, which a headless test
		// cannot reach.
		internal void TestEnableAutoImprove() => AutoImprove = true;

		internal bool IsTileClaimed(int tx, int ty) =>
			Game.GetUnits().OfType<Settlers>().Any(s =>
				s != this && s.Owner == Owner &&
				((!s.Goto.IsEmpty && s.Goto.X == tx && s.Goto.Y == ty) ||
				 (s.X == tx && s.Y == ty && s.Busy)));

		// ── Auto-Improve ────────────────────────────────────────────────────────
		// Session-only flag (not persisted). Drains in NewTurn/MovementDone like
		// AutoClean. Policy:
		//   Despotism/Anarchy: roads and rail upgrades only.
		//   Monarchy or better: full improvement set.
		//   Budget: Size+1 tiles per owned city, ordered by distance from city centre.
		//   Hills    → road, rail, mine
		//   Forest+deer / Jungle → road, rail, canopy (never converted)
		//   Forest-no-deer → road, rail, then converted to plains
		//   Swamp/Mountain/Tundra/Arctic/Ocean → skipped entirely
		//   Grassland/Plains/Desert/River → road, rail, irrigation

		private bool IsBuildIdle() =>
			BuildingRoad == 0 && BuildingIrrigation == 0 && BuildingMine == 0 &&
			BuildingFortress == 0 && BuildingCleanPollution == 0 && BuildingCanopyArray == 0 &&
			BuildingAquafarm == 0 && BuildingCamp == 0 && BuildingLowerTerrain == 0 && BuildingRaiseTerrain == 0 &&
			BuildingPlantForest == 0 && BuildingPlantJungle == 0 && BuildingThawTundra == 0 &&
			BuildingAddRiver == 0 && BuildingTerrace == 0 && BuildingMoistureFarm == 0;

		private bool AutoImproveSkipTerrain(ITile t) =>
			t is null || t.IsOcean || t is Mountains || t is Arctic || t is Tundra ||
			t is Swamp || t.City is not null;

		private bool IsTileWorkedByEnemy(int tx, int ty) =>
			Game.GetCities().Any(c => c.Owner != Owner &&
				c.ResourceTiles.Any(rt => rt.X == tx && rt.Y == ty));

		private bool AutoImproveCanIrrigate(ITile tile)
		{
			if (tile.Irrigation) return false;
			if (tile is River) return true;
			if (!(tile is Desert || tile is Grassland || tile is Plains || tile is Hills)) return false;
			return tile.HasIrrigationSource();
		}

		private bool HasCanopyHere(ITile tile) =>
			Game.OlvirImprovements.ContainsKey((tile.X, tile.Y));

		private bool AutoImproveTileNeedsWork(ITile tile)
		{
			if (AutoImproveSkipTerrain(tile)) return false;

			// Road / rail / tube tier
			bool isRiver = tile is River;
			bool roadOk = !tile.IsOcean && tile.City is null && (!isRiver || Player.HasAdvance<BridgeBuilding>());
			if (roadOk)
			{
				if (!tile.Road) return true;
				if (tile.Road && !tile.RailRoad && Player.HasAdvance<RailRoad>()) return true;
				// Rail is the end of the chain on land. Tubes are sea-only (BuildRoad), and
				// claiming one here would send an auto-improving settler to a railroad to do
				// work BuildRoad refuses — it would stand on the tile every turn.
			}

			if (Player.AnarchyDespotism) return false;

			if (tile is Forest)
			{
				if (tile.Special)
					return Player.HasAdvance<CanopyCultivation>() && !HasCanopyHere(tile);
				return true; // convert non-deer forest
			}
			if (tile is Jungle)
				return Player.HasAdvance<CanopyCultivation>() && !HasCanopyHere(tile);
			if (tile is Hills && !tile.Mine) return true;
			if ((tile is Grassland || tile is Plains || tile is Desert || tile is River) &&
				!tile.Irrigation && AutoImproveCanIrrigate(tile))
				return true;
			return false;
		}

		private bool ExecuteAutoImproveAt(ITile tile)
		{
			// Road / rail — BuildRoad walks the chain itself based on state and tech
			if (!tile.TransportTube && !tile.IsOcean && tile.City is null)
			{
				if (BuildRoad()) return true;
			}

			if (Player.AnarchyDespotism) return false;

			if ((tile is Forest && tile.Special) || tile is Jungle)
			{
				if (Player.HasAdvance<CanopyCultivation>() && !HasCanopyHere(tile))
					if (BuildCanopyArray()) return true;
				return false;
			}

			if (tile is Hills && !tile.Mine)
				if (BuildMines()) return true;

			if (tile is Forest && !tile.Special)
			{
				// BuildIrrigation on Forest converts to Plains (City.cs:386-392 / NewTurn loop)
				if (BuildIrrigation()) return true;
			}

			if ((tile is Grassland || tile is Plains || tile is Desert || tile is River) &&
				!tile.Irrigation && AutoImproveCanIrrigate(tile))
			{
				if (BuildIrrigation()) return true;
			}
			return false;
		}

		private ITile? FindNextImprovementTile()
		{
			var ownCities = Game.GetCities()
				.Where(c => c.Owner == Owner)
				.OrderBy(c => Common.DistanceToTile(X, Y, c.X, c.Y))
				.ToList();

			foreach (City city in ownCities)
			{
				int budget = city.Size + 1;
				int processed = 0;

				ITile[,] grid = city.CityRadius;
				var radius = new List<ITile>();
				for (int xx = 0; xx < 5; xx++)
				for (int yy = 0; yy < 5; yy++)
				{
					ITile t = grid[xx, yy];
					if (t is null) continue;
					if (t.X == city.X && t.Y == city.Y) continue;
					radius.Add(t);
				}
				var ordered = radius
					.OrderBy(t => Common.DistanceToTile(city.X, city.Y, t.X, t.Y))
					.ToList();

				foreach (ITile t in ordered)
				{
					if (processed >= budget) break;
					if (AutoImproveSkipTerrain(t)) continue;
					if (IsTileWorkedByEnemy(t.X, t.Y)) continue;
					if ((t.X != X || t.Y != Y) && IsTileClaimed(t.X, t.Y)) continue;

					processed++;
					if (AutoImproveTileNeedsWork(t)) return t;
				}
			}
			return null;
		}

		private void StartAutoImproveStep()
		{
			if (!AutoImprove) return;
			ITile? next = FindNextImprovementTile();
			if (next is null) { AutoImprove = false; return; }
			if (next.X == X && next.Y == Y)
			{
				if (!ExecuteAutoImproveAt(next)) AutoImprove = false;
			}
			else
			{
				Goto = new Point(next.X, next.Y);
			}
		}

		// Nearest unclaimed polluted tile within 3 of one of our own cities.
		//
		// Same rewrite, and for the same reason, as AI.PollutionBacklog: this walked all
		// 64000 tiles and asked every city about each one, per enrolled cleaner, per turn.
		// The predicate only ever accepts tiles within Chebyshev 3 of one of OUR cities,
		// so enumerating that box around each of our cities visits exactly the candidate
		// set and nothing else — identical answers, ~49 x cities reads instead of 64000.
		//
		// Deduped because city radii overlap, and the nearest-wins comparison is unchanged.
		private ITile? FindNearestCityPollution()
		{
			ITile? best = null;
			int bestDist = int.MaxValue;
			const int R = 3;
			var seen = new HashSet<int>();

			foreach (City c in Game.GetCities())
			{
				if (c.Owner != Owner) continue;
				for (int dy = -R; dy <= R; dy++)
				for (int dx = -R; dx <= R; dx++)
				{
					int ty = c.Y + dy;
					if (ty < 0 || ty >= Map.HEIGHT) continue;
					int tx = (c.X + dx + Map.WIDTH) % Map.WIDTH;
					if (!seen.Add(ty * Map.WIDTH + tx)) continue;

					ITile t = Map[tx, ty];
					if (t is null || !t.Pollution) continue;
					if (IsTileClaimed(tx, ty)) continue;
					// A tile held by a foreign unit can never be entered by a Settlers: it is
					// nonCombat, so GotoStep treats every foreign unit as a wall and Confront
					// refuses the step. Targeting one anyway is how a city stops cleaning
					// altogether — this picks the NEAREST polluted tile, so one rival caravan
					// parked on the closest smog pins the whole crew on an impossible job while
					// reachable tiles behind it stay dirty. That is Nagasaki, 2200 AD: ringed by
					// pollution it could not touch because someone else's caravans were sitting
					// on it. Skip it and clean what we can actually reach.
					if (t.Units.Any(u => u.Owner != Owner)) continue;
					int d = Common.DistanceToTile(X, Y, tx, ty);
					if (d < bestDist) { bestDist = d; best = t; }
				}
			}
			return best;
		}

		protected override void MovementDone(ITile previousTile)
		{
			base.MovementDone(previousTile);
			if (AutoClean && Map[X, Y].Pollution)
				CleanPollution();
			if (AutoImprove && IsBuildIdle())
			{
				if (AutoImproveTileNeedsWork(Map[X, Y]) && !IsTileWorkedByEnemy(X, Y))
					ExecuteAutoImproveAt(Map[X, Y]);

				// Arrived (Goto is cleared on arrival, before this runs) and still idle: nothing
				// here can be worked, so pick the next job now instead of blinking.
				//
				// Only NewTurn used to re-pick, and its only exit from auto-improve is the
				// next==here branch of StartAutoImproveStep. A settler that walks to a tile it
				// cannot work therefore sits there with AutoImprove still set — and if the
				// player nudges it away before the next turn, NewTurn re-picks the same tile
				// from the new position and walks it straight back. That is the settler that
				// keeps returning to the same square to blink at it.
				//
				// This terminates: FindNextImprovementTile is deterministic for a fixed
				// position, so if it hands back the tile we are standing on, StartAutoImproveStep
				// takes the next==here branch, fails the same execute, and clears AutoImprove.
				if (IsBuildIdle() && Goto.IsEmpty)
					StartAutoImproveStep();
			}
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

			// No land tubes. The transit tube is alien infrastructure and belongs to the sea:
			// it is laid on ocean tiles by the Hydro Engineer (HydroEngineer.BuildSeaTube) and
			// nowhere else. A settler upgrading its own railroads to tubes made them ordinary
			// terrain improvement and took the strangeness out of them.
			//
			// Connecting to cities still works and needs nothing here: a coastal city sits on
			// land beside the ocean tiles the tube runs through, and the movement rules already
			// treat a tube tile as passable to land units and as rail for movement cost
			// (Common.cs:578, BaseUnitLand.cs:50). So a tube reaching a city's shore joins it
			// to the network exactly as before.
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
			if (tile.IsOcean) return false;
			if (!tile.GetBorderTiles().Any(t => t.IsOcean)) return false;
			if (Game.OlvirImprovements.ContainsKey((tile.X, tile.Y))) return false;
			BuildingAquafarm = 4;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildLowerTerrain()
		{
			if (!Game.CurrentPlayer.HasAdvance<Geoplasticity>()) return false;
			if (!(Map[X, Y] is Hills)) return false;
			BuildingLowerTerrain = 5;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildRaiseTerrain()
		{
			if (!Game.CurrentPlayer.HasAdvance<Geoplasticity>()) return false;
			if (!(Map[X, Y] is Plains)) return false;
			BuildingRaiseTerrain = 5;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildPlantForest()
		{
			if (!Game.CurrentPlayer.HasAdvance<Bioformatting>()) return false;
			ITile tile = Map[X, Y];
			if (!(tile is Plains || tile is Grassland || tile is Desert || tile is Hills)) return false;
			BuildingPlantForest = 5;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildPlantJungle()
		{
			if (!Game.CurrentPlayer.HasAdvance<Bioformatting>()) return false;
			if (!(Map[X, Y] is Forest)) return false;
			BuildingPlantJungle = 4;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildThawTundra()
		{
			if (!Game.CurrentPlayer.HasAdvance<Bioformatting>()) return false;
			if (!(Map[X, Y] is Tundra)) return false;
			BuildingThawTundra = 5;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		public bool BuildAddRiver()
		{
			if (!Game.CurrentPlayer.HasAdvance<Hydroengineering>()) return false;
			ITile tile = Map[X, Y];
			if (tile.IsOcean || tile is River) return false;
			bool adjacentFresh = tile.GetBorderTiles().Any(t => t is River || (t.IsOcean && Map.Instance.IsFreshwaterAt(t.X, t.Y)));
			if (!adjacentFresh) return false;
			BuildingAddRiver = 6;
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

			// ForestedHills belongs with the clearable terrain, not the irrigable: the order
			// clears the trees back to bare Hills. Without it here the menu offered
			// "Change to Hills" (TileExtensions.AllowChangeTerrain) and the order was then
			// refused, which is worse than not offering it.
			if ((tile is Forest) || (tile is Jungle) || (tile is Swamp) || (tile is ForestedHills))
			{
				BuildingIrrigation = 4;
				MovesLeft = 0;
				PartMoves = 0;
				return true;
			}
			else if (tile.HasIrrigationSource())
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
						// Desert gets its own wording: it cannot chain from a neighbouring
						// irrigated tile (TileExtensions.HasIrrigationSource), so offering that
						// as a remedy would send the player off to do something that will not
						// work. The Moisture Farm is what dry interior desert gets instead.
						GameTask.Enqueue(tile is Desert
							? Message.Error("-- Civilization Note --",
								"This desert has no water source.",
								"Needs a neighboring river, lake",
								"or swamp - desert cannot be fed",
								"from another irrigated tile.")
							: Message.Error("-- Civilization Note --",
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

		// Terracing: Hills only, gated on Masonry. Cut the slope into steps and it holds soil
		// and water without a river beside it — which is the whole point, since Hills
		// irrigation needs fresh water in the cross and interior highlands have none.
		public bool BuildTerrace()
		{
			if (!Game.CurrentPlayer.HasAdvance<Masonry>()) return false;
			ITile tile = Map[X, Y];
			if (tile.IsOcean || tile.City is not null) return false;
			if (!(tile is Hills) || tile.Terrace) return false;
			BuildingTerrace = 5;
			MovesLeft = 0; PartMoves = 0;
			return true;
		}

		// Moisture farming: Desert only, gated on Refining. Deliberately NOT Salt Flat — that
		// terrain is meant to stay good for nothing, and a food improvement that reached it
		// would also undermine the founding gate that keeps cities off it (AI.CentreCanFeed).
		public bool BuildMoistureFarm()
		{
			if (!Game.CurrentPlayer.HasAdvance<Refining>()) return false;
			ITile tile = Map[X, Y];
			if (tile.IsOcean || tile.City is not null) return false;
			if (!(tile is Desert) || tile.MoistureFarm) return false;
			BuildingMoistureFarm = 6;
			MovesLeft = 0; PartMoves = 0;
			return true;
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

		// Habitats the score treats as worth keeping. Jungle and wetland are the two terrains
		// a settler can erase that nothing can put back at full value: Plant Jungle exists but
		// only on forest, and there is no order that makes a wetland at all. Forest is absent
		// deliberately — it is renewable, Plant Forest is a standing order, and clearing one
		// is ordinary husbandry rather than a loss.
		private const int EcoPenalty = 1;

		// Every terrain change a settler makes goes through here, including the ones that
		// cannot touch a habitat. That is the point: three separate orders can erase jungle
		// or wetland today (irrigate, mine, engineer a river) and the rule applied at the
		// call sites is the rule that covers the first one and quietly misses the other two —
		// which is exactly how the keyboard shortcuts drifted from the menus.
		//
		// The penalty falls on the unit's owner, AI civs included: it is a scoring rule about
		// what a civilization did to the map, not a handicap on the player.
		private void ReplaceTerrain(Terrain to)
		{
			ITile from = Map[X, Y];
			if (from.Type != to && (from is Jungle || from is Swamp))
				Game.GetPlayer(Owner).AwardMilestone(-EcoPenalty);
			Map.ChangeTileType(X, Y, to);
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

		// Claim a strategic resource deposit (Iron/Coal/Oil special tiles) with a
		// camp — works anywhere, including far outside any city's radius. The camp
		// belongs to whoever's unit last stood on it (Game.ProcessResourceCamps).
		// May a camp stand on this tile at all? Stated ONCE, here, and read by the AI's
		// camp-seeking scan (AI.Strategy.BestCampSite) as well as by the builder below.
		//
		// The two halves of every other settler job in this codebase have drifted apart at
		// least twice — the settle scan against the founder (six settlers converging on a
		// mountain), the work scan against the irrigator — and both times the symptom was a
		// settler walking somewhere it could not do the job. This is the same shape, so it
		// gets the same treatment before it has the chance.
		internal static bool CanCampOn(ITile? tile)
		{
			if (tile is null) return false;
			if (Game.ResourceAt(tile) == StrategicResource.None) return false;
			if (tile.City is not null) return false;
			return !Game.Instance.ResourceCamps.ContainsKey((tile.X, tile.Y));
		}

		public bool BuildCamp()
		{
			ITile tile = Map[X, Y];
			if (!CanCampOn(tile)) return false;
			BuildingCamp = 3;
			MovesLeft = 0;
			PartMoves = 0;
			return true;
		}

		public override void NewTurn()
		{
			base.NewTurn();
			if (Map[X, Y].IsOcean)
			{
				BuildingRoad = BuildingIrrigation = BuildingMine = BuildingFortress = BuildingCamp = 0;
				if (BuildingAquafarm == 0) return;
			}
			if (BuildingRoad > 0)
			{
				BuildingRoad--;
				{
					if (Map[X, Y].Road)
					{
						if (Human.HasAdvance<RailRoad>())
							Map[X, Y].RailRoad = true;
						else if (BuildingRoad > 0)
							foreach (Settlers settlers in Map[X, Y].Units.OfType<Settlers>().Where(s => s.BuildingRoad > 0))
								settlers.BuildingRoad = 0;
					}
					Map[X, Y].Road = true;
					if (BuildingRoad > 0) { MovesLeft = 0; PartMoves = 0; }
					else Game.InvalidateCitiesAt(X, Y);
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
					ReplaceTerrain(Terrain.Plains);
					Game.InvalidateCitiesAt(X, Y);
				}
				// Clearing wooded slopes leaves the slope. This is the only way to reach the
				// mine on one — and on a coal seam it is the whole point of the terrain.
				else if (Map[X, Y] is ForestedHills)
				{
					Map[X, Y].Irrigation = false;
					Map[X, Y].Mine = false;
					ReplaceTerrain(Terrain.Hills);
					Game.InvalidateCitiesAt(X, Y);
				}
				else if ((Map[X, Y] is Jungle) || (Map[X, Y] is Swamp))
				{
					Map[X, Y].Irrigation = false;
					Map[X, Y].Mine = false;
					ReplaceTerrain(Terrain.Grassland1);
					Game.InvalidateCitiesAt(X, Y);
				}
				else
				{
					Map[X, Y].Irrigation = true;
					Map[X, Y].Mine = false;
					Game.InvalidateCitiesAt(X, Y);
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
					ReplaceTerrain(Terrain.Forest);
					Game.InvalidateCitiesAt(X, Y);
				}
				else
				{
					Map[X, Y].Irrigation = false;
					Map[X, Y].Mine = true;
					Game.InvalidateCitiesAt(X, Y);
				}
			}
			else if (BuildingTerrace > 0)
			{
				BuildingTerrace--;
				if (BuildingTerrace > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Map[X, Y].Terrace = true; Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingMoistureFarm > 0)
			{
				BuildingMoistureFarm--;
				if (BuildingMoistureFarm > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Map[X, Y].MoistureFarm = true; Game.InvalidateCitiesAt(X, Y); }
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
				else
				{
					Map[X, Y].Pollution = false;
					// Scrubbing a grey-goo tile burns out the assemblers with it —
					// the settlers' counter-nanite gear is the only cure short of a nuke.
					Game.GooTiles.Remove((X, Y));
					Game.InvalidateCitiesAt(X, Y);
				}
			}
			else if (BuildingCanopyArray > 0)
			{
				BuildingCanopyArray--;
				if (BuildingCanopyArray > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Game.OlvirImprovements[(X, Y)] = OlvirImprovementType.CanopyArray; Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingCamp > 0)
			{
				BuildingCamp--;
				if (BuildingCamp > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Game.ResourceCamps[(X, Y)] = Owner; Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingAquafarm > 0)
			{
				BuildingAquafarm--;
				if (BuildingAquafarm > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Game.OlvirImprovements[(X, Y)] = OlvirImprovementType.Aquafarm; Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingLowerTerrain > 0)
			{
				BuildingLowerTerrain--;
				if (BuildingLowerTerrain > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Map[X, Y].Mine = false; ReplaceTerrain(Terrain.Plains); Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingRaiseTerrain > 0)
			{
				BuildingRaiseTerrain--;
				if (BuildingRaiseTerrain > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Map[X, Y].Irrigation = false; ReplaceTerrain(Terrain.Hills); Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingPlantForest > 0)
			{
				BuildingPlantForest--;
				if (BuildingPlantForest > 0) { MovesLeft = 0; PartMoves = 0; }
				else
				{
					// Trees on a hill are wooded hills, not flat forest — planting must not
					// flatten the ground it is planted on.
					Terrain planted = Map[X, Y] is Hills ? Terrain.ForestedHills : Terrain.Forest;
					Map[X, Y].Irrigation = false; Map[X, Y].Mine = false;
					ReplaceTerrain(planted); Game.InvalidateCitiesAt(X, Y);
				}
			}
			else if (BuildingPlantJungle > 0)
			{
				BuildingPlantJungle--;
				if (BuildingPlantJungle > 0) { MovesLeft = 0; PartMoves = 0; }
				else { ReplaceTerrain(Terrain.Jungle); Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingThawTundra > 0)
			{
				BuildingThawTundra--;
				if (BuildingThawTundra > 0) { MovesLeft = 0; PartMoves = 0; }
				else { ReplaceTerrain(Terrain.Grassland1); Game.InvalidateCitiesAt(X, Y); }
			}
			else if (BuildingAddRiver > 0)
			{
				BuildingAddRiver--;
				if (BuildingAddRiver > 0) { MovesLeft = 0; PartMoves = 0; }
				else { Map[X, Y].Irrigation = false; Map[X, Y].Mine = false; ReplaceTerrain(Terrain.River); Game.InvalidateCitiesAt(X, Y); }
			}

			if (AutoClean && BuildingRoad == 0 && BuildingIrrigation == 0 && BuildingMine == 0 && BuildingFortress == 0 && BuildingCleanPollution == 0 && BuildingCanopyArray == 0 && BuildingAquafarm == 0 && BuildingCamp == 0
				&& BuildingLowerTerrain == 0 && BuildingRaiseTerrain == 0 && BuildingPlantForest == 0 && BuildingPlantJungle == 0 && BuildingThawTundra == 0 && BuildingAddRiver == 0)
			{
				if (Map[X, Y].Pollution && Game.GetCities().Any(c => c.Owner == Owner && Common.DistanceToTile(c.X, c.Y, X, Y) <= 3))
				{
					CleanPollution();
				}
				else
				{
					ITile? target = FindNearestCityPollution();
					if (target is not null)
						Goto = new Point(target.X, target.Y);
					else
						AutoClean = false;
				}
			}

			if (AutoImprove && IsBuildIdle())
				StartAutoImproveStep();

			if (!RoadTo.IsEmpty && BuildingRoad == 0 && BuildingIrrigation == 0 && BuildingMine == 0 && BuildingFortress == 0 && BuildingCleanPollution == 0 && BuildingCanopyArray == 0 && BuildingAquafarm == 0 && BuildingCamp == 0
				&& BuildingLowerTerrain == 0 && BuildingRaiseTerrain == 0 && BuildingPlantForest == 0 && BuildingPlantJungle == 0 && BuildingThawTundra == 0 && BuildingAddRiver == 0)
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
			// No "Build Transport Tube" on land: tubes are laid on ocean by the Hydro
			// Engineer. Offering it here promised a settler order that BuildRoad refuses.
			string label = t.Road ? "Build RailRoad" : "Build Road";
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

		private MenuItem<int> MenuLowerTerrain() => MenuItem<int>
			.Create("Lower to Plains")
			.SetShortcut("l")
			.OnSelect((s, a) => BuildLowerTerrain());

		private MenuItem<int> MenuRaiseTerrain() => MenuItem<int>
			.Create("Raise to Hills")
			.SetShortcut("h")
			.OnSelect((s, a) => BuildRaiseTerrain());

		private MenuItem<int> MenuPlantForest() => MenuItem<int>
			.Create("Plant Forest")
			.SetShortcut("v")
			.OnSelect((s, a) => BuildPlantForest());

		private MenuItem<int> MenuPlantJungle() => MenuItem<int>
			.Create("Plant Jungle")
			.SetShortcut("j")
			.OnSelect((s, a) => BuildPlantJungle());

		private MenuItem<int> MenuThawTundra() => MenuItem<int>
			.Create("Thaw to Grassland")
			.SetShortcut("k")
			.OnSelect((s, a) => BuildThawTundra());

		private MenuItem<int> MenuAddRiver() => MenuItem<int>
			.Create("Engineer River")
			.SetShortcut("n")
			.OnSelect((s, a) => BuildAddRiver());

		private MenuItem<int> MenuBuildRoadTo() => MenuItem<int>
			.Create("Build Road To...")
			.SetShortcut("o")
			.OnSelect((s, a) => GameTask.Enqueue(Show.RoadTo));

		private MenuItem<int> MenuBuildIrrigation() => MenuItem<int>
			.Create((Map[X, Y] is Forest) ? "Change to Plains" :
					(Map[X, Y] is ForestedHills) ? "Change to Hills" :
					((Map[X, Y] is Jungle) || (Map[X, Y] is Swamp)) ? "Change to Grassland" :
					"Build Irrigation")
			.SetShortcut("i")
			.SetEnabled(Map[X, Y].AllowIrrigation() || Map[X, Y].AllowChangeTerrain())
			.OnSelect((s, a) => GameTask.Enqueue(Orders.BuildIrrigation(this)));

		// No SetShortcut on either, deliberately. Every letter GameMap will forward to a unit
		// menu is already claimed by a Settlers order, and the obvious ones are worse than
		// nothing: 'f' is Fortify (BaseUnit) and 't' opens the Terrain screen and returns
		// before the menu is consulted — the same dead-shortcut trap documented on 'W'. A
		// shortcut that silently does nothing is worse than an honest menu entry, so these
		// wait until we decide which key to take and how to guard it.
		private MenuItem<int> MenuBuildTerrace() => MenuItem<int>
			.Create("Build Terrace")
			.OnSelect((s, a) => GameTask.Enqueue(Orders.BuildTerrace(this)));

		private MenuItem<int> MenuBuildMoistureFarm() => MenuItem<int>
			.Create("Build Moisture Farm")
			.OnSelect((s, a) => GameTask.Enqueue(Orders.BuildMoistureFarm(this)));

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

		private MenuItem<int> MenuBuildCamp() => MenuItem<int>
			.Create($"Build {Game.ResourceAt(Map[X, Y])} Camp")
			.SetShortcut("y")
			.OnSelect((s, a) => BuildCamp());

		private MenuItem<int> MenuAutoImprove() => MenuItem<int>
			.Create("Auto-Improve")
			.SetShortcut("e")
			.OnSelect((s, a) =>
			{
				AutoImprove = true;
				Goto = Point.Empty;
				StartAutoImproveStep();
			});

		// 'x', not 'c': GameMap spends 'c' on centre-on-active-unit, a navigation key pressed
		// constantly, and it never reached this order. The reflex keeps the letter; the order
		// takes the spare one.
		private MenuItem<int> MenuAutoCleanPollution() => MenuItem<int>
			.Create("Auto-Clean Pollution")
			.SetShortcut("x")
			.OnSelect((s, a) =>
			{
				AutoClean = true;
				Goto = Point.Empty;
				ITile? target = FindNearestCityPollution();
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
				if (!tile.IsOcean)
					yield return MenuFoundCity();
				{
					bool noInfra     = !tile.Road && !tile.RailRoad && !tile.TransportTube;
					bool canRailroad = tile.Road  && Human.HasAdvance<RailRoad>()       && !tile.RailRoad && !tile.TransportTube;
					if (!tile.IsOcean && (noInfra || canRailroad))
						yield return MenuBuildRoad();
				}
				if (!tile.IsOcean && !tile.TransportTube)
					yield return MenuBuildRoadTo();
				if (!tile.Irrigation && ((tile is Desert) || (tile is Grassland) || (tile is Hills) || (tile is Plains) || (tile is River) || (tile is Forest) || (tile is Jungle) || (tile is Swamp)))
					yield return MenuBuildIrrigation();
				if (!tile.Mine && ((tile is Desert) || (tile is Hills) || (tile is Mountains) || (tile is Jungle) || (tile is Grassland) || (tile is Plains) || (tile is Swamp)))
					yield return MenuBuildMines();
				if (tile is Hills && !tile.Terrace && Human.HasAdvance<Masonry>())
					yield return MenuBuildTerrace();
				if (tile is Desert && !tile.MoistureFarm && Human.HasAdvance<Refining>())
					yield return MenuBuildMoistureFarm();
				if (!tile.IsOcean && !tile.Fortress)
					yield return MenuBuildFortress();
				if (Game.ResourceAt(tile) != StrategicResource.None && tile.City is null
				    && !Game.ResourceCamps.ContainsKey((tile.X, tile.Y)))
					yield return MenuBuildCamp();
				if (Human.HasAdvance<CanopyCultivation>() && (tile is Forest || tile is Jungle) && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)))
					yield return MenuBuildCanopyArray();
				if (Human.HasAdvance<BioplexEngineering>() && !Game.OlvirImprovements.ContainsKey((tile.X, tile.Y)) && !tile.IsOcean && tile.GetBorderTiles().Any(t => t.IsOcean))
					yield return MenuBuildAquafarm();
				// Terrain-altering orders are not offered on a city tile — reshaping the
				// ground a city stands on is meaningless, and the same convention already
				// governs irrigation and mines. It also removed the 'h' ambiguity between
				// "Raise to Hills" and "Home City", which share a shortcut.
				if (Human.HasAdvance<Geoplasticity>() && tile.City is null && tile is Hills)
					yield return MenuLowerTerrain();
				if (Human.HasAdvance<Geoplasticity>() && tile.City is null && tile is Plains)
					yield return MenuRaiseTerrain();
				if (Human.HasAdvance<Bioformatting>() && tile.City is null && (tile is Plains || tile is Grassland || tile is Desert))
					yield return MenuPlantForest();
				if (Human.HasAdvance<Bioformatting>() && tile.City is null && tile is Forest)
					yield return MenuPlantJungle();
				if (Human.HasAdvance<Bioformatting>() && tile.City is null && tile is Tundra)
					yield return MenuThawTundra();
				if (Human.HasAdvance<Hydroengineering>() && !tile.IsOcean && !(tile is River) && tile.GetBorderTiles().Any(t => t is River || (t.IsOcean && Map.Instance.IsFreshwaterAt(t.X, t.Y))))
					yield return MenuAddRiver();
				if (tile.Pollution)
				{
					yield return MenuCleanPollution();
				}
				if (!AutoClean && FindNearestCityPollution() is not null)
				{
					yield return MenuAutoCleanPollution();
				}
				if (!AutoImprove)
				{
					yield return MenuAutoImprove();
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
				yield return null!; // separator
				yield return MenuDisbandUnit();
			}
		}

		private static readonly string[] _page1 =
		{
			"SETTLERS found new CITIES and",
			"improve the land around the ones",
			"you have.",
			"",
			"They build ROADS, IRRIGATION and",
			"MINES, and clear forest or swamp.",
		};

		private static readonly string[] _page2 =
		{
			"Requires no advance.",
			"",
			"Each costs a point of city",
			"POPULATION to build, and food",
			"upkeep thereafter, so a city",
			"shrinks to make one.",
			"",
			"Early expansion decides most",
			"games. Build settlers before",
			"armies.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

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