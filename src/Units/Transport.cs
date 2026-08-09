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

namespace CivOne.Units
{
	internal class Transport : BaseUnitSea, IBoardable
	{
		public int Cargo
		{
			get
			{
				return 8;
			}
		}

		public override void NewTurn()
		{
			base.NewTurn();
			if (Game.GetPlayer(Owner).HasAdvance<Combustion>())
				MovesLeft += 2;
		}

		private static readonly string[] _page1 =
		{
			"The TRANSPORT carries 8 land units",
			"across any ocean.",
			"",
			"It cannot fight at all.",
		};

		private static readonly string[] _page2 =
		{
			"Requires INDUSTRIALIZATION.",
			"Needs OIL: +50% shields without.",
			"",
			"A whole invasion fits in one hull,",
			"and one lucky submarine can drown",
			"it. Escort transports always.",
			"",
			"Units aboard cannot defend",
			"themselves at sea; the ship's",
			"loss is theirs.",
		};

		public override string[] GetPageText(byte pageNumber) => pageNumber == 1 ? _page1 : _page2;

		public Transport() : base(5, 0, 3, 4)
		{
			Type = UnitType.Transport;
			Name = "Transport";
			RequiredTech = new Industrialization();
			ObsoleteTech = null;
			SetIcon('A', 0, 2);
		}
	}
}