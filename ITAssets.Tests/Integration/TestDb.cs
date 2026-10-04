using Xunit;

namespace ITAssets.Tests.Integration;

public static class TestDb
{
    /// <summary>Misma variable que usa la API: ConnectionStrings__Default</summary>
    public static string? ConnectionString => Environment.GetEnvironmentVariable("ConnectionStrings__Default");
}

/// <summary>[Fact] que se omite automáticamente si no hay base de datos configurada.</summary>
public sealed class FactRequiresDbAttribute : FactAttribute
{
    public FactRequiresDbAttribute()
    {
        if (string.IsNullOrWhiteSpace(TestDb.ConnectionString))
            Skip = "Define la variable de entorno ConnectionStrings__Default para ejecutar las pruebas de integración.";
    }
}
