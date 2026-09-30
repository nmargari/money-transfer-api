using Microsoft.EntityFrameworkCore;
using MoneyTransfer.Api.Data;

namespace MoneyTransfer.Api.Transfers;

public class TransferHistoryService(AppDbContext db)
{
    public async Task<TransferHistoryPage?> GetPageAsync(string customerId, string accountId, PageRequest page, CancellationToken cancellationToken)
    {
        var ownsAccount = await db.Accounts.AnyAsync(a => a.Id == accountId && a.CustomerId == customerId, cancellationToken);

        if (!ownsAccount)
        {
            return null;
        }

        var query = db.Transfers.Where(t => t.SourceAccountId == accountId || t.DestinationAccountId == accountId);

        if (page.AfterId is long afterId)
        {
            query = query.Where(t => t.Id < afterId);
        }

        var items = await query.OrderByDescending(t => t.Id)
                               .Take(page.Limit + 1)
                               .Select(t => new TransferHistoryItem(t.Id,
                                                                    t.SourceAccountId == accountId ? "outgoing" : "incoming",
                                                                    t.SourceAccountId,
                                                                    t.DestinationAccountId,
                                                                    t.Amount,
                                                                    t.Currency,
                                                                    t.CreatedAt))
                               .ToListAsync(cancellationToken);

        var hasMore = items.Count > page.Limit;
        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore ? Cursor.Encode(items[^1].Id) : null;

        return new TransferHistoryPage(items, nextCursor);
    }
}
