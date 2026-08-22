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
	internal class IsaacNewtonsCollege : BaseWonder
	{
		private static readonly string[] _page1 =
		{
			"ISAAC NEWTON'S COLLEGE crowns your",
			"city as a seat of science.",
			"",
			"It greatly increases the SCIENCE",
			"that city produces.",
			"",
			"Most of what he wrote was not",
			"about motion, and the college",
			"keeps all of it.",
		};

		private static readonly string[] _page2 =
		{
			"Requires THEORY OF GRAVITY.",
			"",
			"Pair it with COPERNICUS'",
			"OBSERVATORY in a city full of",
			"libraries to race through the",
			"tech tree.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public IsaacNewtonsCollege() : base(40)
		{
			Name = "Isaac Newton's College";
			RequiredTech = new TheoryOfGravity();
			ObsoleteTech = new NuclearFission();
			SetSmallIcon(6, 2);
			Type = Wonder.IsaacNewtonsCollege;
		}
	}
}