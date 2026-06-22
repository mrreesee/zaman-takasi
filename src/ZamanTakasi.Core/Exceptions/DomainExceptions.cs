namespace ZamanTakasi.Core.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

/// <summary>Geçersiz booking durum geçişi (ör. Pending iken Complete).</summary>
public sealed class InvalidBookingTransitionException : DomainException
{
    public InvalidBookingTransitionException(string message) : base(message) { }
}

/// <summary>Bakiye yetersiz; negatife düşülemez (Zaman Avansı sonraki aşama).</summary>
public sealed class InsufficientBalanceException : DomainException
{
    public InsufficientBalanceException(string message) : base(message) { }
}

/// <summary>İstenen kayıt bulunamadı (API'de 404'e eşlenir).</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Kullanıcının bu eylemi yapmaya yetkisi yok (API'de 403'e eşlenir).</summary>
public sealed class ForbiddenActionException : DomainException
{
    public ForbiddenActionException(string message) : base(message) { }
}

/// <summary>
/// Eşzamanlı işlem çakışması: serializable transaction tekrar denemelere rağmen
/// çözülemedi (API'de 409'a eşlenir). Çağıran güvenle yeniden deneyebilir.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
