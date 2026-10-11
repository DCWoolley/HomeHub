using Xunit;

namespace HomeHub.Web.Tests;

public sealed class LocalDbFactAttribute : FactAttribute
{
    public LocalDbFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HOMEHUB_TEST_LOCALDB") != "1")
            Skip = "Set HOMEHUB_TEST_LOCALDB=1 on Windows with SQL Server LocalDB.";
    }
}
