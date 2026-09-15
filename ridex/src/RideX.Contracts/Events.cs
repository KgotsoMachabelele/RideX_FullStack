namespace RideX.Contracts.Events;

public record RideRequestedMessage(
    Guid RideId, Guid UserId,
    double PickupLat, double PickupLon,
    double DestLat, double DestLon,
    string Preference, DateTime RequestedAt);

public record RideAcceptedMessage(
    Guid RideId, Guid DriverId,
    double PickupLat, double PickupLon);

public record RideCompletedMessage(
    Guid RideId, Guid UserId, Guid DriverId,
    decimal Fare, DateTime CompletedAt);
