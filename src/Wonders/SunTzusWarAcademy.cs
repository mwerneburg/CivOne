// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Advances;
using CivOne.Enums;

namespace CivOne.Wonders
{
	internal class SunTzusWarAcademy : BaseWonder
	{
		public SunTzusWarAcademy() : base(20)
		{
			Name = "Sun Tzu's War Academy";
			RequiredTech = new Feudalism();
			ObsoleteTech = new Gunpowder();
			SetSmallIcon(5, 5);
			Type = Wonder.SunTzusWarAcademy;
		}
	}
}
