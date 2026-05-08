// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Drawing;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Graphics.Sprites;
using CivOne.IO;
using CivOne.Tasks;
using CivOne.Tiles;
using CivOne.Units;

namespace CivOne.Screens.GamePlayPanels
{
	internal class SideBar : BaseScreen
	{
		private bool _update = true;

		private readonly Picture _miniMap, _demographics;
		private Picture _gameInfo;
		
		private void DrawMiniMap(uint gameTick = 0)
		{
			_miniMap.Clear(CassetteTheme.BG0);

			if (GamePlay != null)
			{
				IUnit activeUnit = Game.ActiveUnit;
				ITile[,] tiles = Map[GamePlay.X - 30, GamePlay.Y - 18, 78, 48];
				for (int yy = 0; yy < 48; yy++)
				for (int xx = 0; xx < 78; xx++)
				{
					ITile tile = tiles[xx, yy];
					if (tile == null) continue;

					// Flash active unit
					if (activeUnit != null && Human == activeUnit.Owner && (tile.X == activeUnit.X && tile.Y == activeUnit.Y))
					{
						_miniMap[xx + 1, yy + 1] = (gameTick % 4 <= 1)
							? CassetteTheme.PHOS_GLOW
							: (byte)(tile.IsOcean ? CassetteTheme.CYAN : CassetteTheme.OK);
						continue;
					}

					if (Settings.RevealWorld)
					{
						byte colour = CassetteTheme.BORDER;
						switch (tile.Type)
						{
							case Terrain.Ocean:  colour = CassetteTheme.CYAN;    break;
							case Terrain.Forest: colour = CassetteTheme.OK;      break;
							case Terrain.Swamp:  colour = CassetteTheme.INK_LOW; break;
							case Terrain.Plains: colour = CassetteTheme.OK;      break;
							case Terrain.Tundra: colour = CassetteTheme.INK_HIGH; break;
							case Terrain.River:  colour = CassetteTheme.CYAN;    break;
							case Terrain.Grassland1:
							case Terrain.Grassland2: colour = CassetteTheme.OK;  break;
							case Terrain.Jungle:     colour = CassetteTheme.OK;  break;
							case Terrain.Hills:      colour = CassetteTheme.INK_MID;  break;
							case Terrain.Mountains:  colour = CassetteTheme.INK_HIGH; break;
							case Terrain.Desert:     colour = CassetteTheme.PHOS_DIM; break;
							case Terrain.Arctic:     colour = CassetteTheme.INK_HIGH; break;
						}
						_miniMap[xx + 1, yy + 1] = colour;
					}
					else if (Human.Visible(tile.X, tile.Y))
					{
						if (tile.City != null)
						{
							_miniMap[xx + 1, yy + 1] = Common.ColourLight[tile.City.Owner];
						}
						else
						{
							_miniMap[xx + 1, yy + 1] = tile.IsOcean ? CassetteTheme.CYAN : CassetteTheme.INK_LOW;
						}
					}
				}
			}
			_miniMap.DrawRectangle(31, 18, 18, 11, CassetteTheme.PHOS)
				.DrawRectangle(0, 0, 80, 50, CassetteTheme.BORDER);
		}

		private void DrawDemographics()
		{
			_demographics
				.FillRectangle(0, 0, 80, 39, CassetteTheme.BG1)
				.FillRectangle(0, 38, 80, 1, CassetteTheme.BORDER)
				.FillRectangle(3, 2, 74, 11, CassetteTheme.BG3)
				.FillRectangle(3, 13, 74, 1, CassetteTheme.BORDER)
				.AddScanlines();

			string govName = Human.Government?.Name ?? "";
			_demographics.DrawText(govName.ToUpper(), 0, CassetteTheme.PHOS, 39, 4, TextAlign.Center);

			if (Human.Population > 0)
			{
				string population = Common.NumberSeperator(Human.Population);
				_demographics.DrawText($"{population}#", 0, CassetteTheme.INK_HIGH, 2, 15, TextAlign.Left);
			}
			_demographics.DrawText(Game.GameYear, 0, CassetteTheme.INK_HIGH, 2, 23, TextAlign.Left);

			int width = Resources.GetTextSize(0, Game.GameYear).Width;
			int stage = (int)Math.Floor(((double)Human.Science / Human.ScienceCost) * 4);
			_demographics.AddLayer(Icons.Lamp(stage), 4 + width, 22);

			_demographics.DrawText($"{Human.Gold}$ {Human.LuxuriesRate}.{Human.TaxesRate}.{Human.ScienceRate}", 0, CassetteTheme.INK_MID, 2, 31, TextAlign.Left);

			// Warming indicator: a small coloured dot in the bottom-right corner
			int indicator = Game.WarmingIndicator;
			if (indicator > 0)
			{
				byte dotColour = indicator switch
				{
					1 => CassetteTheme.ALERT,
					2 => CassetteTheme.ALERT,
					3 => CassetteTheme.PHOS_GLOW,
					_ => CassetteTheme.PHOS_GLOW
				};
				_demographics.FillRectangle(70, 30, 7, 7, dotColour)
				             .DrawRectangle(70, 30, 7, 7, CassetteTheme.BG0);
			}
		}
		
		private void DrawGameInfo(uint gameTick = 0)
		{
			IUnit unit = Game.ActiveUnit;

			_gameInfo
				.FillRectangle(0, 0, _gameInfo.Width, _gameInfo.Height, CassetteTheme.BG1)
				.FillRectangle(0, 0, _gameInfo.Width, 1, CassetteTheme.BORDER)
				.AddScanlines();

			if (Game.CurrentPlayer != Human || (unit != null && Human != unit.Owner) || (GameTask.Any() && !GameTask.Is<Show>() && !GameTask.Is<Message>()))
			{
				byte dotColour = (gameTick % 4 < 2) ? CassetteTheme.PHOS_GLOW : CassetteTheme.PHOS_DIM;
				_gameInfo.FillRectangle(2, _gameInfo.Height - 8, 6, 6, dotColour);
				return;
			}

			if (unit != null)
			{
				int yy = 2;
				_gameInfo.DrawText(Human.TribeName, 0, CassetteTheme.PHOS_DIM, 4, 2, TextAlign.Left);
				_gameInfo.DrawText(unit.Name, 0, CassetteTheme.INK_HIGH, 4, (yy += 8), TextAlign.Left);

				if (unit.Veteran)
				{
					_gameInfo.DrawText("Veteran", 0, CassetteTheme.PHOS, 8, (yy += 8), TextAlign.Left);
				}

				if (unit is BaseUnitAir)
				{
					_gameInfo.DrawText($"Moves: {unit.MovesLeft}({(unit as BaseUnitAir).FuelLeft})", 0, CassetteTheme.INK_MID, 4, (yy += 8), TextAlign.Left);
				}
				else if (unit.PartMoves > 0)
				{
					_gameInfo.DrawText($"Moves: {unit.MovesLeft}.{unit.PartMoves}", 0, CassetteTheme.INK_MID, 4, (yy += 8), TextAlign.Left);
				}
				else
				{
					_gameInfo.DrawText($"Moves: {unit.MovesLeft}", 0, CassetteTheme.INK_MID, 4, (yy += 8), TextAlign.Left);
				}
				_gameInfo.DrawText((unit.Home == null ? "NONE" : unit.Home.Name), 0, CassetteTheme.INK_LOW, 4, (yy += 8), TextAlign.Left);
				_gameInfo.DrawText($"({Map[unit.X, unit.Y].Name})", 0, CassetteTheme.INK_LOW, 4, (yy += 8), TextAlign.Left);

				if (Map[unit.X, unit.Y].RailRoad)
					_gameInfo.DrawText("(RailRoad)", 0, CassetteTheme.INK_LOW, 4, (yy += 8), TextAlign.Left);
				else if (Map[unit.X, unit.Y].Road)
					_gameInfo.DrawText("(Road)", 0, CassetteTheme.INK_LOW, 4, (yy += 8), TextAlign.Left);
				if (Map[unit.X, unit.Y].Irrigation)
					_gameInfo.DrawText("(Irrigation)", 0, CassetteTheme.INK_LOW, 4, (yy += 8), TextAlign.Left);
				else if (Map[unit.X, unit.Y].Mine)
					_gameInfo.DrawText("(Mining)", 0, CassetteTheme.INK_LOW, 4, (yy += 8), TextAlign.Left);

				yy += 11;

				IUnit[] units = Map[unit.X, unit.Y].Units.Where(u => u != unit).Take(8).ToArray();
				for (int i = 0; i < units.Length; i++)
				{
					int ix = 7 + ((i % 4) * 16);
					int iy = yy + (((i - (i % 4)) / 4) * 16);
					_gameInfo.AddLayer(units[i].ToBitmap(), ix, iy);
				}
			}
			else
			{
				if (gameTick % 4 < 2)
					_gameInfo.DrawText("End of Turn", 0, CassetteTheme.PHOS, 4, 26, TextAlign.Left);
				_gameInfo.DrawText("Press Enter", 0, CassetteTheme.INK_MID, 4, 42, TextAlign.Left);
				_gameInfo.DrawText("to continue", 0, CassetteTheme.INK_MID, 4, 50, TextAlign.Left);
			}
		}
		
		// ─── notification strip (disorder takes priority over WLTK) ──────────
		private const int NotifMaxLines = 5;

		private int NotifLineH => Resources.GetFontHeight(0) + 2;

		private string[] DisorderCities()
		{
			if (!Game.Started) return System.Array.Empty<string>();
			byte humanId = Game.PlayerNumber(Human);
			return Game.GetCities()
				.Where(c => c.Owner == humanId && SafeIsInDisorder(c))
				.Select(c => c.Name)
				.ToArray();
		}

		private static bool SafeIsInDisorder(CivOne.City c)
		{
			try { return c.IsInDisorder; }
			catch (Exception ex) { Log($"[SideBar] IsInDisorder failed for {c.Name}: {ex.GetType().Name}: {ex.Message}"); return false; }
		}

		private int NotifPanelH(int cityCount)
		{
			if (cityCount == 0) return 0;
			int lh = NotifLineH;
			return (1 + Math.Min(cityCount, NotifMaxLines)) * lh + 3;
		}

		private void DrawNotifications()
		{
			string[] disorder = DisorderCities();
			bool showDisorder = disorder.Length > 0;

			var wltkCities = WLTKNotifications.Cities;
			int count = showDisorder ? disorder.Length : wltkCities.Count;
			if (count == 0) return;

			int lh   = NotifLineH;
			int ph   = NotifPanelH(count);
			int py   = _gameInfo.Height - ph;

			_gameInfo.FillRectangle(2, py, 76, 1, CassetteTheme.BORDER);
			py += 2;

			if (showDisorder)
			{
				_gameInfo.DrawText("DISORDER", 0, CassetteTheme.ALERT, 3, py, TextAlign.Left);
				py += lh;
				int skip = Math.Max(0, disorder.Length - NotifMaxLines);
				foreach (string city in disorder.Skip(skip))
				{
					_gameInfo.DrawText(city.ToUpper(), 0, CassetteTheme.INK_HIGH, 3, py, TextAlign.Left);
					py += lh;
				}
			}
			else
			{
				_gameInfo.DrawText("♡ THE KING", 0, CassetteTheme.PHOS, 3, py, TextAlign.Left);
				py += lh;
				int skip = Math.Max(0, wltkCities.Count - NotifMaxLines);
				foreach (string city in wltkCities.Skip(skip))
				{
					_gameInfo.DrawText(city.ToUpper(), 0, CassetteTheme.INK_HIGH, 3, py, TextAlign.Left);
					py += lh;
				}
			}
		}

		protected override bool HasUpdate(uint gameTick)
		{
			if (WLTKNotifications.ConsumedDirty())
				_update = true;
			if (gameTick % 4 == 0)  // recheck disorder status each second
				_update = true;

			if (_update || (gameTick % 2 == 0))
			{
				if (!(Common.TopScreen is GamePlay))
					gameTick = 0;

				DrawMiniMap(gameTick);
				DrawDemographics();
				DrawGameInfo(gameTick);
				DrawNotifications();

				this.AddLayer(_miniMap, 0, 0)
					.AddLayer(_demographics, 0, 50)
					.AddLayer(_gameInfo, 0, 89);

				_update = false;
				return true;
			}
			return false;
		}
		
		public override bool MouseDown(ScreenEventArgs args)
		{
			if (args.Y <= 50)
			{
				if (args.X < 1 || args.Y < 1 || args.X > 79 || args.Y > 49) return true;
				
				int xx = (args.X - 1) + GamePlay.X - 30;
				int yy = (args.Y - 1) + GamePlay.Y - 18;

				GamePlay.CenterOnPoint(xx, yy);
			}
			if (args.Y >= 62)
			{
				if (Game.CurrentPlayer == Human && Game.ActiveUnit == null)
				{
					GameTask.Enqueue(Turn.End());
				}
			}
			return true;
		}

		private GamePlay GamePlay
		{
			get
			{
				IScreen mapScreen = Common.Screens.FirstOrDefault(s => (s is GamePlay));
				if (mapScreen != null)
					return (mapScreen as GamePlay);
				return null;
			}
		}
		
		public void Resize(int height)
		{
			Bitmap = new Bytemap(80, height);
			_gameInfo?.Dispose();
			_gameInfo = new Picture(80, (height - 89), Palette);
			_update = true;
		}

		public SideBar(Palette palette) : base(80, 192)
		{
			_miniMap = new Picture(80, 50, palette);
			_demographics = new Picture(80, 39, palette);
			_gameInfo = new Picture(80, 103, palette);
			
			DrawMiniMap();
			DrawDemographics();
			DrawGameInfo();
			
			Palette = palette.Copy();
			this.AddLayer(_miniMap, 0, 0)
				.AddLayer(_demographics, 0, 50)
				.AddLayer(_gameInfo, 0, 89);
		}

		public override void Dispose()
		{
			_miniMap.Dispose();
			_demographics.Dispose();
			_gameInfo.Dispose();
			base.Dispose();
		}
	}
}
