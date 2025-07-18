﻿using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using PredictionLeague.Application.Data;
using System.Data;

namespace PredictionLeague.Infrastructure.Data;

public class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}