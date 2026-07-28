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
		// Is this a coast the boat could put its colonists on? Habitable, unclaimed,
		// and clear of existing cities by the usual founding distance.
		internal bool CanLandOn(ITile? t) =>
			t is not null && !t.IsOcean
			&& !(t is Arctic) && !(t is Mountains)
			&& t.City is null
			&& !Game.GetCities().Any(c => c.Size > 0 && Common.DistanceToTile(c.X, c.Y, t.X, t.Y) < 4);

		// The best adjacent coast, used when nobody has named one (the AI, and the
		// menu item's default).
		internal ITile? LandingSite()
		{
			if (Tile is null) return null;
			return Tile.GetBorderTiles()
				.Where(CanLandOn)
				.OrderByDescending(t => t.LandValue)
				.FirstOrDefault();
		}

		// Put ashore and found. Pass the tile to land on a chosen coast — steering the
		// boat into a shoreline is how a player says which one, and on a strait that
		// choice is the whole point. Falls back to the best adjacent site.
		// Returns false when there is nowhere to land.
		internal bool GoAshore(ITile? site = null)
		{
			site ??= LandingSite();
			if (!CanLandOn(site)) return false;
			GameTask.Enqueue(Orders.NewCity(Player, site!.X, site.Y));
			Game.DisbandUnit(this);
			return true;
		}

		// Steering into an adjacent coast lands there rather than bouncing off it.
		// BaseUnitSea.ValidMoveTarget rejects land outright, so without this the boat
		// silently refuses every direction key that points at the new world.
		public override bool MoveTo(int relX, int relY)
		{
			ITile? target = Tile?[relX, relY];
			if (target is not null && !target.IsOcean && target.City is null)
			{
				if (GoAshore(target)) return true;
				// Say why. Steering at a shore and having nothing happen is indistinguishable
				// from the boat being broken, which is how this gap was found.
				if (Human == Owner)
					GameTask.Enqueue(Message.Error("-- Civilization Note --",
						"", "  This shore cannot be settled:", "  it is barren, or lies too",
						"  close to an existing city.", ""));
				return false;
			}
			return base.MoveTo(relX, relY);
		}

		private MenuItem<int> MenuGoAshore() => MenuItem<int>
			.Create("Found New City")
			.SetShortcut("b")
			.OnSelect((s, a) => GoAshore());

		public override IEnumerable<MenuItem<int>> MenuItems
		{
			get
			{
				// The sea menu minus Unload/Home City (a longboat carries nobody and has
				// no home to change), plus the one thing it exists to do.
				if (LandingSite() is not null) yield return MenuGoAshore();
				foreach (MenuItem<int> item in base.MenuItems)
					yield return item;
			}
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
