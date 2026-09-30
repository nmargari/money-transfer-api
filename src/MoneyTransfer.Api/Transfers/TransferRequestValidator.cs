namespace MoneyTransfer.Api.Transfers;

public static class TransferRequestValidator
{
    private const int MaxAccountIdLength = 64;
    private const int MaxIdempotencyKeyLength = 255;

    public static (TransferCommand? Command, Dictionary<string, string[]> Errors) Validate(CreateTransferRequest request, string? idempotencyKey)
    {
        var errors = new Dictionary<string, string[]>();

        if(string.IsNullOrWhiteSpace(idempotencyKey))
        {
            errors["Idempotency-Key"] = ["The Idempotency-Key header is required."];
        }
        else if(idempotencyKey.Length > MaxIdempotencyKeyLength)
        {
            errors["Idempotency-Key"] = [$"The Idempotency-Key header must be at most {MaxIdempotencyKeyLength} characters."];
        }

        ValidateAccountId(request.SourceAccountId, "source_account_id", errors);
        ValidateAccountId(request.DestinationAccountId, "destination_account_id", errors);

        if(request.Amount is null)
        {
            errors["amount"] = ["Amount is required."];
        }
        else if(request.Amount <= 0)
        {
            errors["amount"] = ["Amount must be greater than zero."];
        }

        if(request.Currency is null)
        {
            errors["currency"] = ["Currency is required."];
        }
        else if(request.Currency is not { Length : 3 } || !request.Currency.All(char.IsAsciiLetterUpper))
        {
            errors["currency"] = ["Currency must be a 3-letter uppercase ISO 4217 code."];
        }

        if(request.SourceAccountId is not null && request.SourceAccountId == request.DestinationAccountId)
        {
            errors["destination_account_id"] = ["Destination account must be different from the source account."];
        }

        if(errors.Count > 0)
        {
            return (null, errors);
        }

        var command = new TransferCommand(request.SourceAccountId!,
                                          request.DestinationAccountId!,
                                          request.Amount!.Value,
                                          request.Currency!);

        return (command, errors);
    }

    private static void ValidateAccountId(string? value, string field, Dictionary<string, string[]> errors)
    {
        if(string.IsNullOrWhiteSpace(value))
        {
            errors[field] = ["Account ID is required."];
        }
        else if(value.Length > MaxAccountIdLength)
        {
            errors[field] = [$"Account ID must be at most {MaxAccountIdLength} characters"];
        }
    }
}