#nullable enable
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
			};
			var list = Load();
			list.Add(entry);
			list.Sort((a, b) => b.Score.CompareTo(a.Score));
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
				File.WriteAllLines(FilePath, list.ConvertAll(
					e => $"{e.LeaderName}|{e.TribeName}|{e.Score}|{e.Victory}|{e.Year}"));
			}
			catch (System.Exception ex)
			{
				RuntimeHandler.Runtime.Log($"Hall of Fame save failed: {ex.GetType().Name}: {ex.Message}");
			}
			return entry;
		}
	}
}