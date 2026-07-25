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
	internal class CopernicusObservatory : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"COPERNICUS' OBSERVATORY turns its",
			"city into a seat of learning.",
			"",
			"It greatly increases the SCIENCE",
			"that city produces.",
		};

		private static readonly string[] _page2 =
		{
			"Requires ASTRONOMY.",
			"",
			"Build it in a large city with a",
			"LIBRARY and UNIVERSITY, then add",
			"ISAAC NEWTON'S COLLEGE for a",
			"research powerhouse.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public CopernicusObservatory() : base(30)
		{
			Name = "Copernicus' Observatory";
			RequiredTech = new Astronomy();
			ObsoleteTech = null;
			SetSmallIcon(6, 0);
			Type = Wonder.CopernicusObservatory;
		}
	}
}