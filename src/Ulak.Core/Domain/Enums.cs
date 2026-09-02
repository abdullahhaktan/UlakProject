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
    public const string PickedUp = "PickedUp";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}

/// <summary>Pickup or delivery — a delivery carries at most one of each.</summary>
public static class ProofTypes
{
    public const string Pickup = "Pickup";
    public const string Delivery = "Delivery";

    public static bool IsValid(string? value) =>
        value is Pickup or Delivery;
}

public static class ProofStatuses
{
    public const string PickedUp = "PickedUp";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";

    public static bool IsValid(string? value) =>
        value is PickedUp or Delivered or Failed;

    /// <summary>Valid capture states for a given proof type.</summary>
    public static bool IsValidFor(string? proofType, string? status) => proofType switch
    {
        ProofTypes.Pickup => status is PickedUp or Failed,
        ProofTypes.Delivery => status is Delivered or Failed,
        _ => false,
    };
}
