// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using System.Linq;
using System.Text;
using CivOne.Advances;
using CivOne.Civilizations;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.IO;
using CivOne.Tasks;
using CivOne.Units;
using CivOne.UserInterface;

namespace CivOne.Screens
{
	[Expand]
	internal class NewGame : BaseScreen
	{
		private ICivilization[] _tribesAvailable = null!;
		private string[] _menuItemsDifficulty = null!, _menuItemsCompetition = null!, _menuItemsTribes = null!;

		private readonly int _maxCompetition;
		private readonly Picture _background;

		private int OffsetX => ((Width - 320) / 2);
		private int OffsetY => ((Height - 200) / 2);

		private int _difficulty = -1, _competition = -1, _tribe = -1;
		private string? _leaderName = null, _tribeName = null, _tribeNamePlural = null;
		private bool _done = false, _showIntroText = false, _gameCreated = false;
		private int _borderStyle = -1;
		
		// The panel these menus sit in starts at y=29 (see Free.Difficulties /
		// DIFFS.PIC) and the menu starts 10px below that. Anything past the panel
		// bottom simply draws over its edge and off the screen — which is what the
		// 15-entry competition list and the 24-entry tribe list were doing.
		// Free's panel is both wider and taller than DIFFS.PIC's, so it has more
		// room for the menu; the asset panel keeps its original budget.
		private int MenuSpaceHeight => WidePanel ? 142 : 122;

		// Free mode's DefaultFont draws 7px glyphs one pixel down, so a row needs a
		// full 8px or each line bleeds into the next. The original fonts are
		// smaller and survive 7px rows, which is what asset mode always used.
		private int MinRowHeight => WidePanel ? 8 : 7;

		// The Free background draws a wider panel than DIFFS.PIC does, so the menus
		// can be wider too — needed for the two-column tribe list, whose longest
		// name (Haudenosaunee) is 66px. Keep the narrow geometry in asset mode so
		// the menu stays inside the original artwork.
		private bool WidePanel => Runtime.Settings.Free || !Resources.Exists("DIFFS");
		private int MenuLeft => WidePanel ? 149 : 163;
		private int MenuSpaceWidth => WidePanel ? 140 : 114;

		private Menu CreateMenu(string title, MenuItemEventHandler<int> setChoice, params string[] menuTexts)
		{
			// Fit the list to the panel: use two columns once one column cannot
			// hold the list, then pick the tallest row height that still fits.
			// Rows below 7px overlap the 7px glyphs, so that is the floor.
			// Columns are driven by whether the list actually fits, not by a magic
			// item count: at 7px a 15-entry competition list fits one column, and
			// splitting it produced two half-width columns of overlapping text.
			int columns = ((menuTexts.Length + 1) * MinRowHeight <= MenuSpaceHeight) ? 1 : 2;
			int rows = ((menuTexts.Length + columns - 1) / columns) + 1;   // +1 for the title
			int rowHeight = MenuSpaceHeight / rows;
			if (rowHeight > 8) rowHeight = 8;
			if (rowHeight < MinRowHeight) rowHeight = MinRowHeight;

			Menu menu = new Menu("NewGameMenu", Palette)
			{
				Title = title,
				X = OffsetX + MenuLeft,
				Y = OffsetY + 39,
				MenuWidth = MenuSpaceWidth,
				TitleColour = 3,
				ActiveColour = 11,
				TextColour = 5,
				DisabledColour = 8,
				FontId = 6,
				IndentTitle = 2,
				RowHeight = rowHeight,
				Columns = columns
			};
			
			for (int i = 0; i < menuTexts.Length; i++)
			{
				menu.Items.Add(menuTexts[i], i).OnSelect(setChoice);
			}
			return menu;
		}
		
		private void MenuDifficulty()
		{
			AddMenu(CreateMenu("Difficulty Level...", SetDifficulty, _menuItemsDifficulty));
		}
		
		private void MenuCompetition()
		{
			AddMenu(CreateMenu("Level of Competition...", SetCompetition, _menuItemsCompetition));
		}
		
		private void MenuTribe()
		{
			Menu menu = CreateMenu("Pick your tribe...", SetTribe, _menuItemsTribes);
			menu.Cancel += SetTribe_Cancel;
			AddMenu(menu);
		}
		
		private void InputLeaderName()
		{
			if (Common.HasScreenType<Input>()) return;
			
			ICivilization civ = _tribesAvailable[_tribe];
			Input input = new Input(Palette, civ.Leader.Name, 6, 5, 11, OffsetX + 168, OffsetY + 105, 109, 10, 13);
			input.Accept += LeaderName_Accept;
			input.Cancel += LeaderName_Accept;
			Common.AddScreen(input);
		}
		
		private void SetDifficulty(object sender, MenuItemEventArgs<int> args)
		{
			_difficulty = args.Value;
			CloseMenus();
			Log("Difficulty: {0}", _menuItemsDifficulty[_difficulty]);
		}
		
		private void SetCompetition(object sender, MenuItemEventArgs<int> args)
		{
			_competition = (_maxCompetition - args.Value);
			CloseMenus();
			Log("Competition: {0} Civilizations", _competition);
			
			// Alphabetical: the canonical Civ order is meaningless to a player hunting
			// for one tribe among two dozen. _tribe indexes _tribesAvailable, so both
			// are sorted together.
			_tribesAvailable = Common.Civilizations
				.Where(c => c.PreferredPlayerNumber > 0 && c.PreferredPlayerNumber <= _competition)
				.OrderBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase)
				.ToArray();
			_menuItemsTribes = _tribesAvailable.Select(c => c.Name).ToArray();
		}
		
		private void SetTribe(object sender, MenuItemEventArgs<int> args)
		{
			_tribe = args.Value;
			
			ICivilization civ = _tribesAvailable[_tribe];
			_tribeName = civ.Name;
			_tribeNamePlural = civ.NamePlural;
			CloseMenus();
			Log("Tribe: {0}", _menuItemsTribes[_tribe]);
		}
		
		private void SetTribe_Cancel(object sender, EventArgs args)
		{
			if (sender.GetType() != typeof(Menu)) return;
			
			_tribe = Common.Random.Next(_competition);
			this.AddLayer((Menu)sender);
			CloseMenus();
			
			ICivilization civ = _tribesAvailable[_tribe];
			Input input = new Input(Palette, civ.NamePlural, 6, 5, 11, OffsetX + 168, OffsetY + 105, 109, 10, 11);
			input.Accept += TribeName_Accept;
			input.Cancel += TribeName_Accept;
			Common.AddScreen(input);
		}
		
		private void LeaderName_Accept(object sender, EventArgs args)
		{
			if (sender.GetType() != typeof(Input)) return;
			
			_leaderName = ((Input)sender).Text;
			((Input)sender).Close();
		}
		
		private void TribeName_Accept(object sender, EventArgs args)
		{
			if (sender.GetType() != typeof(Input)) return;
			
			_tribeNamePlural = ((Input)sender).Text;
			_tribeName = ((Input)sender).Text;
			((Input)sender).Close();
		}
		
		private Picture DifficultyPicture
		{
			get
			{
				int pictureId = _difficulty;
				if (pictureId > 4) pictureId = 4;

				int x = (pictureId % 2) == 0 ? 21 : 80;
				int y = 6 + (35 * pictureId);
				return _background[x, y, 53, 47];
			}
		}
		
		// Clockwise positions on a radius-4 circle (screen y-down)
		private static readonly (int dx, int dy)[] _spinDots =
		{
			(0, -4), (3, -3), (4, 0), (3, 3), (0, 4), (-3, 3), (-4, 0), (-3, -3)
		};

		private void DrawSpinner(uint gameTick)
		{
			int cx = OffsetX + 308, cy = OffsetY + 190;
			int frame = (int)(gameTick / 3) % 8;
			this.FillRectangle(cx - 6, cy - 6, 13, 13, 11)
				.DrawRectangle3D(cx - 6, cy - 6, 13, 13);
			for (int i = 0; i < 8; i++)
			{
				(int dx, int dy) = _spinDots[i];
				this.FillRectangle(cx + dx, cy + dy, 1, 1, i == frame ? (byte)0 : (byte)8);
			}
		}

		private void DrawInputBox(string text)
		{
			this.FillRectangle(OffsetX + 158, OffsetY + 88, 161, 33, 11)
				.FillRectangle(OffsetX + 159, OffsetY + 89, 159, 31, 15)
				.DrawText(text, 6, 5, OffsetX + 166, OffsetY + 90)
				.FillRectangle(OffsetX + 166, OffsetY + 103, 113, 14, 5)
				.FillRectangle(OffsetX + 167, OffsetY + 104, 111, 12, 15);
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (HasMenu) return false;

			if (_difficulty == -1) MenuDifficulty();
			else if (_competition == -1) MenuCompetition();
			else if (_tribe == -1) MenuTribe();
			else if (_leaderName is null) InputLeaderName();
			else if (!_done)
			{
				if (_showIntroText) return false;
				if (!Map.Instance.Ready) { DrawSpinner(gameTick); return true; }

				if (!_gameCreated)
				{
					_gameCreated = true;
					ICivilization civ = _tribesAvailable[_tribe];
					Game.CreateGame(_difficulty, _competition, civ, _leaderName, _tribeName, _tribeNamePlural);
				}

				if (_borderStyle < 0) _borderStyle = Common.Random.Next(2);
				this.Clear(15);
				DrawBorder(_borderStyle);

				this.AddLayer(DifficultyPicture, OffsetX + 134, OffsetY + 20);

				int yy = OffsetY + 81;
				foreach (string textLine in TextFile.Instance.GetGameText("KING/INIT"))
				{
					string line = textLine.Replace("$RPLC1", Human.LeaderName).Replace("$US", Human.TribeNamePlural).Replace("^", "");
					this.DrawText(line, 0, 5, OffsetX + 88, yy);
					yy += 8;
					Log(line);
				}
				StringBuilder sb = new StringBuilder();
				int i = 0;
				foreach (IAdvance advance in Human.Advances.OrderBy(a => a.Id))
				{
					sb.Append($"{advance.Name}, ");
					i++;
					if (i % 2 == 0) sb.Append("|");
				}
				sb.Append("and Roads.");

				foreach (string line in sb.ToString().Split('|'))
				{
					this.DrawText(line, 0, 5, OffsetX + 88, yy);
					Log(line);
					yy += 8;
				}

				// Colour 6 (INK_LOW), not 8 (INK_HIGH): this screen's background is
				// cream, and INK_HIGH is a pale cream too — the prompt was near
				// invisible. INK_LOW is muted enough to stay secondary to the body
				// text above it (colour 5) while actually being readable.
				this.DrawText("[ CLICK OR PRESS ANY KEY TO CONTINUE ]", 0, 6, OffsetX + 160, OffsetY + 192, TextAlign.Center);

				_showIntroText = true;
				return true;
			}
			else if (HandleScreenFadeOut())
			{
				return true;
			}
			else
			{
				Destroy();

				GamePlay gamePlay = new GamePlay();
				Common.AddScreen(gamePlay);
				IUnit startUnit = Game.GetUnits().First(x => Game.Human == x.Owner);
				gamePlay.CenterOnPoint(startUnit.X, startUnit.Y);
				
				if (Game.InstantAdvice)
				{
					GameTask.Enqueue(Show.InterfaceHelp);
					GameTask.Enqueue(Message.Help("--- Civilization Note ---", TextFile.Instance.GetGameText("HELP/FIRSTMOVE")));
				}
				return true;
			}
			
			if (_tribe != -1 && _tribeName is null)
			{
				DrawInputBox("Name of your Tribe...");
				return true;
			}
			
			// Draw background
			Bitmap = new Bytemap(Width, Height);
			if (_difficulty == -1)
			{
				this.AddLayer(_background, OffsetX, OffsetY);
			}
			else
			{
				if (_tribe == -1)
					this.AddLayer(_background[140, 0, 180, 200], OffsetX + 140, OffsetY);
				int pictureStack = (_competition <= 0) ? 1 : _competition;
				for (int i = pictureStack; i > 0; i--)
				{
					this.AddLayer(DifficultyPicture, OffsetX + 22 + (i * 2), OffsetY + 100 + (i * 3));
				}
				
				if (_tribe != -1 && _leaderName is null)
				{
					this.DrawText(_tribeNamePlural, 6, 15, OffsetX + 47, OffsetY + 92, TextAlign.Center);
					DrawInputBox("Your Name...");
				}
			}
			
			return true;
		}
		
		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (_tribe != -1 && _leaderName is null)
			{
				if (args.Key == Key.Enter)
				{
					ICivilization civ = _tribesAvailable[_tribe];
					_leaderName = civ.Leader.Name;
					return true;
				}
				return false;
			}
			if (_showIntroText && !_done)
				_done = true;
			return _done;
		}

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (_showIntroText && !_done)
				_done = true;
			return _done;
		}

		private void Resize(object sender, ResizeEventArgs args)
		{
			this.FillRectangle(0, 0, args.Width, args.Height, 5);
			if (_leaderName is null)
			{
				CloseMenus();
			}
			foreach (Input input in Common.Screens.Where(x => x is Input))
			{
				input.X = OffsetX + 168;
				input.Y = OffsetY + 105;
			}
			_showIntroText = false;
		}
		
		public NewGame() : base(MouseCursor.Pointer)
		{
			OnResize += Resize;
			
			if (Runtime.Settings.Free || !Resources.Exists("DIFFS"))
			{
				_background = new Picture(Free.Instance.Difficulties, Common.GetPalette256);
			}
			else
			{
				_background = Resources["DIFFS"];
			}
			
			Palette = _background.Palette;
			this.AddLayer(_background);
			
			if (Settings.Instance.DeityEnabled)
				_menuItemsDifficulty = ["Chieftain (easiest)", "Warlord", "Prince", "King", "Emperor", "Deity (toughest)"];
			else
				_menuItemsDifficulty = ["Chieftain (easiest)", "Warlord", "Prince", "King", "Emperor (toughest)"];
			_maxCompetition = Math.Min(17, Math.Max(3, Map.WIDTH * Map.HEIGHT / 571));
		_menuItemsCompetition = Enumerable.Range(3, _maxCompetition - 2).Reverse().Select(i => $"{i} Civilizations").ToArray();
		}
	}
}