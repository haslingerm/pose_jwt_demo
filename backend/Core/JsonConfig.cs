using System.Text.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace JwtDemo.Core;

public static class JsonConfig
{
    public static void ConfigureJsonSerialization(JsonSerializerOptions options, bool isDev)
    {
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.PropertyNameCaseInsensitive = true;
        // a missing 'required' property or null for a non-nullable property is rejected with 400 Bad Request
        options.RespectNullableAnnotations = true;
        options.RespectRequiredConstructorParameters = true;
        options.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter());
        options.WriteIndented = isDev;
        options.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
    }
}
