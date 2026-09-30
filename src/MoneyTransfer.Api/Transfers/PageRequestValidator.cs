using System.Globalization;

namespace MoneyTransfer.Api.Transfers;

public static class PageRequestValidator
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;
    private const int MaxCursorLength = 64;

    public static (PageRequest? Page, Dictionary<string, string[]> Errors) Validate(string? limit, string? cursor)
    {
        var errors = new Dictionary<string, string[]>();

        var parsedLimit = DefaultLimit;
        if (limit is not null)
        {
            if (!int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLimit))
            {
                errors["limit"] = ["Limit must be a whole number."];
            }
            else if (parsedLimit < 1 || parsedLimit > MaxLimit)
            {
                errors["limit"] = [$"Limit must be between 1 and {MaxLimit}."];
            }
        }

        long? afterId = null;
        if (cursor is not null)
        {
            if (cursor.Length > MaxCursorLength || !Cursor.TryDecode(cursor, out var decodedId))
            {
                errors["cursor"] = ["Cursor is invalid. Use the next_cursor value from a previous response."];
            }
            else
            {
                afterId = decodedId;
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new PageRequest(parsedLimit, afterId), errors);
    }
}