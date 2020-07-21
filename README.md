# MARS + Dapper + MSSQL wrong results test case

## How this was found

We have noticed the system that queries users from the database
starting to return users with a different id than was used to query them.
We were very lucky that there was a sanity check that checks the id and throws
an exception. Otherwise this would have caused way more problems than it did.

## How did this happen

Originally the system was written way before the .NET Core was a thing and
was running on windows.

The connection string containing this flag was copy-pasted when a part of the
system was re-written in .NET Core / Docker.

The issue was noticed when the load has increased enough at the peak hours.

## What is the issue

Sometimes the dapper query may return wrong results under high load, 
with enabled MARS, on Non-Windows clients.

This program starts 2000 concurrent connections that runs `select @Id as Id`
statement. This can be executed on any MSSQL instance and does not even require
a database.

- MSSQL instance
- `MultipleActiveResultSets=True` (MARS) is in the connection string
- Client is not running on Windows (can be MacOS or Linux)
- Using `Dapper` (maybe issue is somehow exacerbated by something the Dapper does)
- High load that saturates the connection pool and causes timeouts.

The issue is hard to reproduce, and may require multiple runs
to observe. One way to increase the likelihood of it occuring is to use
connection that is not very reliable (a router/hotspot in another room).

Example output (`<...>` = skipped rows):

```
<...>
160     Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
63      Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
94      Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
90      Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
333     Wrong result
369     Wrong result
223     Success
97      Wrong result
477     Wrong result
46      Wrong result
159     Timeout expired.  The timeout period elapsed prior to completion of the operation or the server is not responding.
328     Wrong result
485     Success
479     Success
<...>
358     Success
Successful results: 291
Wrong results: 16
Exceptions: 1693
```

The issue rare, but unacceptable. The workaround is to ensure that
`MultipleActiveResultSets` is not set.

More information is at the end of this README.

## OS

- Failed to observe issue on Windows
- Issue was observed on MacOS, Docker/Linux and Docker/WSL.

## MSSQL Server

Observed on AWS

- SQL Server Web Edition 13.00.5426.0.v1
- SQL Server Express Edition 14.00.3281.6.v1

## Connection string

```text
data source=tcp:******,1433;User ID=******;Password=******;MultipleActiveResultSets=True;Max Pool Size=100
```

Issue occurs only when `MultipleActiveResultSets` is enabled.

## Dependency versions

- System.Data.SqlClient `4.8.1`
- Dapper `2.0.35`

Issue originally found on way earlier SqlClient/Dapper version,
so this is probably not a regression.
Failed to observe the issue when queried without `Dapper`.

## Transaction

Looks like issue is way more likely to occur if there is a
transaction attached to the connection. In the original project
the issue was observed even if there was no transaction
attached to the command.

However, I failed to reproduce this issue with a simple `select @Id as Id`
without a transaction. Therefore the code in this project executes the 
queries under a transaction.