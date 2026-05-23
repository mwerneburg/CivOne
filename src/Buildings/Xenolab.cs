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

namespace CivOne.Buildings
{
	internal class Xenolab : BaseBuilding
	{
		public Xenolab() : base(12, 2)
		{
			Name = "Xenolab";
			RequiredTech = new Xenobiology();
			Type = Building.Xenolab;
		}
	}
}
