using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SameieHub.Infrastructure.Persistence;

namespace SameieHub.Tests.Infrastructure;

public class SqliteConstraintErrorsTests
{
    private const int SqliteConstraint = 19;

    [Theory]
    [InlineData(787)]
    [InlineData(1811)]
    public void IsForeignKeyViolation_ForeignKeyCodes_ReturnsTrue(int extendedCode)
    {
        var exception = Wrap(new SqliteException("FOREIGN KEY constraint failed.", SqliteConstraint, extendedCode));

        Assert.True(SqliteConstraintErrors.IsForeignKeyViolation(exception));
    }

    [Fact]
    public void IsForeignKeyViolation_TriggerErrorThatIsNotAForeignKey_ReturnsFalse()
    {
        var exception = Wrap(new SqliteException("Deleting is blocked by a custom rule", SqliteConstraint, 1811));

        Assert.False(SqliteConstraintErrors.IsForeignKeyViolation(exception));
    }

    [Theory]
    [InlineData(1299)]
    [InlineData(1555)]
    [InlineData(2067)]
    public void IsForeignKeyViolation_OtherConstraintCodes_ReturnsFalse(int extendedCode)
    {
        var exception = Wrap(new SqliteException("A constraint failed.", SqliteConstraint, extendedCode));

        Assert.False(SqliteConstraintErrors.IsForeignKeyViolation(exception));
    }

    [Fact]
    public void IsForeignKeyViolation_OtherSqliteError_ReturnsFalse()
    {
        var exception = Wrap(new SqliteException("The database is locked.", 5, 5));

        Assert.False(SqliteConstraintErrors.IsForeignKeyViolation(exception));
    }

    [Fact]
    public void IsForeignKeyViolation_NoSqliteInnerException_ReturnsFalse()
    {
        Assert.False(SqliteConstraintErrors.IsForeignKeyViolation(Wrap(new InvalidOperationException("Other"))));
        Assert.False(SqliteConstraintErrors.IsForeignKeyViolation(new DbUpdateException("No inner exception")));
    }

    private static DbUpdateException Wrap(Exception inner)
    {
        return new DbUpdateException("An error occurred while saving.", inner);
    }
}
