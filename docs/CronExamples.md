# Cron Expression Examples

The SQL Data Transfer Wizard uses standard cron expressions to schedule recurring data transfers. This document provides examples and explanations of common cron patterns to help you configure your transfer schedules effectively.

## Cron Expression Format

A cron expression is a string consisting of five or six fields separated by white space that represents a set of times:

```
* * * * *
┬ ┬ ┬ ┬ ┬
│ │ │ │ │
│ │ │ │ └─ Day of week (0 - 7) (Sunday is 0 or 7)
│ │ │ └─── Month (1 - 12)
│ │ └───── Day of month (1 - 31)
│ └─────── Hour (0 - 23)
└───────── Minute (0 - 59)
```

## Special Characters

- `*` - Wildcard (matches all values)
- `,` - List separator (e.g., `1,3,5`)
- `-` - Range (e.g., `1-5`)
- `/` - Step values (e.g., `*/10`)

## Common Cron Patterns

### Minutely Schedules

| Cron Expression | Description |
|-----------------|-------------|
| `* * * * *` | Every minute |
| `*/5 * * * *` | Every 5 minutes |
| `*/10 * * * *` | Every 10 minutes |
| `*/15 * * * *` | Every 15 minutes |
| `*/30 * * * *` | Every 30 minutes |
| `0,30 * * * *` | At minute 0 and 30 of every hour |

### Hourly Schedules

| Cron Expression | Description |
|-----------------|-------------|
| `0 * * * *` | Every hour at minute 0 |
| `0 */2 * * *` | Every 2 hours (at 0:00, 2:00, 4:00, etc.) |
| `0 */3 * * *` | Every 3 hours (at 0:00, 3:00, 6:00, etc.) |
| `15 */4 * * *` | Every 4 hours at minute 15 (at 0:15, 4:15, 8:15, etc.) |
| `0 9-17 * * *` | Every hour from 9 AM to 5 PM |

### Daily Schedules

| Cron Expression | Description |
|-----------------|-------------|
| `0 0 * * *` | Every day at midnight |
| `0 12 * * *` | Every day at noon |
| `0 0,12 * * *` | Every day at midnight and noon |
| `0 9 * * *` | Every day at 9 AM |
| `0 17 * * *` | Every day at 5 PM |
| `0 8-16 * * *` | Every hour from 8 AM to 4 PM |

### Weekly Schedules

| Cron Expression | Description |
|-----------------|-------------|
| `0 0 * * 0` | Every Sunday at midnight |
| `0 0 * * 1` | Every Monday at midnight |
| `0 0 * * 6` | Every Saturday at midnight |
| `0 9 * * 1-5` | Every weekday at 9 AM (Monday through Friday) |
| `0 0 * * 1,3,5` | Every Monday, Wednesday, and Friday at midnight |

### Monthly Schedules

| Cron Expression | Description |
|-----------------|-------------|
| `0 0 1 * *` | First day of every month at midnight |
| `0 0 15 * *` | 15th day of every month at midnight |
| `0 0 1,15 * *` | 1st and 15th day of every month at midnight |
| `0 12 L * *` | Last day of every month at noon |
| `0 0 1 */3 *` | First day of every quarter (Jan, Apr, Jul, Oct) at midnight |

### Yearly Schedules

| Cron Expression | Description |
|-----------------|-------------|
| `0 0 1 1 *` | January 1st at midnight (New Year) |
| `0 0 1 7 *` | July 1st at midnight |

## Business Hours Examples

| Cron Expression | Description |
|-----------------|-------------|
| `0 9-17 * * 1-5` | Every hour from 9 AM to 5 PM, Monday through Friday |
| `0 9 * * 1-5` | Every weekday at 9 AM |
| `0 17 * * 1-5` | Every weekday at 5 PM |
| `0 9,12,17 * * 1-5` | 9 AM, noon, and 5 PM every weekday |

## Testing Cron Expressions

When setting up a transfer with a cron expression, the SQL Data Transfer Wizard automatically calculates and displays the next scheduled execution time based on the current time. This helps verify that your expression will run at the intended times.

For complex cron expressions, you can also use online cron expression testers like:

- [Crontab Guru](https://crontab.guru/)
- [Cronhub Cron Expression Editor](https://cronhub.io/tools/cron-editor)

## Best Practices

1. **Start with common patterns** - Use the examples above as a starting point
2. **Consider database load** - Avoid scheduling heavy transfers during peak hours
3. **Spread out schedules** - Stagger transfers to avoid resource contention
4. **Test your expressions** - Verify the next execution times before saving
5. **Use appropriate frequency** - Match the schedule to how often the data needs to be refreshed

## Notes on Implementation

The SQL Data Transfer Wizard uses the NCrontab.Signed and Cronos libraries to parse and evaluate cron expressions. When a job is scheduled, the application calculates the next run time and stores it in the `NextRunDateTime` field of the ImportData table.

The Worker Service periodically checks for jobs where:
1. The job has a valid cron expression
2. The current time is at or past the calculated next run time

After completing a transfer, the Worker Service updates the `LastRunDateTime` field and calculates the new `NextRunDateTime` based on the cron expression. 