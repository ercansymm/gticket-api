namespace GBILET.Core.Helpers;

public enum BookingStatusCategory
{
    Confirmed,
    Pending,
    Cancelled,
    Failed
}

public static class BookingStatusMapper
{
    public static BookingStatusCategory Categorize(string status, bool isFinalized)
    {
        if (isFinalized)
            return BookingStatusCategory.Confirmed;

        return status switch
        {
            "Ticketed" or "Paid" or "Confirmed" or "Finalized" or "Completed"
                => BookingStatusCategory.Confirmed,

            "Created" or "PreBooked" or "Booking" or "Reservation" or "Awaiting3DSecure" or "PaymentPending"
                => BookingStatusCategory.Pending,

            "Cancelled"
                => BookingStatusCategory.Cancelled,

            "PaymentFailed"
                => BookingStatusCategory.Failed,

            _ => BookingStatusCategory.Pending
        };
    }

    public static string CategoryToString(BookingStatusCategory category)
    {
        return category switch
        {
            BookingStatusCategory.Confirmed => "confirmed",
            BookingStatusCategory.Pending => "pending",
            BookingStatusCategory.Cancelled => "cancelled",
            BookingStatusCategory.Failed => "failed",
            _ => "pending"
        };
    }

    public static string[] GetRawStatusesForCategory(BookingStatusCategory category)
    {
        return category switch
        {
            BookingStatusCategory.Confirmed => new[]
            {
                "Ticketed", "Paid", "Confirmed", "Finalized", "Completed"
            },
            BookingStatusCategory.Pending => new[]
            {
                "Created", "PreBooked", "Booking", "Reservation",
                "Awaiting3DSecure", "PaymentPending"
            },
            BookingStatusCategory.Cancelled => new[] { "Cancelled" },
            BookingStatusCategory.Failed => new[] { "PaymentFailed" },
            _ => System.Array.Empty<string>()
        };
    }
}
