// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Collections.Generic;
using System.Linq;
using CivOne.Enums;
using CivOne.Leaders;

namespace CivOne.Civilizations
{
	public abstract class BaseCivilization<T> : BaseCivilization, ICivilization where T : ILeader, new()
	{
		public int Id { get; }

		private string _name = null!;
		public string Name
		{
			get => Modifications.LastOrDefault(x => x.Name.HasValue)?.Name.Value.Name ?? _name;
			private set => _name = value;
		}

		private string _namePlural = null!;
		public string NamePlural
		{
			get => Modifications.LastOrDefault(x => x.Name.HasValue)?.Name.Value.Plural ?? _namePlural;
			private set => _namePlural = value;
		}

		private ILeader _leader = null!;
		public ILeader Leader
		{
			get => Modifications.LastOrDefault(x => x.LeaderId.HasValue)?.LeaderId.Value.ToInstance() ?? _leader;
			private set => _leader = value;
		}
		
		public byte PreferredPlayerNumber { get; }

		private byte _startX;
		public byte StartX
		{
			get => (byte)(Modifications.LastOrDefault(x => x.StartingPosition.HasValue)?.StartingPosition.Value.X ?? _startX);
			protected set => _startX = value;
		}

		private byte _startY;
		public byte StartY
		{
			get => (byte)(Modifications.LastOrDefault(x => x.StartingPosition.HasValue)?.StartingPosition.Value.Y ?? _startY);
			protected set => _startY = value;
		}

		private string[] _cityNames = null!;
		public string[] CityNames
		{
			get => Modifications.LastOrDefault(x => x.CityNames.HasValue)?.CityNames.Value ?? _cityNames;
			protected set => _cityNames = value;
		}

		public BaseCivilization(Civilization civilization, string name, string namePlural) : base(civilization)
		{
			Id = (Civilization == Civilization.Barbarians ? 15 : (int)Civilization);
			// Barbarians, Olvir and TheOthers share slot 0 (the latter two join mid-game via
			// AddPlayer, never at setup); original civs (1–14) cycle slots 1–7 twice,
			// creating buddy pairs for pre-0AD respawns. Extended civs (17–26) occupy
			// exclusive slots 8–17 with no buddy — enum value N maps to slot N−9.
			PreferredPlayerNumber = (byte)(
				Civilization == Civilization.Barbarians || Civilization == Civilization.Olvir
					|| Civilization == Civilization.TheOthers ? 0 :
				(int)Civilization >= 17 && (int)Civilization <= 26 ? (int)Civilization - 9 :
				((int)Civilization - 1) % 7 + 1);
			Name = name;
			NamePlural = namePlural;
			Leader = new T();
		}
	}

	public abstract class BaseCivilization : BaseInstance
	{
		protected Civilization Civilization { get; }

		private static Dictionary<Civilization, List<CivilizationModification>> _modifications = new Dictionary<Civilization, List<CivilizationModification>>();
		internal static void LoadModifications()
		{
			_modifications.Clear();

			CivilizationModification[] modifications = Reflect.GetModifications<CivilizationModification>().ToArray();
			if (modifications.Length == 0) return;

			Log("Applying civilization modifications");

			foreach (CivilizationModification modification in modifications)
			{
				if (!_modifications.ContainsKey(modification.Civilization))
					_modifications.Add(modification.Civilization, new List<CivilizationModification>());
				_modifications[modification.Civilization].Add(modification);
			}

			Log("Finished applying civilization modifications");
		}
		public IEnumerable<CivilizationModification> Modifications => _modifications.ContainsKey(Civilization) ? _modifications[Civilization].ToArray() : new CivilizationModification[0];

		protected BaseCivilization(Civilization civilization)
		{
			Civilization = civilization;
		}
	}
}