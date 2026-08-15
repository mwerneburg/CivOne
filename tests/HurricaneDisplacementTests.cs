// CivOne tests
//
// What the advisor says when a storm drives people out.
//
// It used to say "Pop -1." — a ledger entry for the one event in the game that actually
// displaces a population, and the size change is already visible on the city itself. Now the
// message says what it meant, scaled by the SHARE of the city lost: losing two from a town of
// four and two from a metropolis of twenty are not the same event.
//
// The share is the point. The raw number is what the old line reported and what made it
// useless.

using System.Linq;

namespace CivOne.Tests
{
	public class HurricaneDisplacementTests
	{
		[Fact]
		public void NoLossSaysNothing()
		{
			Assert.Empty(City.DisplacementText(sizeBefore: 8, displaced: 0));
			Assert.Empty(City.DisplacementText(sizeBefore: 0, displaced: 0));
		}

		// A nick out of a large city is the lower town, not a catastrophe.
		[Fact]
		public void ASmallShareIsTheLowerTown()
		{
			string[] text = City.DisplacementText(sizeBefore: 20, displaced: 2);

			Assert.Contains("lower town", string.Join(" ", text));
		}

		// The same two people out of a town of four is half the place.
		[Fact]
		public void TheSameLossFromASmallTownIsHalfOfIt()
		{
			string[] text = City.DisplacementText(sizeBefore: 4, displaced: 2);

			Assert.Contains("Half the city", string.Join(" ", text));
		}

		// The middle band exists, or the scaling is just a threshold.
		[Fact]
		public void AQuarterIsItsOwnDegree()
		{
			string[] text = City.DisplacementText(sizeBefore: 12, displaced: 3);

			Assert.Contains("quarters", string.Join(" ", text));
		}

		// Three distinct messages, or the scaling carries no information.
		[Fact]
		public void TheThreeDegreesReadDifferently()
		{
			string small  = string.Join(" ", City.DisplacementText(20, 1));
			string medium = string.Join(" ", City.DisplacementText(20, 6));
			string large  = string.Join(" ", City.DisplacementText(20, 12));

			Assert.Equal(3, new[] { small, medium, large }.Distinct().Count());
		}

		// The old ledger line must not come back.
		[Fact]
		public void ItNeverJustReportsTheNumber()
		{
			foreach ((int before, int lost) in new[] { (20, 1), (20, 6), (20, 12), (4, 2), (2, 1) })
			{
				string text = string.Join(" ", City.DisplacementText(before, lost));
				Assert.DoesNotContain("Pop -", text);
				Assert.DoesNotContain(lost.ToString(), text);
			}
		}

		// Advisor lines are drawn without wrapping, same as every other message in the game.
		[Fact]
		public void EveryLineFitsTheAdvisorBox()
		{
			foreach ((int before, int lost) in new[] { (20, 1), (20, 6), (20, 12), (4, 2), (1, 1) })
			foreach (string line in City.DisplacementText(before, lost))
				Assert.True(line.Length <= 44, $"{line.Length} chars — \"{line}\"");
		}
	}
}
