// -----------------------------------------------------------------------
// <copyright file="SQLiteTranslatorQuickTest.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.SQLite
{
    using System;
    using System.Linq.Expressions;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.Provider.SQLite;
    using Microsoft.AzureStack.Services.Update.Common.Persistence.UnitTest.Entities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SQLiteTranslatorQuickTest
    {
        [TestMethod]
        [TestCategory("SQLite")]
        public void VerifyDateTimeTranslation()
        {
            // Test the exact predicate mentioned in the issue
            Expression<Func<TestEntity, bool>> expression = e => e.CreatedTime < DateTime.UtcNow.AddDays(-90);
            
            var translator = new SQLiteExpressionTranslator<TestEntity>();
            var result = translator.Translate(expression);
            
            Console.WriteLine($"SQL: {result.Sql}");
            foreach (var param in result.Parameters)
            {
                Console.WriteLine($"Parameter {param.Key}: {param.Value} (Type: {param.Value?.GetType()})");
            }
            
            // Verify the SQL is correct
            Assert.AreEqual("(datetime(CreatedTime) < datetime(@p0))", result.Sql);
            
            // Verify the parameter is an ISO 8601 string
            Assert.IsTrue(result.Parameters.ContainsKey("@p0"));
            var paramValue = result.Parameters["@p0"];
            Assert.IsInstanceOfType(paramValue, typeof(string));
            
            // Verify it's a valid ISO 8601 date string
            var dateString = (string)paramValue;
            Assert.IsTrue(dateString.Contains("T"));
            Assert.IsTrue(dateString.Contains("-"));
            
            // Parse it to ensure it's valid
            var parsedDate = DateTime.Parse(dateString);
            var expectedDate = DateTime.UtcNow.AddDays(-90);
            var diff = Math.Abs((parsedDate - expectedDate).TotalSeconds);
            Assert.IsTrue(diff < 2, $"Date difference too large: {diff} seconds");
        }
    }
}