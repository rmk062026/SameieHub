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

    [Theory]
    [InlineData(2067)]
    [InlineData(1555)]
    public void IsUniqueViolation_UniqueAndPrimaryKeyCodesThatNameTheIndex_ReturnsTrue(int extendedCode)
    {
        var exception = Wrap(new SqliteException("UNIQUE constraint failed: EvChargers.HousingUnitId", SqliteConstraint, extendedCode));

        Assert.True(SqliteConstraintErrors.IsUniqueViolation(exception, "EvChargers.HousingUnitId"));
    }

    [Fact]
    public void IsUniqueViolation_AnotherIndexOrAnotherCode_ReturnsFalse()
    {
        var otherIndex = Wrap(new SqliteException("UNIQUE constraint failed: Meters.EvChargerId", SqliteConstraint, 2067));
        var foreignKey = Wrap(new SqliteException("FOREIGN KEY constraint failed.", SqliteConstraint, 787));

        Assert.False(SqliteConstraintErrors.IsUniqueViolation(otherIndex, "EvChargers.HousingUnitId"));
        Assert.False(SqliteConstraintErrors.IsUniqueViolation(foreignKey, "EvChargers.HousingUnitId"));
        Assert.False(SqliteConstraintErrors.IsUniqueViolation(Wrap(new InvalidOperationException("Other")), "EvChargers.HousingUnitId"));
    }

    [Fact]
    public void IsCheckViolation_CheckCodeThatNamesTheConstraint_ReturnsTrueOtherwiseFalse()
    {
        var check = Wrap(new SqliteException("CHECK constraint failed: CK_ChargingFacilities_OneFacility", SqliteConstraint, 275));
        var otherCheck = Wrap(new SqliteException("CHECK constraint failed: CK_Other", SqliteConstraint, 275));
        var unique = Wrap(new SqliteException("UNIQUE constraint failed: X.Y", SqliteConstraint, 2067));

        Assert.True(SqliteConstraintErrors.IsCheckViolation(check, "CK_ChargingFacilities_OneFacility"));
        Assert.False(SqliteConstraintErrors.IsCheckViolation(otherCheck, "CK_ChargingFacilities_OneFacility"));
        Assert.False(SqliteConstraintErrors.IsCheckViolation(unique, "CK_ChargingFacilities_OneFacility"));
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
