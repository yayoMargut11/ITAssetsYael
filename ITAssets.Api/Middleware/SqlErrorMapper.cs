namespace ITAssets.Api.Middleware;

public readonly record struct ErrorInfo(int Status, string Code, string Message);

/// <summary>
/// Traduce el NÚMERO de error de SQL Server (nunca el mensaje) a una respuesta controlada.
/// Los números 500xx son los THROW de negocio definidos en los Stored Procedures.
/// </summary>
public static class SqlErrorMapper
{
    private static readonly Dictionary<int, ErrorInfo> Map = new()
    {
        [50001] = new(404, "ASSET_NOT_FOUND", "El activo no existe."),
        [50002] = new(409, "ASSET_NOT_AVAILABLE", "El activo no está disponible para asignarse (ya asignado, en mantenimiento o retirado)."),
        [50003] = new(404, "EMPLOYEE_NOT_FOUND", "El colaborador no existe."),
        [50004] = new(422, "EMPLOYEE_INACTIVE", "El colaborador está inactivo y no puede recibir activos."),
        [50010] = new(409, "ASSET_CODE_DUPLICATE", "Ya existe un activo con ese AssetCode."),
        [50011] = new(409, "SERIAL_DUPLICATE", "Ya existe un activo con ese número de serie."),
        [50012] = new(422, "SUPPLIER_REQUIRED_FOR_RENTAL", "Un activo arrendado debe tener proveedor."),
        [50013] = new(404, "SUPPLIER_NOT_FOUND", "El proveedor no existe o está inactivo."),
        [50014] = new(422, "INVALID_VALUE", "Alguno de los valores enviados no es válido."),
        [50020] = new(409, "ASSET_ASSIGNED_RETURN_FIRST", "El activo está asignado; devuélvelo antes de cambiar su estado."),
        [50021] = new(409, "ASSET_RETIRED", "El activo está retirado y ya no puede modificarse."),
        [50022] = new(422, "INVALID_STATUS_TRANSITION", "El estado 'Asignado' solo se establece mediante una asignación."),
        [50023] = new(409, "CONCURRENCY_CONFLICT", "El activo fue modificado por otro usuario. Recarga e intenta de nuevo."),
        [50030] = new(409, "NO_ACTIVE_ASSIGNMENT", "El activo no tiene una asignación activa."),
        [50040] = new(409, "EMPLOYEE_NUMBER_DUPLICATE", "Ya existe un colaborador con ese número de empleado."),
        [50041] = new(409, "SUPPLIER_NAME_DUPLICATE", "Ya existe un proveedor con ese nombre."),
        // Índices únicos (red de seguridad ante condiciones de carrera)
        [2601] = new(409, "CONFLICT", "La operación entra en conflicto con otro registro (por ejemplo, el activo ya fue asignado)."),
        [2627] = new(409, "CONFLICT", "La operación entra en conflicto con otro registro (por ejemplo, el activo ya fue asignado)."),
        // Deadlock elegido como víctima
        [1205] = new(409, "CONCURRENCY_CONFLICT", "La operación no pudo completarse por concurrencia. Intenta de nuevo."),
    };

    public static bool TryMap(int sqlErrorNumber, out ErrorInfo info) => Map.TryGetValue(sqlErrorNumber, out info);
}
