using System;
using Cronos;
using System.Collections.Generic;
using Xunit;

namespace DataTransfer.API.Tests
{
    public class CronExpressionTests
    {
        [Theory]
        [InlineData("*/5 * * * *", 5)] // Every 5 minutes
        [InlineData("0 * * * *", 60)] // Hourly
        [InlineData("0 */2 * * *", 120)] // Every 2 hours
        [InlineData("0 0 * * *", 1440)] // Daily at midnight
        [InlineData("0 12 * * *", 1440)] // Daily at noon
        public void CanCalculateNextOccurrence(string cronExpression, int expectedMinutesRange)
        {
            // Arrange
            var expression = CronExpression.Parse(cronExpression);
            var now = DateTime.UtcNow;
            
            // Act
            var nextOccurrence = expression.GetNextOccurrence(now);
            
            // Assert
            Assert.NotNull(nextOccurrence);
            
            // The next occurrence should be in the future
            Assert.True(nextOccurrence > now);
            
            // The next occurrence should be within the expected range
            var minutesDiff = (nextOccurrence.Value - now).TotalMinutes;
            Assert.True(minutesDiff <= expectedMinutesRange, 
                $"Expected next occurrence within {expectedMinutesRange} minutes, but was {minutesDiff} minutes");
        }
        
        [Theory]
        [InlineData("invalid")] // Invalid expression
        [InlineData("* * * *")] // Too few parts
        [InlineData("*/70 * * * *")] // Invalid minute value
        [InlineData("* 25 * * *")] // Invalid hour value
        public void ThrowsExceptionForInvalidExpressions(string cronExpression)
        {
            // Assert
            Assert.Throws<CronFormatException>(() => CronExpression.Parse(cronExpression));
        }
        
        [Fact]
        public void CanHandleCommonCronExpressions()
        {
            // Common patterns
            var examples = new Dictionary<string, string>
            {
                { "*/5 * * * *", "Every 5 minutes" },
                { "0 * * * *", "Every hour" },
                { "0 0 * * *", "Daily at midnight" },
                { "0 12 * * *", "Daily at noon" },
                { "0 0 * * MON", "Every Monday at midnight" },
                { "0 0 1 * *", "At midnight on the first day of every month" },
                { "*/10 * * * *", "Every 10 minutes" },
                { "0 8-18 * * MON-FRI", "Hourly from 8 AM to 6 PM, Monday to Friday" }
            };
            
            var now = DateTime.UtcNow;
            
            foreach (var example in examples)
            {
                // Parse and get next occurrence
                var expression = CronExpression.Parse(example.Key);
                var nextOccurrence = expression.GetNextOccurrence(now);
                
                // Assert always returns a value and it's in the future
                Assert.NotNull(nextOccurrence);
                Assert.True(nextOccurrence > now);
                
                Console.WriteLine($"{example.Value}: Next occurrence at {nextOccurrence}");
            }
        }
    }
} 