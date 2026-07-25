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
using CivOne.Graphics;

namespace CivOne.Buildings
{
	internal class PowerPlant : BaseBuilding
	{
		private static Picture? _iconCache = null;
		
		private static readonly string[] _page1 =
		{
			"A POWER PLANT lets a FACTORY add",
			"100% to shield production instead",
			"of 50%.",
		};

		private static readonly string[] _page2 =
		{
			"Requires REFINING.",
			"",
			"The dirtiest of the three plants:",
			"it does nothing to reduce",
			"pollution, and coal smoke is",
			"added to the factory's own.",
			"",
			"Replace it with a HYDRO or NUCLEAR",
			"plant when you can.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public PowerPlant() : base(16, 4)
		{
			Name = "Power Plant";
			RequiredTech = new Refining();
			if (_iconCache is null)
			{
				SetIcon(4, 1, false);
				Picture icon = new Picture(52, 50, Icon.Palette);
				icon.AddLayer(Icon.Crop(31, 0, 20, 50), 1);
				icon.AddLayer(Icon.Crop(0, 0, 32, 50), 19);
				icon.FillRectangle(50, 0, 2, 50, 0);
				_iconCache = icon;
			}
			Icon = _iconCache;
			SetSmallIcon(3, 3);
			// TODO: Fix icon in patch, should be: SetSmallIcon(3, 4);
			Type = Building.PowerPlant;
		}
	}
}