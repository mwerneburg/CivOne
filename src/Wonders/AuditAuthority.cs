#nullable enable
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
	// Acts as a second capital for corruption purposes: the host city is
	// corruption-free, and every other city of the builder uses
	// min(distance-to-Palace, distance-to-Audit-Authority) when computing the
	// distance term. See City.Corruption.
	internal class AuditAuthority : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"The AUDIT AUTHORITY anchors a second",
			"seat of oversight far from the",
			"capital. Treat its host city as if",
			"the Palace stood there: corruption",
			"in surrounding cities is calculated",
			"from whichever centre is closer.",
		};

		private static readonly string[] _page2 =
		{
			"Distance breeds graft. Where the",
			"capital's auditors cannot reach,",
			"ledgers thin and clerks grow rich.",
			"The Authority cures this by simply",
			"being elsewhere — a parallel",
			"institution with the same teeth,",
			"stationed where the empire most",
			"needs an honest accountant.",
		};

		public override string[] GetPageText(byte pageNumber)
			=> pageNumber == 1 ? _page1 : _page2;

		public AuditAuthority() : base(30)
		{
			Name = "Audit Authority";
			RequiredTech = new CodeOfLaws();
			ObsoleteTech = null;
			SetSmallIcon(4, 6);
			Type = Wonder.AuditAuthority;
		}
	}
}
