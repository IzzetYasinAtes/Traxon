using Traxon.CryptoTrader.Domain.Abstractions;

namespace Traxon.CryptoTrader.Domain.Trading;

/// <summary>
/// Bir sinyalin belirli bir engine tarafindan degerlendirme sonucu.
/// Accept edildiyse TradeId set edilir; reject edildiyse RejectionReason dolar.
/// </summary>
public sealed class SignalEngineResult : Entity<Guid>
{
    public Guid SignalRecordId { get; private set; }
    public string EngineName { get; private set; } = null!;
    public bool Accepted { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? TradeId { get; private set; }
    public DateTime EvaluatedAt { get; private set; }

    private SignalEngineResult() { }

    public SignalEngineResult(
        Guid signalRecordId,
        string engineName,
        bool accepted,
        string? rejectionReason,
        Guid? tradeId,
        DateTime evaluatedAt)
    {
        Id = Guid.NewGuid();
        SignalRecordId = signalRecordId;
        EngineName = engineName;
        Accepted = accepted;
        RejectionReason = rejectionReason;
        TradeId = tradeId;
        EvaluatedAt = evaluatedAt;
    }
}
