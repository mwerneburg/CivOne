// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System;
using CivOne.Persistence;
using CivOne.Screens.Reports;

namespace CivOne.Screens
{
	internal static class EndSequence
	{
		// Saves the entry to the Hall of Fame and returns the 0-based rank index.
		internal static int SaveAndGetIndex(Player player, string victoryType)
		{
			string year = Common.YearString(Game.Instance.GameTurn);
			HallOfFame.AddAndSave(player, victoryType, year);
			var entries = HallOfFame.Load();
			for (int i = 0; i < entries.Count; i++)
			{
				if (entries[i].LeaderName == player.LeaderName
					&& entries[i].Score   == player.Score
					&& entries[i].Year    == year)
					return i;
			}
			return -1;
		}

		// Persist the finished game so the replay can be watched again later. The end
		// sequence plays it exactly once and then quits, so anyone called away loses
		// the only showing — and a long autoplay game is precisely the one you want to
		// review. Written to the autosave slot, which Load Game already lists: load it,
		// then Game menu → History Replay. The replay log is serialised with the save
		// (Game.Cos.cs restores ReplayData on load), so it survives the round trip.
		private static void SaveFinishedGame()
		{
			try { Game.Instance.SaveCos(Settings.Instance.AutoSavePath); }
			catch (Exception ex)
			{
				RuntimeHandler.Runtime.Log($"Final save failed: {ex.Message}");
			}
		}

		// Opens: CivilizationScore → GameReplay → HallOfFameScreen → quit()
		// Called from FinalScore.Closed (or conquest ft.Done for conquest path).
		internal static void ChainAfterFinal(int hofIdx, Action quit)
		{
			SaveFinishedGame();

			var score = new CivilizationScore();
			score.Closed += (s, a) =>
			{
				var replay = new GameReplay();
				replay.Closed += (s2, a2) =>
				{
					var hof = new HallOfFameScreen(hofIdx);
					hof.Closed += (s3, a3) => quit();
					Common.AddScreen(hof);
				};
				Common.AddScreen(replay);
			};
			Common.AddScreen(score);
		}

		// Full sequence entry point used by GameOver (loss/retire path):
		//   saves HoF → FinalScore → CivilizationScore → GameReplay → HallOfFameScreen → quit()
		internal static void Run(string victoryType, Action quit)
		{
			int hofIdx = SaveAndGetIndex(Game.Instance.HumanPlayer, victoryType);
			var final = new FinalScore(victoryType);
			final.Closed += (s, a) => ChainAfterFinal(hofIdx, quit);
			Common.AddScreen(final);
		}
	}
}
