using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradeJournal.Api.Tests;

internal static class JsonHelpers
{
	public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() },
	};
}
