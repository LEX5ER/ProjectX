namespace ProjectX.PM.Infrastructure.Persistence;

public static class ApplicationDatabaseDefaults
{
    public const string ConnectionStringName = "ApplicationDb";

    public const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ProjectX.PM.Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
}
