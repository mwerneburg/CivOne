// CivOne tests
//
// The Scavengers were added to VisitorArchetype after the contact screens were written, and
// every one of those screens branches with a `switch` that has a `default:` labelled
// "Refugees". A new enum member therefore inherits the Olvir's prose in silence — no compiler
// warning, no crash, nothing to notice until a player mid-game is shown the correct Scavenger
// art and then told a damaged refugee ship is asking permission to land. That happened.
//
// So this does not test the new text. It tests that no archetype shares another's words —
// the property that was violated — and it enumerates the enum rather than listing archetypes,
// so the NEXT one added fails here instead of in somebody's 1635 AD game.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CivOne.Enums;
using CivOne.Screens;

namespace CivOne.Tests
{
	public class ContactTextCoverageTests
	{
		// Every archetype a game can actually draw. None is the pre-signal placeholder and has
		// no visitors to describe, so it is excluded rather than given text.
		private static IEnumerable<VisitorArchetype> Drawable =>
			Enum.GetValues(typeof(VisitorArchetype)).Cast<VisitorArchetype>()
				.Where(a => a != VisitorArchetype.None);

		private static string Render(Type screen, string method, params object[] args)
		{
			string[] lines = (string[])screen
				.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
				.Invoke(null, args)!;
			return string.Join("\n", lines);
		}

		// `alsoDistinctFromNone`: where a screen's `default:` is a generic no-op ("no new
		// interpretable data") rather than the Refugee text, an archetype can fall through it
		// and still be unique — distinctness alone does not prove it was given words of its
		// own. Removing the Scavenger case from TauCetiApproachWarning passed this file until
		// the None comparison was added. Screens whose default IS the Refugee text cannot use
		// it: there, Refugees and None legitimately render the same.
		private static void AssertAllDistinct(string what, Func<VisitorArchetype, string> render,
		                                      bool alsoDistinctFromNone = false)
		{
			string fallback = alsoDistinctFromNone ? render(VisitorArchetype.None) : null!;
			var byText = new Dictionary<string, VisitorArchetype>();
			foreach (VisitorArchetype arch in Drawable)
			{
				string text = render(arch);
				Assert.False(string.IsNullOrWhiteSpace(text), $"{what}: {arch} produced nothing");
				if (alsoDistinctFromNone && text == fallback)
					Assert.Fail($"{what}: {arch} fell through to the generic fallback");
				if (byText.TryGetValue(text, out VisitorArchetype twin))
					Assert.Fail($"{what}: {arch} is being described with {twin}'s words");
				byText[text] = arch;
			}
		}

		// The probe's own report, at every outcome tier: lost, partial, identified, tech
		// transfer, pact. Tier 2 upward names the visitors outright, so a shared block there is
		// not a shade of flavour — it is the wrong species named on screen.
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(3)]
		[InlineData(4)]
		public void EveryArchetypeGetsItsOwnProbeReport(int tier)
		{
			Sim.EnsureRuntime();
			AssertAllDistinct($"probe result tier {tier}", arch =>
				Render(typeof(ProbeResultTransmission), "BuildLines",
					"1635 AD", arch, tier, Array.Empty<string>()));
		}

		[Fact]
		public void EveryArchetypeGetsItsOwnApproachWarning()
		{
			Sim.EnsureRuntime();
			AssertAllDistinct("Tau Ceti approach", arch =>
				Render(typeof(TauCetiApproachWarning), "BuildLines", "1635 AD", arch, true, 4),
				alsoDistinctFromNone: true);
		}

		// The one that stings most: finish humanity's defence against the harvest and be
		// congratulated on a peaceful refugee landing.
		[Fact]
		public void EveryArchetypeGetsItsOwnDomeOutcome()
		{
			Sim.EnsureRuntime();
			AssertAllDistinct("dome complete", arch =>
				Render(typeof(DomeCompleteTransmission), "BuildLines", "1635 AD", arch, true, true));
		}
	}
}
