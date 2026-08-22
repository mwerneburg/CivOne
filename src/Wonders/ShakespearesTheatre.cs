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
	internal class ShakespearesTheatre : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"SHAKESPEARE'S THEATRE delights its",
			"city without end.",
			"",
			"No citizen in that city is ever",
			"UNHAPPY — it can never fall into",
			"disorder.",
			"",
			"The company will perform anything",
			"that is put in front of them.",
			"Anything at all.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MEDICINE.",
			"",
			"Build it in a city you mean to",
			"pack with citizens or drive hard",
			"in production, free of any worry",
			"about unrest.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public ShakespearesTheatre() : base(40)
		{
			Name = "Shakespeare's Theatre";
			RequiredTech = new Medicine();
			ObsoleteTech = new Electronics();
			SetSmallIcon(6, 1);
			Type = Wonder.ShakespearesTheatre;
		}
	}
}