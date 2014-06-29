﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Collections;
using System.Configuration;
using System.Threading.Tasks;
using Ivony.Data.Queries;
using Ivony.Fluent;

using System.Linq;
using System.Threading;
using System.Data.Common;
using Ivony.Data.Common;

namespace Ivony.Data.SqlClient
{
  /// <summary>
  /// 用于操作 SQL Server 的数据库访问工具
  /// </summary>
  public class SqlDbExecutor : DbExecutorBase, IAsyncDbExecutor<ParameterizedQuery>, IAsyncDbExecutor<StoredProcedureQuery>, IDbTransactionProvider<SqlDbExecutor>
  {



    /// <summary>
    /// 获取当前连接字符串
    /// </summary>
    protected string ConnectionString
    {
      get;
      private set;
    }


    /// <summary>
    /// 创建 SqlServer 数据库查询执行程序
    /// </summary>
    /// <param name="connectionString">连接字符串</param>
    /// <param name="configuration">当前要使用的数据库配置信息</param>
    public SqlDbExecutor( string connectionString, SqlDbConfiguration configuration )
    {
      if ( connectionString == null )
        throw new ArgumentNullException( "connectionString" );

      if ( configuration == null )
        throw new ArgumentNullException( "configuration" );



      ConnectionString = connectionString;
      Configuration = configuration;
    }


    /// <summary>
    /// 当前要使用的数据库配置信息
    /// </summary>
    protected SqlDbConfiguration Configuration
    {
      get;
      private set;
    }


    /// <summary>
    /// 创建数据库事务上下文
    /// </summary>
    /// <returns>数据库事务上下文</returns>
    public SqlDbTransactionContext CreateTransaction()
    {
      return new SqlDbTransactionContext( ConnectionString, Configuration );
    }


    IDbTransactionContext<SqlDbExecutor> IDbTransactionProvider<SqlDbExecutor>.CreateTransaction()
    {
      return CreateTransaction();
    }



    /// <summary>
    /// 获取追踪数据库查询过程的追踪服务
    /// </summary>
    protected override IDbTraceService TraceService
    {
      get { return Configuration.TraceService; }
    }



    /// <summary>
    /// 执行查询命令并返回执行上下文
    /// </summary>
    /// <param name="query">正在执行的查询对象</param>
    /// <param name="command">查询命令</param>
    /// <param name="tracing">用于追踪查询过程的追踪器</param>
    /// <returns>查询执行上下文</returns>
    protected virtual IDbExecuteContext ExecuteCommand<TQuery>( TQuery query, SqlCommand command, IDbTracing tracing = null ) where TQuery : IDbQuery
    {
      var connection = new SqlConnection( ConnectionString );
      connection.Open();
      command.Connection = connection;

      if ( Configuration.QueryExecutingTimeout.HasValue )
        command.CommandTimeout = (int) Configuration.QueryExecutingTimeout.Value.TotalSeconds;

      var reader = command.ExecuteReader();

#warning 尚未将返回参数值与查询对象绑定。

      return new SqlDbExecuteContext( connection, reader, tracing );
    }


    /// <summary>
    /// 异步执行查询命令并返回执行上下文
    /// </summary>
    /// <param name="query">正在执行的查询对象</param>
    /// <param name="command">查询命令</param>
    /// <param name="token">取消指示</param>
    /// <param name="tracing">用于追踪查询过程的追踪器</param>
    /// <returns>查询执行上下文</returns>
    protected virtual async Task<IAsyncDbExecuteContext> ExecuteCommandAsync<TQuery>( TQuery query, SqlCommand command, IDbTracing tracing = null, CancellationToken token = default( CancellationToken ) ) where TQuery : IDbQuery
    {
      var connection = new SqlConnection( ConnectionString );
      await connection.OpenAsync( token );
      command.Connection = connection;

      if ( Configuration.QueryExecutingTimeout.HasValue )
        command.CommandTimeout = (int) Configuration.QueryExecutingTimeout.Value.TotalSeconds;


      var reader = await command.ExecuteReaderAsync( token );
      var context = new SqlDbExecuteContext( connection, reader, tracing );

      return context;
    }



    IDbExecuteContext IDbExecutor<ParameterizedQuery>.Execute( ParameterizedQuery query )
    {
      return ExecuteQuery( query, q => new SqlParameterizedQueryParser().Parse( q ), ExecuteCommand );
    }

    Task<IAsyncDbExecuteContext> IAsyncDbExecutor<ParameterizedQuery>.ExecuteAsync( ParameterizedQuery query, CancellationToken token )
    {
      return ExecuteQuery( query, q => new SqlParameterizedQueryParser().Parse( q ), ExecuteCommandAsync );
    }

    IDbExecuteContext IDbExecutor<StoredProcedureQuery>.Execute( StoredProcedureQuery query )
    {
      return ExecuteQuery( query, CreateCommand, ExecuteCommand );
    }

    Task<IAsyncDbExecuteContext> IAsyncDbExecutor<StoredProcedureQuery>.ExecuteAsync( StoredProcedureQuery query, CancellationToken token )
    {
      return ExecuteQuery( query, CreateCommand, ExecuteCommandAsync );
    }


    /// <summary>
    /// 通过存储过程查询创建 SqlCommand 对象
    /// </summary>
    /// <param name="query">存储过程查询对象</param>
    /// <returns>SQL 查询命令对象</returns>
    protected SqlCommand CreateCommand( StoredProcedureQuery query )
    {
      var command = new SqlCommand( query.Name );
      command.CommandType = CommandType.StoredProcedure;
      query.Parameters.ForAll( parameter => command.Parameters.AddWithValue( parameter.Name, parameter.Value ) );

      return command;
    }
  }

}
