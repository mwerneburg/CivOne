#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Collections.Generic;
using System.Drawing;
using CivOne.Advances;
using CivOne.Enums;
using CivOne.Tiles;
using CivOne.UserInterface;

namespace CivOne.Units
{
	internal class Explorer : BaseUnitLand
	{
		public bool AutoExplore { get; private set; }

		public override bool Busy
		{
			get => base.Busy;
			set
			{
				base.Busy = value;
				AutoExplore = false;
			}
		}

		private int CountUnseenTiles(int x, int y)
		{
			int count = 0;
			int w = Map.WIDTH, h = Map.HEIGHT;
			for (int dy = -1; dy <= 1; dy++)
			for (int dx = -1; dx <= 1; dx++)
			{
				int tx = (x + dx + w) % w;
				int ty = y + dy;
				if (ty < 0 || ty >= h) continue;
				if (!Player.Visible(tx, ty)) count++;
			}
			return count;
		}

		private ITile? BestExploreTile()
		{
			int w = Map.WIDTH, h = Map.HEIGHT;
			ITile? best = null;
			int bestScore = 0;
			for (int dy = -8; dy <= 8; dy++)
			for (int dx = -8; dx <= 8; dx++)
			{
				if (dx == 0 && dy == 0) continue;
				int tx = (X + dx + w) % w;
				int ty = Y + dy;
				if (ty < 0 || ty >= h) continue;
				ITile tile = Map[tx, ty];
				if (tile is null || tile.IsOcean) continue;
				int dist = Common.DistanceToTile(X, Y, tx, ty);
				int score = CountUnseenTiles(tx, ty) - dist;
				if (score > bestScore) { bestScore = score; best = tile; }
			}
			return best;
		}

		public override void NewTurn()
		{
			base.NewTurn();
			if (AutoExplore && Goto.IsEmpty)
			{
				ITile? dest = BestExploreTile();
				if (dest is not null)
					Goto = new Point(dest.X, dest.Y);
				else
					AutoExplore = false;
			}
		}

		protected override void MovementDone(ITile previousTile)
		{
			base.MovementDone(previousTile);
			if (AutoExplore && Goto.IsEmpty && (MovesLeft > 0 || PartMoves > 0))
			{
				ITile? dest = BestExploreTile();
				if (dest is not null)
					Goto = new Point(dest.X, dest.Y);
				else
					AutoExplore = false;
			}
		}

		private MenuItem<int> MenuAutoExplore() => MenuItem<int>
			.Create("Auto Explore")
			.SetShortcut("e")
			.OnSelect((s, a) =>
			{
				AutoExplore = true;
				ITile? dest = BestExploreTile();
				if (dest is not null)
					Goto = new Point(dest.X, dest.Y);
				else
					AutoExplore = false;
			});

		public override IEnumerable<MenuItem<int>> MenuItems
		{
			get
			{
				yield return MenuNoOrders();
				yield return MenuFortify();
				yield return MenuWait();
				yield return MenuSentry();
				yield return MenuGoTo();
				yield return MenuAutoExplore();
				if (Map[X, Y].Irrigation || Map[X, Y].Mine || Map[X, Y].Road || Map[X, Y].RailRoad)
					yield return MenuPillage();
				if (Map[X, Y].City is not null)
					yield return MenuHomeCity();
				var upgrade = MenuUpgrade();
				if (upgrade is not null)
				{
					yield return null!; // separator
					yield return upgrade;
				}
				yield return null!; // separator
				yield return MenuDisbandUnit();
			}
		}

		public Explorer() : base(2, 0, 1, 2)
		{
			Type = UnitType.Explorer;
			Name = "Explorer";
			RequiredTech = null;
			ObsoleteTech = new Combustion();
			SetIcon('C', 1, 0);
		}
	}
}
