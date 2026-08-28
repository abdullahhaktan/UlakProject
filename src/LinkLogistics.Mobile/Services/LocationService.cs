namespace LinkLogistics.Mobile.Services;

public sealed record CapturedLocation(decimal Latitude, decimal Longitude);

public sealed class LocationService
{
    public async Task<CapturedLocation?> TryGetAsync(CancellationToken ct)
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                return null;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)), ct)
                ?? await Geolocation.Default.GetLastKnownLocationAsync();

            return location is null
                ? null
                : new CapturedLocation((decimal)location.Latitude, (decimal)location.Longitude);
        }
        catch
        {
            return null;
        }
    }
}
