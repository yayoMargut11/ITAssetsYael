namespace ITAssets.Api.Models;

public static class Roles
{
    public const string Admin = "Administrador";
    public const string Operator = "Operador";
    public const string AdminOrOperator = Admin + "," + Operator;
}

public static class AssetStatuses
{
    public const string Available = "Disponible";
    public const string Assigned = "Asignado";
    public const string Maintenance = "Mantenimiento";
    public const string Retired = "Retirado";
    public static readonly string[] All = { Available, Assigned, Maintenance, Retired };
}

public static class OwnershipTypes
{
    public const string Owned = "Propio";
    public const string Rented = "Arrendado";
    public static readonly string[] All = { Owned, Rented };
}

public static class ServiceTypes
{
    public static readonly string[] All = { "Compra", "Mantenimiento", "Arrendamiento" };
}
