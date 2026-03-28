namespace Traxon.CryptoTrader.Domain.Common;

public sealed class Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    private Result(T value) { Value = value; Error = null; }
    private Result(Error error) { Value = default; Error = error; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Error, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static readonly Error NotEnoughCandles     = new("Domain.NotEnoughCandles",     "Buffer does not have enough candles for calculation.");
    public static readonly Error InvalidCandle        = new("Domain.InvalidCandle",        "Candle data is invalid.");
    public static readonly Error BufferFull           = new("Domain.BufferFull",           "Candle buffer is full.");
    public static readonly Error InvalidEdge          = new("Domain.InvalidEdge",          "Edge is below minimum threshold.");
    public static readonly Error PortfolioInsufficient = new("Domain.PortfolioInsufficient", "Insufficient balance.");
    public static readonly Error InsufficientConfirmation = new("Domain.InsufficientConfirmation", "Not enough indicator confirmations (minimum 3/5 required).");
    public static readonly Error InvalidMarketPrice       = new("Domain.InvalidMarketPrice",       "Market price must be between $0.30 and $0.60.");
    public static readonly Error SignalDirectionMismatch  = new("Domain.SignalDirectionMismatch",  "Fair value direction does not match indicator confirmation direction.");
    public static readonly Error TradeNotFound          = new("Domain.TradeNotFound",          "Trade with given ID not found.");
    public static readonly Error EngineNotReady         = new("Domain.EngineNotReady",         "Trading engine is not ready.");
    public static readonly Error DuplicatePosition      = new("Domain.DuplicatePosition",      "A position for this asset is already open.");
    public static readonly Error Disabled               = new("Engine.Disabled",               "Engine is disabled in configuration.");
    public static readonly Error MarketNotFound         = new("Polymarket.MarketNotFound",      "No active market found for this asset/direction.");
}
