using Microsoft.AspNetCore.Mvc;
using ZamanTakasi.Core.Exceptions;

namespace ZamanTakasi.Api.Middleware;

/// <summary>Domain istisnalarını uygun HTTP durum kodu + ProblemDetails'e eşler.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException          => (StatusCodes.Status404NotFound, "Bulunamadı"),
                ForbiddenActionException   => (StatusCodes.Status403Forbidden, "Yetkisiz işlem"),
                InsufficientBalanceException => (StatusCodes.Status400BadRequest, "Yetersiz bakiye"),
                InvalidBookingTransitionException => (StatusCodes.Status400BadRequest, "Geçersiz durum geçişi"),
                DomainException            => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
                _                          => (StatusCodes.Status500InternalServerError, "Sunucu hatası")
            };

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Beklenmeyen hata");

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError ? "Beklenmeyen bir hata oluştu." : ex.Message
            };
            ctx.Response.StatusCode = status;
            await ctx.Response.WriteAsJsonAsync(problem);
        }
    }
}
