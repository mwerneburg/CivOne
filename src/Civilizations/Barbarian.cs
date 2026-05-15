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
using CivOne.Leaders;
using CivOne.Tiles;

namespace CivOne.Civilizations
{
	internal class Barbarian : BaseCivilization<Atilla>
	{
		internal static bool IsSeaSpawnTurn  => Game.Started && (Game.GameTurn % 8 == 0) && (Game.GameTurn > 150 || Game.GameTurn >= (5 - Game.Difficulty) * 32) && !Game.Players.Any(x => x.HasAdvance<Combustion>());
		internal static bool IsLandSpawnTurn => Game.Started && (Game.GameTurn % 4 == 0) && (Game.GameTurn >= (5 - Game.Difficulty) * 16);

		internal static IEnumerable<UnitType> SeaSpawnUnits
		{
			get
			{
				if (!IsSeaSpawnTurn) yield break;
				yield return (Game.GameTurn < 300) ? UnitType.Sail : UnitType.Frigate;
				
				UnitType unitType = (Game.Players.Any(x => x.HasAdvance<Gunpowder>())) ? UnitType.Knights : UnitType.Legion;
				int unitCount = (Game.GameTurn < 150) ? 1 : (Game.GameTurn < 300) ? 2 : 3;
				for (int i = 0; i < unitCount; i++)
					yield return unitType;
				yield return UnitType.Diplomat;
			}
		}

		internal static ITile SeaSpawnPosition
		{
			get
			{
				ITile[] tiles = Map.AllTiles().Where(t => t is not null && t.IsOcean).ToArray();
				for (int i = 0; i < 1000; i++)
				{
					ITile tile = tiles[Common.Random.Next(tiles.Length)];
					if (tile is null || !tile.IsOcean || tile.GetBorderTiles().Any(t => t is null || !t.IsOcean)) continue;
					return tile;
				}
				return null;
			}
		}

		// Land spawn: pick a land tile that is unexplored by all human players and has no city.
		internal static ITile LandSpawnPosition
		{
			get
			{
				ITile[] candidates = Map.AllTiles()
					.Where(t => t is not null && !t.IsOcean && t.City is null
						&& !Game.Players.Any(p => p.IsHuman && p.Visible(t.X, t.Y)))
					.ToArray();
				if (candidates.Length == 0) return null;
				for (int i = 0; i < 200; i++)
				{
					ITile tile = candidates[Common.Random.Next(candidates.Length)];
					if (tile.Units.Length == 0) return tile;
				}
				return null;
			}
		}

		internal static IEnumerable<UnitType> LandSpawnUnits
		{
			get
			{
				if (!IsLandSpawnTurn) yield break;
				UnitType unitType = Game.Players.Any(x => x.HasAdvance<Gunpowder>()) ? UnitType.Knights
					: Game.Players.Any(x => x.HasAdvance<BronzeWorking>())           ? UnitType.Legion
					: UnitType.Militia;
				int count = (Game.GameTurn < 150) ? 1 : (Game.GameTurn < 300) ? 2 : 3;
				for (int i = 0; i < count; i++)
					yield return unitType;
			}
		}

		public Barbarian() : base(Civilization.Barbarians, "Barbarian", "Barbarians")
		{
			StartX = 255;
			StartY = 255;
			CityNames = new string[]
			{
				"Mecca",
				"Naples",
				"Sidon",
				"Tyre",
				"Tarsus",
				"Issus",
				"Cunaxa",
				"Cremona",
				"Cannae",
				"Capua",
				"Turin",
				"Genoa",
				"Utica",
				"Crete",
				"Damascus",
				"Verona",
				"Salamis",
				"Lisbon",
				"Hamburg",
				"Prague",
				"Salzburg",
				"Bergen",
				"Venice",
				"Milan",
				"Ghent",
				"Pisa",
				"Cordoba",
				"Seville",
				"Dublin",
				"Toronto",
				"Melbourne",
				"Sydney"
			};
		}
	}
}