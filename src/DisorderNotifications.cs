// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;

namespace CivOne
{
	// Civil disorder used to interrupt once per city per turn, forever: an art screen when a
	// city fell into disorder and a "Mayor flees in panic" advisor message every turn it
	// stayed there. Measured at 55,994 paced samples over one 23-minute game, second only to
	// the celebration art. A large empire in trouble could not be watched at all.
	//
	// The cities are collected here as their turns run and reported ONCE, by Player.NewTurn,
	// which the turn queue schedules after every city of that player has been processed.
	// The escalation events — marketplace burned, bank looted, government collapsed — are
	// deliberately NOT folded in: those are real losses, they are rare, and they are the
	// part worth interrupting for.
	//
	// Sibling of WLTKNotifications, which does the same job for celebrations. The two states
	// are mutually exclusive per city, so they are never both reporting the same place.
	internal static class DisorderNotifications
	{
		private static readonly List<string> _cities = new();

		public static IReadOnlyList<string> Cities => _cities;

		public static void Add(string cityName)
		{
			if (!_cities.Contains(cityName)) _cities.Add(cityName);
		}

		public static void Clear() => _cities.Clear();

		// How many names the digest prints before falling back to a count. A civ can hold
		// hundreds of cities in an autoplayed game, and the point is to be readable.
		public const int MaxNamed = 6;

		// "York, Bath, Leeds and 12 more" — the lines a Newspaper can print as-is.
		public static string[] Summary()
		{
			int n = _cities.Count;
			if (n == 0) return System.Array.Empty<string>();

			var lines = new List<string> { n == 1 ? "Civil disorder in:" : $"Civil disorder in {n} cities:" };
			int named = n <= MaxNamed ? n : MaxNamed;
			for (int i = 0; i < named; i += 3)
			{
				int take = System.Math.Min(3, named - i);
				lines.Add(string.Join(", ", _cities.GetRange(i, take)) + (i + take < named ? "," : ""));
			}
			if (n > named) lines.Add($"...and {n - named} more.");
			return lines.ToArray();
		}
	}
}
