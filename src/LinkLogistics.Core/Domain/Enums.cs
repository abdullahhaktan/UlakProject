namespace LinkLogistics.Core.Domain;

public static class UserRoles
{
    public const string Driver = "Driver";
    public const string Ops = "Ops";
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
