// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using CivOne.Enums;

namespace CivOne.Wonders
{
	// The machines' mission, and it is not annihilation — it is renovation.
	//
	// Skynet does not breathe. A world with a breathable atmosphere is a world configured for
	// somebody else, and the Reprocessor is the correction: stacks and scrubbers running the
	// air through a chemistry that suits machines. Nobody is being attacked. That is the point.
	//
	// Unlike The Vessel this ends nothing. It changes the board everyone else has to live on:
	// the climate turns against organic life while the faction driving it is indifferent to
	// the result. The counterplay is the infrastructure a player usually skips — Mass Transit,
	// Hydro and Hoover, Recycling — plus the direct answer of taking the city off them.
	//
	// REVERSIBLE by design (Game.ReprocessorActive): the effect is recomputed from whether a
	// living Skynet still holds the city, not latched at completion. Take the city, or break
	// the machines, and the air stops getting worse. What already drowned stays drowned.
	internal class TheReprocessor : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"THE REPROCESSOR runs the sky",
			"through a chemistry that suits",
			"machines.",
			"",
			"The climate turns against every",
			"civilization that has to breathe.",
		};

		private static readonly string[] _page2 =
		{
			"Built only by the MACHINES.",
			"",
			"It stops the moment they no longer",
			"hold the city — but the coastline",
			"it has already taken does not come",
			"back.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public TheReprocessor() : base(60)
		{
			Name = "The Reprocessor";
			// No RequiredTech: Skynet wakes with the knowledge of the labs it seized.
			RequiredTech = null;
			ObsoleteTech = null;
			SetSmallIcon(1, 5);
			Type = Wonder.TheReprocessor;
		}
	}
}
