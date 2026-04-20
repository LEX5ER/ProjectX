namespace ProjectX.IAM.Infrastructure.Persistence;

internal static class ApplicationDatabaseDefaults
{
    public const string ConnectionStringName = "Defaults";
    public const string DefaultConnectionString = "Server=(LocalDb)\\MSSQLLocalDB;Database=ProjectX.IAM.Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
}
