using System.Data;
using Dapper;

namespace PharmaCore.Persistence;

/// <summary>Maps DateOnly to/from SQL DATE regardless of Dapper/SqlClient version support.</summary>
public sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateTime dt => DateOnly.FromDateTime(dt),
        DateOnly d => d,
        _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
    };

    public static void Register() => SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
}
