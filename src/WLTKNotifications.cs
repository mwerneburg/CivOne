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
	internal static class WLTKNotifications
	{
		private static readonly List<string> _cities = new List<string>();
		private static bool _dirty;

		public static IReadOnlyList<string> Cities => _cities;

		// True once per read — lets SideBar redraw only when the list changes.
		public static bool ConsumedDirty()
		{
			if (!_dirty) return false;
			_dirty = false;
			return true;
		}

		public static void Add(string cityName)
		{
			if (!_cities.Contains(cityName))
			{
				_cities.Add(cityName);
				_dirty = true;
			}
		}

		public static void Remove(string cityName)
		{
			if (_cities.Remove(cityName))
				_dirty = true;
		}

		public static void Clear()
		{
			if (_cities.Count == 0) return;
			_cities.Clear();
			_dirty = true;
		}
	}
}
