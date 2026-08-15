// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.IO;

namespace CivOne.Persistence
{
	internal class HofEntry
	{
		internal string LeaderName = null!;
		internal string TribeName = null!;
		internal int    Score;
		internal string Victory = null!;
		internal string Year = null!;
		// The decision log's game id (DecisionLogger.GameId), so one game leaves one entry.
		// Empty for entries written before this field existed.
		internal string GameId = string.Empty;
	}

	internal static class HallOfFame
	{
		private static string FilePath => Path.Combine(Settings.Instance.SavesDirectory, "civone.hof");

		internal static List<HofEntry> Load()
		{
			var list = new List<HofEntry>();
			if (!File.Exists(FilePath)) return list;
			foreach (string line in File.ReadAllLines(FilePath))
			{
				string[] parts = line.Split('|');
				if (parts.Length < 5) continue;
				if (!int.TryParse(parts[2], out int score)) continue;
				list.Add(new HofEntry
				{
					LeaderName = parts[0],
					TribeName  = parts[1],
					Score      = score,
					Victory    = parts[3],
					Year       = parts[4],
					// Sixth field, added later: older files have five and load fine.
					GameId     = parts.Length > 5 ? parts[5] : string.Empty,
				});
			}
			return list;
		}

		internal static HofEntry AddAndSave(Player player, string victory, string year)
		{
			var entry = new HofEntry
			{
				LeaderName = player.LeaderName,
				TribeName  = player.TribeNamePlural,
				Score      = player.Score,
				Victory    = victory,
				Year       = year,
				GameId     = DecisionLogger.GameId ?? string.Empty,
			};
			var list = Load();
			// One entry per GAME. A game can reach an ending more than once — a milestone
			// ending, then a later one; or a save reloaded and finished again — and each
			// call appended another row, so one run could fill the table with near-copies
			// of itself. The game id survives save/load (see DecisionLogger.BeginGame),
			// which is what makes it usable as the key here.
			//
			// Guarded on a NON-EMPTY id: entries written before this field existed all carry
			// "", and matching on that would collapse every historical run into one row.
			if (!string.IsNullOrEmpty(entry.GameId))
				list.RemoveAll(e => e.GameId == entry.GameId);
			list.Add(entry);
			list.Sort((a, b) => b.Score.CompareTo(a.Score));
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllLines(FilePath, list.ConvertAll(
					e => $"{e.LeaderName}|{e.TribeName}|{e.Score}|{e.Victory}|{e.Year}|{e.GameId}"));
			}
			catch (System.Exception ex)
			{
				RuntimeHandler.Runtime.Log($"Hall of Fame save failed: {ex.GetType().Name}: {ex.Message}");
			}
			return entry;
		}
	}
}