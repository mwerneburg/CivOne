// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Linq;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tasks;
using CivOne.Tiles;

namespace CivOne.Units
{
	// A settler that sails. The Norse and Polynesian answer to a coastline: put the
	// boat ashore and build there.
	//
	// Land settlers can only reach what the land connects to, so a civ that starts
	// on an island — or behind a strait, or walled off by desert — has no expansion
	// at all: AI.Strategy.HasExpansionRoom is land-only by design. The Longboat is
	// the way out. It sails five tiles a turn across open ocean, and founds a city
	// on any adjacent coast, consuming itself as a land Settlers would.
	//
	// Deliberately defenceless: no attack, no diplomacy, and it carries nothing but
	// its own colonists. It is a one-way journey, not a navy.
	internal class Longboat : BaseUnitSea
	{
		// Coast the boat can land on: adjacent, habitable, unclaimed, and clear of
		// existing cities by the usual founding distance.
		internal ITile? LandingSite()
		{
			if (Tile is null) return null;
			return Tile.GetBorderTiles()
				.Where(t => t is not null && !t.IsOcean
				         && !(t is Arctic) && !(t is Mountains)
				         && t.City is null
				         && !Game.GetCities().Any(c => c.Size > 0 && Common.DistanceToTile(c.X, c.Y, t.X, t.Y) < 4))
				.OrderByDescending(t => t.LandValue)
				.FirstOrDefault();
		}

		// Put ashore and found. Returns false when there is nowhere to land.
		internal bool GoAshore()
		{
			ITile? site = LandingSite();
			if (site is null) return false;
			GameTask.Enqueue(Orders.NewCity(Player, site.X, site.Y));
			Game.DisbandUnit(this);
			return true;
		}

		private static readonly string[] _page1 =
		{
			"A LONGBOAT carries colonists over",
			"open sea and puts them ashore.",
			"",
			"It sails FIVE tiles a turn and",
			"founds a city on any coast it",
			"reaches, exactly as SETTLERS do",
			"on land — and is spent doing so.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MAP MAKING.",
			"",
			"Costs one point of city",
			"POPULATION, like Settlers.",
			"",
			"It cannot fight, treat with",
			"anyone, or carry other units. A",
			"longboat is a one-way journey,",
			"not a navy.",
			"",
			"For a people whose land runs out",
			"at the shore, it is the only",
			"expansion there is.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Longboat() : base(6, 0, 1, 5)
		{
			Type = UnitType.Longboat;
			Name = "Longboat";
			RequiredTech = new MapMaking();
			ObsoleteTech = null;
			SetIcon('B', 0, 1); // Trireme's unit-panel icon
		}
	}
}
