using LMS.Data;
using Microsoft.EntityFrameworkCore;

namespace LMS.Helpers;

/// <summary>
/// Helper class for managing database transactions with retry execution strategy
/// حل مشكلة التعارض بين SqlServerRetryingExecutionStrategy والمعاملات اليدوية
/// </summary>
public static class TransactionHelper
{
    /// <summary>
    /// Executes a database operation within a retry-safe transaction.
    /// Use this when you need explicit transaction control with EnableRetryOnFailure.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="action">The async action to execute within the transaction</param>
    public static async Task ExecuteInTransactionAsync<TContext>(
        TContext context, 
        Func<Task> action) where TContext : DbContext
    {
        var strategy = context.Database.CreateExecutionStrategy();
        
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                await action();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Executes a database operation within a retry-safe transaction and returns a result.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    /// <typeparam name="TResult">The return type</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="action">The async function to execute within the transaction</param>
    /// <returns>The result of the operation</returns>
    public static async Task<TResult> ExecuteInTransactionAsync<TContext, TResult>(
        TContext context, 
        Func<Task<TResult>> action) where TContext : DbContext
    {
        var strategy = context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                var result = await action();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    /// <summary>
    /// Executes a database operation within a retry-safe transaction with error handling.
    /// Returns a tuple indicating success/failure with an optional error message.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="action">The async action to execute within the transaction</param>
    /// <param name="logger">Optional logger for error logging</param>
    /// <returns>A tuple with (Success, ErrorMessage)</returns>
    public static async Task<(bool Success, string? Error)> TryExecuteInTransactionAsync<TContext>(
        TContext context,
        Func<Task> action,
        ILogger? logger = null) where TContext : DbContext
    {
        try
        {
            await ExecuteInTransactionAsync(context, action);
            return (true, null);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger?.LogWarning(ex, "Concurrency conflict during transaction");
            return (false, "حدث تعارض في البيانات. يرجى إعادة المحاولة");
        }
        catch (DbUpdateException ex)
        {
            logger?.LogError(ex, "Database update error during transaction");
            
            // Check for specific constraint violations
            if (ex.InnerException?.Message.Contains("UNIQUE") == true ||
                ex.InnerException?.Message.Contains("duplicate") == true)
            {
                return (false, "البيانات موجودة بالفعل. يرجى التحقق من المدخلات");
            }
            
            if (ex.InnerException?.Message.Contains("FOREIGN KEY") == true)
            {
                return (false, "لا يمكن إتمام العملية بسبب وجود بيانات مرتبطة");
            }
            
            return (false, "حدث خطأ أثناء حفظ البيانات");
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogError(ex, "Invalid operation during transaction");
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error during transaction");
            return (false, "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى");
        }
    }

    /// <summary>
    /// Executes a database operation within a retry-safe transaction with error handling and returns a result.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type</typeparam>
    /// <typeparam name="TResult">The return type</typeparam>
    /// <param name="context">The database context</param>
    /// <param name="action">The async function to execute within the transaction</param>
    /// <param name="logger">Optional logger for error logging</param>
    /// <returns>A tuple with (Success, Result, ErrorMessage)</returns>
    public static async Task<(bool Success, TResult? Result, string? Error)> TryExecuteInTransactionAsync<TContext, TResult>(
        TContext context,
        Func<Task<TResult>> action,
        ILogger? logger = null) where TContext : DbContext
    {
        try
        {
            var result = await ExecuteInTransactionAsync(context, action);
            return (true, result, null);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            logger?.LogWarning(ex, "Concurrency conflict during transaction");
            return (false, default, "حدث تعارض في البيانات. يرجى إعادة المحاولة");
        }
        catch (DbUpdateException ex)
        {
            logger?.LogError(ex, "Database update error during transaction");
            return (false, default, "حدث خطأ أثناء حفظ البيانات");
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error during transaction");
            return (false, default, "حدث خطأ غير متوقع. يرجى المحاولة مرة أخرى");
        }
    }
}

/// <summary>
/// Extension methods for ApplicationDbContext to simplify transaction usage
/// </summary>
public static class DbContextTransactionExtensions
{
    /// <summary>
    /// Executes a database operation within a retry-safe transaction.
    /// </summary>
    public static Task ExecuteInTransactionAsync(
        this ApplicationDbContext context,
        Func<Task> action)
    {
        return TransactionHelper.ExecuteInTransactionAsync(context, action);
    }

    /// <summary>
    /// Executes a database operation within a retry-safe transaction and returns a result.
    /// </summary>
    public static Task<TResult> ExecuteInTransactionAsync<TResult>(
        this ApplicationDbContext context,
        Func<Task<TResult>> action)
    {
        return TransactionHelper.ExecuteInTransactionAsync<ApplicationDbContext, TResult>(context, action);
    }

    /// <summary>
    /// Executes a database operation within a retry-safe transaction with error handling.
    /// </summary>
    public static Task<(bool Success, string? Error)> TryExecuteInTransactionAsync(
        this ApplicationDbContext context,
        Func<Task> action,
        ILogger? logger = null)
    {
        return TransactionHelper.TryExecuteInTransactionAsync(context, action, logger);
    }

    /// <summary>
    /// Executes a database operation within a retry-safe transaction with error handling and returns a result.
    /// </summary>
    public static Task<(bool Success, TResult? Result, string? Error)> TryExecuteInTransactionAsync<TResult>(
        this ApplicationDbContext context,
        Func<Task<TResult>> action,
        ILogger? logger = null)
    {
        return TransactionHelper.TryExecuteInTransactionAsync<ApplicationDbContext, TResult>(context, action, logger);
    }
}
