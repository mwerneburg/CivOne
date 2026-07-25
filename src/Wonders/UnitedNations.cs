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
	internal class UnitedNations : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The UNITED NATIONS gives you a",
			"voice among all nations.",
			"",
			"Rival civilizations grow far more",
			"willing to make peace, and slower",
			"to make war on you.",
		};

		private static readonly string[] _page2 =
		{
			"Requires COMMUNISM.",
			"",
			"A shield of diplomacy for a",
			"peaceful or trade-focused empire —",
			"it buys the quiet you need to",
			"build.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public UnitedNations() : base(60)
		{
			Name = "United Nations";
			RequiredTech = new Communism();
			ObsoleteTech = null;
			SetSmallIcon(7, 3);
			Type = Wonder.UnitedNations;
		}
	}
}