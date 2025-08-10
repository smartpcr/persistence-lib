// -----------------------------------------------------------------------
// <copyright file="TypeExtensions.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.Contracts.Extensions
{
    using System;
    using System.Data;
    using System.Text;

    /// <summary>
    /// Extension methods for System.Type to support database operations.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Converts a CLR type to its corresponding SqlDbType.
        /// This follows the same mapping logic as BaseEntityMapper.InferSqlType.
        /// </summary>
        /// <param name="clrType">The CLR type to convert.</param>
        /// <returns>The corresponding SqlDbType.</returns>
        public static SqlDbType ToSqlDbType(this Type clrType)
        {
            if (clrType == null)
            {
                throw new ArgumentNullException(nameof(clrType));
            }

            // Handle nullable types by getting the underlying type
            var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;

            // String types
            if (underlyingType == typeof(string))
            {
                return SqlDbType.NVarChar;
            }

            // Integer types
            if (underlyingType == typeof(int))
            {
                return SqlDbType.Int;
            }

            if (underlyingType == typeof(long))
            {
                return SqlDbType.BigInt;
            }

            if (underlyingType == typeof(short))
            {
                return SqlDbType.SmallInt;
            }

            if (underlyingType == typeof(byte))
            {
                return SqlDbType.TinyInt;
            }

            // Boolean
            if (underlyingType == typeof(bool))
            {
                return SqlDbType.Bit;
            }

            // Decimal types
            if (underlyingType == typeof(decimal))
            {
                return SqlDbType.Decimal;
            }

            if (underlyingType == typeof(double))
            {
                return SqlDbType.Float;
            }

            if (underlyingType == typeof(float))
            {
                return SqlDbType.Real;
            }

            // Date and time types
            if (underlyingType == typeof(DateTime))
            {
                return SqlDbType.DateTime2;
            }

            if (underlyingType == typeof(DateTimeOffset))
            {
                return SqlDbType.DateTimeOffset;
            }

            if (underlyingType == typeof(TimeSpan))
            {
                return SqlDbType.Time;
            }

            // Binary types
            if (underlyingType == typeof(byte[]))
            {
                return SqlDbType.VarBinary;
            }

            // GUID
            if (underlyingType == typeof(Guid))
            {
                return SqlDbType.UniqueIdentifier;
            }

            // Enum types
            if (underlyingType.IsEnum)
            {
                return SqlDbType.NVarChar; // Store enums as string by default
            }

            // Char types
            if (underlyingType == typeof(char))
            {
                return SqlDbType.NChar;
            }

            if (underlyingType == typeof(char[]))
            {
                return SqlDbType.NVarChar;
            }

            // XML type
            if (underlyingType == typeof(System.Xml.XmlDocument) ||
                underlyingType == typeof(System.Xml.Linq.XDocument))
            {
                return SqlDbType.Xml;
            }

            // Default to NVarChar for complex types (will be serialized as JSON)
            return SqlDbType.NVarChar;
        }

        /// <summary>
        /// Converts a CLR type to its corresponding SqlDbType with additional metadata.
        /// </summary>
        /// <param name="clrType">The CLR type to convert.</param>
        /// <param name="size">Output parameter for the size/length of the SQL type.</param>
        /// <param name="precision">Output parameter for the precision (for decimal types).</param>
        /// <param name="scale">Output parameter for the scale (for decimal types).</param>
        /// <returns>The corresponding SqlDbType.</returns>
        public static SqlDbType ToSqlDbType(this Type clrType, out int? size, out byte? precision, out byte? scale)
        {
            size = null;
            precision = null;
            scale = null;

            var sqlType = clrType.ToSqlDbType();

            // Set additional metadata based on SQL type
            switch (sqlType)
            {
                case SqlDbType.NVarChar:
                    var underlyingType = Nullable.GetUnderlyingType(clrType) ?? clrType;
                    if (clrType == typeof(string))
                    {
                        size = 255; // Default size for strings
                    }
                    else if (underlyingType.IsEnum)
                    {
                        size = 50; // Reasonable size for enum string values
                    }
                    else
                    {
                        size = -1; // MAX for complex serialized types
                    }
                    break;

                case SqlDbType.VarBinary:
                    size = -1; // MAX for byte arrays
                    break;

                case SqlDbType.Decimal:
                    precision = 18;
                    scale = 2;
                    break;

                case SqlDbType.NChar:
                    size = 1;
                    break;
            }

            return sqlType;
        }

        /// <summary>
        /// Converts a CLR type to a complete SQL column definition string.
        /// </summary>
        /// <param name="clrType">The CLR type to convert.</param>
        /// <param name="columnName">The name of the column.</param>
        /// <param name="isNullable">Whether the column allows NULL values. If not specified, inferred from type.</param>
        /// <param name="defaultValue">The default value for the column.</param>
        /// <param name="isPrimaryKey">Whether this column is a primary key.</param>
        /// <param name="isIdentity">Whether this column is an identity column.</param>
        /// <param name="checkConstraint">Check constraint expression for the column.</param>
        /// <returns>A complete SQL column definition string.</returns>
        public static string ToSqlColumnDefinition(
            this Type clrType,
            string columnName,
            bool? isNullable = null,
            object defaultValue = null,
            bool isPrimaryKey = false,
            bool isIdentity = false,
            string checkConstraint = null)
        {
            if (string.IsNullOrWhiteSpace(columnName))
            {
                throw new ArgumentException("Column name cannot be null or empty.", nameof(columnName));
            }

            var definition = new StringBuilder();

            // Column name
            definition.Append($"[{columnName}] ");

            // Data type
            definition.Append(clrType.ToSqlTypeString());

            // Identity
            if (isIdentity)
            {
                definition.Append(" IDENTITY(1,1)");
            }

            // Nullability
            bool actualNullability = isNullable ?? clrType.IsNullable();
            if (!actualNullability || isPrimaryKey)
            {
                definition.Append(" NOT NULL");
            }
            else
            {
                definition.Append(" NULL");
            }

            // Default value
            if (defaultValue != null)
            {
                definition.Append($" DEFAULT {FormatSqlValue(defaultValue, clrType)}");
            }

            // Primary key
            if (isPrimaryKey)
            {
                definition.Append(" PRIMARY KEY");
            }

            // Check constraint
            if (!string.IsNullOrWhiteSpace(checkConstraint))
            {
                definition.Append($" CHECK ({checkConstraint})");
            }

            return definition.ToString();
        }

        /// <summary>
        /// Formats a value for use in SQL statements.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <param name="clrType">The CLR type of the value.</param>
        /// <returns>The formatted SQL value string.</returns>
        private static string FormatSqlValue(object value, Type clrType)
        {
            if (value == null)
            {
                return "NULL";
            }

            var underlyingType = clrType.GetUnderlyingTypeOrSelf();

            // Handle special SQL keywords
            if (value is string strValue)
            {
                var upperValue = strValue.ToUpperInvariant();
                if (upperValue == "GETDATE()" || upperValue == "NEWID()" ||
                    upperValue == "GETUTCDATE()" || upperValue == "CURRENT_TIMESTAMP")
                {
                    return strValue;
                }

                // Regular string value
                return $"N'{strValue.Replace("'", "''")}'";
            }

            // Handle different types
            return underlyingType switch
            {
                Type t when t == typeof(bool) => (bool)value ? "1" : "0",
                Type t when t == typeof(DateTime) => $"'{((DateTime)value):yyyy-MM-dd HH:mm:ss.fff}'",
                Type t when t == typeof(DateTimeOffset) => $"'{((DateTimeOffset)value):yyyy-MM-dd HH:mm:ss.fff zzz}'",
                Type t when t == typeof(Guid) => $"'{value}'",
                Type t when t == typeof(byte[]) => $"0x{BitConverter.ToString((byte[])value).Replace("-", "")}",
                Type t when t.IsEnum => $"N'{value.ToString()}'", // Store enums as strings
                Type t when IsNumericType(t) => value.ToString(),
                _ => $"N'{value.ToString().Replace("'", "''")}'",
            };
        }

        /// <summary>
        /// Determines if a type is a numeric type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is numeric; otherwise, false.</returns>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        /// <summary>
        /// Gets the SQL type string representation for DDL statements.
        /// </summary>
        /// <param name="clrType">The CLR type to convert.</param>
        /// <returns>The SQL type string for use in CREATE TABLE statements.</returns>
        public static string ToSqlTypeString(this Type clrType)
        {
            var sqlType = clrType.ToSqlDbType(out var size, out var precision, out var scale);

            return ToSqlTypeString(sqlType, size, precision, scale);
        }

        public static string ToSqlTypeString(this SqlDbType sqlType, int? size, byte? precision, byte? scale)
        {
            return sqlType switch
            {
                SqlDbType.BigInt => "BIGINT",
                SqlDbType.Binary => size > 0 ? $"BINARY({size})" : "BINARY",
                SqlDbType.Bit => "BIT",
                SqlDbType.Char => size > 0 ? $"CHAR({size})" : "CHAR",
                SqlDbType.Date => "DATE",
                SqlDbType.DateTime => "DATETIME",
                SqlDbType.DateTime2 => "DATETIME2",
                SqlDbType.DateTimeOffset => "DATETIMEOFFSET",
                SqlDbType.Decimal => precision.HasValue && scale.HasValue && precision > 0 && scale >= 0
                    ? $"DECIMAL({precision},{scale})"
                    : "DECIMAL(18,2)",
                SqlDbType.Float => "FLOAT",
                SqlDbType.Image => "IMAGE",
                SqlDbType.Int => "INT",
                SqlDbType.Money => "MONEY",
                SqlDbType.NChar => size > 0 ? $"NCHAR({size})" : "NCHAR",
                SqlDbType.NText => "NTEXT",
                SqlDbType.NVarChar => size == -1 ? "NVARCHAR(MAX)" : $"NVARCHAR({(size > 0 ? size : 255)})",
                SqlDbType.Real => "REAL",
                SqlDbType.SmallDateTime => "SMALLDATETIME",
                SqlDbType.SmallInt => "SMALLINT",
                SqlDbType.SmallMoney => "SMALLMONEY",
                SqlDbType.Text => "TEXT",
                SqlDbType.Time => "TIME",
                SqlDbType.Timestamp => "TIMESTAMP",
                SqlDbType.TinyInt => "TINYINT",
                SqlDbType.UniqueIdentifier => "UNIQUEIDENTIFIER",
                SqlDbType.VarBinary => size == -1 ? "VARBINARY(MAX)" : $"VARBINARY({(size > 0 ? size : 8000)})",
                SqlDbType.VarChar => size == -1 ? "VARCHAR(MAX)" : $"VARCHAR({(size > 0 ? size : 255)})",
                SqlDbType.Xml => "XML",
                _ => "NVARCHAR(MAX)" // Default fallback
            };
        }

        /// <summary>
        /// Determines if a type is a nullable type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if the type is nullable; otherwise, false.</returns>
        public static bool IsNullable(this Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        /// <summary>
        /// Gets the underlying type if the type is nullable, otherwise returns the type itself.
        /// </summary>
        /// <param name="type">The type to process.</param>
        /// <returns>The underlying type or the original type.</returns>
        public static Type GetUnderlyingTypeOrSelf(this Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        /// <summary>
        /// Determines if a type requires size specification in SQL.
        /// </summary>
        /// <param name="sqlType">The SQL type to check.</param>
        /// <returns>True if the type requires size specification; otherwise, false.</returns>
        public static bool RequiresSize(this SqlDbType sqlType)
        {
            return sqlType switch
            {
                SqlDbType.Binary or
                SqlDbType.Char or
                SqlDbType.NChar or
                SqlDbType.NVarChar or
                SqlDbType.VarBinary or
                SqlDbType.VarChar => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines if a type requires precision and scale specification in SQL.
        /// </summary>
        /// <param name="sqlType">The SQL type to check.</param>
        /// <returns>True if the type requires precision/scale specification; otherwise, false.</returns>
        public static bool RequiresPrecisionScale(this SqlDbType sqlType)
        {
            return sqlType switch
            {
                SqlDbType.Decimal or
                SqlDbType.Money or
                SqlDbType.SmallMoney => true,
                _ => false
            };
        }
    }
}