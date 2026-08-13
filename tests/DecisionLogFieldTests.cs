// CivOne tests
//
// The city_prod record has to carry the gates on the food-first rule, or the next run leaves
// the same question open the last one did.
//
// The 2200 AD run showed 1,011 production decisions by cities of size <= 6 with food income
// <= 0 choosing Caravan (14%), Diplomat (12%) and Colosseum (10%), against Granary 2% and
// Harbour 0%. Nothing in the record could distinguish "already had one" from "landlocked"
// from "capped without an Aqueduct" from "no Pottery" — so the finding stalled at inference.
//
// A missing field here fails silently: the run completes, the file looks fine, and the answer
// simply is not in it. That is a week of playing time, so the fields are demanded.

using System.Linq;

namespace CivOne.Tests
{
	public class DecisionLogFieldTests
	{
		private static string LoggerSource()
		{
			var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
			while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "CivOne.csproj")))
				dir = dir.Parent;
			Assert.NotNull(dir);
			return System.IO.File.ReadAllText(
				System.IO.Path.Combine(dir!.FullName, "src", "DecisionLogger.cs"));
		}

		[Theory]
		[InlineData("has_granary")]
		[InlineData("has_harbour")]
		[InlineData("coastal")]
		[InlineData("growth_blocked")]
		[InlineData("disorder")]
		[InlineData("pottery")]
		public void TheCityProductionRecordCarriesTheFoodGates(string field)
		{
			string src = LoggerSource();
			// Anchored on the KV entry, not the bare string: "city_prod" also appears in the
			// schema comment at the top of the file, and matching that took the substring from
			// the header and found a closing "}));" belonging to some other record — which is
			// how the first version of this test failed against code that was perfectly correct.
			int at = src.IndexOf("\"city_prod\"),");
			Assert.True(at > 0, "the city_prod record has moved or been rewritten");
			// The record ends at the closing of its Enqueue(Fmt(new[] { ... })) block.
			string record = src.Substring(at, src.IndexOf("}));", at) - at);

			Assert.Contains($"KV(\"{field}\"", record);
		}

		// The schema comment at the top of the file is the only documentation of this format,
		// and a record that drifts from it is worse than one with no comment at all.
		[Theory]
		[InlineData("has_granary")]
		[InlineData("has_harbour")]
		[InlineData("growth_blocked")]
		[InlineData("pottery")]
		public void TheDocumentedSchemaMatchesTheRecord(string field)
		{
			string src = LoggerSource();
			string header = src.Substring(0, src.IndexOf("namespace CivOne"));

			Assert.Contains(field, header);
		}
	}
}
