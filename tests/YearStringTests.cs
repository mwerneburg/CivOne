// CivOne tests
//
// The date string, which reaches almost everything: report headers, save names, the Hall of
// Fame, the Space Race footer, every advisor message that quotes a year. Twenty-two call
// sites in src alone, so a change to its shape is a change to the whole game's voice.

namespace CivOne.Tests
{
	public class YearStringTests
	{
		// Turn 0 is 4000 years before year 1 — the era label is the thing under test.
		[Theory]
		[InlineData(0,   "4000 BCE")]
		[InlineData(100, "2000 BCE")]
		[InlineData(199, "20 BCE")]
		public void YearsBeforeTheCommonEraUseBCE(ushort turn, string expected)
		{
			Assert.Equal(expected, Common.YearString(turn));
		}

		// Nothing may still say BC.
		[Theory]
		[InlineData(0)]
		[InlineData(50)]
		[InlineData(150)]
		public void NothingSaysBCAnyMore(ushort turn)
		{
			string s = Common.YearString(turn);

			Assert.EndsWith("BCE", s);
			Assert.DoesNotContain(" BC ", s);
		}

		// AD is deliberately unchanged — see the comment in Common.YearString. Pinned so the
		// pairing stays a decision rather than drifting halfway.
		[Theory]
		[InlineData(200, "1 AD")]
		[InlineData(400, "1850 AD")]
		[InlineData(750, "2200 AD")]
		public void YearsAfterItStillUseAD(ushort turn, string expected)
		{
			Assert.Equal(expected, Common.YearString(turn));
		}

		// The turn/year mapping itself must not move: 750 is 2200 AD, which is the backstop
		// ending, and 650 is 2100, which is the score ending. Both are load-bearing.
		[Fact]
		public void TheEndgameTurnsStillLandOnTheirYears()
		{
			Assert.Equal(2200, Common.TurnToYear(750));
			Assert.Equal(2100, Common.TurnToYear(650));
		}
	}
}
