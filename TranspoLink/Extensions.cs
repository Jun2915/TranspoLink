using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

namespace TranspoLink;

public static class Extensions
{
    // --- Request Extensions ---
    public static bool IsAjax(this HttpRequest request)
    {
        return request.Headers.XRequestedWith == "XMLHttpRequest";
    }

    // --- ModelState Extensions ---
    public static bool IsValid(this ModelStateDictionary ms, string key)
    {
        return ms.GetFieldValidationState(key) == ModelValidationState.Valid;
    }

    // --- Date and Time Extensions ---
    public static DateOnly ToDateOnly(this DateTime dt)
    {
        return DateOnly.FromDateTime(dt);
    }

    public static TimeOnly ToTimeOnly(this DateTime dt)
    {
        return TimeOnly.FromDateTime(dt);
    }

    public static DateOnly Today(this DateOnly date)
    {
        return DateOnly.FromDateTime(DateTime.Today);
    }

    public static TimeOnly Now(this TimeOnly date)
    {
        return TimeOnly.FromDateTime(DateTime.Now);
    }

    // --- Session Extensions ---
    public static void Set<T>(this ISession session, string key, T value)
    {
        session.SetString(key, JsonSerializer.Serialize(value));
    }

    public static T? Get<T>(this ISession session, string key)
    {
        var value = session.GetString(key);
        return value == null ? default : JsonSerializer.Deserialize<T>(value);
    }
}