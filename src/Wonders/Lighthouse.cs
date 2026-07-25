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
	internal class Lighthouse : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The LIGHTHOUSE guides your ships",
			"through dangerous waters.",
			"",
			"Your naval units sail the open sea",
			"safely and put out as VETERANS.",
		};

		private static readonly string[] _page2 =
		{
			"Requires MAP MAKING.",
			"",
			"But a light that burns too long",
			"over the wrong waters may wake the",
			"LEVIATHAN from the deep.",
			"",
			"Obsolete once MAGNETISM brings the",
			"compass.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Lighthouse() : base(20)
		{
			Name = "Lighthouse";
			RequiredTech = new MapMaking();
			ObsoleteTech = new Magnetism();
			SetSmallIcon(4, 4);
			Type = Wonder.Lighthouse;
		}
	}
}