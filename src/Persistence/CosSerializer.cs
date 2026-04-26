// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CivOne.Persistence
{
	internal static class CosSerializer
	{
		private static readonly ISerializer _serializer =
			new SerializerBuilder()
				.WithNamingConvention(PascalCaseNamingConvention.Instance)
				.ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
				.Build();

		private static readonly IDeserializer _deserializer =
			new DeserializerBuilder()
				.WithNamingConvention(PascalCaseNamingConvention.Instance)
				.IgnoreUnmatchedProperties()
				.Build();

		internal static string Serialize(CosFile data) => _serializer.Serialize(data);

		internal static CosFile Deserialize(string yaml) => _deserializer.Deserialize<CosFile>(yaml);

		internal static CosMeta DeserializeMeta(string yaml) => _deserializer.Deserialize<CosFile>(yaml)?.Meta;
	}
}
