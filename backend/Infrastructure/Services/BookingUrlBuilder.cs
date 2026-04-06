namespace backend.Infrastructure.Services;

public static class BookingUrlBuilder
{
    public sealed record BookingSearchRequest(string TargetUrl, string LocationLabel, bool IsDirectBookingUrl);

    public static string BuildSearchUrl(string location, DateOnly checkIn, DateOnly checkOut, int adults, int kids, int rooms)
    {
        return BuildSearchRequest(location, checkIn, checkOut, adults, kids, rooms).TargetUrl;
    }

    public static BookingSearchRequest BuildSearchRequest(
        string location,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms)
    {
        if (IsBookingUrl(location))
        {
            var directTargetUrl = EnsureDirectBookingUrlQuery(location, checkIn, checkOut, adults, kids, rooms);
            return new BookingSearchRequest(
                directTargetUrl,
                ExtractLocationLabel(location),
                true);
        }

        var normalizedLocation = NormalizeLabel(location);
        var encodedLocation = Uri.EscapeDataString(normalizedLocation);
        var sanitizedAdults = Math.Max(1, adults);
        var sanitizedKids = Math.Max(0, kids);
        var sanitizedRooms = Math.Max(1, rooms);

        var targetUrl = $"https://www.booking.com/searchresults.html?ss={encodedLocation}&checkin={checkIn:yyyy-MM-dd}&checkout={checkOut:yyyy-MM-dd}&group_adults={sanitizedAdults}&no_rooms={sanitizedRooms}&group_children={sanitizedKids}&lang=es-co&selected_currency=COP";
        return new BookingSearchRequest(targetUrl, normalizedLocation, false);
    }

    public static bool IsBookingUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Host.Contains("booking.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string ExtractLocationLabel(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !uri.Host.Contains("booking.com", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeLabel(value);
        }

        var query = ParseQuery(uri.Query);
        if (query.TryGetValue("ss", out var ssValue) && !string.IsNullOrWhiteSpace(ssValue))
        {
            return NormalizeLabel(ssValue);
        }

        return uri.ToString();
    }

    private static Dictionary<string, string> ParseQuery(string rawQuery)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var query = rawQuery.TrimStart('?');
        if (string.IsNullOrWhiteSpace(query))
        {
            return values;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]).Replace('+', ' ') : string.Empty;
            values[key] = value;
        }

        return values;
    }

    private static string EnsureDirectBookingUrlQuery(
        string inputUrl,
        DateOnly checkIn,
        DateOnly checkOut,
        int adults,
        int kids,
        int rooms)
    {
        if (!Uri.TryCreate(inputUrl, UriKind.Absolute, out var uri))
        {
            return inputUrl;
        }

        var query = ParseQuery(uri.Query);
        query["checkin"] = checkIn.ToString("yyyy-MM-dd");
        query["checkout"] = checkOut.ToString("yyyy-MM-dd");
        query["group_adults"] = Math.Max(1, adults).ToString();
        query["group_children"] = Math.Max(0, kids).ToString();
        query["no_rooms"] = Math.Max(1, rooms).ToString();
        query["lang"] = "es-co";
        query["selected_currency"] = "COP";

        var queryString = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        var builder = new UriBuilder(uri)
        {
            Query = queryString
        };

        return builder.Uri.ToString();
    }

    private static string NormalizeLabel(string value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Unknown location";
        }

        return normalized;
    }
}
