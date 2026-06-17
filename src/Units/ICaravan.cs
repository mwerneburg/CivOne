// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace CivOne.Units
{
	// Trade unit shared behaviour. Implemented by the land Caravan and the
	// sea-faring SeaCaravan so the CaravanChoice dialog can drive either one.
	internal interface ICaravan : IUnit
	{
		void KeepMoving(City city);
		void EstablishTradeRoute(City city);
		void HelpBuildWonder(City city);
	}
}
