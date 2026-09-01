namespace Ulak.Core.Domain;

public static class UserRoles
{
    public const string Driver = "Driver";

    /// <summary>Firm owner/manager: web panel + mobile, manages drivers, sees all records.</summary>
    public const string Admin = "Admin";
}

public static class DeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}

public static class ProofStatuses
{
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";

    public static bool IsValid(string? value) =>
        value is Delivered or Failed;
}
