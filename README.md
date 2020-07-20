# MARS + Dapper + MSSQL wrong results test case

Query may wrong results under high load, with enabled MARS, Non-Windows clients.

- MSSQL database
- `MultipleActiveResultSets=True` (MARS) is in the connection string
- Client is not running on Windows (can be MacOS or Linux)
- Using `Dapper` (maybe issue is somehow exacerbated by something the Dapper does)
- High load that saturates the connection pool and causes timeouts.

The issue is hard to reproduce, and may require multiple runs of this program
to observe. One way to increase the chances of it occuring is to connect
to DB over a connection that is not very reliable 
(a router/hotspot in another room).

This program starts 2000 concurrent connections that runs `select @Id as Id`
statement. This can be executed on any MSSQL instance and does not even require
a database.

Example output with reproduced issue (`<...>` = skipped rows):

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

## OS

- Failed to observe issue on Windows
- Issue observed on MacOS, Docker/Linux and Docker/WSL.

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
transaction attached to the connection. In original project
the issue was observed even if there was no transaction
attached to command (but it was more rare).

However, I failed to reproduce this issue with a simple `select @Id as Id`
without a transaction. Therefore the code in this project executes the 
queries under a transaction.