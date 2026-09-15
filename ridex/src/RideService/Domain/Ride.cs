namespace RideService.Domain;

public enum RideStatus { Requested = 1, Accepted, InProgress, Completed, Cancelled }

public class Ride
{
    public Guid        Id          { get; private set; }
    public Guid        UserId      { get; private set; }
    public Guid?       DriverId    { get; private set; }
    public double      PickupLat   { get; private set; }
    public double      PickupLon   { get; private set; }
    public double      DestLat     { get; private set; }
    public double      DestLon     { get; private set; }
    public string      Preference  { get; private set; } = string.Empty;
    public RideStatus  Status      { get; private set; }
    public decimal?    Fare        { get; private set; }
    public DateTime    CreatedAt   { get; private set; }
    public DateTime?   AcceptedAt  { get; private set; }
    public DateTime?   StartedAt   { get; private set; }
    public DateTime?   CompletedAt { get; private set; }

    private Ride() { }

    public static Ride Create(Guid userId, double pLat, double pLon,
                              double dLat, double dLon, string pref) => new()
    {
        Id         = Guid.NewGuid(),
        UserId     = userId,
        PickupLat  = pLat, PickupLon = pLon,
        DestLat    = dLat, DestLon   = dLon,
        Preference = pref,
        Status     = RideStatus.Requested,
        CreatedAt  = DateTime.UtcNow
    };

    public void AcceptByDriver(Guid driverId)
    {
        EnsureStatus(RideStatus.Requested, "accept");
        DriverId = driverId; Status = RideStatus.Accepted; AcceptedAt = DateTime.UtcNow;
    }

    public void Start()
    {
        EnsureStatus(RideStatus.Accepted, "start");
        Status = RideStatus.InProgress; StartedAt = DateTime.UtcNow;
    }

    public void Complete(decimal fare)
    {
        EnsureStatus(RideStatus.InProgress, "complete");
        Status = RideStatus.Completed; Fare = fare; CompletedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is RideStatus.Completed or RideStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel a {Status} ride.");
        Status = RideStatus.Cancelled;
    }

    private void EnsureStatus(RideStatus required, string action)
    {
        if (Status != required)
            throw new InvalidOperationException($"Cannot {action} a {Status} ride.");
    }
}
