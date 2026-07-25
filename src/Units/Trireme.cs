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
using CivOne.IO;
using CivOne.Tasks;
using CivOne.Tiles;

namespace CivOne.Units
{
	internal class Trireme : BaseUnitSea, IBoardable
	{
		protected override void MovementDone(ITile previousTile)
		{
			base.MovementDone(previousTile);
			
			if (MovesLeft > 0) return;

			// Check if the Trireme is at open sea
			if (Tile.GetBorderTiles().Any(t => !(t is Ocean))) return;

			// The Trireme unit is surrounded by oceans, there's a 50% chance it will be lost at sea
			if (Common.Random.Next(0, 100) < 50) return;

			// Notify only the owner: an AI civ's Trireme lost in open sea is not the human's
			// business (the message read as if it were the player's own loss).
			bool notify = Human == Owner;
			Game.DisbandUnit(this);
			if (notify)
				GameTask.Enqueue(Message.Error("-- Civilization Note --", TextFile.Instance.GetGameText("ERROR/TRIREME")));
		}

		public int Cargo
		{
			get
			{
				return 2;
			}
		}

		private static readonly string[] _page1 =
		{
			"The TRIREME is the first ship, an",
			"oared galley that carries 2 land",
			"units.",
			"",
			"It must hug the COAST: alone in",
			"the open sea at end of turn, it",
			"may be LOST.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MAP MAKING.",
			"Made obsolete by NAVIGATION.",
			"",
			"The risk is real; end each turn",
			"within sight of land.",
			"",
			"THE LIGHTHOUSE grants an extra",
			"move to your ships, and quiets",
			"the danger of the deep. Something",
			"in that deep may notice.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Trireme() : base(4, 1, 0, 3)
		{
			Type = UnitType.Trireme;
			Name = "Trireme";
			RequiredTech = new MapMaking();
			ObsoleteTech = new Navigation();
			SetIcon('B', 0, 1);
		}
	}
}