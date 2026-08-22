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
	internal class Oracle : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"ANGKOR WAT, the great temple",
			"complex of the Khmer Empire,",
			"radiates spiritual authority",
			"across your civilization. Every",
			"Temple in your empire provides",
			"double the happiness bonus while",
			"this wonder stands.",
			"",
			"A place built to be answered in",
			"should be asked only what you",
			"would want answered.",
		};

		private static readonly string[] _page2 =
		{
			"Built by Khmer king Suryavarman II",
			"in the 12th century, Angkor Wat",
			"is the largest religious monument",
			"ever constructed. Dedicated first",
			"to Vishnu and later to the Buddha,",
			"its cosmological design maps the",
			"entire universe in stone, making",
			"it sacred to all who enter.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public Oracle() : base(30)
		{
			Name = "Angkor Wat";
			RequiredTech = new Mysticism();
			ObsoleteTech = new Religion();
			SetSmallIcon(5, 1);
			Type = Wonder.Oracle;
		}
	}
}
