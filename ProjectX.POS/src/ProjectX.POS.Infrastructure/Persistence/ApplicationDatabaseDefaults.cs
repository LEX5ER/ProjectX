namespace ProjectX.POS.Infrastructure.Persistence;

public static class ApplicationDatabaseDefaults
{
    public const string ConnectionStringName = "ApplicationDb";

    public const string DefaultConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ProjectX.POS.Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
}
