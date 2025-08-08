//-------------------------------------------------------------------------------
// <copyright file="TypeExtensions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite
{
    using System;
    using System.Data;
    using System.Reflection;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Mappings;

    internal static class TypeExtensions
    {
        public static bool IsAnonymousType(this Type type)
        {
            return type.IsClass
                   && type.IsSealed
                   && type.Attributes.HasFlag(TypeAttributes.NotPublic)
                   && type.Name.StartsWith("<>")
                   && type.Name.Contains("AnonymousType");
        }

        public static SQLiteDbType ToSQLiteDbType(this SqlDbType dbType)
        {
            return dbType switch
            {
                // Integer types - map to INTEGER
                SqlDbType.Int => SQLiteDbType.Integer,
                SqlDbType.BigInt => SQLiteDbType.Integer,
                SqlDbType.SmallInt => SQLiteDbType.Integer,
                SqlDbType.TinyInt => SQLiteDbType.Integer,
                SqlDbType.Bit => SQLiteDbType.Integer,

                // Floating point types - map to REAL
                SqlDbType.Float => SQLiteDbType.Real,
                SqlDbType.Real => SQLiteDbType.Real,
                SqlDbType.Decimal => SQLiteDbType.Real, // do not use SQLiteDbType.Numeric
                SqlDbType.Money => SQLiteDbType.Real,
                SqlDbType.SmallMoney => SQLiteDbType.Real,

                // String types - map to TEXT
                SqlDbType.NVarChar => SQLiteDbType.Text,
                SqlDbType.VarChar => SQLiteDbType.Text,
                SqlDbType.NChar => SQLiteDbType.Text,
                SqlDbType.Char => SQLiteDbType.Text,
                SqlDbType.Text => SQLiteDbType.Text,
                SqlDbType.NText => SQLiteDbType.Text,

                // Date/time types - map to TEXT (SQLite stores dates as text)
                SqlDbType.DateTime => SQLiteDbType.Text,
                SqlDbType.DateTime2 => SQLiteDbType.Text,
                SqlDbType.DateTimeOffset => SQLiteDbType.Text,
                SqlDbType.Date => SQLiteDbType.Text,
                SqlDbType.Time => SQLiteDbType.Integer, // CLR type timespan
                SqlDbType.SmallDateTime => SQLiteDbType.Text,

                // Binary types - map to BLOB
                SqlDbType.Binary => SQLiteDbType.Blob,
                SqlDbType.VarBinary => SQLiteDbType.Blob,
                SqlDbType.Image => SQLiteDbType.Blob,

                // GUID - map to TEXT
                SqlDbType.UniqueIdentifier => SQLiteDbType.Text,

                // XML - map to TEXT
                SqlDbType.Xml => SQLiteDbType.Text,

                // SQL Server specific types - map to appropriate SQLite types
                SqlDbType.Timestamp => SQLiteDbType.Blob,
                SqlDbType.Variant => SQLiteDbType.Text,

                // Default fallback - map to TEXT for unknown types
                _ => SQLiteDbType.Text,
            };
        }
    }
}