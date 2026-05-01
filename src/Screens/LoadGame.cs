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
using System.IO;
using System.Linq;
using CivOne.Enums;
using CivOne.Events;
using CivOne.Graphics;
using CivOne.Persistence;
using CivOne.UserInterface;

namespace CivOne.Screens
{
	[Modal, Expand]
	internal class LoadGame : BaseScreen
	{
		private int OX => (Width - 320) / 2;
		private int OY => (Height - 200) / 2;

		private class SaveGameFile
		{
			public bool ValidFile { get; private set; }
			public bool IsCos { get; private set; }
			public string CosFile { get; private set; }
			public string SveFile { get; private set; }
			public string MapFile { get; private set; }

			public string Name { get; private set; }

			private ushort ReadUShort(BinaryReader reader, int position)
				=> Common.BinaryReadUShort(reader, position);

			private string[] ReadStrings(BinaryReader reader, int position, int length, int itemLength)
				=> Common.BinaryReadStrings(reader, position, length, itemLength);

			public SaveGameFile(string filename)
			{
				ValidFile = false;
				Name = "(EMPTY)";
				CosFile = $"{filename}.cos";
				SveFile = $"{filename}.SVE";
				MapFile = $"{filename}.MAP";

				if (File.Exists(CosFile))
				{
					try
					{
						var meta = CosSerializer.DeserializeMeta(File.ReadAllText(CosFile));
						if (meta != null)
						{
							Name = meta.Name ?? "(UNKNOWN)";
							IsCos = true;
							ValidFile = true;
						}
					}
					catch (Exception ex)
					{
						Log($"Could not read .cos file: {ex.Message}");
						Name = "(COULD NOT READ SAVE FILE)";
					}
				}
				else if (File.Exists(SveFile) && File.Exists(MapFile))
				{
					try
					{
						using (FileStream fs = new FileStream(SveFile, FileMode.Open))
						using (BinaryReader br = new BinaryReader(fs))
						{
							if (fs.Length != 37856)
							{
								Name = "(INCORRECT FILE SIZE)";
								return;
							}
							string turn = Common.YearString(ReadUShort(br, 0));
							ushort humanPlayer = ReadUShort(br, 2);
							ushort difficultyLevel = ReadUShort(br, 10);
							string leaderName = ReadStrings(br, 16, 112, 14)[humanPlayer];
							string civName = ReadStrings(br, 128, 96, 12)[humanPlayer];
							string title = Common.DifficultyName(difficultyLevel);
							Name = $"{title} {leaderName}, {civName}/{turn}";
						}
						ValidFile = true;
					}
					catch (Exception ex)
					{
						Log($"Could not open .SVE file: {ex.InnerException}");
						Name = "(COULD NOT READ SAVE FILE HEADER)";
					}
				}
			}
		}

		private MouseCursor _cursor = MouseCursor.None;
		public override MouseCursor Cursor => _cursor;
		
		private char _driveLetter = 'C';
		private bool _update = true;
		private Menu _menu;
		
		public bool Cancel { get; private set; }
		
		private IEnumerable<SaveGameFile> GetSaveGames()
		{
			string path = Path.Combine(Settings.SavesDirectory, char.ToLower(_driveLetter).ToString());
			for (int i = 0; i < 4; i++)
			{
				string filename = Path.Combine(path, string.Format("CIVIL{0}", i));
				yield return new SaveGameFile(filename);
			}
			// AutoSave slot — fixed path, always shown last
			yield return new SaveGameFile(Settings.Instance.AutoSavePath.Replace(".cos", ""));
		}

		private void LoadSaveFile(object sender, MenuItemEventArgs<int> args)
		{
			int item = args.Value;
			SaveGameFile file = GetSaveGames().ToArray()[item];
			SaveGame.SelectedGame = Math.Min(item, 3);
			Log("Load game: {0}", file.Name);
			Destroy();
			if (file.IsCos)
				Game.LoadCos(file.CosFile);
			else
				Game.LoadGame(file.SveFile, file.MapFile);
			if (Game.Started)
				Common.AddScreen(new GamePlay());
		}
		
		private void LoadEmptyFile(object sender, MenuItemEventArgs<int> args)
		{
			Log("Empty save file, cancel");
			Cancel = true;
			_update = true;
		}

		private MenuItemEventHandler<int> LoadFileHandler(SaveGameFile file)
		{
			if (file.ValidFile)
				return LoadSaveFile;
			return LoadEmptyFile;
		}
		
		private void DrawDriveQuestion()
		{
			this.Clear(0)
				.FillRectangle(OX, OY, 320, 200, 15)
				.DrawText("Which drive contains your", 0, 5, OX + 92, OY + 72, TextAlign.Left)
				.DrawText("saved game files?", 0, 5, OX + 104, OY + 80, TextAlign.Left)
				.DrawText($"{_driveLetter}:", 0, 5, OX + 146, OY + 96, TextAlign.Left)
				.DrawText("Press drive letter and", 0, 5, OX + 104, OY + 112, TextAlign.Left)
				.DrawText("Return when disk is inserted", 0, 5, OX + 80, OY + 120, TextAlign.Left)
				.DrawText("Press Escape to cancel", 0, 5, OX + 104, OY + 128, TextAlign.Left);
		}
		
		protected override bool HasUpdate(uint gameTick)
		{
			if (_menu != null)
			{
				if (_menu.Update(gameTick))
				{
					this.Clear(0)
						.FillRectangle(OX, OY, 320, 200, 15)
						.AddLayer(_menu, OX, OY);
					return true;
				}
				return Cancel;
			}
			else if (_update)
			{
				DrawDriveQuestion();
				_update = false;
				return true;
			}
			return Cancel;
		}
		
		public override bool KeyDown(KeyboardEventArgs args)
		{
			if (Cancel) return false;
			
			char c = Char.ToUpper(args.KeyChar);
			if (args.Key == Key.Escape)
			{
				Log("Cancel");
				Cancel = true;
				_update = true;
				return true;
			}
			else if (_menu != null)
			{
				return _menu.KeyDown(args);
			}
			else if (args.Key == Key.Enter)
			{
				_menu = new Menu(Palette)
				{
					Title = "Select Load File...",
					X = 51,
					Y = 70,
					MenuWidth = 217,
					TitleColour = 12,
					ActiveColour = 11,
					TextColour = 5,
					FontId = 0,
					IndentTitle = 2,
					RowHeight = 8
				};
				
				int i = 0;
				foreach (SaveGameFile file in GetSaveGames())
				{
					bool isAuto = (i == 4);
					string label = isAuto ? $"AUTO: {file.Name}" : file.Name;
					_menu.Items.Add(label, i++).OnSelect(LoadFileHandler(file));
				}
				_cursor = MouseCursor.Pointer;
			}
			else if (c >= 'A' && c <= 'Z')
			{
				_driveLetter = c;
				_update = true;
				return true;
			}
			return false;
		}
		
		private ScreenEventArgs LocalArgs(ScreenEventArgs args) =>
			new ScreenEventArgs(args.X - OX, args.Y - OY, args.Buttons);

		public override bool MouseDown(ScreenEventArgs args)
		{
			if (_menu != null)
				return _menu.MouseDown(LocalArgs(args));
			return false;
		}

		public override bool MouseUp(ScreenEventArgs args)
		{
			if (_menu != null)
				return _menu.MouseUp(LocalArgs(args));
			return false;
		}

		public override bool MouseDrag(ScreenEventArgs args)
		{
			if (_menu != null)
				return _menu.MouseDrag(LocalArgs(args));
			return false;
		}
		
		public LoadGame(Palette palette)
		{
			Palette = palette;
		}
	}
}