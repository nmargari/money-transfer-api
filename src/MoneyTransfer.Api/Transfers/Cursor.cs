using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace MoneyTransfer.Api.Transfers;

public static class Cursor
{
    public static string Encode(long transferId) => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(transferId.ToString(CultureInfo.InvariantCulture)));

    public static bool TryDecode(string value, out long transferId)
    {
        transferId = 0;
        try
        {
            var text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));

            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out transferId) && transferId > 0;
        }
        catch(FormatException)
        {
            return false;
        }
    }
}
